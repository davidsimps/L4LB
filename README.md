# L4LB – C# Layer-4 TCP Reverse Proxy

A small, real, runnable Layer 4 (TCP) load balancer built with .NET 8.
It accepts client TCP connections, forwards them to **healthy** backends using the **least-connections** policy, removes offline services via active health checks, and exposes simple observability via `/metrics` and `/health`.

## Requirements

* .NET 8 SDK
* `nc` (netcat) or any TCP server for quick local testing

## Quick start (run & see it work)

```bash
# 1) Start two dummy TCP backends
nc -lk 9001 &
nc -lk 9002 &

# 2) Run the load balancer from the src folder
cd src
dotnet run -- -listen 0.0.0.0:7000 -admin 0.0.0.0:9090 -backends 127.0.0.1:9001,127.0.0.1:9002

# 3) Connect as a client (in another terminal) and type some text
nc 127.0.0.1 7000

# 4) Observability
curl http://127.0.0.1:9090/metrics
curl http://127.0.0.1:9090/health
```

### What you should see

* Each new client going to either `9001` or `9002` (balanced by **least connections**).
* If you stop one backend (Ctrl-C the `nc -lk`), the `/health` endpoint goes **503** until at least one backend is healthy, and the balancer stops sending new connections to the offline service.
* `/metrics` shows each backend’s health, active connection counts, and last probe errors.

## How to test specific behaviours

* **Balancing:** open multiple client shells and run `nc 127.0.0.1 7000` in each. Watch `/metrics` and see `activeConnections` distribute.
* **Health removal:** kill `nc -lk 9001` and see new connections only go to `9002`. Restart it, and after a probe or two it will be used again.
* **Idle timeout:** leave a client idle; it will close after the configured `-idle-timeout` (defaults to 5 minutes).
* **Throughput / full-duplex:** paste data in either client or backend console; traffic flows in **both directions at the same time**.

## How it works (plain explanation)

* **Reverse proxy at Layer 4:** the balancer listens on a TCP port and forwards raw TCP streams to backends. It is *not* an HTTP proxy; it is protocol agnostic at L4.
* **Policy: least connections:** each new client is sent to the **healthy** backend with the **fewest** active connections. This suits uneven or long-lived sessions better than round robin.
* **Health checks:** a small background loop tries to connect to each backend at a short interval (by default, every 2s). Success → marked healthy; failure/timeouts → marked unhealthy. Unhealthy backends are **skipped** for new connections (existing ones aren’t dropped).
* **Full-duplex relaying:** TCP allows data in both directions simultaneously. The proxy runs two concurrent copy loops:

  * client → proxy → backend
  * backend → proxy → client
    That’s the “full-duplex” aspect.
* **Observability:** a tiny built-in HTTP server exposes:

  * `GET /metrics` — JSON snapshot of backend health and counters
  * `GET /health` — overall health (200 if any backend is healthy; 503 otherwise)

## Project structure

```
L4LB/
  src/
    Program.cs               # composition root
    Config.cs                # CLI parsing and settings
    Models/Backend.cs        # backend state (health + counters)
    Core/LoadBalancer.cs     # least-connections selection
    Core/HealthChecker.cs    # active TCP connect probes
    Core/TcpReverseProxy.cs  # L4 accept + full-duplex relaying
    Admin/AdminServer.cs     # /metrics and /health
  tests/
    L4LB.Tests/
      L4LB.Tests.csproj
      LoadBalancerTests.cs   # selection and health-skipping tests
      BackendTests.cs        # counter behaviour tests
```

## Command-line options

```
-listen HOST:PORT          # front door (default 0.0.0.0:5432)
-admin  HOST:PORT          # admin HTTP (default 0.0.0.0:9090)
-backends host:port,...    # comma-separated list (default 127.0.0.1:9001,127.0.0.1:9002)
-health-interval ms        # probe interval (default 2000)
-health-timeout  ms        # single probe timeout (default 800)
-idle-timeout    ms        # per-connection idle timeout (default 300000)
```

## Run the unit tests

```bash
# From the repo root
dotnet build
dotnet test
```

The tests cover the **least-connections** selection and the **active connection counter** behaviour.

## FAQ (short)

* **Why `/health` not `/healthz`?**
  `/healthz` is a Google/Kubernetes convention to avoid route clashes. Here we use plain **`/health`**.

* **Is this a real load balancer?**
  Yes. It listens, checks health, and forwards live TCP connections with a real scheduling policy.

## Core Features

* Layer-4 TCP reverse proxy
* Balances across multiple services
* Removes offline services from selection via active health checks
* Many concurrent clients
* Self-contained, simple to read, with comments and tests
