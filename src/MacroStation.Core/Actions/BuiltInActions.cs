using System.Text.Json.Nodes;
using MacroStation.Core.Input;
using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Actions;

/// <summary>Sends a key chord. Settings: { "keys": "ctrl+c" }</summary>
public sealed class HotkeyAction(IInputService input) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.hotkey";

    public string Type => TypeId;
    public string DisplayName => "Kısayol tuşu";
    public string Category => "Klavye";
    public string? Description => "Bir tuş kombinasyonu gönderir";
    public string? Icon => "keyboard";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var keys = settings["keys"]?.GetValue<string>();
        input.SendKeyCombo(HotkeyParser.Parse(keys ?? ""));
        return Task.CompletedTask;
    }

    public static JsonObject Settings(string keys) => new() { ["keys"] = keys };
}

/// <summary>Types a piece of text. Settings: { "text": "hello" }</summary>
public sealed class TypeTextAction(IInputService input) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.typeText";

    public string Type => TypeId;
    public string DisplayName => "Metin yaz";
    public string Category => "Klavye";
    public string? Description => "Sabit bir metni yazar";
    public string? Icon => "type";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var text = settings["text"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(text))
            input.TypeText(text);
        return Task.CompletedTask;
    }

    public static JsonObject Settings(string text) => new() { ["text"] = text };
}
