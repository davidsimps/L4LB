using L4LB.Models;
using Xunit;

namespace L4LB.Tests;

public class BackendTests
{
    [Fact]
    public void Connection_Counter_Goes_Up_And_Down()
    {
        // h = host
        var backend = new Backend(("h", 1));
        Assert.Equal(0, backend.ActiveConnectionCount);
        backend.IncrementConnectionCount();
        backend.IncrementConnectionCount();
        Assert.Equal(2, backend.ActiveConnectionCount);
        backend.DecrementConnectionCount();
        Assert.Equal(1, backend.ActiveConnectionCount);
    }
}