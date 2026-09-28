# MeetSync Pro: Performance & Scalability Guide

This document outlines performance benchmarks, high-concurrency socket tuning guidelines, Redis state store caching policies, and K6 load testing procedures for **MeetSync Pro**.

---

## 1. System Architecture Throughput Benchmarks

| Metric | Target SLA / Capacity | Architecture Strategy |
| :--- | :--- | :--- |
| **Concurrent WebRTC Peers per Node** | 500 Active Connections | Thin SignalR Hub + Async I/O Pipelines |
| **WebSocket Message Throughput** | 10,000+ Msgs / Sec | Binary/JSON MessagePack + Non-blocking Sockets |
| **Connection Latency (p95)** | < 150 ms | Ephemeral Socket Expansion + Redis State Store |
| **Signaling Message RTT (p95)** | < 50 ms | Lock-free `ConcurrentDictionary` / In-Memory State |
| **Error Rate Threshold** | < 0.01% | Resilient Fallback State Store (`RedisMeetingStateStore`) |

---

## 2. Redis State Store & Backplane Efficiency

* **State Store Abstraction (`IMeetingStateStore`):**
  * Meeting room state and connection mappings are stored as fast Redis Hashes (`meetsync:room:{roomName}:participants`) and Keys (`meetsync:conn:{connectionId}:room`).
  * Automatic fallback to `InMemoryMeetingStateStore` if Redis cluster experiences transient network interruptions.
* **SignalR Redis Backplane (`AddStackExchangeRedis`):**
  * Enables seamless multi-node scaling across server clusters without requiring sticky session affinity.
  * Message pub/sub channels are namespaced using `RedisChannel.Literal("MeetSyncSignalR")`.

---

## 3. High-Concurrency Tuning Guidelines

### A. Kestrel Web Server Socket Tuning (`appsettings.json`)
```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 50000,
      "MaxConcurrentUpgradedConnections": 25000,
      "KeepAliveTimeout": "00:02:00",
      "Http2": {
        "MaxStreamsPerConnection": 100
      }
    }
  }
}
```

### B. OS & Socket Ephemeral Port Tuning (Windows PowerShell)
To prevent TCP port exhaustion under high Virtual User (VU) load test conditions:
```powershell
# Expand Windows Ephemeral Port Range
Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' -Name 'MaxUserPort' -Value 65534
Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' -Name 'TcpTimedWaitDelay' -Value 30
```

### C. .NET 10 Server Garbage Collection (`.csproj`)
Enable Server Garbage Collection and High-Performance Concurrent GC for WebRTC signaling nodes:
```xml
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
</PropertyGroup>
```

---

## 4. Running K6 SignalR Load Tests

To execute automated load testing against a running MeetSync Pro instance:

```bash
# Install K6 (via winget or choco)
winget install k6

# Execute K6 Load Test Script
k6 run --env TARGET_URL=ws://localhost:5252/meetingHub tests/load-testing/meetsync-signalr-loadtest.js
```

### Automated SLA Threshold Checks
* **`meetsync_error_rate`**: Must be under 1% (`rate < 0.01`).
* **`meetsync_connection_latency_ms`**: 95th percentile under 200 ms.
* **`meetsync_message_rtt_ms`**: 95th percentile under 100 ms.
