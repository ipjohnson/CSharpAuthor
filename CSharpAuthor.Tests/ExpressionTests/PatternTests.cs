using System.Collections.Generic;
using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.ExpressionTests;

/// <summary>
/// Patterns, including their own precedence ladder. <c>a or b and c</c> is
/// <c>a or (b and c)</c>, so a tree that meant otherwise has to say so — the same silent
/// failure as expression precedence, in a smaller alphabet.
/// </summary>
public class PatternTests
{
    private static ITypeDefinition Widget => TypeDefinition.Get("TestNamespace", "Widget");

    private static ITypeDefinition Int32Type => TypeDefinition.Get(typeof(int));

    // -------------------------------------------------------------------------------
    // Primary patterns
    // -------------------------------------------------------------------------------

    [Fact]
    public void TheSimplePatterns()
    {
        ExAssert.Emits("a is _", Ex.Is(Ex.Id("a"), Pat.Discard));
        ExAssert.Emits("a is null", Ex.Is(Ex.Id("a"), Pat.Null));
        ExAssert.Emits("a is Widget", Ex.Is(Ex.Id("a"), Pat.Type(Widget)));
        ExAssert.Emits("a is Widget w", Ex.Is(Ex.Id("a"), Pat.Declaration(Widget, "w")));
        ExAssert.Emits("a is var w", Ex.Is(Ex.Id("a"), Pat.Var("w")));
        ExAssert.Emits("a is var (x, y)", Ex.Is(Ex.Id("a"), Pat.VarTuple("x", "y")));
    }

    [Fact]
    public void ConstantPatterns()
    {
        ExAssert.Emits("a is 42", Ex.Is(Ex.Id("a"), Pat.Constant(Ex.Int(42))));
        ExAssert.Emits("a is \"text\"", Ex.Is(Ex.Id("a"), Pat.Constant(Ex.Str("text"))));
        ExAssert.Emits("a is -1", Ex.Is(Ex.Id("a"), Pat.Constant(Ex.Negate(Ex.Int(1)))));
        ExAssert.Emits("a is Colour.Red", Ex.Is(Ex.Id("a"), Pat.Constant(Ex.Id("Colour").Dot("Red"))));
    }

