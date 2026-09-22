using MacroStation.Core.Variables;

namespace MacroStation.Tests;

public class TemplateTests
{
    [Fact]
    public void Renders_plain_text_without_variables()
    {
        var store = new VariableStore();

        Assert.Equal("Merhaba", Template.Parse("Merhaba").Render(store));
    }

    [Fact]
    public void Substitutes_variable_with_format()
    {
        var store = new VariableStore();
        store.Set("system.cpu", 42.3);

        Assert.Equal("CPU: 42%", Template.Parse("CPU: {system.cpu|0}%").Render(store));
    }

    [Fact]
    public void Missing_variable_renders_as_empty()
    {
        Assert.Equal("x: ", Template.Parse("x: {nope}").Render(new VariableStore()));
    }

    [Fact]
    public void Escaped_braces_are_literal()
    {
        Assert.Equal("{literal}", Template.Parse("{{literal}}").Render(new VariableStore()));
    }

    [Fact]
    public void Unmatched_or_empty_token_is_left_as_is()
    {
        var store = new VariableStore();

        Assert.Equal("a {", Template.Parse("a {").Render(store));
        Assert.Equal("a {} b", Template.Parse("a {} b").Render(store));
    }

    [Fact]
    public void Formats_datetime_with_default_and_custom_pattern()
    {
        var store = new VariableStore();
        store.Set("t", new DateTime(2026, 9, 22, 8, 5, 3));

        Assert.Equal("08:05", Template.Parse("{t}").Render(store));
        Assert.Equal("08:05:03", Template.Parse("{t|HH:mm:ss}").Render(store));
    }

    [Fact]
    public void Formats_timespan_with_and_without_days()
    {
        var store = new VariableStore();

        store.Set("up", TimeSpan.FromSeconds(3725));
        Assert.Equal("01:02:05", Template.Parse("{up}").Render(store));

        store.Set("up", TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));
        Assert.Equal("1.00:01:00", Template.Parse("{up}").Render(store));
    }

    [Fact]
    public void Formats_bool_with_default_and_custom_words()
    {
        var store = new VariableStore();
        store.Set("muted", true);

        Assert.Equal("Açık", Template.Parse("{muted}").Render(store));
        Assert.Equal("EVET", Template.Parse("{muted|EVET/HAYIR}").Render(store));

        store.Set("muted", false);
        Assert.Equal("Kapalı", Template.Parse("{muted}").Render(store));
        Assert.Equal("HAYIR", Template.Parse("{muted|EVET/HAYIR}").Render(store));
    }

    [Fact]
    public void VariableNames_lists_every_distinct_token_once()
    {
        var template = Template.Parse("{a} {b|0} {a}");

        Assert.Equal(["a", "b"], template.VariableNames);
    }

    [Fact]
    public void Caches_parsed_templates_by_source_text()
    {
        Assert.Same(Template.Parse("{x}"), Template.Parse("{x}"));
    }
}
