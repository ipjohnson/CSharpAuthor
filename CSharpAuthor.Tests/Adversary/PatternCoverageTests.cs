using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Patterns. The measured coverage is 0% and this file is what that number means.
/// </summary>
/// <remarks>
/// <para>
/// The library has one thing in this whole area: <c>SyntaxHelpers.Is(component, type)</c>, which
/// writes a bare type pattern with no designation - and writes it from
/// <c>ITypeDefinition.Name</c>, so it is wrong for a generic or an array (see
/// <see cref="ExpressionAdversaryTests"/>). Nothing else in the pattern grammar has an entry point.
/// </para>
/// <para>
/// These are recorded as tests rather than as a list in a document because a test is checked by the
/// build. Each one names the API that would be needed, so the shape of <c>IPattern</c> can be read
/// off the file: every case here has to be constructible and every case has to compose with
/// <c>and</c>, <c>or</c> and <c>not</c>.
/// </para>
/// </remarks>
public class PatternCoverageTests
{
    [Fact(Skip = "ADVERSARY GAP: no API - declaration pattern. 'x is Dog d' cannot be written: Is takes no designation, so the tested value cannot be captured and every use has to cast a second time.")]
    public void DeclarationPattern()
    {
        Assert.True(false, "need Is(value, type, designation)");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - constant pattern. 'x is 0', 'x is null', 'x is \"a\"' have no component; the null test has to be written as an equality expression instead.")]
    public void ConstantPattern()
    {
        Assert.True(false, "need a constant pattern node");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - relational patterns. 'x is > 0', 'x is <= 10' cannot be written, so a range test cannot be expressed as a pattern at all.")]
    public void RelationalPattern()
    {
        Assert.True(false, "need relational pattern nodes for < <= > >=");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - pattern combinators. 'and', 'or' and 'not' have no node, so no pattern can be composed with another; SyntaxHelpers.And/Or build boolean expressions, which is a different grammar position.")]
    public void PatternCombinators()
    {
        Assert.True(false, "need and / or / not pattern combinators");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - 'is not null' cannot be written as a pattern. This is the single most common pattern in generated code and there is no route to it.")]
    public void NotNullPattern()
    {
        Assert.True(false, "need a not-pattern over a constant null pattern");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - property pattern. 'x is { Count: > 0 }' and nested designations like 'x is { Owner: { Name: var n } }' have no component.")]
    public void PropertyPattern()
    {
        Assert.True(false, "need a property pattern with nested subpatterns and designations");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - positional/recursive pattern. 'x is Point(0, var y)' cannot be written, so a deconstructible type cannot be matched.")]
    public void PositionalPattern()
    {
        Assert.True(false, "need a positional pattern");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - list pattern. 'x is [1, 2, ..]' has no component.")]
    public void ListPattern()
    {
        Assert.True(false, "need a list pattern");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - slice pattern. 'x is [first, .. var rest]' has no component.")]
    public void SlicePattern()
    {
        Assert.True(false, "need a slice pattern with an optional designation");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - var pattern. 'x is var v' has no component.")]
    public void VarPattern()
    {
        Assert.True(false, "need a var pattern");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - discard pattern. 'x is _' has no component, and neither does the discard arm of a switch expression.")]
    public void DiscardPattern()
    {
        Assert.True(false, "need a discard pattern");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - parenthesised pattern. Without one, a combinator fix cannot control its own precedence: 'a or b and c' means 'a or (b and c)'.")]
    public void ParenthesisedPattern()
    {
        Assert.True(false, "need a parenthesised pattern");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - a pattern cannot appear in a case label. CaseBlockDefinition writes 'case <value>:' from an expression, so 'case Dog d when d.Age > 2:' cannot be produced.")]
    public void PatternInACaseLabel()
    {
        Assert.True(false, "need case patterns and case guards");
    }

    /// <summary>
    /// The one pattern that can be written, so the eventual pattern API has something to stay
    /// compatible with. Unskipped.
    /// </summary>
    [Fact]
    public void TypePatternWithoutADesignationIsTheOnlyOneAvailable()
    {
        var expression = Is(CodeOutputComponent.Get("x"), TypeDefinition.Get(typeof(string)));

        Assert.Equal("x is string", Emit.Component(expression));

        RoslynAssert.StatementCompiles("object x = null;\nif (" + Emit.Component(expression) + ") { }");
    }
}
