namespace ArduinoBridge;

public interface ISerialPort : IDisposable
{
    string PortName { get; }
    int ReadTimeout { get; set; }
    int WriteTimeout { get; set; }
    void Open();
    void Close();
    void DiscardInBuffer();
    void Write(string text);
    string ReadLine();
}

public interface ISerialPortFactory
{
    string[] GetPortNames();
    ISerialPort Create(string portName, int baudRate, int readTimeout, int writeTimeout);
}
