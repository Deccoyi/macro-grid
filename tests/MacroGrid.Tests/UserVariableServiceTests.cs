using System.Text.Json;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class UserVariableServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-uvars-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _store = new();

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private UserVariableService New(VariableStore? store = null) => new(_dir, store ?? _store);

    private static UserVariable Num(string name, object? initial = null, bool keep = false) => new(name, VariableType.Number, initial, keep);

    private static void MustReplace(UserVariableService service, params UserVariable[] list)
    {
        Assert.True(service.TryReplace(list, out var error), error);
    }

    [Fact]
    public void A_new_variable_appears_in_the_store_with_its_start_value()
    {
        var service = New();
        MustReplace(service, Num("count", 5), new("title", VariableType.Text), new("armed", VariableType.Boolean, true));

        Assert.Equal(5.0, _store.Get("user.count"));
        Assert.Equal("", _store.Get("user.title"));
        Assert.Equal(true, _store.Get("user.armed"));
    }

    [Fact]
    public void A_number_without_a_start_value_is_unavailable()
    {
        var service = New();
        MustReplace(service, Num("count"), Num("blank", ""));

        Assert.Null(_store.Get("user.count"));
        Assert.Null(_store.Get("user.blank"));
        Assert.Contains("user.count", _store.Snapshot().Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1abc")]
    [InlineData("a b")]
    [InlineData("a.b")]
    [InlineData("é")]
    [InlineData("_x")]
    public void An_invalid_name_is_refused_and_nothing_changes(string name)
    {
        var service = New();
        MustReplace(service, Num("keep_me", 1));

        Assert.False(service.TryReplace([Num("fine"), Num(name)], out var error));
        Assert.NotEmpty(error);
        Assert.Equal(["keep_me"], service.List().Select(v => v.Name));
    }

    [Fact]
    public void Names_are_unique_ignoring_case()
    {
        var service = New();

        Assert.False(service.TryReplace([Num("Count"), Num("count")], out _));
    }

    [Fact]
    public void The_name_length_and_the_count_are_limited()
    {
        var service = New();

        Assert.True(service.TryReplace([Num(new string('a', UserVariables.MaxNameLength))], out _));
        Assert.False(service.TryReplace([Num(new string('a', UserVariables.MaxNameLength + 1))], out _));
        var tooMany = Enumerable.Range(0, UserVariables.MaxCount + 1).Select(i => Num("v" + i)).ToList();
        Assert.False(service.TryReplace(tooMany, out _));
        Assert.True(service.TryReplace(tooMany.Take(UserVariables.MaxCount).ToList(), out _));
    }

    [Fact]
    public void The_type_of_an_existing_variable_cannot_change()
    {
        var service = New();
        MustReplace(service, Num("count", 1));

        Assert.False(service.TryReplace([new("count", VariableType.Text)], out var error));
        Assert.Contains("type", error);
        Assert.Equal(VariableType.Number, service.List().Single().Type);
    }

    [Fact]
    public void A_start_value_that_does_not_fit_is_refused()
    {
        var service = New();

        Assert.False(service.TryReplace([Num("n", "abc")], out _));
        Assert.False(service.TryReplace([new("b", VariableType.Boolean, "maybe")], out _));
        Assert.False(service.TryReplace([new("t", VariableType.Text, new string('x', UserVariables.MaxTextLength + 1))], out _));
    }

    [Fact]
    public void A_removed_variable_leaves_the_store_and_an_edit_keeps_the_current_value()
    {
        var service = New();
        MustReplace(service, Num("a", 1), Num("b", 2));
        service.Set("user.a", 10);

        MustReplace(service, Num("a", 1) with { Description = "edited" });

        Assert.Equal(10.0, _store.Get("user.a"));
        Assert.DoesNotContain("user.b", _store.Snapshot().Keys);
    }

    [Fact]
    public void A_description_is_cleaned_and_cut()
    {
        var service = New();
        MustReplace(service, Num("a") with { Description = "ok‮text" + new string('x', 400) });

        var description = service.List().Single().Description;
        Assert.DoesNotContain('‮', description);
        Assert.Equal(UserVariables.MaxDescriptionLength, description.Length);
    }

    [Fact]
    public void Set_converts_to_the_type_and_reports_why_it_failed()
    {
        var service = New();
        MustReplace(service, Num("n", 0), new("t", VariableType.Text), new("b", VariableType.Boolean, false));

        Assert.Equal(UserVariableWrite.Ok, service.Set("user.n", "12.5"));
        Assert.Equal(12.5, _store.Get("user.n"));
        Assert.Equal(UserVariableWrite.InvalidValue, service.Set("user.n", "abc"));
        Assert.Equal(UserVariableWrite.InvalidValue, service.Set("user.n", "NaN"));
        Assert.Equal(12.5, _store.Get("user.n"));
        Assert.Equal(UserVariableWrite.Ok, service.Set("user.t", 3.0));
        Assert.Equal("3", _store.Get("user.t"));
        Assert.Equal(UserVariableWrite.Ok, service.Set("user.b", "On"));
        Assert.Equal(true, _store.Get("user.b"));
        Assert.Equal(UserVariableWrite.NotFound, service.Set("user.missing", 1));
        Assert.Equal(UserVariableWrite.NotFound, service.Set("system.cpu", 1));
    }

    [Fact]
    public void Number_text_uses_the_invariant_culture()
    {
        Assert.True(UserVariables.TryConvert(VariableType.Number, "1,5", out var withComma) == false || (double)withComma! == 15);
        Assert.True(UserVariables.TryConvert(VariableType.Number, "1.5", out var value));
        Assert.Equal(1.5, value);
    }

    [Fact]
    public void Toggle_add_and_reset_follow_the_type()
    {
        var service = New();
        MustReplace(service, Num("n", 5), new("b", VariableType.Boolean, true), new("t", VariableType.Text, "x"), Num("none"));

        Assert.Equal(UserVariableWrite.Ok, service.Toggle("user.b"));
        Assert.Equal(false, _store.Get("user.b"));
        Assert.Equal(UserVariableWrite.WrongType, service.Toggle("user.n"));
        Assert.Equal(UserVariableWrite.Ok, service.Add("user.n", -2));
        Assert.Equal(3.0, _store.Get("user.n"));
        Assert.Equal(UserVariableWrite.WrongType, service.Add("user.t", 1));
        Assert.Equal(UserVariableWrite.Ok, service.Add("user.none", 4));
        Assert.Equal(4.0, _store.Get("user.none"));
        Assert.Equal(UserVariableWrite.InvalidValue, service.Add("user.n", double.MaxValue * 2));
        Assert.Equal(UserVariableWrite.Ok, service.Reset("user.n"));
        Assert.Equal(5.0, _store.Get("user.n"));
        Assert.Equal(UserVariableWrite.Ok, service.Reset("user.none"));
        Assert.Null(_store.Get("user.none"));
    }

    [Fact]
    public void Concurrent_adds_do_not_lose_updates()
    {
        var service = New();
        MustReplace(service, Num("n", 0));

        Parallel.For(0, 500, _ => service.Add("user.n", 1));

        Assert.Equal(500.0, _store.Get("user.n"));
    }

    [Fact]
    public void Definitions_come_back_after_a_restart()
    {
        MustReplace(New(), Num("count", 7), new("title", VariableType.Text, "hello", Description: "A title"));

        var restarted = new VariableStore();
        var service = New(restarted);

        Assert.Equal(["count", "title"], service.List().Select(v => v.Name));
        Assert.Equal(7.0, restarted.Get("user.count"));
        Assert.Equal("hello", restarted.Get("user.title"));
        Assert.Equal("A title", service.List()[1].Description);
    }

    [Fact]
    public async Task A_kept_value_comes_back_and_the_others_return_to_the_start_value()
    {
        var service = New();
        MustReplace(service, Num("kept", 1, keep: true), Num("lost", 1), new("flag", VariableType.Boolean, false, true));
        service.Set("user.kept", 42);
        service.Set("user.lost", 42);
        service.Toggle("user.flag");
        await service.StopAsync(CancellationToken.None);

        var restarted = new VariableStore();
        New(restarted);

        Assert.Equal(42.0, restarted.Get("user.kept"));
        Assert.Equal(1.0, restarted.Get("user.lost"));
        Assert.Equal(true, restarted.Get("user.flag"));
    }

    [Fact]
    public async Task Only_kept_variables_are_written_to_the_values_file()
    {
        var service = New();
        MustReplace(service, Num("kept", 1, keep: true), Num("lost", 1));
        await service.StopAsync(CancellationToken.None);

        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_dir, "user-variable-values.json")));
        var values = json.RootElement.GetProperty("values");
        Assert.True(values.TryGetProperty("kept", out _));
        Assert.False(values.TryGetProperty("lost", out _));
    }

    [Fact]
    public void A_broken_file_is_set_aside_and_the_list_starts_empty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "user-variables.json"), "{ not json");

        var service = New();

        Assert.Empty(service.List());
        Assert.True(File.Exists(Path.Combine(_dir, "user-variables.json.broken")));
    }

    [Fact]
    public void A_bad_entry_in_the_file_is_skipped_and_the_rest_load()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "user-variables.json"), """
            { "formatVersion": 1, "variables": [
              { "name": "1bad", "type": "number" },
              { "name": "good", "type": "number", "initial": 3 },
              { "name": "GOOD", "type": "number" } ] }
            """);

        var service = New();

        Assert.Equal(["good"], service.List().Select(v => v.Name));
        Assert.Equal(3.0, _store.Get("user.good"));
    }

    [Fact]
    public void The_picker_lists_every_variable_with_its_type()
    {
        var service = New();
        MustReplace(service, Num("count", 1) with { Description = "Counter" }, new("armed", VariableType.Boolean));

        var info = service.Describe().ToList();

        Assert.Equal(["user.count", "user.armed"], info.Select(v => v.Name));
        Assert.All(info, v => Assert.Equal("Global Variable List", v.Category));
        Assert.Equal("{user.count}", info[0].Example);
        Assert.Equal("Counter", info[0].Description);
        Assert.Equal(VariableType.Boolean, info[1].Type);
    }
}
