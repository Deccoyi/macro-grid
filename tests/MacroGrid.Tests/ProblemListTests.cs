using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public sealed class ProblemListTests
{
    [Fact]
    public void The_same_problem_is_one_entry_with_a_count()
    {
        var list = new ProblemList();
        for (var i = 0; i < 5; i++) list.Report("p", "Plugin", ProblemSeverity.Warning, "P100", "Typing refused: no");

        var problem = Assert.Single(list.Snapshot());
        Assert.Equal(5, problem.Count);
        Assert.True(problem.LastAt >= problem.FirstAt);
    }

    [Fact]
    public void A_different_source_code_or_message_is_its_own_entry_and_the_newest_is_first()
    {
        var list = new ProblemList();
        list.Report("a", "A", ProblemSeverity.Error, "P1", "one");
        list.Report("b", "B", ProblemSeverity.Error, "P1", "one");
        list.Report("a", "A", ProblemSeverity.Error, "P2", "one");
        list.Report("a", "A", ProblemSeverity.Error, "P1", "one");

        var all = list.Snapshot();
        Assert.Equal(3, all.Count);
        Assert.Equal(("a", "P1", 2), (all[0].Source, all[0].Code, all[0].Count));
    }

    [Fact]
    public void Clear_removes_everything_or_one_source_and_changes_the_version()
    {
        var list = new ProblemList();
        list.Report("a", "A", ProblemSeverity.Info, "P1", "x");
        list.Report("b", "B", ProblemSeverity.Info, "P1", "x");
        var before = list.Version;

        list.Clear("a");
        Assert.Equal("b", Assert.Single(list.Snapshot()).Source);
        Assert.True(list.Version > before);

        list.Clear();
        Assert.Empty(list.Snapshot());
    }

    [Fact]
    public void Resolve_removes_only_the_named_codes_of_one_source()
    {
        var list = new ProblemList();
        list.Report("a", "A", ProblemSeverity.Error, "P120", "x");
        list.Report("a", "A", ProblemSeverity.Warning, "P100", "y");

        list.Resolve("a", "P120");

        Assert.Equal("P100", Assert.Single(list.Snapshot()).Code);
    }

    [Fact]
    public void The_list_is_capped_and_a_message_is_cleaned_and_shortened()
    {
        var list = new ProblemList();
        for (var i = 0; i < ProblemList.MaxEntries + 20; i++) list.Report("a", "A", ProblemSeverity.Info, "P1", "m" + i);
        Assert.Equal(ProblemList.MaxEntries, list.Snapshot().Count);

        list.Clear();
        list.Report("a", "A", ProblemSeverity.Info, "P1", "line1\r\nline2\u0007" + new string('x', 1000));
        var message = Assert.Single(list.Snapshot()).Message;
        Assert.DoesNotContain('\n', message);
        Assert.DoesNotContain('\u0007', message);
        Assert.Equal(300, message.Length);
    }

    [Fact]
    public void A_line_keeps_its_target_and_a_repeat_updates_it()
    {
        var list = new ProblemList();
        list.Report("s", "S", ProblemSeverity.Error, "P131", "m", "k", 10, new ProblemTarget("p1", "pg1", "w1", "press", 0));
        list.Report("s", "S", ProblemSeverity.Error, "P131", "m", "k", 10, new ProblemTarget("p1", "pg2", "w1", "press", 0));
        var line = Assert.Single(list.Snapshot());
        Assert.Equal(2, line.Count);
        Assert.Equal("pg2", line.Target!.PageId);
    }
}
