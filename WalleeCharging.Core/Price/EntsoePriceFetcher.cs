
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;

namespace WalleeCharging.Price;

public class EntsoePriceFetcher : IPriceFetcher
{
    private readonly string DOMAIN = "10YBE----------2";
    private readonly string URL_TEMPLATE = 
    "https://web-api.tp.entsoe.eu/api?"
        +"securityToken={0}&documentType=A44&in_Domain={1}&out_Domain={1}&periodStart={2}&periodEnd={3}";
    private readonly TimeSpan QUARTER_HOUR = TimeSpan.FromMinutes(15);
    
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public EntsoePriceFetcher(string apiKey, ILogger<EntsoePriceFetcher> logger)
    {
        _apiKey = apiKey;
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
            HttpUtility.UrlEncode(DOMAIN),
            HttpUtility.UrlEncode(periodStartText),
            HttpUtility.UrlEncode(periodStartText));

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            string responseText = await response.Content.ReadAsStringAsync();
            var logLevel = response.StatusCode == HttpStatusCode.OK ? LogLevel.Debug : LogLevel.Error;
            _logger.Log(logLevel, "Response from entso.eu api: {response}", responseText);
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
            
            var pointElementName = XName.Get("Point", namespaceName);
            var positionElementName = XName.Get("position", namespaceName);
            var priceElementName = XName.Get("price.amount", namespaceName);
            var pointElements = xdoc.Descendants(pointElementName);
            var pricePointList = new List<ElectricityPrice>();
            int previousPosition = 0;
            ElectricityPrice lastPricePoint;

            foreach (var pointElement in pointElements)
            {
                var positionElement = pointElement.Element(positionElementName);
                if (positionElement == null)
                {
                    throw new InvalidDataException("entso.eu API response has a 'Point' point without a 'position'.");
                }
                int position = Int32.Parse(positionElement.Value);
                              
                // Check for holes in the data at the start, these cannot be filled.
                if (previousPosition == 0 && position != 1)
                {
                    throw new InvalidDataException("entso.eu API response does not start with a 'Point' at position 1");
                }

                // Check for improper order of the points.
                // Should never happen, but we need to be careful to avoid an infinite loop below.
                if (position < previousPosition)
                {
                    throw new InvalidDataException("entso.eu API response has a 'Point' point with a 'position' that indicates improper order.");
                }

                // Check for non-consecutive "position" values and fill in the holes in the data.
                // We do this by just adding one quarter-hour to the previous price point and copying the price.
                while (position != (previousPosition + 1))
                {
                    lastPricePoint = pricePointList[pricePointList.Count - 1];
                    pricePointList.Add(new ElectricityPrice(lastPricePoint.Time.Add(QUARTER_HOUR), lastPricePoint.PriceEurocentPerMWh));
                    previousPosition++;
                }

                // Add a price point for the current Point element
                var priceElement = pointElement.Element(priceElementName);
                if (priceElement == null)
                {
                    throw new InvalidDataException("HTTP request to entso.eu API returned a 'Point' point without a 'price.amount'.");
                }
                decimal priceEuroMWh = decimal.Parse(priceElement.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                int priceEurocentMWh = (int)(priceEuroMWh * 100);
                int offset = position - 1;
                var pointDateTime = startTime.Add(offset*QUARTER_HOUR);
                pricePointList.Add(new ElectricityPrice(pointDateTime, priceEurocentMWh));

                previousPosition++;
            }

            // Check for missing data after the last price point, and fill it in if necessary.
            lastPricePoint = pricePointList[pricePointList.Count - 1];
            while (lastPricePoint.Time.Add(QUARTER_HOUR) < endTime)
            {
                pricePointList.Add(new ElectricityPrice(lastPricePoint.Time.Add(QUARTER_HOUR), lastPricePoint.PriceEurocentPerMWh));
                lastPricePoint = pricePointList[pricePointList.Count - 1];
            }
         
            return pricePointList.ToArray();
        }
        catch (HttpRequestException e)
        {
            throw new PriceFetcherException("HTTP request to entso.eu API failed.", e);
        }
        catch (TaskCanceledException e)
        {
            throw new PriceFetcherException("HTTP request to entso.eu API timed out or was cancelled.", e);
        }
        catch (Exception e) when (e is XmlException || e is FormatException || e is InvalidDataException || e is InvalidOperationException)
        {
            throw new PriceFetcherException("HTTP request to entso.eu API returned unexpected data", e);
        }

    }
}
