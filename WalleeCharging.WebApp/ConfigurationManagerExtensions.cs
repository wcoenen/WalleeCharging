using Microsoft.Extensions.Configuration;

namespace WalleeCharging.WebApp;

public static class ConfigurationManagerExtensions
{
    public static string GetRequiredValue(this ConfigurationManager configurationManager, string key)
    {
        return GetRequiredValue<string>(configurationManager, key);
    }

    public static T GetRequiredValue<T>(this ConfigurationManager configurationManager, string key)
    {
        T? value = configurationManager.GetValue<T>(key);
        if (value == null)
        {
            throw new System.Configuration.ConfigurationErrorsException($"missing required configuration key '{key}'");
        }
        else
        {
            return value;
        }
    }
}