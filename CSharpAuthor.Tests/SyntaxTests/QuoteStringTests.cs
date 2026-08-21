using Xunit;

namespace CSharpAuthor.Tests.SyntaxTests;

public class QuoteStringTests
{
    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("C:\\path", "\"C:\\\\path\"")]
    [InlineData("line\nbreak", "\"line\\nbreak\"")]
    [InlineData("carriage\rreturn", "\"carriage\\rreturn\"")]
    [InlineData("tab\there", "\"tab\\there\"")]
    [InlineData("null\0char", "\"null\\0char\"")]
    [InlineData("", "\"\"")]
    [InlineData(null, "\"\"")]
    public void EscapesWhatWouldCloseTheLiteral(string? value, string expected)
    {
        Assert.Equal(expected, SyntaxHelpers.QuoteString(value));
    }

    /// <summary>
    /// The C# lexer treats these as line terminators, so they end a literal just as <c>\n</c> does
    /// even though they are neither control characters nor quotes.
    /// </summary>
    [Theory]
    [InlineData('\u0085', "\"\\u0085\"")]
    [InlineData('\u2028', "\"\\u2028\"")]
    [InlineData('\u2029', "\"\\u2029\"")]
    [InlineData('\u0001', "\"\\u0001\"")]
    public void EscapesCharactersWithNoShortForm(char value, string expected)
    {
        Assert.Equal(expected, SyntaxHelpers.QuoteString(value.ToString()));
    }

    [Fact]
    public void LeavesOrdinaryUnicodeAlone()
    {
        Assert.Equal("\"café ☕\"", SyntaxHelpers.QuoteString("café ☕"));
    }

    /// <summary>
    /// The <c>{argN}</c> substitution builds a literal of its own, and used to do it with quoting
    /// inlined rather than by calling <see cref="SyntaxHelpers.QuoteString"/> - so it escaped
    /// nothing.
    /// </summary>
    [Fact]
    public void AddCodeSubstitutionEscapesToo()
    {
        var method = new MethodDefinition("Test");

        method.AddCode("var name = {arg1};", "say \"hi\"");

        var context = new OutputContext();

        method.WriteOutput(context);

        AssertEqual.ContainsWithoutNewLine("var name = \"say \\\"hi\\\"\";", context.Output());
    }
}
