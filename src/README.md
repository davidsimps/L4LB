# C# Layer-4 TCP Reverse Proxy (Load Balancer)

**What it does**
- Listens on a TCP port and forwards each client to a **healthy** backend.
- Uses the **least-connections** algorithm (good for uneven/long-lived sessions).
- Performs **active TCP health checks** and removes offline services from selection.
- Exposes **/metrics** and **/health** via a tiny built-in HTTP server.

**Run locally**
```bash
# run two dummy backends
nc -lk 9001 &
nc -lk 9002 &

# run the balancer (from src)
dotnet run -- -listen 0.0.0.0:7000 -admin 0.0.0.0:9090 -backends 127.0.0.1:9001,127.0.0.1:9002

# test
nc 127.0.0.1 7000
curl http://127.0.0.1:9090/metrics
curl http://127.0.0.1:9090/health
```

**Code map**
- `Program.cs` – wires together HealthChecker, AdminServer, and TcpReverseProxy.
- `Config.cs` – parses CLI and holds run settings.
- `Models/Backend.cs` – backend state (health + counters).
- `Core/LoadBalancer.cs` – selects backend with fewest connections.
- `Core/HealthChecker.cs` – active TCP probes.
- `Core/TcpReverseProxy.cs` – full-duplex relaying between client and backend.
- `Admin/AdminServer.cs` – minimal HTTP for visibility.

**Notes**
- Simple flow; every public method has a brief summary.