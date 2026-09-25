using System.Text.Json.Nodes;

namespace MacroGrid.Core.Model;

public sealed record ActionBinding(string Type, JsonObject Settings);
