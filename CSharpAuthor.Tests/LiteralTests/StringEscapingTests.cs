using System.Collections.Generic;
using Xunit;

namespace CSharpAuthor.Tests.LiteralTests;

/// <summary>
/// One test per escape class. Before these, <c>QuoteString</c> concatenated quotes around the raw
/// value, so any content holding a quote, a backslash or a line break produced a file that did not
/// parse - silently, at generation time, on the consumer's machine.
/// </summary>
public class StringEscapingTests
{
    [Fact]
    public void QuoteInsideStringIsEscaped()
    {
        Assert.Equal("\"he said \\\"hi\\\"\"", SyntaxHelpers.QuoteString("he said \"hi\""));
    }

    [Fact]
    public void BackslashIsEscaped()
    {
        Assert.Equal("\"C:\\\\temp\\\\file.txt\"", SyntaxHelpers.QuoteString(@"C:\temp\file.txt"));
    }

    [Fact]
    public void BackslashBeforeQuoteDoesNotEscapeTheQuote()
    {
        // The failure mode worth naming: escaping the quote but not the backslash gives \" where
        // the backslash swallows the escape and the literal runs on.
        Assert.Equal("\"a\\\\\\\"b\"", SyntaxHelpers.QuoteString("a\\\"b"));
    }

    [Fact]
    public void NewLineAndCarriageReturnAreEscaped()
    {
        Assert.Equal("\"line1\\r\\nline2\"", SyntaxHelpers.QuoteString("line1\r\nline2"));
    }

    [Fact]
    public void TabIsEscaped()
    {
        Assert.Equal("\"a\\tb\"", SyntaxHelpers.QuoteString("a\tb"));
    }

    [Fact]
    public void NullCharacterIsEscaped()
    {
        Assert.Equal("\"a\\0b\"", SyntaxHelpers.QuoteString("a\0b"));
    }

    [Fact]
    public void OtherControlCharactersBecomeUnicodeEscapes()
    {
        Assert.Equal("\"a\\u001Bb\"", SyntaxHelpers.QuoteString("a\u001bb"));
        Assert.Equal("\"\\u0001\"", SyntaxHelpers.QuoteString("\u0001"));
    }

    [Fact]
    public void BellBackspaceFormFeedAndVerticalTabAreEscaped()
    {
        Assert.Equal("\"\\a\\b\\f\\v\"", SyntaxHelpers.QuoteString("\a\b\f\v"));
    }

    [Fact]
    public void ValidSurrogatePairIsWrittenThroughAsOneCharacter()
    {
        // U+1F600, which is a high/low surrogate pair in UTF-16. It is one character and stays one.
        const string emoji = "\U0001F600";

        Assert.Equal("\"" + emoji + "\"", SyntaxHelpers.QuoteString(emoji));
    }

    [Fact]
    public void LoneSurrogateBecomesAUnicodeEscape()
    {
        // A high surrogate with nothing after it is not a character. Written through, it produces
        // bytes no compiler will read back.
        Assert.Equal("\"\\uD83D\"", SyntaxHelpers.QuoteString("\ud83d"));
    }

    [Fact]
    public void NonAsciiPrintableCharactersAreLeftAlone()
    {
        Assert.Equal("\"naïve café\"", SyntaxHelpers.QuoteString("naïve café"));
    }

    [Fact]
    public void EmptyStringIsAPairOfQuotes()
    {
        Assert.Equal("\"\"", SyntaxHelpers.QuoteString(""));
    }

    [Fact]
    public void VerbatimStringDoublesItsQuotesAndLeavesTheRest()
    {
        Assert.Equal("@\"C:\\temp\"", LiteralFormatter.QuoteVerbatimString(@"C:\temp"));
        Assert.Equal("@\"he said \"\"hi\"\"\"", LiteralFormatter.QuoteVerbatimString("he said \"hi\""));
    }

    [Fact]
    public void CharacterLiteralEscapesItsOwnQuoteButNotTheStringQuote()
    {
        Assert.Equal("'\\''", LiteralFormatter.QuoteChar('\''));
        Assert.Equal("'\"'", LiteralFormatter.QuoteChar('"'));
        Assert.Equal("'\\\\'", LiteralFormatter.QuoteChar('\\'));
        Assert.Equal("'\\n'", LiteralFormatter.QuoteChar('\n'));
    }

    [Fact]
    public void StringArrayElementsAreEscaped()
    {
        // The overload that treats a sequence of strings as string literals. The params overload of
        // NewArray deliberately treats each string as a code fragment instead.
        var array = CodeOutputComponent.Get(new List<string> { "he said \"hi\"", @"C:\temp" });

        var outputContext = new OutputContext();

        array.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "{ \"he said \\\"hi\\\"\", \"C:\\\\temp\" }",
            outputContext.Output());
    }

    /// <summary>
    /// A quoted string routed through <c>{argN}</c> is escaped. The escaping is
    /// <see cref="SyntaxHelpers.QuoteString"/>'s job, not the substitution's - the substitution
    /// only has to leave the result alone.
    /// </summary>
    [Fact]
    public void AddCodeArgumentSubstitutionEscapesAQuotedString()
    {
        var method = new MethodDefinition("Test");

        method.AddCode("Log({arg1});", SyntaxHelpers.QuoteString("he said \"hi\""));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("Log(\"he said \\\"hi\\\"\");", outputContext.Output());
    }

    /// <summary>
    /// The other half of the contract: a bare string is a fragment of code, so nothing is quoted
    /// and nothing is escaped. Passing text that is not valid C# produces text that is not valid
    /// C#, which is the caller's decision to make.
    /// </summary>
    [Fact]
    public void AddCodeArgumentSubstitutionLeavesABareStringAlone()
    {
        var method = new MethodDefinition("Test");

        method.AddCode("Log({arg1});", "Greeting.Value");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("Log(Greeting.Value);", outputContext.Output());
    }

    /// <summary>
    /// U+2028 and U+2029 are escaped, in both of the library's literal writers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They are <c>new_line</c> characters in the C# grammar, so a regular literal holding one ends
    /// early: CS1010, unterminated string literal. They are also Unicode Zl/Zp rather than Cc, so
    /// <c>char.IsControl</c> does not report them - which is how both writers came to pass them
    /// through while handling every other invisible character.
    /// </para>
    /// <para>
    /// Reachable from any generator that embeds scraped or user-supplied text; U+2028 is what a
    /// JSON or JavaScript source hands over for a line break.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData('\u2028', "\\u2028")]
    [InlineData('\u2029', "\\u2029")]
    public void UnicodeLineSeparatorsAreEscaped(char separator, string expected)
    {
        var value = "a" + separator + "b";

        // LiteralFormatter, via SyntaxHelpers.
        Assert.Equal("\"a" + expected + "b\"", SyntaxHelpers.QuoteString(value));

        // CSharpText, which is the one Ex.Str and Ex.Char go through. StringLiteralStatement.Quote
        // handled these correctly all along; these two did not, and the rule now lives in one place.
        Assert.Equal("\"a" + expected + "b\"", Expressions.CSharpText.StringLiteral(value));
        Assert.Equal("'" + expected + "'", Expressions.CSharpText.CharLiteral(separator));
    }
}
