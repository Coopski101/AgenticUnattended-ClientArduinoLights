# AI Agent Beacon — Arduino Bridge Client

A .NET console app that connects to the beacon-core SSE server, receives AI agent state events, and forwards them over serial to an Arduino driving a physical LED beacon.

## Architecture

```
 ┌──────────────────────────┐     SSE (HTTP)     ┌─────────────────────────┐     Serial      ┌───────────┐
 │  beacon-core server      │ ──────────────────► │  arduino-bridge client  │ ──────────────► │  Arduino  │
 │  (localhost:17321)       │   /events stream    │  (.NET console app)     │   COM port      │  (USB)    │
 └──────────────────────────┘                     └─────────────────────────┘                  └───────────┘
```

## Projects

| Path | Description |
|------|-------------|
| `src/ArduinoBridge/` | .NET 9 console app — SSE client + serial bridge |
| `firmware/` | Arduino sketch for driving indicator lights |

## Serial Protocol

| Command | Meaning | LED Behavior |
|---------|---------|--------------|
| `W\n` | Waiting | Red light on |
| `D\n` | Done | Green light on |
| `C\n` | Clear | Both off |

## Configuration

| Setting | Default | Env Var |
|---------|---------|---------|
| SSE URL | `http://127.0.0.1:17321` | `BEACON_SSE_URL` |
| COM Port | Auto-detect | `BEACON_COM_PORT` |
| Baud Rate | `9600` | `BEACON_BAUD_RATE` |

## Quick Start

```bash
cd src/ArduinoBridge
dotnet run
```

To test without a live AI agent session, start the core server in fake mode:

```bash
set BEACON_FAKE_MODE=1
# (in the beacon-core repo)
dotnet run
```
