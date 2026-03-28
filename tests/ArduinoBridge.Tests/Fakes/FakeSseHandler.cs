using System.Net;
using System.Text;

namespace ArduinoBridge.Tests;

public class FakeSseHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    public List<HttpRequestMessage> Requests { get; } = [];

    public void EnqueueResponse(HttpStatusCode status, string? content = null, string? contentType = null)
    {
        var resp = new HttpResponseMessage(status);
        if (content is not null)
        {
            resp.Content = new StringContent(content, Encoding.UTF8, contentType ?? "text/event-stream");
        }
        _responses.Enqueue(resp);
    }

    public void EnqueueHealthOk() => EnqueueResponse(HttpStatusCode.OK, "ok", "text/plain");

    public void EnqueueSseStream(params string[] lines)
    {
        string body = string.Join("\n", lines) + "\n";
        EnqueueResponse(HttpStatusCode.OK, body, "text/event-stream");
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count > 0)
            return Task.FromResult(_responses.Dequeue());
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
