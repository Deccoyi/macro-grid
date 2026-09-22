using MacroStation.Core.Variables;

namespace MacroStation.Tests;

public class VariableStoreTests
{
    [Fact]
    public void Get_returns_null_for_unknown_name()
    {
        Assert.Null(new VariableStore().Get("nope"));
    }

    [Fact]
    public void Set_then_get_roundtrips()
    {
        var store = new VariableStore();

        store.Set("x", 5.0);

        Assert.Equal(5.0, store.Get("x"));
    }

    [Fact]
    public void Changed_fires_only_when_the_value_actually_changes()
    {
        var store = new VariableStore();
        var fired = 0;
        store.Changed += _ => fired++;

        store.Set("x", 1.0);
        store.Set("x", 1.0); // same value again: must not fire
        store.Set("x", 2.0);

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Changed_passes_the_variable_name()
    {
        var store = new VariableStore();
        string? seen = null;
        store.Changed += name => seen = name;

        store.Set("system.cpu", 1.0);

        Assert.Equal("system.cpu", seen);
    }

    [Fact]
    public void Snapshot_is_a_point_in_time_copy()
    {
        var store = new VariableStore();
        store.Set("a", 1.0);

        var snapshot = store.Snapshot();
        store.Set("a", 2.0);
        store.Set("b", 3.0);

        Assert.Equal(1.0, snapshot["a"]);
        Assert.False(snapshot.ContainsKey("b"));
    }
}
