namespace CostVision.Web.Extensions;

public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddCostVisionConfiguration(
        this IConfigurationBuilder configuration, string contentRootPath, string[] args)
    {
        string path = Path.Combine(Path.GetFullPath(contentRootPath), "Config", "config.json");
        configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(args);

        return configuration;
    }
}
