using L4LB.Models;

namespace L4LB.Core;

/// <summary>
/// Selects a backend using the least-connections policy.
/// For small backend pools, a simple O(n) scan keeps the code easy to follow.
/// </summary>
public sealed class LoadBalancer
{
    private readonly List<Backend> _backends;
    public IReadOnlyList<Backend> Backends => _backends;

    /// <summary>Create a new balancer seeded with backend endpoints.</summary>
    public LoadBalancer(IEnumerable<(string Host, int Port)> backends)
        => _backends = backends.Select(endpoint => new Backend(endpoint)).ToList();

    /// <summary>
    /// Select the healthy backend with the fewest active connections.
    /// Returns null if no healthy backend exists.
    /// </summary>
    public Backend? SelectBackendWithFewestConnections()
    {
        Backend? selected = null; long best = long.MaxValue;
        foreach (var backend in _backends)
        {
            if (!backend.IsHealthy) continue;
            var count = backend.ActiveConnectionCount;
            if (count < best) { selected = backend; best = count; }
        }
        return selected;
    }
}