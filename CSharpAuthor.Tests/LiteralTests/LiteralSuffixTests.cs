using Xunit;

namespace CSharpAuthor.Tests.LiteralTests;

/// <summary>
/// A literal has to denote the type it came from. <c>float f = 1.5;</c> is CS0664 and
/// <c>char c = a;</c> is CS0103 - both were what this library emitted.
/// </summary>
public class LiteralSuffixTests
{
    [Theory]
    [InlineData(1.5f, "1.5f")]
    [InlineData(0f, "0f")]
    [InlineData(-2.25f, "-2.25f")]
    public void FloatCarriesItsSuffix(float value, string expected)
    {
        Assert.Equal(expected, LiteralFormatter.Format(value));
    }

    [Theory]
    [InlineData(1.5d, "1.5d")]
    [InlineData(1d, "1d")]
    public void DoubleCarriesItsSuffix(double value, string expected)
    {
        Assert.Equal(expected, LiteralFormatter.Format(value));
    }

    [Fact]
    public void DecimalCarriesItsSuffix()
    {
        Assert.Equal("1.5m", LiteralFormatter.Format(1.5m));
        Assert.Equal("0.0m", LiteralFormatter.Format(0.0m));
    }

    [Fact]
    public void IntegerTypesCarryTheSuffixTheyNeed()
    {
        Assert.Equal("5", LiteralFormatter.Format(5));
        Assert.Equal("5L", LiteralFormatter.Format(5L));
        Assert.Equal("5U", LiteralFormatter.Format(5U));
        Assert.Equal("5UL", LiteralFormatter.Format(5UL));
    }

    [Fact]
    public void LongValueBeyondIntRangeKeepsItsPrecision()
    {
        Assert.Equal("9223372036854775807L", LiteralFormatter.Format(long.MaxValue));
        Assert.Equal("18446744073709551615UL", LiteralFormatter.Format(ulong.MaxValue));
        Assert.Equal("4294967295U", LiteralFormatter.Format(uint.MaxValue));
    }

    [Fact]
    public void FloatRoundTripsRatherThanRounding()
    {
        Assert.Equal("0.1f", LiteralFormatter.Format(0.1f));
        Assert.Equal("0.1d", LiteralFormatter.Format(0.1d));
        Assert.Equal("3.14159265358979d", LiteralFormatter.Format(3.14159265358979d));
    }

    [Fact]
    public void NonFiniteValuesUseTheirNamedFormBecauseThereIsNoLiteralForThem()
    {
        Assert.Equal("float.NaN", LiteralFormatter.Format(float.NaN));
        Assert.Equal("float.PositiveInfinity", LiteralFormatter.Format(float.PositiveInfinity));
        Assert.Equal("float.NegativeInfinity", LiteralFormatter.Format(float.NegativeInfinity));
        Assert.Equal("double.NaN", LiteralFormatter.Format(double.NaN));
        Assert.Equal("double.PositiveInfinity", LiteralFormatter.Format(double.PositiveInfinity));
        Assert.Equal("double.NegativeInfinity", LiteralFormatter.Format(double.NegativeInfinity));
    }

    [Fact]
    public void CharIsQuoted()
    {
        Assert.Equal("'a'", LiteralFormatter.Format('a'));
        Assert.Equal("'\\0'", LiteralFormatter.Format('\0'));
        Assert.Equal("'\\t'", LiteralFormatter.Format('\t'));
    }

    [Fact]
    public void BoolAndNullKeepTheirKeywords()
    {
        Assert.Equal("true", LiteralFormatter.Format(true));
        Assert.Equal("false", LiteralFormatter.Format(false));
        Assert.Equal("null", LiteralFormatter.Format(null));
    }

    [Fact]
    public void StringStaysACodeFragment()
    {
        // Deliberate: throughout this library a string argument is code, not text.
        // AddCode("Foo()") and CodeOutputComponent.Get("Lifetime.Scoped") both depend on it.
        Assert.Equal("Lifetime.Scoped", LiteralFormatter.Format("Lifetime.Scoped"));
    }

    [Fact]
    public void FloatFieldInitializerCompilesAsAFloat()
    {
        var field = new FieldDefinition(TypeDefinition.Get(typeof(float)), "_rate")
        {
            InitializeValue = CodeOutputComponent.Get(1.5f)
        };

        var outputContext = new OutputContext();

        field.WriteOutput(outputContext);

        // The type name is asserted elsewhere; what matters here is the suffix on the literal.
        AssertEqual.ContainsWithoutNewLine("_rate = 1.5f;", outputContext.Output());
    }

    [Fact]
    public void CharFieldInitializerCompilesAsAChar()
    {
        var field = new FieldDefinition(TypeDefinition.Get(typeof(char)), "_separator")
        {
            InitializeValue = CodeOutputComponent.Get('a')
        };

        var outputContext = new OutputContext();

        field.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("_separator = 'a';", outputContext.Output());
    }

    [Fact]
    public void CharFieldInitializerEscapesAQuote()
    {
        var field = new FieldDefinition(TypeDefinition.Get(typeof(char)), "_quote")
        {
            InitializeValue = CodeOutputComponent.Get('\'')
        };

        var outputContext = new OutputContext();

        field.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("_quote = '\\'';", outputContext.Output());
    }

    [Fact]
    public void ParameterDefaultValueCarriesItsSuffix()
    {
        var method = new MethodDefinition("Scale") { Modifiers = ComponentModifier.Public };

        method.AddParameter(typeof(float), "factor").DefaultValue = CodeOutputComponent.Get(1.5f);

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("factor = 1.5f)", outputContext.Output());
    }
}
