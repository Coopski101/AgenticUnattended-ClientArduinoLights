using System.IO.Ports;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ArduinoBridge;

public sealed class BridgeService : IDisposable
{
    private readonly BridgeConfig _config;
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly Dictionary<string, string> _sessions = new();
    private SerialPort? _serial;
    private string _lastCommand = "";

    public event Action<string>? LogMessage;

    public BridgeService(BridgeConfig config)
    {
        _config = config;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Log("Agentic AI Beacon — Arduino Bridge");

        _serial = await DiscoverArduino(_config.ComPort, _config.BaudRate, ct);
        if (_serial is null)
        {
            Log("No beacon Arduino found.");
            return;
        }

        Log($"Serial: {_serial.PortName} @ {_serial.BaudRate}");

        int backoff = _config.ReconnectBaseMs;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await WaitForHealth(ct);
                backoff = _config.ReconnectBaseMs;
                Log("Connected — listening for events...");

                using var req = new HttpRequestMessage(HttpMethod.Get, $"{_config.SseUrl}/events");
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                string? eventType = null;

                while (!ct.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(ct);
                    if (line is null) break;

                    if (line.StartsWith("event:"))
                        eventType = line["event:".Length..].Trim();
                    else if (line.StartsWith("data:") && eventType is not null)
                    {
                        ProcessEvent(eventType, line["data:".Length..].Trim());
                        eventType = null;
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Connection lost: {ex.Message}");
            }

            if (!ct.IsCancellationRequested)
            {
                Log($"Reconnecting in {backoff}ms...");
                try { await Task.Delay(backoff, ct); }
                catch (OperationCanceledException) { break; }
                backoff = Math.Min(backoff * 2, _config.ReconnectMaxMs);
            }
        }

        SendCommand("C");
        _serial.Close();
        Log("Goodbye.");
    }

    private void ProcessEvent(string eventType, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string sessionId = root.GetProperty("sessionId").GetString() ?? "";

        switch (eventType)
        {
            case "Waiting":
                _sessions[sessionId] = "Waiting";
                break;
            case "Done":
                _sessions[sessionId] = "Done";
                break;
            case "Clear":
                _sessions[sessionId] = "Clear";
                break;
            case "SessionEnded":
                _sessions.Remove(sessionId);
                break;
        }

        Reconcile();
    }

    private void Reconcile()
    {
        string command;
        if (_sessions.Values.Any(v => v == "Waiting"))
            command = "W";
        else if (_sessions.Values.Any(v => v == "Done"))
            command = "D";
        else
            command = "C";

        SendCommand(command);
    }

    private void SendCommand(string cmd)
    {
        if (cmd == _lastCommand) return;
        _lastCommand = cmd;
        try
        {
            _serial?.Write(cmd);
            Log($"→ Serial: {cmd}");
        }
        catch (Exception ex)
        {
            Log($"Serial write failed: {ex.Message}");
        }
    }

    private async Task WaitForHealth(CancellationToken ct)
    {
        int delay = 1000;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var resp = await _http.GetAsync($"{_config.SseUrl}/health", ct);
                if (resp.IsSuccessStatusCode) return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { }
            Log($"Server not ready — retrying in {delay}ms...");
            await Task.Delay(delay, ct);
            delay = Math.Min(delay * 2, 30000);
        }
        ct.ThrowIfCancellationRequested();
    }

    private async Task<SerialPort?> DiscoverArduino(string? preferredPort, int baud, CancellationToken ct)
    {
        int delay = _config.ReconnectBaseMs;
        while (!ct.IsCancellationRequested)
        {
            string[] ports = !string.IsNullOrEmpty(preferredPort)
                ? [preferredPort]
                : SerialPort.GetPortNames();

            foreach (string name in ports)
            {
                if (ct.IsCancellationRequested) return null;

                SerialPort? sp = null;
                try
                {
                    sp = new SerialPort(name, baud) { ReadTimeout = 2000, WriteTimeout = 1000 };
                    sp.Open();
                    Thread.Sleep(2000);
                    sp.DiscardInBuffer();
                    sp.Write("H");
                    string reply = sp.ReadLine().Trim();
                    if (reply == "OK")
                    {
                        sp.ReadTimeout = SerialPort.InfiniteTimeout;
                        sp.WriteTimeout = SerialPort.InfiniteTimeout;
                        Log($"Handshake OK on {name}");
                        return sp;
                    }
                    sp.Close();
                }
                catch
                {
                    try { sp?.Close(); } catch { }
                }
            }

            Log($"No beacon found — retrying in {delay}ms...");
            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) { return null; }
            delay = Math.Min(delay * 2, _config.ReconnectMaxMs);
        }
        return null;
    }

    private void Log(string message) =>
        LogMessage?.Invoke($"{DateTime.Now:HH:mm:ss} {message}");

    public void Dispose()
    {
        _http.Dispose();
        _serial?.Dispose();
    }
}
