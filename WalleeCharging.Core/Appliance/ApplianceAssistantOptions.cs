public class ApplianceAssistantOptions
{
    /// <summary>
    /// An array of appliance profiles, each with a name and a consumption profile.
    /// </summary>
    public ApplianceProfile[] Profiles { get; set; } = Array.Empty<ApplianceProfile>();
}