using System.Linq;
using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.ExpressionTests;

/// <summary>
/// Invariant 4: the escape hatch composes. Dropping to text costs one node, never the
/// file — so a <see cref="Raw"/> still holds its types unrendered, still contributes their
/// namespaces, still honours <see cref="TypeOutputMode"/>, and still brackets itself when
/// it is used as an operand.
/// </summary>
public class RawCompositionTests
{
    private static ITypeDefinition ServiceLifetime =>
        TypeDefinition.Get("Microsoft.Extensions.DependencyInjection", "ServiceLifetime");

    // -------------------------------------------------------------------------------
    // A Raw stands in all three roles.
    // -------------------------------------------------------------------------------

    [Fact]
    public void RawStandsWhereAnExpressionAStatementOrAPatternIsExpected()
    {
        var raw = new Raw("value");

        Assert.IsAssignableFrom<IExpressionNode>(raw);
        Assert.IsAssignableFrom<IStatementNode>(raw);
        Assert.IsAssignableFrom<IPatternNode>(raw);
        Assert.IsAssignableFrom<IOutputComponent>(raw);
    }

    [Fact]
    public void ARawConvertsIntoAnExpressionAndKeepsItsPrecedence()
    {
        Ex expression = new Raw("a + b");

        Assert.Equal(ExPrecedence.Lowest, expression.Precedence);
    }

    [Fact]
    public void ARawConvertsIntoAPattern()
    {
        Pat pattern = Raw.At(PatPrecedence.Primary, "> 5");

        var arm = Ex.SwitchInline(Ex.Id("a"), Ex.Arm(pattern, Ex.Id("b")));

        ExAssert.Emits("a switch { > 5 => b }", arm);
    }

    // -------------------------------------------------------------------------------
    // Types inside a Raw stay unrendered until serialization.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ATypeInsideARawIsStillDeferredAndStillDerivesItsUsing()
    {
        var raw = new Raw(ServiceLifetime, ".Transient");
        var context = new OutputContext();

        raw.WriteOutput(context);
        context.GenerateUsingStatements();

        Assert.Equal(
            "using Microsoft.Extensions.DependencyInjection;\n\nServiceLifetime.Transient",
            context.Output());
    }

    [Fact]
    public void ATypeInsideARawHonoursGlobalModeAndEmitsNoUsing()
    {
        // This is the shape of the bug that motivated the rewrite: a raw fragment that
        // tracked no namespace resolved only because of a stray `using` that Global mode
        // should never have produced. Carrying the type instead of its text fixes both
        // halves at once.
        var raw = new Raw(ServiceLifetime, ".Transient");
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        raw.WriteOutput(context);
        context.GenerateUsingStatements();

        Assert.Equal(
            "global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient",
            context.Output());
    }

