using System.IO.Ports;

namespace ArduinoBridge;

public sealed class SystemSerialPort : ISerialPort
{
    private readonly SerialPort _inner;

    public SystemSerialPort(string portName, int baudRate, int readTimeout, int writeTimeout)
    {
        _inner = new SerialPort(portName, baudRate)
        {
            ReadTimeout = readTimeout,
            WriteTimeout = writeTimeout
        };
    }

    public string PortName => _inner.PortName;
    public int ReadTimeout { get => _inner.ReadTimeout; set => _inner.ReadTimeout = value; }
    public int WriteTimeout { get => _inner.WriteTimeout; set => _inner.WriteTimeout = value; }
    public void Open() => _inner.Open();
    public void Close() => _inner.Close();
    public void DiscardInBuffer() => _inner.DiscardInBuffer();
    public void Write(string text) => _inner.Write(text);
    public string ReadLine() => _inner.ReadLine();
    public void Dispose() => _inner.Dispose();
}

public sealed class SystemSerialPortFactory : ISerialPortFactory
{
    public string[] GetPortNames() => SerialPort.GetPortNames();

    public ISerialPort Create(string portName, int baudRate, int readTimeout, int writeTimeout) =>
        new SystemSerialPort(portName, baudRate, readTimeout, writeTimeout);
}
