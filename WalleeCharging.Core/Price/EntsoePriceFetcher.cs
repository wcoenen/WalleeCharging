﻿
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace WalleeCharging.Price;

public class EntsoePriceFetcher : IPriceFetcher
{
    private readonly string DOMAIN = "10YBE----------2";
    private readonly string URL_TEMPLATE = 
    "https://web-api.tp.entsoe.eu/api?"
        +"securityToken={0}&documentType=A44&in_Domain={1}&out_Domain={1}&TimeInterval={2}";
    
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public EntsoePriceFetcher(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
    }

    public async Task<ElectricityPrice[]> GetPricesAsync(DateTime day, CancellationToken cancellationToken)
    {
        if (day.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTimeKind must be UTC");

        // rate limit
        await Task.Delay(1000, cancellationToken);

        // The entsoe API accepts a TimeInterval query parameter formatted as the start and end of the
        // time interval in ISO 8601 format, with a slash between them.
        // Example from the entso.eu API documentation: "2016-01-01T00:00Z/2016-01-02T00:00Z"
        var dayEnd = day.AddDays(1);
        string timeInterval = 
            day.ToString("o", CultureInfo.InvariantCulture) 
            + "/"
            + dayEnd.ToString("o", CultureInfo.InvariantCulture);

        string url = string.Format(
            URL_TEMPLATE,
            HttpUtility.UrlEncode(_apiKey),
            HttpUtility.UrlEncode(DOMAIN),
            HttpUtility.UrlEncode(timeInterval));

        var response = await _httpClient.GetAsync(url, cancellationToken);
        string responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        
        var xdoc = XDocument.Parse(responseText);
        var priceElementName = XName.Get("price.amount", "urn:iec62325.351:tc57wg16:451-3:publicationdocument:7:0");
        var priceElements = xdoc.Descendants(priceElementName);
        
        DateTime dayUtc = day.ToUniversalTime();
        var electricityPrices = priceElements
            // parse the text values returned by the API into decimals (euro/Mwh)
            .Select(
                x => decimal.Parse(x.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture))
            // convert the decimal values (euro/MWh) into integers (eurocents/MWh)
            .Select(x => (int)(x*100))
            // finally construct ElectricityPrice objects with a timestamp
            .Select((price,index)=>
                new ElectricityPrice(dayUtc.AddHours(index), price));
        
        return electricityPrices.ToArray();
    }
}
