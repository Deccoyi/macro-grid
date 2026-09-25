using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public class VariableCatalogTests
{
    private sealed class FakeSource(params VariableInfo[] infos) : IVariableCatalogSource
    {
        public IEnumerable<VariableInfo> Describe() => infos;
    }

    [Fact]
    public void Merges_and_sorts_entries_from_every_source()
    {
        var catalog = new VariableCatalog(
        [
            new FakeSource(new VariableInfo("z.two", "Second", "{z.two}", "Test")),
            new FakeSource(new VariableInfo("a.one", "Birinci", "{a.one}", "Test")),
        ]);

        Assert.Equal(["a.one", "z.two"], catalog.All.Select(v => v.Name));
    }

    [Fact]
    public void Type_metadata_serializes_for_the_editor_and_reads_back()
    {
        var info = new VariableInfo("a.muted", "Muted", "{a.muted}", "Test") { Type = VariableType.Boolean };

        var json = System.Text.Json.JsonSerializer.Serialize(info, MacroGrid.Protocol.ProtocolJson.Options);
        Assert.Contains("\"type\":\"boolean\"", json);
        Assert.DoesNotContain("unit", json);

        var parsed = System.Text.Json.JsonSerializer.Deserialize<VariableInfo>(
            """{"name":"a.cpu","description":"CPU","example":"{a.cpu}","category":"Test","type":"number","unit":"%","values":["x"]}""",
            MacroGrid.Protocol.ProtocolJson.Options)!;
        Assert.Equal(VariableType.Number, parsed.Type);
        Assert.Equal("%", parsed.Unit);
        Assert.Equal(["x"], parsed.Values!);
    }

    [Fact]
    public void Variables_without_type_metadata_are_text()
    {
        var parsed = System.Text.Json.JsonSerializer.Deserialize<VariableInfo>(
            """{"name":"a.b","description":"B","example":"{a.b}","category":"Test"}""",
            MacroGrid.Protocol.ProtocolJson.Options)!;

        Assert.Equal(VariableType.Text, parsed.Type);
        Assert.Null(parsed.Unit);
    }
}
