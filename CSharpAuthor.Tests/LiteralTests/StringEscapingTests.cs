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

    [Fact]
    public void AddCodeArgumentSubstitutionEscapesTheString()
    {
        var method = new MethodDefinition("Test");

        method.AddCode("Log({arg1});", "he said \"hi\"");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("Log(\"he said \\\"hi\\\"\");", outputContext.Output());
    }
}
