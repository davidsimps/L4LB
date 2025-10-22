using L4LB.Core;
using Xunit;

namespace L4LB.Tests;

public class LoadBalancerTests
{
    [Fact]
    public void Selects_Healthy_Backend_With_Fewest_Connections()
    {
        // h = host
        var lb = new LoadBalancer(new[] { ("h1", 1), ("h2", 2), ("h3", 3) });
        foreach (var b in lb.Backends) b.SetHealth(true, null);
        lb.Backends[0].IncrementConnectionCount();     // h1:1
        lb.Backends[2].IncrementConnectionCount();     // h3:1
        lb.Backends[2].IncrementConnectionCount();     // h3:2
        // h2 has 0 → expected pick
        var picked = lb.SelectBackendWithFewestConnections();
        Assert.NotNull(picked);
        Assert.Equal("h2", picked!.Host);
    }

    [Fact]
    public void Ignores_Unhealthy_Backends()
    {
        var lb = new LoadBalancer(new[] { ("h1", 1), ("h2", 2) });
        lb.Backends[0].SetHealth(false, "down");
        lb.Backends[1].SetHealth(true, null);
        var picked = lb.SelectBackendWithFewestConnections();
        Assert.Equal("h2", picked!.Host);
    }
}