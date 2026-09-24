using MacroGrid.Core.Sessions;

namespace MacroGrid.Tests;

public class ToggleStateStoreTests
{
    [Fact]
    public void Toggle_starts_true_and_flips_on_each_call()
    {
        var store = new ToggleStateStore();

        Assert.True(store.Toggle("w1"));
        Assert.False(store.Toggle("w1"));
        Assert.True(store.Toggle("w1"));
    }

    [Fact]
    public void Get_defaults_to_false_for_an_unknown_widget()
    {
        Assert.False(new ToggleStateStore().Get("unknown"));
    }
}
