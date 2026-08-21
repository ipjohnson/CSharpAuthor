using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.ExpressionTests;

/// <summary>
/// Interpolated strings. Literal text and hole text obey opposite rules, and conflating
/// them is the entire bug class: braces double in one and delimit in the other, quotes
/// escape in one and are content in the other.
/// </summary>
public class InterpolationTests
{
    [Fact]
    public void TextAndHolesAlternate()
    {
        ExAssert.Emits(
            "$\"Hello {name}!\"",
            Ex.Interpolate("Hello ", Ex.Id("name"), "!"));
    }

    [Fact]
    public void AnEmptyInterpolationIsStillAnInterpolatedString()
    {
        ExAssert.Emits("$\"\"", Ex.Interpolate());
    }

    [Fact]
    public void BracesInLiteralTextAreDoubled()
    {
        ExAssert.Emits(
            "$\"{{literal}} {n}\"",
            Ex.Interpolate("{literal} ", Ex.Id("n")));
    }

    [Fact]
    public void QuotesInLiteralTextAreEscaped()
    {
        ExAssert.Emits(
            "$\"he said \\\"hi\\\"\"",
            Ex.Interpolate("he said \"hi\""));
    }

    [Fact]
    public void QuotesInsideAHoleAreContentAndAreNotEscaped()
    {
        // Verified to compile: `$"{dict["k"]}"`. Escaping here would produce
        // `$"{dict[\"k\"]}"`, which does not.
        ExAssert.Emits(
            "$\"{dict[\"k\"]}\"",
            Ex.Interpolate(Ex.Id("dict").Index(Ex.Str("k"))));
    }

    [Fact]
    public void BackslashesInLiteralTextAreEscapedAndThenBracesAreDoubled()
    {
        // Order matters: doubling braces first would leave the backslash unescaped, and
        // escaping after doubling would double the escape.
        ExAssert.Emits(
            "$\"a\\\\b{{c}}\"",
            Ex.Interpolate("a\\b{c}"));
    }

    [Fact]
    public void NewlinesInLiteralTextBecomeEscapes()
    {
        ExAssert.Emits("$\"line\\nnext\"", Ex.Interpolate("line\nnext"));
    }

    [Fact]
    public void AlignmentIsWrittenAfterAComma()
    {
        ExAssert.Emits(
            "$\"{value,10}\"",
            Ex.Interpolate(Ex.Hole(Ex.Id("value"), alignment: 10)));
    }

    [Fact]
    public void NegativeAlignmentLeftAligns()
    {
        ExAssert.Emits(
            "$\"{value,-10}\"",
            Ex.Interpolate(Ex.Hole(Ex.Id("value"), alignment: -10)));
    }

    [Fact]
    public void FormatIsWrittenAfterAColon()
    {
        ExAssert.Emits(
            "$\"{value:N2}\"",
            Ex.Interpolate(Ex.Hole(Ex.Id("value"), format: "N2")));
    }

    [Fact]
    public void AlignmentAndFormatTogether()
    {
        ExAssert.Emits(
            "$\"{value,10:N2}\"",
            Ex.Interpolate(Ex.Hole(Ex.Id("value"), 10, "N2")));
    }

    [Fact]
    public void AConditionalInAHoleIsBracketedSoItsColonIsNotAFormatSpecifier()
    {
        // Verified: `$"{(p ? 1 : 2)}"` is the form that compiles.
        var hole = Ex.Conditional(Ex.Id("flag"), Ex.Int(1), Ex.Int(2));

        ExAssert.Emits("$\"{(flag ? 1 : 2)}\"", Ex.Interpolate(hole));
    }

    [Fact]
    public void ALambdaInAHoleIsBracketedForTheSameReason()
    {
        ExAssert.Emits(
            "$\"{(x => x)}\"",
            Ex.Interpolate(Ex.Lambda("x", Ex.Id("x"))));
    }

    [Fact]
    public void ACoalesceInAHoleNeedsNothing()
    {
        ExAssert.Emits(
            "$\"{a ?? b}\"",
            Ex.Interpolate(Ex.Coalesce(Ex.Id("a"), Ex.Id("b"))));
    }

    [Fact]
    public void ASumInAHoleNeedsNothing()
    {
        ExAssert.Emits(
            "$\"{a + b}\"",
            Ex.Interpolate(Ex.Add(Ex.Id("a"), Ex.Id("b"))));
    }

    [Fact]
    public void ARawWithAnUnprovableShapeInAHoleIsBracketed()
    {
        ExAssert.Emits("$\"{(a ? b : c)}\"", Ex.Interpolate(Ex.Raw("a ? b : c")));
    }

    [Fact]
    public void ATypeInAHoleStaysDeferred()
    {
        var expression = Ex.Interpolate("type=", ExAssert.Type("Widget"));
        var context = new OutputContext();

        expression.WriteOutput(context);
        context.GenerateUsingStatements();

        Assert.Equal("using TestNamespace;\n\n$\"type={Widget}\"", context.Output());
    }

    [Fact]
    public void AnInterpolatedStringNestsInsideAHole()
    {
        var inner = Ex.Interpolate(Ex.Id("a"));

        ExAssert.Emits("$\"{$\"{a}\"}\"", Ex.Interpolate(inner));
    }

    [Fact]
    public void AStringLiteralInAHoleKeepsItsOwnQuotes()
    {
        ExAssert.Emits(
            "$\"{Format(\"x\")}\"",
            Ex.Interpolate(Ex.Id("Format").Invoke(Ex.Str("x"))));
    }

    [Fact]
    public void TheVerbatimFormDoublesQuotesAndLeavesBackslashesAlone()
    {
        ExAssert.Emits(
            "$@\"C:\\path\\{name}\"",
            Ex.InterpolateVerbatim("C:\\path\\", Ex.Id("name")));
    }

    [Fact]
    public void TheVerbatimFormDoublesBracesToo()
    {
        ExAssert.Emits("$@\"{{x}} \"\"q\"\"\"", Ex.InterpolateVerbatim("{x} \"q\""));
    }

    [Fact]
    public void AnInterpolatedStringIsPrimaryAndComposesWithoutBrackets()
    {
        ExAssert.Emits(
            "$\"{a}\".Length",
            Ex.Interpolate(Ex.Id("a")).Dot("Length"));
    }

    [Fact]
    public void ValuesThatAreNotNodesBecomeLiteralHoles()
    {
        ExAssert.Emits("$\"n={42}\"", Ex.Interpolate("n=", 42));
    }
}
