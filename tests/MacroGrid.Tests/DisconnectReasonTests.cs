using System.Net.WebSockets;
using MacroGrid.Core.Sessions;

namespace MacroGrid.Tests;

public sealed class DisconnectReasonTests
{
    [Fact]
    public void Close_status_and_description_are_logged()
    {
        Assert.Equal("client closed: 1000 NormalClosure \"bye\"", DisconnectReason.Describe(WebSocketCloseStatus.NormalClosure, "bye"));
        Assert.Equal("client closed: 1001 EndpointUnavailable", DisconnectReason.Describe(WebSocketCloseStatus.EndpointUnavailable, null));
        Assert.Equal("client closed without a status", DisconnectReason.Describe(null, null));
    }

    [Fact]
    public void Description_is_flattened_and_capped()
    {
        var text = DisconnectReason.Describe(WebSocketCloseStatus.PolicyViolation, "a\r\nb" + new string('x', 300));
        Assert.DoesNotContain('\n', text);
        Assert.True(text.Length < 160);
    }

    [Fact]
    public void Exception_type_and_message_are_logged()
    {
        var text = DisconnectReason.Describe(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The remote party closed the connection."));
        Assert.StartsWith("WebSocketException: ", text);
        Assert.Contains("ConnectionClosedPrematurely", text);
        Assert.Equal("OperationCanceledException: boom", DisconnectReason.Describe(new OperationCanceledException("boom")));
    }
}