    [Fact]
    public void AComputedConstantIsBracketed()
    {
        var pattern = Pat.Constant(Ex.Add(Ex.Int(1), Ex.Int(2)));

        ExAssert.Emits("a is (1 + 2)", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void RelationalPatterns()
    {
        ExAssert.Emits("a is > 5", Ex.Is(Ex.Id("a"), Pat.GreaterThan(Ex.Int(5))));
        ExAssert.Emits("a is <= 5", Ex.Is(Ex.Id("a"), Pat.LessThanOrEqual(Ex.Int(5))));
    }

    [Fact]
    public void TypeTestShorthands()
    {
        ExAssert.Emits("a is Widget", Ex.Is(Ex.Id("a"), Widget));
        ExAssert.Emits("a is not Widget", Ex.IsNot(Ex.Id("a"), Widget));
        ExAssert.Emits("a as Widget", Ex.As(Ex.Id("a"), Widget));
        ExAssert.Emits("a is not null", Ex.Is(Ex.Id("a"), Pat.NotNull()));
    }

    // -------------------------------------------------------------------------------
    // Combinators and their precedence
    // -------------------------------------------------------------------------------

    [Fact]
    public void AndBindsTighterThanOr()
    {
        var pattern = Pat.Or(Pat.And(Pat.GreaterThan(Ex.Int(0)), Pat.LessThan(Ex.Int(10))), Pat.Null);

        ExAssert.Emits("a is > 0 and < 10 or null", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void AnOrUnderAnAndIsBracketed()
    {
        var pattern = Pat.And(Pat.Or(Pat.Type(Widget), Pat.Null), Pat.NotNull());

        ExAssert.Emits("a is (Widget or null) and not null", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void OrIsLeftAssociative()
    {
        var left = Pat.Or(Pat.Or(Pat.Constant(Ex.Int(1)), Pat.Constant(Ex.Int(2))), Pat.Constant(Ex.Int(3)));
        var right = Pat.Or(Pat.Constant(Ex.Int(1)), Pat.Or(Pat.Constant(Ex.Int(2)), Pat.Constant(Ex.Int(3))));

        ExAssert.Emits("a is 1 or 2 or 3", Ex.Is(Ex.Id("a"), left));
        ExAssert.Emits("a is 1 or (2 or 3)", Ex.Is(Ex.Id("a"), right));
    }

    [Fact]
    public void NotBracketsAnythingLooserThanItself()
    {
        ExAssert.Emits("a is not Widget", Ex.Is(Ex.Id("a"), Pat.Not(Pat.Type(Widget))));
        ExAssert.Emits(
            "a is not (Widget or null)",
            Ex.Is(Ex.Id("a"), Pat.Not(Pat.Or(Pat.Type(Widget), Pat.Null))));
        ExAssert.Emits(
            "a is not (Widget and not null)",
            Ex.Is(Ex.Id("a"), Pat.Not(Pat.And(Pat.Type(Widget), Pat.NotNull()))));
    }

    [Fact]
    public void ExplicitPatternBracketsSurvive()
    {
        ExAssert.Emits("a is (Widget)", Ex.Is(Ex.Id("a"), Pat.Parenthesized(Pat.Type(Widget))));
    }

    // -------------------------------------------------------------------------------
    // Recursive patterns
    // -------------------------------------------------------------------------------

    [Fact]
    public void APropertyPatternWithoutAType()
    {
        var pattern = Pat.Property(null, new[] { Pat.Prop("Length", Pat.GreaterThan(Ex.Int(0))) });

        ExAssert.Emits("a is { Length: > 0 }", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void APropertyPatternWithATypeAndADesignation()
    {
        var pattern = Pat.Property(
            Widget,
            new[] { Pat.Prop("Length", Pat.GreaterThan(Ex.Int(0))) },
            "w");

        ExAssert.Emits("a is Widget { Length: > 0 } w", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void NestedPropertyPatternsCarryTheirOwnDesignations()
    {
        var inner = Pat.Property(null, new[] { Pat.Prop("Y", Pat.GreaterThan(Ex.Int(2))) }, "i");
        var outer = Pat.Property(Widget, new[] { Pat.Prop("Inner", inner) });

        ExAssert.Emits("a is Widget { Inner: { Y: > 2 } i }", Ex.Is(Ex.Id("a"), outer));
    }

    [Fact]
    public void AnEmptyPropertyPatternIsAnObjectTest()
    {
        var pattern = Pat.Property(null, new KeyValuePair<string, Pat>[0]);

        ExAssert.Emits("a is { }", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void APositionalPatternReadsLikeADeconstruction()
    {
        var pattern = Pat.Positional(Widget, Pat.Constant(Ex.Int(0)), Pat.Var("y"));

        ExAssert.Emits("a is Widget(0, var y)", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void APositionalPatternWithoutAType()
    {
        var pattern = Pat.Positional(null, Pat.Var("x"), Pat.Var("y"));

        ExAssert.Emits("a is (var x, var y)", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void TheFullRecursiveForm()
    {
        var pattern = Pat.Recursive(
            Widget,
            new[] { Pat.Constant(Ex.Int(0)) },
            new[] { Pat.Prop("Name", Pat.NotNull()) },
            "w");

        ExAssert.Emits("a is Widget(0) { Name: not null } w", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void PropertyNamesAreKeywordEscaped()
    {
        var pattern = Pat.Property(null, new[] { Pat.Prop("class", Pat.NotNull()) });

        ExAssert.Emits("a is { @class: not null }", Ex.Is(Ex.Id("a"), pattern));
    }

    // -------------------------------------------------------------------------------
    // List patterns
    // -------------------------------------------------------------------------------

    [Fact]
    public void AListPattern()
    {
        var pattern = Pat.List(Pat.Constant(Ex.Int(1)), Pat.Constant(Ex.Int(2)));

        ExAssert.Emits("a is [1, 2]", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void ASlicePattern()
    {
        var pattern = Pat.List(Pat.Constant(Ex.Int(1)), Pat.Slice(), Pat.Constant(Ex.Int(9)));

        ExAssert.Emits("a is [1, .., 9]", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void ASlicePatternMayCaptureTheRest()
    {
        var pattern = Pat.List(Pat.Constant(Ex.Int(1)), Pat.Slice(Pat.Var("rest")));

        ExAssert.Emits("a is [1, .. var rest]", Ex.Is(Ex.Id("a"), pattern));
    }

    [Fact]
    public void AnEmptyListPattern()
    {
        ExAssert.Emits("a is []", Ex.Is(Ex.Id("a"), Pat.List()));
    }

    // -------------------------------------------------------------------------------
    // Patterns inside switch expressions
    // -------------------------------------------------------------------------------

    [Fact]
    public void ASwitchExpressionOverPatterns()
    {
        var expression = Ex.SwitchInline(
            Ex.Id("shape"),
            Ex.Arm(Pat.Declaration(Widget, "w"), Ex.Id("w").Dot("Area")),
            Ex.Arm(Pat.Null, Ex.Int(0)),
            Ex.Arm(Pat.Discard, Ex.Throw(Ex.New(Widget))));

        ExAssert.Emits(
            "shape switch { Widget w => w.Area, null => 0, _ => throw new Widget() }",
            expression);
    }

    [Fact]
    public void ASwitchExpressionRendersOneArmPerLineByDefault()
    {
        var expression = Ex.Switch(
            Ex.Id("value"),
            Ex.Arm(Pat.Constant(Ex.Int(1)), Ex.Str("one")),
            Ex.Arm(Pat.Discard, Ex.Str("many")));

        ExAssert.Emits(
            "value switch\n{\n    1 => \"one\",\n    _ => \"many\"\n}",
            expression);
    }

    [Fact]
    public void ARelationalPatternRangeInASwitchArm()
    {
        var expression = Ex.SwitchInline(
            Ex.Id("n"),
            Ex.Arm(Pat.And(Pat.GreaterThanOrEqual(Ex.Int(0)), Pat.LessThan(Ex.Int(10))), Ex.Str("small")),
            Ex.Arm(Pat.Discard, Ex.Str("large")));

        ExAssert.Emits(
            "n switch { >= 0 and < 10 => \"small\", _ => \"large\" }",
            expression);
    }

    [Fact]
    public void ATypedPatternKeepsItsTypeDeferred()
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Ex.Is(Ex.Id("a"), Pat.Declaration(Int32Type, "n")).WriteOutput(context);

        Assert.Equal("a is int n", context.Output());
    }
}
