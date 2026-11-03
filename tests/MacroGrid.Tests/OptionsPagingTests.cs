using System.Text.Json.Nodes;
using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class OptionsPagingTests
{
    private sealed class OldSource(IReadOnlyList<SettingOption> options) : IOptionsSource
    {
        public Task<OptionsResult> GetOptionsAsync(string sourceId, JsonObject currentValues, CancellationToken cancellationToken) =>
            Task.FromResult(new OptionsResult(options));
    }

    private sealed class SearchSource(Func<OptionsQuery, OptionsResult> answer) : IOptionsSearch
    {
        public OptionsQuery? Last;
        public Task<OptionsResult> GetOptionsAsync(string sourceId, JsonObject currentValues, CancellationToken cancellationToken) => throw new InvalidOperationException("must not be called");
        public Task<OptionsResult> SearchOptionsAsync(string sourceId, JsonObject currentValues, OptionsQuery query, CancellationToken cancellationToken)
        {
            Last = query;
            return Task.FromResult(answer(query));
        }
    }

    private static List<SettingOption> Many(int count) => [.. Enumerable.Range(0, count).Select(i => new SettingOption($"v{i}", $"Item {i:000}"))];

    private static Task<OptionsResult> Run(IOptionsSource source, OptionsQuery? query) =>
        OptionsPaging.RunAsync(source, "s", [], query, CancellationToken.None);

    [Fact]
    public void The_query_is_clamped()
    {
        var q = new OptionsQuery("  " + new string('a', 300) + "  ", -5, 999, new string('x', 600));
        Assert.Equal(100, q.Text.Length);
        Assert.Equal(0, q.Offset);
        Assert.Equal(50, q.Limit);
        Assert.Null(q.Selected);
        Assert.Equal(1, new OptionsQuery(null, 0, 0, "").Limit);
        Assert.Equal("", new OptionsQuery(null, 0, 10, "").Text);
    }

    [Fact]
    public async Task An_old_source_is_filtered_and_paged_on_the_server()
    {
        var source = new OldSource(Many(120));

        var first = await Run(source, new OptionsQuery("item", 0, 20, null));
        Assert.Equal(20, first.Options.Count);
        Assert.True(first.HasMore);

        var last = await Run(source, new OptionsQuery("item", 100, 20, null));
        Assert.Equal(20, last.Options.Count);
        Assert.False(last.HasMore);
        Assert.Equal("v100", last.Options[0].Value);

        var one = await Run(source, new OptionsQuery("item 007", 0, 20, null));
        Assert.Equal("v7", Assert.Single(one.Options).Value);
        Assert.False(one.HasMore);
    }

    [Fact]
    public async Task Matching_ignores_case_and_accents_including_the_Turkish_i()
    {
        var source = new OldSource([new("a", "Işık"), new("b", "İstanbul"), new("c", "Çağrı"), new("d", "Other")]);

        Assert.Equal("a", Assert.Single((await Run(source, new OptionsQuery("isik", 0, 10, null))).Options).Value);
        Assert.Equal("b", Assert.Single((await Run(source, new OptionsQuery("ISTANBUL", 0, 10, null))).Options).Value);
        Assert.Equal("c", Assert.Single((await Run(source, new OptionsQuery("cagri", 0, 10, null))).Options).Value);
        Assert.Equal("b", Assert.Single((await Run(source, new OptionsQuery("İSTAN", 0, 10, null))).Options).Value);
    }

    [Fact]
    public async Task The_selected_value_comes_first_on_page_zero_only()
    {
        var source = new OldSource(Many(30));

        var first = await Run(source, new OptionsQuery("", 0, 10, "v25"));
        Assert.Equal("v25", first.Options[0].Value);
        Assert.Equal(10, first.Options.Count);
        Assert.False(first.SelectedMissing);

        // The next page starts at the number of options already held, selected included: nothing is skipped or repeated.
        var second = await Run(source, new OptionsQuery("", first.Options.Count, 10, "v25"));
        Assert.DoesNotContain(second.Options, o => o.Value == "v25");
        Assert.Equal("v9", second.Options[0].Value);
        Assert.Null(second.SelectedMissing);

        var all = new List<string>(first.Options.Select(o => o.Value));
        for (var page = first; page.HasMore;)
        {
            page = await Run(source, new OptionsQuery("", all.Count, 10, "v25"));
            all.AddRange(page.Options.Select(o => o.Value));
        }
        Assert.Equal(30, all.Count);
        Assert.Equal(30, all.Distinct().Count());
    }

    [Fact]
    public void The_editors_label_keys_are_removed_before_a_plugin_sees_the_values()
    {
        var values = new JsonObject { ["voice"] = "a", ["voice" + OptionsPaging.LabelSuffix] = "Alpha" };
        var clean = OptionsPaging.WithoutLabelKeys(values);
        Assert.Equal(["voice"], clean.Select(p => p.Key));
        Assert.Same(clean, OptionsPaging.WithoutLabelKeys(clean));
    }

    [Fact]
    public async Task A_selected_value_that_is_gone_is_reported_missing()
    {
        var result = await Run(new OldSource(Many(5)), new OptionsQuery("", 0, 10, "gone"));
        Assert.True(result.SelectedMissing);
        Assert.Equal(5, result.Options.Count);
    }

    [Fact]
    public async Task A_search_source_gets_the_clamped_query_and_is_not_filtered_again()
    {
        var source = new SearchSource(q => new OptionsResult([new("x", "Unrelated label")]) { HasMore = true });
        var result = await Run(source, new OptionsQuery("zzz", 40, 500, null));

        Assert.Equal(50, source.Last!.Limit);
        Assert.Equal(40, source.Last.Offset);
        Assert.Equal("x", Assert.Single(result.Options).Value);
        Assert.True(result.HasMore);
    }

    [Fact]
    public async Task A_search_source_over_fifty_options_is_an_error()
    {
        var result = await Run(new SearchSource(_ => new OptionsResult(Many(51))), new OptionsQuery("", 0, 50, null));
        Assert.Empty(result.Options);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task An_answer_over_256_KB_is_an_error_not_the_options()
    {
        var big = new OldSource([.. Enumerable.Range(0, 40).Select(i => new SettingOption($"v{i}", new string('x', 8000)))]);

        Assert.NotNull((await Run(big, new OptionsQuery("", 0, 50, null))).Error);
        var legacy = await Run(big, null);
        Assert.Empty(legacy.Options);
        Assert.NotNull(legacy.Error);
    }

    [Fact]
    public async Task The_old_call_without_a_query_returns_the_whole_list()
    {
        var result = await Run(new OldSource(Many(80)), null);
        Assert.Equal(80, result.Options.Count);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task A_source_error_is_passed_on_unpaged()
    {
        var result = await Run(new SearchSource(_ => new OptionsResult([], "Not connected")), new OptionsQuery("", 0, 10, null));
        Assert.Equal("Not connected", result.Error);
    }
}
