using System.Net;
using CostVision.Web.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace CostVision.Web.Extensions;

public static class ForwardedHeadersServiceCollectionExtensions
{
    public static IServiceCollection AddForwardedHeadersConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ForwardedHeadersSettings>()
            .Bind(configuration.GetSection(ForwardedHeadersSettings.SECTION_NAME))
            .Validate(
                settings => settings.KnownProxies.Count > 0,
                "Не указан ни один доверенный reverse proxy.")
            .Validate(
                settings => settings.KnownProxies.All(address => IPAddress.TryParse(address, out _)),
                "Адрес доверенного reverse proxy должен быть корректным IP-адресом.")
            .ValidateOnStart();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                    | ForwardedHeaders.XForwardedProto
                    | ForwardedHeaders.XForwardedHost;

                foreach (string knownProxy in settings.Value.KnownProxies)
                    options.KnownProxies.Add(IPAddress.Parse(knownProxy));
            });

        return services;
    }
}
