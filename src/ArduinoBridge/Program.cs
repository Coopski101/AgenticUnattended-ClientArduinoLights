using System.IO.Ports;
using System.Net.Http.Headers;
using System.Text.Json;
using ArduinoBridge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var config = new BridgeConfig();
builder.Configuration.GetSection("Bridge").Bind(config);

builder.Logging.AddSimpleConsole(opts =>
{
    opts.SingleLine = true;
    opts.TimestampFormat = "HH:mm:ss ";
});

using IHost host = builder.Build();
ILogger log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ArduinoBridge");

log.LogInformation("Agentic AI Beacon — Arduino Bridge");
log.LogInformation("Press Ctrl+C to exit.");

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

SerialPort? serial = OpenSerial(config.ComPort, config.BaudRate);
if (serial is null)
{
    log.LogCritical("No serial port found. Exiting.");
    return;
}

log.LogInformation("Serial: {PortName} @ {BaudRate}", serial.PortName, serial.BaudRate);

Dictionary<string, string> sessions = new();
string lastCommand = "";

using HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
int backoff = config.ReconnectBaseMs;

while (!cts.IsCancellationRequested)
{
    try
    {
        await WaitForHealth(http, config.SseUrl, cts.Token);
        backoff = config.ReconnectBaseMs;

        log.LogInformation("Connected — listening for events...");

        using HttpRequestMessage req = new(HttpMethod.Get, $"{config.SseUrl}/events");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using HttpResponseMessage resp = await http.SendAsync(
            req,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token
        );
        resp.EnsureSuccessStatusCode();

        using Stream stream = await resp.Content.ReadAsStreamAsync(cts.Token);
        using StreamReader reader = new(stream);

        string? eventType = null;

        while (!cts.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cts.Token);
            if (line is null)
                break;

            if (line.StartsWith("event:"))
            {
                eventType = line["event:".Length..].Trim();
            }
            else if (line.StartsWith("data:") && eventType is not null)
            {
                string json = line["data:".Length..].Trim();
                ProcessEvent(eventType, json);
                eventType = null;
            }
        }
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        break;
    }
    catch (Exception ex)
    {
        log.LogWarning("Connection lost: {Message}", ex.Message);
    }

    if (!cts.IsCancellationRequested)
    {
        log.LogInformation("Reconnecting in {BackoffMs}ms...", backoff);
        try
        {
            await Task.Delay(backoff, cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        backoff = Math.Min(backoff * 2, config.ReconnectMaxMs);
    }
}

SendCommand("C");
serial.Close();
log.LogInformation("Goodbye.");

void ProcessEvent(string eventType, string json)
{
    using JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;
    string sessionId = root.GetProperty("sessionId").GetString() ?? "";
    string reason = root.TryGetProperty("reason", out JsonElement r) ? r.GetString() ?? "" : "";

    log.LogDebug("[{EventType}] session={SessionId} reason={Reason}", eventType, sessionId, reason);

    switch (eventType)
    {
        case "Waiting":
            sessions[sessionId] = "Waiting";
            break;
        case "Done":
            sessions[sessionId] = "Done";
            break;
        case "Clear":
            sessions[sessionId] = "Clear";
            break;
        case "SessionEnded":
            sessions.Remove(sessionId);
            break;
    }

    Reconcile();
}

void Reconcile()
{
    string command;
    if (sessions.Values.Any(v => v == "Waiting"))
        command = "W";
    else if (sessions.Values.Any(v => v == "Done"))
        command = "D";
    else
        command = "C";

    SendCommand(command);
}

void SendCommand(string cmd)
{
    if (cmd == lastCommand)
        return;
    lastCommand = cmd;
    try
    {
        serial.Write(cmd);
        log.LogInformation("→ Serial: {Command}", cmd);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Serial write failed");
    }
}

async Task WaitForHealth(HttpClient http, string baseUrl, CancellationToken ct)
{
    int delay = 1000;
    while (!ct.IsCancellationRequested)
    {
        try
        {
            using HttpResponseMessage resp = await http.GetAsync($"{baseUrl}/health", ct);
            if (resp.IsSuccessStatusCode)
                return;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch { }
        log.LogWarning("Server not ready — retrying in {DelayMs}ms...", delay);
        await Task.Delay(delay, ct);
        delay = Math.Min(delay * 2, 30000);
    }
    ct.ThrowIfCancellationRequested();
}

SerialPort? OpenSerial(string? portName, int baud)
{
    if (!string.IsNullOrEmpty(portName))
    {
        SerialPort sp = new(portName, baud);
        sp.Open();
        return sp;
    }

    foreach (string name in SerialPort.GetPortNames())
    {
        try
        {
            SerialPort sp = new(name, baud);
            sp.Open();
            log.LogInformation("Auto-detected serial port: {PortName}", name);
            return sp;
        }
        catch { }
    }

    return null;
}
