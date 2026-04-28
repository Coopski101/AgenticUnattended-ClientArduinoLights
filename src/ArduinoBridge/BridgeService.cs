using System.Net.Http.Headers;

namespace ArduinoBridge;

public sealed class BridgeService : IDisposable
{
    private readonly BridgeConfig _config;
    private readonly HttpClient _http;
    private readonly SessionTracker _tracker;
    private readonly ISerialPortFactory _serialFactory;
    private ISerialPort? _serial;
    private string _lastCommand = "";

    public event Action<string>? LogMessage;

    public BridgeService(BridgeConfig config, HttpMessageHandler? httpHandler = null, ISerialPortFactory? serialFactory = null)
    {
        _config = config;
        _http = new HttpClient(httpHandler ?? new HttpClientHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        _tracker = new SessionTracker();
        _serialFactory = serialFactory ?? new SystemSerialPortFactory();
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

        Log($"Serial: {_serial.PortName}");

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
                        string command = _tracker.ProcessEvent(eventType, line["data:".Length..].Trim());
                        SendCommand(command);
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

    private async Task<ISerialPort?> DiscoverArduino(string? preferredPort, int baud, CancellationToken ct)
    {
        int delay = _config.ReconnectBaseMs;
        while (!ct.IsCancellationRequested)
        {
            string[] ports = !string.IsNullOrEmpty(preferredPort)
                ? [preferredPort]
                : _serialFactory.GetPortNames();

            foreach (string name in ports)
            {
                if (ct.IsCancellationRequested) return null;

                ISerialPort? sp = null;
                try
                {
                    sp = _serialFactory.Create(name, baud, 2000, 1000);
                    sp.Open();
                    await Task.Delay(_config.HandshakeDelayMs, ct);
                    sp.DiscardInBuffer();
                    sp.Write("H");
                    await Task.Delay(_config.HandshakeDelayMs, ct);
                    string reply = sp.ReadLine().Trim();
                    if (reply == "OK")
                    {
                        sp.ReadTimeout = -1;
                        sp.WriteTimeout = -1;
                        Log($"Handshake OK on {name}");
                        return sp;
                    }
                    sp.Close();
                }
                catch (OperationCanceledException) { sp?.Dispose(); return null; }
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
