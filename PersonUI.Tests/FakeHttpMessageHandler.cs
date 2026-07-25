using System.Net;

namespace PersonUI.Tests;

public class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public static FakeHttpMessageHandler Json(HttpStatusCode statusCode, string json) =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

    public static FakeHttpMessageHandler PlainText(HttpStatusCode statusCode, string text) =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(text, System.Text.Encoding.UTF8, "text/plain"),
        });

    public static FakeHttpMessageHandler Empty(HttpStatusCode statusCode) =>
        new(_ => new HttpResponseMessage(statusCode));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(respond(request));
    }
}
