using Microsoft.Extensions.Configuration;

namespace WalleeCharging.WebApp;

public static class ConfigurationManagerExtensions
{
    public static string GetRequiredValue(this ConfigurationManager configurationManager, string key)
    {
        return configurationManager[key] 
            ?? throw new System.Configuration.ConfigurationErrorsException($"missing required configuration key '{key}'");
    }
}