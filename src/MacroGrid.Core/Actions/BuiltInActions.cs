using System.Text.Json.Nodes;
using MacroGrid.Core.Input;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>Sends a key chord. Settings: { "keys": "ctrl+c" }</summary>
public sealed class HotkeyAction(IInputService input) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.hotkey";

    public string Type => TypeId;
    public string DisplayName => "Hotkey";
    public string Category => "Keyboard";
    public string? Description => "Sends a key combination";
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
    public string DisplayName => "Type text";
    public string Category => "Keyboard";
    public string? Description => "Types a fixed text";
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
