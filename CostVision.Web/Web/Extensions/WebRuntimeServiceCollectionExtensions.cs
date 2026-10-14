using CostVision.Web.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace CostVision.Web.Extensions;

public static class WebRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddWebRuntimeConfiguration(
        this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        string root = Path.GetFullPath(contentRootPath);
        services.AddOptions<UploadSettings>()
            .Bind(configuration.GetSection(UploadSettings.SECTION_NAME))
            .Validate(settings => settings.MaxRequestBodyBytes > 0, "Uploads: invalid request body limit.")
            .ValidateOnStart();
        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<UploadSettings>>((options, settings) =>
            {
                options.Limits.MaxRequestBodySize = settings.Value.MaxRequestBodyBytes;
            });

        services.AddOptions<DataProtectionSettings>()
            .Bind(configuration.GetSection(DataProtectionSettings.SECTION_NAME))
            .PostConfigure(settings =>
            {
                if (string.IsNullOrWhiteSpace(settings.KeyDirectory))
                {
                    string directory = "keys-linux";
                    if (OperatingSystem.IsWindows())
                    {
                        directory = "keys-windows";
                    }

                    settings.KeyDirectory = Path.Combine(root, directory);
                }
                else
                {
                    settings.KeyDirectory = Path.GetFullPath(settings.KeyDirectory, root);
                }
            })
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.ApplicationName), "DataProtection: application name is required.")
            .ValidateOnStart();
        services.AddDataProtection();
        services.AddOptions<DataProtectionOptions>()
            .Configure<IOptions<DataProtectionSettings>>((options, settings) =>
            {
                options.ApplicationDiscriminator = settings.Value.ApplicationName;
            });
        services.AddOptions<KeyManagementOptions>()
            .Configure<IOptions<DataProtectionSettings>, ILoggerFactory>((options, settings, loggerFactory) =>
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(settings.Value.KeyDirectory), loggerFactory);
            });

        services.AddOptions<SwaggerSettings>()
            .Bind(configuration.GetSection(SwaggerSettings.SECTION_NAME))
            .ValidateOnStart();

        return services;
    }
}
