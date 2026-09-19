
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
    private readonly string _responseDebugDirectory;

    public EntsoePriceFetcher(IOptions<EntsoeOptions> options, ILogger<EntsoePriceFetcher> logger)
    {
        _apiKey = options.Value.ApiKey ?? throw new ArgumentException("ENTSOE ApiKey is not configured");
        _domain = options.Value.Domain ?? throw new ArgumentException("ENTSOE Domain is not configured");
        _responseDebugDirectory = options.Value.ResponseDebugDirectory;
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

        bool problematicResponseContent = false;
        string? responseText = null;
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            responseText = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var prices = EntsoeResponseParser.Parse(responseText, _logger);
            return prices;
        }
        catch (HttpRequestException e)
        {
            problematicResponseContent = true;
            throw new PriceFetcherException("HTTP request to entso.eu API got a non-success response.", e);
        }
        catch (TaskCanceledException e)
        {
            throw new PriceFetcherException("HTTP request to entso.eu API timed out or was cancelled.", e);
        }
        catch (Exception e) when (e is XmlException || e is FormatException || e is InvalidDataException || e is InvalidOperationException)
        {
            problematicResponseContent = true;
            throw new PriceFetcherException("HTTP request to entso.eu API returned unexpected data", e);
        }
        finally
        {
            // If the response content was problematic, or if debug logging is enabled, write the response to a file.
            // Only the filepath is logged, to avoid cluttering the logs with potentially large XML content.
            // This is also a good habit to avoid log forging vulnerabilties.
            if ((problematicResponseContent || _logger.IsEnabled(LogLevel.Debug)) && responseText != null)
            {
                Directory.CreateDirectory(_responseDebugDirectory);
                string responseDebugFilePath = Path.Combine(_responseDebugDirectory, $"entsoe_response_{Guid.NewGuid()}.xml");
                await File.WriteAllTextAsync(responseDebugFilePath, responseText, cancellationToken);

                LogLevel logLevel = problematicResponseContent ? LogLevel.Warning : LogLevel.Debug;
                _logger.Log(logLevel, "Wrote entsoe.eu api response to {responseDebugFilePath}", responseDebugFilePath);
            }
        }
    }
}
