using System.Net;

namespace ArduinoBridge.Tests;

public class BridgeServiceTests : IDisposable
{
    private readonly FakeSseHandler _httpHandler = new();
    private readonly FakeSerialPortFactory _serialFactory = new();
    private readonly BridgeConfig _config = new()
    {
        SseUrl = "http://localhost:9999",
        BaudRate = 9600,
        ReconnectBaseMs = 10,
        ReconnectMaxMs = 50,
        HandshakeDelayMs = 0
    };

    public void Dispose() => _httpHandler.Dispose();

    private BridgeService CreateService() =>
        new(_config, _httpHandler, _serialFactory);

    private FakeSerialPort AddArduinoPort(string name = "COM99")
    {
        var port = new FakeSerialPort(name);
        port.ReadLineResponses.Enqueue("OK");
        _serialFactory.Ports.Add(port);
        return port;
    }

    [Fact]
    public async Task RunAsync_NoArduinoFound_LogsAndReturns()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(500);

        using var svc = CreateService();
        var logs = new List<string>();
        svc.LogMessage += logs.Add;

        await svc.RunAsync(cts.Token);

        Assert.Contains(logs, l => l.Contains("No beacon Arduino found"));
    }

    [Fact]
    public async Task RunAsync_DiscoverArduino_SendsHandshake()
    {
        var port = AddArduinoPort();

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Waiting",
            """data: {"sessionId":"s1"}""",
            "",
            "event: Done",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(2000);
        using var svc = CreateService();
        await svc.RunAsync(cts.Token);

        Assert.Contains("H", port.Written);
    }

    [Fact]
    public async Task RunAsync_SseWaitingEvent_SendsWToSerial()
    {
        var port = AddArduinoPort();

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Waiting",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(2000);
        using var svc = CreateService();
        await svc.RunAsync(cts.Token);

        Assert.Contains("W", port.Written);
    }

    [Fact]
    public async Task RunAsync_SseDoneEvent_SendsDToSerial()
    {
        var port = AddArduinoPort();

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Done",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(2000);
        using var svc = CreateService();
        await svc.RunAsync(cts.Token);

        Assert.Contains("D", port.Written);
    }

    [Fact]
    public async Task RunAsync_SendsClearOnShutdown()
    {
        var port = AddArduinoPort();

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Waiting",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(2000);
        using var svc = CreateService();
        await svc.RunAsync(cts.Token);

        Assert.Equal("C", port.Written.Last());
    }

    [Fact]
    public async Task RunAsync_DuplicateCommand_NotSentTwice()
    {
        var port = AddArduinoPort();

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Waiting",
            """data: {"sessionId":"s1"}""",
            "",
            "event: Waiting",
            """data: {"sessionId":"s2"}"""
        );

        using var cts = new CancellationTokenSource(2000);
        using var svc = CreateService();
        await svc.RunAsync(cts.Token);

        int wCount = port.Written.Count(c => c == "W");
        Assert.Equal(1, wCount);
    }

    [Fact]
    public async Task RunAsync_HealthCheckRetries_UntilSuccess()
    {
        var port = AddArduinoPort();

        _httpHandler.EnqueueResponse(System.Net.HttpStatusCode.ServiceUnavailable);
        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Done",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(3000);
        using var svc = CreateService();
        var logs = new List<string>();
        svc.LogMessage += logs.Add;

        await svc.RunAsync(cts.Token);

        Assert.Contains(logs, l => l.Contains("Server not ready"));
        Assert.Contains("D", port.Written);
    }

    [Fact]
    public async Task RunAsync_ConnectionLost_Reconnects()
    {
        var port = AddArduinoPort();

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueResponse(HttpStatusCode.InternalServerError);

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Done",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(3000);
        using var svc = CreateService();
        var logs = new List<string>();
        svc.LogMessage += logs.Add;

        await svc.RunAsync(cts.Token);

        Assert.Contains(logs, l => l.Contains("Reconnecting"));
        Assert.Contains("D", port.Written);
    }

    [Fact]
    public async Task RunAsync_LogsStartupMessage()
    {
        using var cts = new CancellationTokenSource(500);
        using var svc = CreateService();
        var logs = new List<string>();
        svc.LogMessage += logs.Add;

        await svc.RunAsync(cts.Token);

        Assert.Contains(logs, l => l.Contains("Agentic AI Beacon"));
    }

    [Fact]
    public async Task RunAsync_DiscoverArduino_SkipsBadPort()
    {
        var badPort = new FakeSerialPort("COM1") { ThrowOnOpen = true };
        _serialFactory.Ports.Add(badPort);

        var goodPort = new FakeSerialPort("COM2");
        goodPort.ReadLineResponses.Enqueue("OK");
        _serialFactory.Ports.Add(goodPort);

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Done",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(3000);
        using var svc = CreateService();
        await svc.RunAsync(cts.Token);

        Assert.Contains("D", goodPort.Written);
    }

    [Fact]
    public async Task RunAsync_PreferredComPort_UsedDirectly()
    {
        _config.ComPort = "COM42";
        var port = new FakeSerialPort("COM42");
        port.ReadLineResponses.Enqueue("OK");
        _serialFactory.Ports.Add(port);

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Done",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(2000);
        using var svc = CreateService();
        await svc.RunAsync(cts.Token);

        Assert.Contains("D", port.Written);
    }

    [Fact]
    public async Task RunAsync_SseMultipleEvents_TracksStateCorrectly()
    {
        var port = AddArduinoPort();

        _httpHandler.EnqueueHealthOk();
        _httpHandler.EnqueueSseStream(
            "event: Waiting",
            """data: {"sessionId":"s1"}""",
            "",
            "event: Done",
            """data: {"sessionId":"s1"}""",
            "",
            "event: Clear",
            """data: {"sessionId":"s1"}"""
        );

        using var cts = new CancellationTokenSource(2000);
        using var svc = CreateService();
        await svc.RunAsync(cts.Token);

        Assert.Equal(["H", "W", "D", "C"], port.Written);
    }
}
