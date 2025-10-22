using L4LB.Models;
using System.Net.Sockets;

namespace L4LB.Core;

/// <summary>
/// Periodically performs a short TCP connect to each backend to determine health.
/// If the dial succeeds the backend is healthy, otherwise it is unhealthy.
/// </summary>
public static class HealthChecker
{
    /// <summary>Starts the periodic health monitoring loop on a background task.</summary>
    public static void StartHealthMonitoring(LoadBalancer balancer, TimeSpan interval, TimeSpan timeout, CancellationToken cancelToken)
    {
        _ = Task.Run(async () =>
        {
            while (!cancelToken.IsCancellationRequested)
            {
                var probes = balancer.Backends.Select(b => ProbeBackendAsync(b, timeout, cancelToken));
                try { await Task.WhenAll(probes); } catch { /* ignore transient probe errors */ }
                try { await Task.Delay(interval, cancelToken); } catch { }
            }
        }, cancelToken);
    }

    /// <summary>Probe a single backend with a TCP connect that times out quickly.</summary>
    private static async Task ProbeBackendAsync(Backend backend, TimeSpan timeout, CancellationToken cancelToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
            cts.CancelAfter(timeout);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(backend.Host, backend.Port, cts.Token);
            backend.SetHealth(true, null);
        }
        catch (Exception ex)
        {
            backend.SetHealth(false, ex.Message);
        }
    }
}