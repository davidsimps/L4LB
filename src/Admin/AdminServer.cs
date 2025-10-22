using L4LB.Core;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace L4LB.Admin;

/// <summary>
/// Minimal, dependency-free HTTP server exposing /metrics and /health.
/// Keeps the project self-contained and easy to understand.
/// </summary>
public static class AdminServer
{
    /// <summary>
    /// Start the admin HTTP listener on a background task.
    /// </summary>
    public static void Start(LoadBalancer balancer, (string Host, int Port) adminEndpoint, CancellationToken cancelToken)
    {
        _ = Task.Run(async () =>
        {
            var listener = new TcpListener(IPAddress.Parse(adminEndpoint.Host), adminEndpoint.Port);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Start();
            Console.WriteLine($"Admin HTTP on http://{adminEndpoint.Host}:{adminEndpoint.Port}  (GET /metrics, /health)");

            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(cancelToken);
                    _ = Task.Run(() => HandleAdminClientAsync(client, balancer, cancelToken), cancelToken);
                }
            }
            catch (OperationCanceledException) { /* normal on shutdown */ }
            finally { listener.Stop(); }
        }, cancelToken);
    }

    /// <summary>
    /// Handle a single admin HTTP client; supports GET /metrics and GET /health.
    /// </summary>
    private static async Task HandleAdminClientAsync(TcpClient client, LoadBalancer balancer, CancellationToken cancelToken)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var (method, path) = await ReadHttpRequestLineAsync(stream, cancelToken);

            if (path == "/metrics")
            {
                var payload = JsonSerializer.Serialize(new
                {
                    time = DateTimeOffset.UtcNow,
                    backends = balancer.Backends.Select(b => new
                    {
                        address = $"{b.Host}:{b.Port}",
                        healthy = b.IsHealthy,
                        activeConnections = b.ActiveConnectionCount,
                        lastProbeError = b.LastProbeError
                    })
                });
                await WriteHttpResponseAsync(stream, 200, "application/json", payload);
                return;
            }

            if (path == "/health")
            {
                var anyHealthy = balancer.Backends.Any(b => b.IsHealthy);
                await WriteHttpResponseAsync(stream, anyHealthy ? 200 : 503, "text/plain", anyHealthy ? "ok" : "no healthy backends");
                return;
            }

            await WriteHttpResponseAsync(stream, 404, "text/plain", "not found");
        }
    }

    /// <summary>
    /// Read the first HTTP line (method + path) and drain headers.
    /// </summary>
    private static async Task<(string Method, string Path)> ReadHttpRequestLineAsync(NetworkStream ns, CancellationToken cancelToken)
    {
        var requestLine = await ReadLineAsync(ns, cancelToken);
        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var method = parts.Length > 0 ? parts[0] : "GET";
        var path = parts.Length > 1 ? parts[1] : "/";
        await DrainHttpHeadersAsync(ns, cancelToken);
        return (method, path);
    }

    /// <summary>
    /// Read a single CRLF-terminated line.
    /// </summary>
    private static async Task<string> ReadLineAsync(NetworkStream ns, CancellationToken cancelToken)
    {
        var sb = new StringBuilder();
        var singleByte = new byte[1];
        while (true)
        {
            int n = await ns.ReadAsync(singleByte.AsMemory(0, 1), cancelToken);
            if (n <= 0) break;
            if (singleByte[0] == (byte)'\n') break;
            sb.Append((char)singleByte[0]);
        }
        // Trim the optional preceding '\r'
        return sb.ToString().TrimEnd('\r');
    }

    /// <summary>
    /// Consume HTTP headers until CRLF CRLF.
    /// </summary>
    private static async Task DrainHttpHeadersAsync(NetworkStream ns, CancellationToken cancelToken)
    {
        var singleByte = new byte[1];
        int crlfSeen = 0;
        while (true)
        {
            int n = await ns.ReadAsync(singleByte.AsMemory(0, 1), cancelToken);
            if (n <= 0) break;
            var b = singleByte[0];
            if (b == (byte)'\r' || b == (byte)'\n')
            {
                crlfSeen++;
                if (crlfSeen >= 4) break;
            }
            else
            {
                crlfSeen = 0;
            }
        }
    }

    /// <summary>
    /// Write a minimal HTTP/1.1 response.
    /// </summary>
    private static async Task WriteHttpResponseAsync(NetworkStream ns, int status, string contentType, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var headers =
            $"HTTP/1.1 {status} \r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n\r\n";

        await ns.WriteAsync(Encoding.ASCII.GetBytes(headers));
        await ns.WriteAsync(bodyBytes);
        await ns.FlushAsync();
    }
}
