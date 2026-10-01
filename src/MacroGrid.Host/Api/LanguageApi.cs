using System.Text;
using MacroGrid.Core.Languages;
using MacroGrid.Host.Ui;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints for language packs (see docs/design/language-packs.md): the stored packs, and the two CSV files the language screen opens and saves.</summary>
internal static class LanguageApi
{
    public static RouteGroupBuilder MapLanguageApi(this RouteGroupBuilder api)
    {
        api.MapGet("/languages", (LanguagePackStore packs) => ApiResults.Json(packs.List()));

        api.MapGet("/languages/{tag}", (string tag, LanguagePackStore packs) =>
            packs.Read(tag) is { } json ? Results.Content(json, "application/json", Encoding.UTF8) : Results.NotFound());

        api.MapPut("/languages/{tag}", async (string tag, HttpRequest request, LanguagePackStore packs) =>
        {
            var json = await ReadBodyAsync(request, LanguagePackStore.MaxBytes);
            if (json is null) return ApiResults.BadRequest("The language pack is larger than 1 MB.");
            try
            {
                packs.Save(tag, json);
                return Results.NoContent();
            }
            catch (LanguagePackException ex)
            {
                return ApiResults.BadRequest(ex.Message);
            }
        });

        api.MapDelete("/languages/{tag}", (string tag, LanguagePackStore packs) =>
            packs.Delete(tag) ? Results.NoContent() : Results.NotFound());

        api.MapPost("/browse/import-language-csv", async (IUiDialogService dialogs) =>
        {
            var path = await dialogs.BrowseForFileAsync("Import language", "Language table (*.csv)|*.csv");
            if (path is null) return Results.Json(new { path = (string?)null });
            try
            {
                return Results.Json(new { path, text = LanguageCsvFile.ReadText(path) });
            }
            catch (LanguagePackException ex)
            {
                return ApiResults.BadRequest(ex.Message);
            }
        });

        api.MapPost("/browse/export-language-csv", async (HttpRequest request, IUiDialogService dialogs) =>
        {
            var json = await ReadBodyAsync(request, LanguageCsvFile.MaxExportBytes * 2);
            if (json is null) return ApiResults.BadRequest("The file would be larger than 2 MB.");
            ExportBody? body;
            try { body = System.Text.Json.JsonSerializer.Deserialize<ExportBody>(json, MacroGrid.Protocol.ProtocolJson.Options); }
            catch (System.Text.Json.JsonException) { return ApiResults.InvalidJson(); }

            try
            {
                var (fileName, bytes) = LanguageCsvFile.PrepareExport(body?.FileName, body?.Text);
                var path = await dialogs.SaveFileAsync("Export language", fileName, "Language table (*.csv)|*.csv", "csv", bytes);
                return Results.Json(new { path });
            }
            catch (LanguagePackException ex)
            {
                return ApiResults.BadRequest(ex.Message);
            }
        });

        return api;
    }

    private sealed record ExportBody(string? FileName, string? Text);

    /// <summary>The request body as text, or null when it is longer than <paramref name="maxBytes"/> (it is never read past that).</summary>
    private static async Task<string?> ReadBodyAsync(HttpRequest request, int maxBytes)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > maxBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
