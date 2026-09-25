namespace MacroGrid.Tests;

/// <summary>An <see cref="HttpMessageHandler"/> that answers from a delegate and remembers the last request, so tests never touch the network.</summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? Last { get; private set; }

    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Last = request;
        Calls++;
        return Task.FromResult(respond(request));
    }
}
