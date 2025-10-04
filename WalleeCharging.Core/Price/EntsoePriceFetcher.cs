
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WalleeCharging.Price;

public class EntsoePriceFetcher : IPriceFetcher
{
    private readonly string _domain;
    private readonly string URL_TEMPLATE = 
    "https://web-api.tp.entsoe.eu/api?"
        +"securityToken={0}&documentType=A44&in_Domain={1}&out_Domain={1}&periodStart={2}&periodEnd={3}";
    private readonly TimeSpan QUARTER_HOUR = TimeSpan.FromMinutes(15);
    
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public EntsoePriceFetcher(IOptions<EntsoeOptions> options, ILogger<EntsoePriceFetcher> logger)
    {
        _apiKey = options.Value.ApiKey ?? throw new ArgumentException("ENTSOE ApiKey is not configured");
        _domain = options.Value.Domain ?? throw new ArgumentException("ENTSOE Domain is not configured");
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task<ElectricityPrice[]> GetPricesAsync(int year, int month, int day, CancellationToken cancellationToken)
    {
        // rate limit
        await Task.Delay(1000, cancellationToken);

        // The entsoe API accepts periodStart and periodEnd query parameters formatted as yyyyMMddHHmm.
        // To fetch the data of one day, these should be the same.
        DateTime periodStart = new DateTime(year, month, day);
        string periodStartText = periodStart.ToString("yyyyMMddHHmm");
        _logger.Log(LogLevel.Debug, "requesting prices from entso.eu api for {periodStartText}", periodStartText);
        string url = string.Format(
            URL_TEMPLATE,
            HttpUtility.UrlEncode(_apiKey),
            HttpUtility.UrlEncode(_domain),
            HttpUtility.UrlEncode(periodStartText),
            HttpUtility.UrlEncode(periodStartText));

        string? responseText = null;
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            responseText = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var xdoc = XDocument.Parse(responseText);
            if (xdoc.Root == null)
            {
                // fixes a nullability error - shouldn't actually happen
                throw new InvalidDataException();
            }

            // The entso.eu API will sometimes update the "publication document" version embedded in the XML namespace without warning
            // and without managing multiple versions of the API.
            //
            // Example namespaces:
            //      urn:iec62325.351:tc57wg16:451-3:publicationdocument:7:0
            //      urn:iec62325.351:tc57wg16:451-3:publicationdocument:7:3
            //
            // So we have little choice but to accept any namespace.
            string namespaceName = xdoc.Root.Name.Namespace.NamespaceName;

            // Check that there is only one time resolution in the response, and it is PT15M.
            var resolutionElementName = XName.Get("resolution", namespaceName);
            string timeResolution = xdoc.Descendants(resolutionElementName).Single().Value;
            if (timeResolution != "PT15M")
            {
                throw new InvalidDataException($"Expected time resolution 'PT15M' in response from entso.eu API but got '{timeResolution}'");
            }

            // Get the time interval from the response.
            var timeIntervalElementName = XName.Get("period.timeInterval", namespaceName);
            var startElementName = XName.Get("start", namespaceName);
            var endElementName = XName.Get("end", namespaceName);
            var timeIntervalElement = xdoc.Descendants(timeIntervalElementName).Single();
            string? startText = timeIntervalElement.Element(startElementName)?.Value;
            string? endText = timeIntervalElement.Element(endElementName)?.Value;
            if (startText == null || endText == null)
            {
                throw new InvalidDataException("Missing time interval data in in response from entso.eu API");
            }
            DateTime startTime = DateTime.Parse(startText, null, DateTimeStyles.RoundtripKind);
            DateTime endTime = DateTime.Parse(endText, null, DateTimeStyles.RoundtripKind);
            if (startTime > endTime)
            {
                throw new InvalidDataException($"Got improper time interval in response from entso.eu API: {startText} to {endText}");
            }

            // Extract price points. Each price point is structured as shown below,
            // with the position representing the exact time in the requested time range.
            //
            // To calculate the time when a price takes effect, we subtract one from the position
            // (to turn it into a proper zero-based offset) and then add that number of quarter-hours to the
            // start of the requested range.
            //
            // Unfortunately, it seems that the API can omit positions if the price doesn't change.
            // For example, if position 23 is missing, then the price of position 22 is in effect
            // for more than one quarter-hour. We fill in such holes.
            //
            // <Point>
            //        <position>1</position>
            //        <price.amount>74.33</price.amount>
            // </Point>

            XName pointElementName = XName.Get("Point", namespaceName);
            XName positionElementName = XName.Get("position", namespaceName);
            XName priceElementName = XName.Get("price.amount", namespaceName);
            List<XElement> pointElements = xdoc.Descendants(pointElementName).ToList();
            var pricePointList = new List<ElectricityPrice>();

            for (int i = 0; i < pointElements.Count; i++)
            {
                // Get the position and price of this point.
                XElement pointElement = pointElements[i];
                XElement? positionElement = pointElement.Element(positionElementName);
                if (positionElement == null)
                {
                    throw new InvalidDataException("entso.eu API response has a 'Point' point without a 'position'.");
                }
                int position = Int32.Parse(positionElement.Value);
                var priceElement = pointElement.Element(priceElementName);
                if (priceElement == null)
                {
                    throw new InvalidDataException("HTTP request to entso.eu API returned a 'Point' point without a 'price.amount'.");
                }
                decimal priceEuroMWh = decimal.Parse(priceElement.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                int priceEurocentMWh = (int)(priceEuroMWh * 100);

                // Get the position of the next point, if any.
                XElement? nextPointElement = (i < pointElements.Count - 1) ? pointElements[i + 1] : null;
                XElement? nextPositionElement = nextPointElement?.Element(positionElementName);
                int? nextPosition = (nextPositionElement != null) ? Int32.Parse(nextPositionElement.Value) : null;

                // Check for holes in the data at the start.
                if (i== 0 && position != 1)
                {
                    throw new InvalidDataException("entso.eu API response does not start with a 'Point' at position 1");
                }

                // Check for improper order of the points.
                if (nextPosition != null && nextPosition <= position)
                {
                    throw new InvalidDataException("entso.eu API response has a 'Point' point with a 'position' that indicates improper order.");
                }

                // Calculate the time when this price point takes effect.
                // This is based on the overall startTime, point position and time resolution.
                DateTime priceStartTime = startTime.Add((position - 1) * QUARTER_HOUR);

                // The price is in effect until the next price point, or the end of the requested range if there is no next point.
                // It is not guarantueed that the next point is one quarter-hour later. Not all positions are required to be present.
                DateTime priceEndTime = (nextPosition != null) ? startTime.Add((nextPosition.Value - 1) * QUARTER_HOUR) : endTime;

                pricePointList.Add(new ElectricityPrice(priceStartTime, priceEndTime, priceEurocentMWh));
            }

            _logger.Log(LogLevel.Debug, "Successfully parsed this response from entso.eu api: {response}", responseText);
            return pricePointList.ToArray();
        }
        catch (HttpRequestException e)
        {
            _logger.Log(LogLevel.Error, "Content of non-success response from entso.eu api: {response}", responseText);
            throw new PriceFetcherException("HTTP request to entso.eu API failed.", e);
        }
        catch (TaskCanceledException e)
        {
            throw new PriceFetcherException("HTTP request to entso.eu API timed out or was cancelled.", e);
        }
        catch (Exception e) when (e is XmlException || e is FormatException || e is InvalidDataException || e is InvalidOperationException)
        {
            _logger.Log(LogLevel.Error, "Unparsed content of response from entso.eu api: {response}", responseText);
            throw new PriceFetcherException("HTTP request to entso.eu API returned unexpected data", e);
        }

    }
}
