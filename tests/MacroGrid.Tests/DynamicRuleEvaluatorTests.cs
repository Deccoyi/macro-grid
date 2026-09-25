using MacroGrid.Core.Model;
using MacroGrid.Core.Variables;

namespace MacroGrid.Tests;

public class DynamicRuleEvaluatorTests
{
    private static ConditionNode Compare(string variable, string op, string value, string? value2 = null) =>
        new() { Kind = ConditionKinds.Compare, Variable = variable, Operator = op, Value = value, Value2 = value2 };

    private static ConditionNode Combine(string kind, params ConditionNode[] children) =>
        new() { Kind = kind, Children = [.. children] };

    private static VariableStore StoreWith(string name, object? value)
    {
        var store = new VariableStore();
        store.Set(name, value);
        return store;
    }

    [Theory]
    [InlineData(60, ">", "50", true)]
    [InlineData(40, ">", "50", false)]
    [InlineData(50, ">=", "50", true)]
    [InlineData(50, "<=", "50", true)]
    [InlineData(40, "<", "50", true)]
    [InlineData(50, "==", "50", true)]
    [InlineData(51, "!=", "50", true)]
    public void Evaluates_numeric_comparisons(double liveValue, string op, string value, bool expected)
    {
        var binding = new DynamicBinding { Cases = [new DynamicCase(Compare("v", op, value), "match")] };

        var result = DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", liveValue));

        Assert.Equal(expected ? "match" : null, result);
    }

    [Theory]
    [InlineData(60, "50", "80", true)]
    [InlineData(40, "50", "80", false)]
    [InlineData(50, "50", "80", true)]
    [InlineData(80, "50", "80", true)]
    [InlineData(60, "80", "50", true)] // bounds given in reverse order still work
    public void Between_is_inclusive_and_order_independent(double liveValue, string lo, string hi, bool expected)
    {
        var binding = new DynamicBinding { Cases = [new DynamicCase(Compare("v", DynamicOperators.Between, lo, hi), "match")] };

        var result = DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", liveValue));

        Assert.Equal(expected ? "match" : null, result);
    }

