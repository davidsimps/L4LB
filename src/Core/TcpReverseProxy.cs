using L4LB.Models;
using System.Net;
using System.Net.Sockets;

namespace L4LB.Core;

/// <summary>
/// Layer-4 TCP reverse proxy. Accepts client connections, selects a healthy
/// backend, connects to it, then relays bytes in both directions (full duplex).
/// </summary>
public static class TcpReverseProxy
{
    /// <summary>Start listening for client TCP connections and proxy them.</summary>
    public static async Task StartAsync(LoadBalancer balancer, (string Host, int Port) listenEndpoint, TimeSpan idleTimeout, CancellationToken cancelToken)
    {
        var listener = new TcpListener(IPAddress.Parse(listenEndpoint.Host), listenEndpoint.Port);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Start();
        Console.WriteLine($"L4 LB listening on {listenEndpoint.Host}:{listenEndpoint.Port}");

        try
        {
            while (!cancelToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancelToken);
                _ = Task.Run(() => HandleClientConnectionAsync(client, balancer, idleTimeout, cancelToken), cancelToken);
            }
        }
        catch (OperationCanceledException) { }
        finally { listener.Stop(); }
    }

    /// <summary>
    /// Handle a single client connection by selecting a backend and relaying data
    /// in both directions until one side closes or a timeout occurs.
    /// </summary>
    private static async Task HandleClientConnectionAsync(TcpClient client, LoadBalancer balancer, TimeSpan idleTimeout, CancellationToken cancelToken)
    {
        using (client)
        {
            try
            {
                var backend = balancer.SelectBackendWithFewestConnections();
                if (backend is null) return; // No healthy target → drop

                using var upstream = new TcpClient();
                using var connectDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await upstream.ConnectAsync(backend.Host, backend.Port, connectDeadline.Token);

                backend.IncrementConnectionCount();
                try
                {
                    using NetworkStream clientStream = client.GetStream();
                    using NetworkStream backendStream = upstream.GetStream();

                    ApplyIdleTimeouts(clientStream, idleTimeout);
                    ApplyIdleTimeouts(backendStream, idleTimeout);

                    // Two independent pumps implement full-duplex relaying.
                    var clientToBackend = RelayAsync(clientStream, backendStream, cancelToken);   // client → backend
                    var backendToClient = RelayAsync(backendStream, clientStream, cancelToken);   // backend → client

                    await Task.WhenAny(Task.WhenAll(clientToBackend, backendToClient), Task.Delay(idleTimeout, cancelToken));
                }
                finally
                {
                    backend.DecrementConnectionCount();
                    try { upstream.Client.Shutdown(SocketShutdown.Both); } catch { }
                }
            }
            catch
            {
                // Per-connection errors are common (resets, timeouts). Keep the listener alive.
            }
            finally
            {
                try { client.Client.Shutdown(SocketShutdown.Both); } catch { }
            }
        }
    }

    /// <summary>
    /// Copy bytes from source to destination until EOF or error. This is the core
    /// of an L4 proxy, and enables concurrent bidirectional traffic (full duplex).
    /// </summary>
    private static async Task RelayAsync(Stream source, Stream destination, CancellationToken cancelToken)
    {
        var buffer = new byte[32 * 1024];
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancelToken);
                if (read <= 0) break;
                await destination.WriteAsync(buffer.AsMemory(0, read), cancelToken);
                await destination.FlushAsync(cancelToken);
            }
        }
        catch { /* normal on disconnects */ }
        try { destination.Flush(); } catch { }
    }

    /// <summary>Apply symmetrical idle timeouts to a NetworkStream.</summary>
    private static void ApplyIdleTimeouts(NetworkStream stream, TimeSpan idleTimeout)
    {
        stream.ReadTimeout = (int)idleTimeout.TotalMilliseconds;
        stream.WriteTimeout = (int)idleTimeout.TotalMilliseconds;
    }
}