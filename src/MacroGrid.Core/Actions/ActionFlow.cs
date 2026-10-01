namespace MacroGrid.Core.Actions;

/// <summary>What a run of an action list does with the next step.</summary>
public enum FlowStep
{
    /// <summary>Run it (an ordinary step on a branch that is taken).</summary>
    Run,

    /// <summary>Do not run it: it is on a branch that is not taken, or it is a marker that only steers the flow.</summary>
    Skip,

    /// <summary>The list ends here.</summary>
    Stop,
}

/// <summary>
/// The logic steps of an action list. The list stays flat: <c>core.if</c> starts a block, <c>core.else</c> splits it, <c>core.endIf</c> ends it and
/// <c>core.stop</c> ends the whole list. A list that is not well formed still runs safely: a missing End means the block goes to the end of the
/// list, a stray Otherwise or End does nothing. There are no loops, so a run always ends.
/// </summary>
public static class ActionFlow
{
    public const string If = "core.if";
    public const string Else = "core.else";
    public const string EndIf = "core.endIf";
    public const string Stop = "core.stop";

    public const string WhenCondition = "condition";
    public const string WhenPreviousFailed = "previousFailed";
    public const string WhenPreviousOk = "previousOk";

    public static bool IsLogic(string type) =>
        type.Equals(If, StringComparison.OrdinalIgnoreCase) || type.Equals(Else, StringComparison.OrdinalIgnoreCase)
        || type.Equals(EndIf, StringComparison.OrdinalIgnoreCase) || type.Equals(Stop, StringComparison.OrdinalIgnoreCase);

    /// <summary>The nesting depth of every row, as the editor indents it: an If, its Otherwise and its End sit at the same depth, the steps between them one deeper.</summary>
    public static int[] Depths(IReadOnlyList<string> types)
    {
        var depths = new int[types.Count];
        var open = 0;
        for (var i = 0; i < types.Count; i++)
        {
            var type = types[i];
            if (type.Equals(If, StringComparison.OrdinalIgnoreCase)) { depths[i] = open; open++; }
            else if (type.Equals(Else, StringComparison.OrdinalIgnoreCase)) depths[i] = Math.Max(open - 1, 0);
            else if (type.Equals(EndIf, StringComparison.OrdinalIgnoreCase)) { if (open > 0) open--; depths[i] = open; }
            else depths[i] = open;
        }
        return depths;
    }

    /// <summary>The state of one run of a list: how many Ifs are open, whether the current branch is skipped, and how the last step that ran ended.</summary>
    public sealed class Run
    {
        private int _open;
        private int _skip; // 0 = running; 1 = the current branch is not taken; more = inside a nested If of a branch that is not taken

        /// <summary>How the last step that actually ran ended. Before any step ran, nothing has failed.</summary>
        public bool PreviousFailed { get; private set; }

        /// <summary>Decides what to do with the next step. <paramref name="ifMet"/> is called only when an If is reached on a branch that is taken.</summary>
        public FlowStep Next(string type, Func<bool> ifMet)
        {
            if (type.Equals(If, StringComparison.OrdinalIgnoreCase))
            {
                _open++;
                if (_skip > 0) _skip++;
                else if (!ifMet()) _skip = 1;
                return FlowStep.Skip;
            }
            if (type.Equals(Else, StringComparison.OrdinalIgnoreCase))
            {
                if (_open > 0 && _skip <= 1) _skip = _skip == 0 ? 1 : 0;
                return FlowStep.Skip;
            }
            if (type.Equals(EndIf, StringComparison.OrdinalIgnoreCase))
            {
                if (_open == 0) return FlowStep.Skip;
                _open--;
                _skip = _skip > 1 ? _skip - 1 : 0;
                return FlowStep.Skip;
            }
            if (type.Equals(Stop, StringComparison.OrdinalIgnoreCase))
                return _skip == 0 ? FlowStep.Stop : FlowStep.Skip;
            return _skip == 0 ? FlowStep.Run : FlowStep.Skip;
        }

        /// <summary>Records how a step that ran ended.</summary>
        public void Ran(bool failed) => PreviousFailed = failed;
    }
}
