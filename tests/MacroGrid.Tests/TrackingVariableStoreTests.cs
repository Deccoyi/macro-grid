using MacroGrid.Core.Variables;

namespace MacroGrid.Tests;

public class TrackingVariableStoreTests
{
    [Fact]
    public void A_plugin_cannot_write_or_remove_a_user_variable()
    {
        var store = new VariableStore();
        store.Set("user.count", 5.0);
        var tracking = new TrackingVariableStore(store);

        tracking.Set("user.count", 99.0);
        tracking.Set("USER.other", 1.0);
        tracking.Remove("user.count");
        tracking.RemoveAll();

        Assert.Equal(5.0, store.Get("user.count"));
        Assert.Null(store.Get("USER.other"));
    }

    [Fact]
    public void Other_names_still_work_and_are_removed_with_the_plugin()
    {
        var store = new VariableStore();
        var tracking = new TrackingVariableStore(store);

        tracking.Set("demo.value", 1.0);
        Assert.Equal(1.0, store.Get("demo.value"));
        tracking.RemoveAll();

        Assert.Null(store.Get("demo.value"));
    }
}
