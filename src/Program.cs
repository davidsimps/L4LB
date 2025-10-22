using L4LB.Admin;
using L4LB.Core;
using L4LB;

/// <summary>
/// Application entry point. Parses configuration, starts health monitoring,
/// admin HTTP server, and the TCP reverse proxy listener.
/// </summary>
var config = Config.Parse(args);
var loadBalancer = new LoadBalancer(config.BackendEndpoints);

using var shutdownCts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; shutdownCts.Cancel(); };

// Start periodic health checks that mark backends up/down.
HealthChecker.StartHealthMonitoring(loadBalancer, config.HealthCheckInterval, config.HealthCheckTimeout, shutdownCts.Token);

// Start tiny admin HTTP for /metrics and /health (observability).
AdminServer.Start(loadBalancer, config.AdminListenEndpoint, shutdownCts.Token);

// Start the Layer-4 TCP reverse proxy that forwards to healthy backends.
await TcpReverseProxy.StartAsync(loadBalancer, config.ClientListenEndpoint, config.IdleConnectionTimeout, shutdownCts.Token);