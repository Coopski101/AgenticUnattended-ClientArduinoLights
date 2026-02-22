# AI Agent Beacon — Arduino Bridge Client

A .NET console app that connects to the beacon-core SSE server, receives AI agent state events from one or more VS Code windows, aggregates them into a single indicator state, and forwards that state over serial to an Arduino driving a physical LED beacon.

## Architecture

```
 ┌──────────────────────────┐     SSE (HTTP)     ┌─────────────────────────┐     Serial      ┌───────────┐
 │  beacon-core server      │ ──────────────────► │  arduino-bridge client  │ ──────────────► │  Arduino  │
 │  (localhost:17321)       │   /events stream    │  (.NET console app)     │   COM port      │  (USB)    │
 └──────────────────────────┘                     └─────────────────────────┘                  └───────────┘
```

## How It Works

The beacon-core server tracks AI agent activity across all open VS Code windows and publishes state changes over SSE (see [BEACON_WIRE_PROTOCOL.md](BEACON_WIRE_PROTOCOL.md) for full details). The bridge client:

1. Checks `/health` and waits (with exponential backoff) until the server is available.
2. Opens an SSE connection to `/events` and reads the `event:` / `data:` frames.
3. Tracks per-session state in a dictionary keyed by `sessionId`.
4. Aggregates all sessions into a single indicator command using priority rules:
   - **Any session Waiting** → send `W` (red flash — agent needs attention)
   - **Any session Done** (none waiting) → send `D` (green flash — agent finished)
   - **All sessions Clear/Idle** → send `C` (lights off)
5. Sends the command over serial only when the aggregate state changes (de-duplicated).
6. On disconnect, reconnects with exponential backoff.

This means the Arduino only ever receives a single-letter command reflecting the overall state of all agent sessions, keeping the firmware simple.

## SSE Events Handled

| Event | Client Action |
|-------|---------------|
| `Waiting` | Record session as waiting, reconcile |
| `Done` | Record session as done, reconcile |
| `Clear` | Record session as clear, reconcile |
| `SessionStarted` | Ignored (no state change needed) |
| `SessionEnded` | Remove session from tracker, reconcile |

## Projects

| Path | Description |
|------|-------------|
| `src/ArduinoBridge/` | .NET 9 console app — SSE client + serial bridge |
| `firmware/beacon/` | Arduino Nano sketch for driving indicator lights |

## Serial Protocol

| Command | Meaning | LED Behavior |
|---------|---------|--------------|
| `W` | Waiting | Red light flashing (500 ms pulse) |
| `D` | Done | Green light flashing (500 ms pulse) |
| `C` | Clear | Both lights off |

## Configuration

Settings are in `appsettings.json` under the `Bridge` section:

```json
{
  "Bridge": {
    "SseUrl": "http://127.0.0.1:17321",
    "ComPort": null,
    "BaudRate": 9600,
    "ReconnectBaseMs": 1000,
    "ReconnectMaxMs": 30000
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `SseUrl` | `http://127.0.0.1:17321` | Beacon-core SSE server URL |
| `ComPort` | `null` (auto-detect) | Serial port name (e.g. `COM3`) |
| `BaudRate` | `9600` | Serial baud rate |
| `ReconnectBaseMs` | `1000` | Initial reconnect delay |
| `ReconnectMaxMs` | `30000` | Max reconnect delay |

Logging is configured via the standard `Logging` section. `appsettings.Development.json` enables `Debug` level for the `ArduinoBridge` category.

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
