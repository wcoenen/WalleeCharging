public class ApplianceAssistantOptions
{
    /// <summary>
    /// An array of appliance profiles, each with a name and a consumption profile.
    /// </summary>
    public ApplianceProfile[] Profiles { get; set; } = Array.Empty<ApplianceProfile>();

    /// <summary>
    /// The maximum number of hours to look ahead when looking for the optimal start time of an appliance.
    /// </summary>
    public int MaxLookAheadHours { get; set; } = 12;
}