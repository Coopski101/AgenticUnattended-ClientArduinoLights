namespace ArduinoBridge;

public sealed class BridgeConfig
{
    public string SseUrl { get; set; } = "http://127.0.0.1:17321";
    public string? ComPort { get; set; }
    public int BaudRate { get; set; } = 9600;
    public int ReconnectBaseMs { get; set; } = 1000;
    public int ReconnectMaxMs { get; set; } = 30000;
}
