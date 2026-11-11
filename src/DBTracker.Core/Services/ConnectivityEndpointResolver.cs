using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ConnectivityEndpoint(bool CanCheck, string Host, int Port, bool UsedHostOverride, bool UsedPortOverride, string? DisabledReason);

/// <summary>Resolves the host:port a connectivity check probes from ServerIp/DatabaseIp and optional overrides, including the nothing-to-check case.</summary>
public static class ConnectivityEndpointResolver
{
    public static ConnectivityEndpoint Resolve(string serverIp, string databaseIp, DatabaseEngine engine, string? hostOverride, int? portOverride)
    {
        var host = !string.IsNullOrWhiteSpace(hostOverride)
            ? hostOverride!.Trim()
            : !string.IsNullOrWhiteSpace(serverIp) ? serverIp.Trim()
            : databaseIp.Trim();
        var usedHostOverride = !string.IsNullOrWhiteSpace(hostOverride);

        if (string.IsNullOrWhiteSpace(host))
        {
            return new ConnectivityEndpoint(false, "", 0, usedHostOverride, false, "No host, IP or DNS override configured, so there's nothing to check.");
        }

        var defaultPort = DefaultPortMapper.GetDefaultPort(engine);
        var port = defaultPort;
        var usedPortOverride = false;

        if (portOverride is { } p)
        {
            if (!IsValidPort(p))
            {
                // Falls back to the engine default rather than probing an invalid port.
                port = defaultPort;
            }
            else
            {
                port = p;
                usedPortOverride = true;
            }
        }

        if (port <= 0)
        {
            return new ConnectivityEndpoint(false, host, 0, usedHostOverride, usedPortOverride, "No known default port for this engine and no valid port override configured.");
        }

        return new ConnectivityEndpoint(true, host, port, usedHostOverride, usedPortOverride, null);
    }

    public static bool IsValidPort(int port) => port is > 0 and <= 65535;
}
