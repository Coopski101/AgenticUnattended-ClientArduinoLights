using System.Text.Json;

namespace ArduinoBridge;

public class SessionTracker
{
    private readonly Dictionary<string, string> _sessions = new();

    public string ProcessEvent(string eventType, string json)
    {
        using var doc = JsonDocument.Parse(json);
        string sessionId = doc.RootElement.GetProperty("sessionId").GetString() ?? "";
        return ApplyEvent(eventType, sessionId);
    }

    internal string ApplyEvent(string eventType, string sessionId)
    {
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

        return Reconcile();
    }

    private string Reconcile()
    {
        if (_sessions.Values.Any(v => v == "Waiting"))
            return "W";
        if (_sessions.Values.Any(v => v == "Done"))
            return "D";
        return "C";
    }
}
