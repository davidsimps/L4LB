namespace L4LB.Models;

/// <summary>
/// Represents one backend target plus its runtime health and simple counters.
/// </summary>
public sealed class Backend
{
    public string Host { get; }
    public int Port { get; }

    /// <summary>True if the last health probe succeeded.</summary>
    public bool IsHealthy { get; private set; }

    /// <summary>Number of active client connections currently routed here.</summary>
    public long ActiveConnectionCount => _activeConnectionCount;

    /// <summary>Last probe error message, if any (useful for /metrics display).</summary>
    public string LastProbeError => _lastProbeError;

    private long _activeConnectionCount; // updated via Interlocked
    private string _lastProbeError = string.Empty;

    /// <summary>Create a backend from a host/port tuple.</summary>
    public Backend((string Host, int Port) endpoint)
    { Host = endpoint.Host; Port = endpoint.Port; }

    /// <summary>Record health state from a probe.</summary>
    public void SetHealth(bool healthy, string? error)
    { IsHealthy = healthy; _lastProbeError = error ?? string.Empty; }

    /// <summary>Increment active connection counter.</summary>
    public void IncrementConnectionCount() => Interlocked.Increment(ref _activeConnectionCount);

    /// <summary>Decrement active connection counter.</summary>
    public void DecrementConnectionCount() => Interlocked.Decrement(ref _activeConnectionCount);
}