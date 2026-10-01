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
    public void The_global_variable_list_shows_up_in_the_picker_with_its_own_group()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ms-vc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new UserVariableService(dir, new VariableStore());
            service.TryReplace([new UserVariable("count", VariableType.Number, 1)], out _);
            var catalog = new VariableCatalog([new FakeSource(new VariableInfo("a.one", "One", "{a.one}", "Test")), service]);

            Assert.Equal(["a.one", "user.count"], catalog.All.Select(v => v.Name));
            Assert.Equal("Global Variable List", catalog.All.Single(v => v.Name == "user.count").Category);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
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