    [Fact]
    public void ATypeInsideARawHonoursFullNameMode()
    {
        var raw = new Raw(ServiceLifetime, ".Transient");
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.FullName });

        raw.WriteOutput(context);
        context.GenerateUsingStatements();

        Assert.Equal(
            "Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient",
            context.Output());
    }

    [Fact]
    public void TypeReferencesExposesWhatTheNamePlanNeedsBeforeAnythingIsWritten()
    {
        var inner = new Raw(ExAssert.Type("Inner"), ".Value");
        var outer = new Raw("Outer.Call(", inner, ", ", ServiceLifetime, ".Singleton)");

        var names = outer.TypeReferences.Select(type => type.Name).ToList();

        Assert.Equal(new[] { "Inner", "ServiceLifetime" }, names);
    }

    [Fact]
    public void ARawInsideAnInterpolatedStringStillDefersItsType()
    {
        var expression = Ex.Interpolate("lifetime=", new Raw(ServiceLifetime, ".Transient"));
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        expression.WriteOutput(context);
        context.GenerateUsingStatements();

        Assert.Equal(
            "$\"lifetime={global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient}\"",
            context.Output());
    }

    // -------------------------------------------------------------------------------
    // Precedence inferred from the shape of the text. The failure mode is a redundant
    // bracket, never a changed program.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ARawHoldingABinaryOperatorIsBracketedAsAnOperand()
    {
        // The silent-wrongness case: `a + b * c` would reassociate the sum.
        ExAssert.Emits("(a + b) * c", Ex.Multiply(Ex.Raw("a + b"), Ex.Id("c")));
    }

    [Fact]
    public void ARawHoldingAMemberChainIsNotBracketed()
    {
        ExAssert.Emits(
            "ServiceLifetime.Transient == a",
            Ex.Equal(Ex.Raw("ServiceLifetime.Transient"), Ex.Id("a")));
    }

    [Theory]
    [InlineData("value")]
    [InlineData("@class")]
    [InlineData("a.b.c")]
    [InlineData("a.b(c)")]
    [InlineData("a.b(c)[0]")]
    [InlineData("a.b!")]
    [InlineData("a++")]
    [InlineData("global::Ns.Type.Member")]
    [InlineData("List<int>.Empty")]
    [InlineData("Dictionary<string, List<int>>.Empty")]
    [InlineData("new Foo()")]
    [InlineData("new Foo { A = 1 }")]
    [InlineData("new[] { 1, 2 }")]
    [InlineData("typeof(int)")]
    [InlineData("nameof(Foo)")]
    [InlineData("default(int)")]
    [InlineData("default")]
    [InlineData("this")]
    [InlineData("base.Method()")]
    [InlineData("\"text\"")]
    [InlineData("'c'")]
    [InlineData("1")]
    [InlineData("1.5")]
    [InlineData("0xFF")]
    [InlineData("1.ToString()")]
    [InlineData("a . b")]
    [InlineData("f(a + b)")]
    [InlineData("a[i + 1]")]
    public void TheseShapesAreProvenPrimary(string text)
    {
        Assert.Equal(ExPrecedence.Primary, new Raw(text).Precedence);
    }

    [Theory]
    [InlineData("a + b")]
    [InlineData("a+b")]
    [InlineData("a ?? b")]
    [InlineData("a is B")]
    [InlineData("a as B")]
    [InlineData("x => x + 1")]
    [InlineData("a, b")]
    [InlineData("a ? b : c")]
    [InlineData("a = b")]
    [InlineData("a && b")]
    [InlineData("(int)x")]
    [InlineData("1..2")]
    [InlineData("a switch { _ => b }")]
    [InlineData("a with { X = 1 }")]
    [InlineData("$\"{a}\"")]
    [InlineData("a.")]
    [InlineData("a < b")]
    [InlineData("await a + b")]
    public void TheseShapesFallBackToTheConservativeAnswer(string text)
    {
        Assert.Equal(ExPrecedence.Lowest, new Raw(text).Precedence);
    }

    [Fact]
    public void ARawHoldingANullConditionalChainIsTreatedAsAChain()
    {
        Assert.Equal(ExPrecedence.NullChain, new Raw("a?.b").Precedence);

        // And therefore brackets rather than silently extending the chain.
        ExAssert.Emits("(a?.b).c", Ex.Raw("a?.b").Dot("c"));
    }

    [Fact]
    public void ARawHoldingAPrefixOperatorIsUnary()
    {
        Assert.Equal(ExPrecedence.Unary, new Raw("-1").Precedence);
        Assert.Equal(ExPrecedence.Unary, new Raw("!flag").Precedence);
        Assert.Equal(ExPrecedence.Unary, new Raw("await task").Precedence);
    }

    [Fact]
    public void ARawStartingWithAMinusIsBracketedUnderNegation()
    {
        // `--1` would be a pre-decrement.
        ExAssert.Emits("-(-1)", Ex.Negate(Ex.Raw("-1")));
    }

    [Fact]
    public void ATypePartCountsAsAnIdentifierWhenTheShapeIsRead()
    {
        Assert.Equal(ExPrecedence.Primary, new Raw(ExAssert.Type("Foo"), ".Bar").Precedence);
        Assert.Equal(ExPrecedence.Primary, new Raw("new ", ExAssert.Type("Foo"), "()").Precedence);
        Assert.Equal(ExPrecedence.Lowest, new Raw(ExAssert.Type("Foo"), ".Bar + 1").Precedence);
    }

    [Fact]
    public void ALooseNestedNodeForcesTheConservativeAnswer()
    {
        var nested = Ex.Add(Ex.Id("a"), Ex.Id("b"));

        Assert.Equal(ExPrecedence.Lowest, new Raw(nested, ".Length").Precedence);
    }

    [Fact]
    public void AnAssertedPrecedenceOverridesTheInference()
    {
        ExAssert.Emits("a + b * c", Ex.Multiply(Ex.RawAt(ExPrecedence.Multiplicative, "a + b"), Ex.Id("c")));
        Assert.Equal(ExPrecedence.Primary, Raw.Primary("whatever + you + say").Precedence);
    }

    [Fact]
    public void AnEmptyRawIsHarmless()
    {
        Assert.Equal(ExPrecedence.Primary, new Raw().Precedence);
        ExAssert.Emits("", new Raw());
    }

    // -------------------------------------------------------------------------------
    // Composition with the rest of the layer.
    // -------------------------------------------------------------------------------

    [Fact]
    public void ARawComposesWithTheOperatorOverloads()
    {
        Ex left = new Raw("a + b");
        Ex right = new Raw("c");

        ExAssert.Emits("(a + b) && c", left & right);
    }

    [Fact]
    public void ARawCarriesNestedComponentsThrough()
    {
        var inner = Ex.Call(ExAssert.Type("Helper"), "Make", Ex.Str("x"));
        var raw = new Raw("return ", inner, ";");

        ExAssert.Emits("return Helper.Make(\"x\");", raw);
    }

    [Fact]
    public void ARawRendersAsAStatementLine()
    {
        var context = new OutputContext();

        context.IncrementIndent();
        Ex.Raw("Foo.Bar()").AsStatement().WriteOutput(context);

        Assert.Equal("    Foo.Bar();\n", context.Output());
    }
}
