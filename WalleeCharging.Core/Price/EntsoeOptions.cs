public class EntsoeOptions
{
    /// <summary>
    /// API key for accessing the ENTSO-E transparency platform.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// EIC code for which to fetch prices, e.g. "10YNL----------L" for the Netherlands, "10YBE----------2" for Belgium, ...
    /// See https://eepublicdownloads.entsoe.eu/clean-documents/nc-tasks/SDAC%20costs%20coefficient%20%E2%80%93%2017.06.2021.pdf for a list.
    /// </summary>
    public string? Domain { get; set; }
}