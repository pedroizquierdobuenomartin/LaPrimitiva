using System.Net;

namespace LaPrimitiva.App.Security;

public static class LocalOnlyPolicy
{
    private static readonly string[] PortOnlyConfigurationKeys = ["http_ports", "https_ports"];

    public static void ValidateStartupConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ValidateUrlList(configuration["urls"], "urls");

        foreach (var key in PortOnlyConfigurationKeys)
        {
            if (!string.IsNullOrWhiteSpace(configuration[key]))
            {
                throw new InvalidOperationException(
                    $"La configuración '{key}' publica en todas las interfaces. " +
                    "Configure una URL explícita de loopback mediante 'urls'.");
            }
        }

        foreach (var endpoint in configuration.GetSection("Kestrel:Endpoints").GetChildren())
        {
            ValidateUrlList(endpoint["Url"], $"Kestrel:Endpoints:{endpoint.Key}:Url");
        }
    }

    public static bool IsLoopbackAddress(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        var normalizedAddress = address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;

        return IPAddress.IsLoopback(normalizedAddress);
    }

    private static void ValidateUrlList(string? configuredUrls, string configurationKey)
    {
        if (string.IsNullOrWhiteSpace(configuredUrls))
        {
            return;
        }

        foreach (var configuredUrl in configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !IsLoopbackHost(uri.Host))
            {
                throw new InvalidOperationException(
                    $"La URL '{configuredUrl}' de '{configurationKey}' no es de loopback. " +
                    "La aplicación solo puede escuchar en localhost, 127.0.0.1 o ::1.");
            }
        }
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IsLoopbackAddress(address);
    }
}
