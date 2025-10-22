namespace L4LB;

/// <summary>
/// Centralised configuration object plus minimal CLI parsing.
/// </summary>
public sealed record Config(
    (string Host, int Port) ClientListenEndpoint,
    (string Host, int Port) AdminListenEndpoint,
    (string Host, int Port)[] BackendEndpoints,
    TimeSpan HealthCheckInterval,
    TimeSpan HealthCheckTimeout,
    TimeSpan IdleConnectionTimeout)
{
    /// <summary>
    /// Parse command line arguments of the form -key value.
    /// Provides me sensible defaults for local demos.
    /// </summary>
    public static Config Parse(string[] args)
    {
        var clientListen = ("0.0.0.0", 5432);
        var adminListen  = ("0.0.0.0", 9090);
        var backends     = new[] { ("127.0.0.1", 9001), ("127.0.0.1", 9002) };
        var healthEvery  = TimeSpan.FromSeconds(2);
        var healthTimeout= TimeSpan.FromMilliseconds(800);
        var idleTimeout  = TimeSpan.FromMinutes(5);

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!args[i].StartsWith('-')) continue;
            var key = args[i].TrimStart('-');
            var val = args[i + 1];
            switch (key)
            {
                case "listen":           clientListen = ParseHostPort(val); break;
                case "admin":            adminListen  = ParseHostPort(val); break;
                case "backends":         backends     = ParseBackendsCsv(val); break;
                case "health-interval":  healthEvery  = TimeSpan.FromMilliseconds(ParseInt(val, (int)healthEvery.TotalMilliseconds)); break;
                case "health-timeout":   healthTimeout= TimeSpan.FromMilliseconds(ParseInt(val, (int)healthTimeout.TotalMilliseconds)); break;
                case "idle-timeout":     idleTimeout  = TimeSpan.FromMilliseconds(ParseInt(val, (int)idleTimeout.TotalMilliseconds)); break;
            }
        }
        return new Config(clientListen, adminListen, backends, healthEvery, healthTimeout, idleTimeout);
    }

    /// <summary>Parse a HOST:PORT string into a tuple.</summary>
    public static (string Host, int Port) ParseHostPort(string hostPort)
    {
        var idx = hostPort.LastIndexOf(':');
        if (idx < 0) throw new ArgumentException($"Invalid host:port '{hostPort}'");
        return (hostPort[..idx], int.Parse(hostPort[(idx + 1)..]));
    }

    /// <summary>Parse comma-separated backends list into tuples.</summary>
    public static (string Host, int Port)[] ParseBackendsCsv(string csv)
        => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
              .Select(ParseHostPort)
              .ToArray();

    /// <summary>Parse an int or return fallback on failure.</summary>
    private static int ParseInt(string s, int fallback) => int.TryParse(s, out var v) ? v : fallback;
}