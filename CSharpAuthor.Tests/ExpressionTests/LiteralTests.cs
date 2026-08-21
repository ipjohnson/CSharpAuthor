using System.Globalization;
using System.Threading;
using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.ExpressionTests;

/// <summary>
/// Literals and identifiers. Every failure in this file is silent in the sense that
/// matters: it produces text that either does not compile or compiles to the wrong value,
/// with nothing thrown along the way.
/// </summary>
public class LiteralTests
{
    [Fact]
    public void ABareStringIsAnIdentifierAndAStringLiteralIsExplicit()
    {
        // The asymmetry is deliberate. Getting it backwards is the commonest
        // code-generation bug there is, so the literal always has to be asked for.
        Ex identifier = "name";

        ExAssert.Emits("name", identifier);
        ExAssert.Emits("\"name\"", Ex.Str("name"));
    }

    [Fact]
    public void StringLiteralsEscapeTheirQuotes()
    {
        ExAssert.Emits("\"he said \\\"hi\\\"\"", Ex.Str("he said \"hi\""));
    }

    [Fact]
    public void StringLiteralsEscapeBackslashesAndControlCharacters()
    {
        ExAssert.Emits("\"a\\\\b\"", Ex.Str("a\\b"));
        ExAssert.Emits("\"a\\nb\"", Ex.Str("a\nb"));
        ExAssert.Emits("\"a\\r\\nb\"", Ex.Str("a\r\nb"));
        ExAssert.Emits("\"a\\tb\"", Ex.Str("a\tb"));
        ExAssert.Emits("\"a\\0b\"", Ex.Str("a\0b"));
        ExAssert.Emits("\"a\\u0001b\"", Ex.Str("a\u0001b"));
    }

    [Fact]
    public void AWellFormedSurrogatePairIsLeftReadable()
    {
        // U+1F600, written as its pair. Escaping it would be correct but unreadable.
        ExAssert.Emits("\"\U0001F600\"", Ex.Str("\U0001F600"));
    }

    [Fact]
    public void ALoneSurrogateIsEscapedBecauseItIsNotACharacter()
    {
        ExAssert.Emits("\"\\ud800\"", Ex.Str("\ud800"));
    }

    [Fact]
    public void VerbatimStringLiteralsDoubleTheirQuotesAndKeepBackslashes()
    {
        ExAssert.Emits("@\"C:\\path\"", Ex.VerbatimStr("C:\\path"));
        ExAssert.Emits("@\"say \"\"hi\"\"\"", Ex.VerbatimStr("say \"hi\""));
    }

    [Fact]
    public void AVerbatimLiteralFallsBackWhenTheContentHidesACarriageReturn()
    {
        // A bare CR inside a verbatim literal is invisible and does not survive a trip
        // through an editor, so the escaped form is used instead.
        ExAssert.Emits("\"a\\rb\"", Ex.VerbatimStr("a\rb"));
    }

    [Fact]
    public void CharacterLiteralsAreQuotedAsCharacters()
    {
        // `char c = a;` is CS0103 — an identifier, not a character.
        ExAssert.Emits("'a'", Ex.Char('a'));
        ExAssert.Emits("'\\''", Ex.Char('\''));
        ExAssert.Emits("'\"'", Ex.Char('"'));
        ExAssert.Emits("'\\\\'", Ex.Char('\\'));
        ExAssert.Emits("'\\n'", Ex.Char('\n'));
        ExAssert.Emits("'\\0'", Ex.Char('\0'));
    }

    [Fact]
    public void NumericLiteralsCarryTheSuffixThatMakesThemTheRightType()
    {
        // `float f = 1.5;` is CS0664 without the suffix.
        ExAssert.Emits("1", Ex.Int(1));
        ExAssert.Emits("1U", Ex.UInt(1));
        ExAssert.Emits("1L", Ex.Long(1));
        ExAssert.Emits("1UL", Ex.ULong(1));
        ExAssert.Emits("1.5F", Ex.Float(1.5f));
        ExAssert.Emits("1.5D", Ex.Double(1.5));
        ExAssert.Emits("1.5M", Ex.Decimal(1.5m));
        ExAssert.Emits("-1", Ex.Int(-1));
    }

    [Fact]
    public void WholeNumberFloatingLiteralsKeepTheirSuffix()
    {
        ExAssert.Emits("1F", Ex.Float(1f));
        ExAssert.Emits("1D", Ex.Double(1));
    }

    [Fact]
    public void NonFiniteFloatingValuesBecomeNamedConstants()
    {
        ExAssert.Emits("double.NaN", Ex.Double(double.NaN));
        ExAssert.Emits("double.PositiveInfinity", Ex.Double(double.PositiveInfinity));
        ExAssert.Emits("float.NegativeInfinity", Ex.Float(float.NegativeInfinity));
    }

    [Fact]
    public void NumericLiteralsAreInvariantOfTheAmbientCulture()
    {
        // On de-DE the default formatting of 1.5 is `1,5`, which is two arguments.
        var previous = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            ExAssert.Emits("1.5D", Ex.Double(1.5));
            ExAssert.Emits("1.5F", Ex.Float(1.5f));
            ExAssert.Emits("1.5M", Ex.Decimal(1.5m));
            ExAssert.Emits("1000", Ex.Int(1000));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void KeywordIdentifiersAreVerbatimEscaped()
    {
        // `void M(string class)` is CS1001.
        ExAssert.Emits("@class", Ex.Id("class"));
        ExAssert.Emits("@event", Ex.Id("event"));
        ExAssert.Emits("@int", Ex.Id("int"));
        ExAssert.Emits("a.@return", Ex.Id("a").Dot("return"));
    }

    [Fact]
    public void ContextualKeywordsAreLegalIdentifiersAndAreLeftAlone()
    {
        ExAssert.Emits("var", Ex.Id("var"));
        ExAssert.Emits("value", Ex.Id("value"));
        ExAssert.Emits("record", Ex.Id("record"));
        ExAssert.Emits("when", Ex.Id("when"));
    }

    [Fact]
    public void AnAlreadyEscapedIdentifierIsNotEscapedTwice()
    {
        ExAssert.Emits("@class", Ex.Id("@class"));
    }

    [Fact]
    public void ValueDispatchesToTheRightLiteral()
    {
        ExAssert.Emits("null", Ex.Value(null));
        ExAssert.Emits("\"text\"", Ex.Value("text"));
        ExAssert.Emits("'c'", Ex.Value('c'));
        ExAssert.Emits("true", Ex.Value(true));
        ExAssert.Emits("42", Ex.Value(42));
        ExAssert.Emits("42L", Ex.Value(42L));
        ExAssert.Emits("1.5D", Ex.Value(1.5));
        ExAssert.Emits("1.5F", Ex.Value(1.5f));
        ExAssert.Emits("1.5M", Ex.Value(1.5m));
    }

    [Fact]
    public void ValueKeepsATypeDeferred()
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Ex.Value(ExAssert.Type("Widget")).WriteOutput(context);

        Assert.Equal("global::TestNamespace.Widget", context.Output());
    }

    [Fact]
    public void TheImplicitConversionsCoverIdentifiersIntegersAndBooleans()
    {
        Ex identifier = "a";
        Ex number = 42;
        Ex flag = true;

        ExAssert.Emits("a", identifier);
        ExAssert.Emits("42", number);
        ExAssert.Emits("true", flag);
    }

    [Fact]
    public void ATypeDefinitionConvertsIntoAnExpressionImplicitly()
    {
        Ex type = TypeDefinition.Get("TestNamespace", "Widget");

        ExAssert.Emits("Widget", type);
    }
}