    [Fact]
    public void Returns_default_when_no_case_matches()
    {
        var binding = new DynamicBinding { Cases = [new DynamicCase(Compare("v", ">", "50"), "red")], Default = "green" };

        Assert.Equal("green", DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", 10.0)));
    }

    [Fact]
    public void Returns_null_when_no_case_matches_and_no_default()
    {
        var binding = new DynamicBinding { Cases = [new DynamicCase(Compare("v", ">", "50"), "red")] };

        Assert.Null(DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", 10.0)));
    }

    [Fact]
    public void First_matching_case_wins()
    {
        var binding = new DynamicBinding
        {
            Cases =
            [
                new DynamicCase(Compare("v", ">", "80"), "red"),
                new DynamicCase(Compare("v", ">", "50"), "yellow"),
            ],
            Default = "green",
        };

        Assert.Equal("yellow", DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", 60.0)));
        Assert.Equal("red", DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", 90.0)));
        Assert.Equal("green", DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", 10.0)));
    }

    [Fact]
    public void String_equality_is_case_insensitive()
    {
        var binding = new DynamicBinding { Cases = [new DynamicCase(Compare("mode", "==", "Recording"), "red")] };

        Assert.Equal("red", DynamicRuleEvaluator.Evaluate(binding, StoreWith("mode", "recording")));
    }

    [Fact]
    public void And_requires_every_child_true()
    {
        var store = new VariableStore();
        store.Set("cpu", 90.0);
        store.Set("ram", 30.0);
        var binding = new DynamicBinding
        {
            Cases = [new DynamicCase(Combine(ConditionKinds.And, Compare("cpu", ">", "50"), Compare("ram", ">", "50")), "kritik")],
        };

        Assert.Null(DynamicRuleEvaluator.Evaluate(binding, store)); // ram is not > 50

        store.Set("ram", 60.0);
        Assert.Equal("kritik", DynamicRuleEvaluator.Evaluate(binding, store));
    }

    [Fact]
    public void Or_requires_any_child_true()
    {
        var store = new VariableStore();
        store.Set("cpu", 10.0);
        store.Set("ram", 90.0);
        var binding = new DynamicBinding
        {
            Cases = [new DynamicCase(Combine(ConditionKinds.Or, Compare("cpu", ">", "80"), Compare("ram", ">", "80")), "uyar")],
        };

        Assert.Equal("uyar", DynamicRuleEvaluator.Evaluate(binding, store));
    }

    [Fact]
    public void Xor_is_true_for_an_odd_number_of_true_children()
    {
        var store = new VariableStore();
        store.Set("a", 1.0);
        store.Set("b", 1.0);
        store.Set("c", 1.0);
        var binding = new DynamicBinding
        {
            Cases = [new DynamicCase(Combine(ConditionKinds.Xor, Compare("a", "==", "1"), Compare("b", "==", "1"), Compare("c", "==", "0")), "x")],
        };

        // a==1 true, b==1 true, c==0 false -> two true children -> XOR false
        Assert.Null(DynamicRuleEvaluator.Evaluate(binding, store));

        store.Set("c", 0.0); // now c==0 is also true -> three true children -> odd -> XOR true
        Assert.Equal("x", DynamicRuleEvaluator.Evaluate(binding, store));
    }

    [Fact]
    public void Not_negates_its_single_child()
    {
        var binding = new DynamicBinding
        {
            Cases = [new DynamicCase(Combine(ConditionKinds.Not, Compare("muted", "==", "true")), "sesli")],
        };

        Assert.Null(DynamicRuleEvaluator.Evaluate(binding, StoreWith("muted", "true")));
        Assert.Equal("sesli", DynamicRuleEvaluator.Evaluate(binding, StoreWith("muted", "false")));
    }

    [Fact]
    public void CollectVariables_walks_the_whole_tree()
    {
        var tree = Combine(ConditionKinds.And,
            Compare("cpu", ">", "50"),
            Combine(ConditionKinds.Not, Compare("muted", "==", "true")));

        Assert.Equal(["cpu", "muted"], DynamicRuleEvaluator.CollectVariables(tree));
    }

    [Fact]
    public void Missing_variable_does_not_throw_and_simply_does_not_match()
    {
        var binding = new DynamicBinding { Cases = [new DynamicCase(Compare("nope", ">", "50"), "x")] };

        Assert.Null(DynamicRuleEvaluator.Evaluate(binding, new VariableStore()));
    }

    [Theory]
    [InlineData(true, "==", "true", true)]
    [InlineData(true, "==", "True", true)]
    [InlineData(true, "==", "1", true)]
    [InlineData(true, "==", "0", false)]
    [InlineData(true, "!=", "false", true)]
    [InlineData(false, "==", "false", true)]
    [InlineData(false, "==", "0", true)]
    [InlineData(false, "==", " FALSE ", true)]
    [InlineData(false, "!=", "1", true)]
    [InlineData(false, "==", "1", false)]
    [InlineData(true, ">", "0", false)] // only == and != mean anything for a boolean
    [InlineData(true, "==", "On", false)] // the display word of a template is not a condition value
    public void Boolean_accepts_true_false_and_one_zero(bool liveValue, string op, string value, bool expected)
    {
        var binding = new DynamicBinding { Cases = [new DynamicCase(Compare("muted", op, value), "match")] };

        var result = DynamicRuleEvaluator.Evaluate(binding, StoreWith("muted", liveValue));

        Assert.Equal(expected ? "match" : null, result);
    }

    [Fact]
    public void One_and_zero_stay_numeric_for_a_number()
    {
        var binding = new DynamicBinding { Cases = [new DynamicCase(Compare("v", "==", "1"), "match")] };

        Assert.Equal("match", DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", 1.0)));
        Assert.Null(DynamicRuleEvaluator.Evaluate(binding, StoreWith("v", 2.0)));
    }
}
