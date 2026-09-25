using System.Text.Json;
using MacroGrid.Protocol;

namespace MacroGrid.Host.Api;

/// <summary>Response and request-body helpers shared by the editor API endpoints.</summary>
internal static class ApiResults
{
    /// <summary>200 with the value serialized by the protocol's JSON options.</summary>
    public static IResult Json<T>(T value) => Results.Json(value, ProtocolJson.Options);

    /// <summary>400 with an <c>{ "error": "..." }</c> body.</summary>
    public static IResult BadRequest(string? error) => Results.BadRequest(new { error });

    public static IResult InvalidJson() => BadRequest("Invalid JSON.");

    /// <summary>
    /// Reads the request body as JSON. <c>Valid</c> is false when the body is not well-formed JSON;
    /// <c>Value</c> is null when the body is the literal <c>null</c>.
    /// </summary>
    public static async Task<(bool Valid, T? Value)> ReadJsonAsync<T>(HttpRequest request)
    {
        try
        {
            return (true, await JsonSerializer.DeserializeAsync<T>(request.Body, ProtocolJson.Options));
        }
        catch (JsonException)
        {
            return (false, default);
        }
    }
}
