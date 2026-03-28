namespace ArduinoBridge.Tests;

public class FakeSerialPort : ISerialPort
{
    public string PortName { get; }
    public int ReadTimeout { get; set; }
    public int WriteTimeout { get; set; }
    public bool IsOpen { get; private set; }
    public List<string> Written { get; } = [];
    public Queue<string> ReadLineResponses { get; } = new();
    public bool ThrowOnOpen { get; set; }

    public FakeSerialPort(string portName)
    {
        PortName = portName;
    }

    public void Open()
    {
        if (ThrowOnOpen) throw new InvalidOperationException("Port unavailable");
        IsOpen = true;
    }

    public void Close() => IsOpen = false;
    public void DiscardInBuffer() { }
    public void Write(string text) => Written.Add(text);

    public string ReadLine()
    {
        if (ReadLineResponses.Count > 0) return ReadLineResponses.Dequeue();
        throw new TimeoutException("No data");
    }

    public void Dispose() => IsOpen = false;
}

public class FakeSerialPortFactory : ISerialPortFactory
{
    public List<FakeSerialPort> Ports { get; } = [];
    private int _createIndex;

    public string[] GetPortNames() => Ports.Select(p => p.PortName).ToArray();

    public ISerialPort Create(string portName, int baudRate, int readTimeout, int writeTimeout)
    {
        if (_createIndex < Ports.Count)
            return Ports[_createIndex++];
        throw new InvalidOperationException("No more fake ports");
    }
}
