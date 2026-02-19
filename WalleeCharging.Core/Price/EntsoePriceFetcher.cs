
using System.Web;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WalleeCharging.Price;

public class EntsoePriceFetcher : IPriceFetcher
{
    private readonly string _domain;
    private readonly string URL_TEMPLATE =
    "https://web-api.tp.entsoe.eu/api?"
        +"securityToken={0}&documentType=A44&in_Domain={1}&out_Domain={1}&periodStart={2}&periodEnd={3}";

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

            var prices = EntsoeResponseParser.Parse(responseText);
            _logger.Log(LogLevel.Debug, "Successfully parsed this response from entso.eu api: {response}", responseText);
            return prices;
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
