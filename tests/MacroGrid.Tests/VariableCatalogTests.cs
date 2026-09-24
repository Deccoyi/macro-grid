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
            new FakeSource(new VariableInfo("z.two", "İkinci", "{z.two}", "Test")),
            new FakeSource(new VariableInfo("a.one", "Birinci", "{a.one}", "Test")),
        ]);

        Assert.Equal(["a.one", "z.two"], catalog.All.Select(v => v.Name));
    }
}
