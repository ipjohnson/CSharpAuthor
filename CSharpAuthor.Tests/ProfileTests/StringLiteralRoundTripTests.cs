using System.Linq;
using CSharpAuthor.Profiles;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// Every literal this library writes, read back by the compiler that will read it.
/// </summary>
/// <remarks>
/// The oracle is Roslyn's own lexer: emit the value, parse the text, ask for the value back. That
/// checks the thing that matters - that the string in the generated file is the string that was
/// asked for - rather than that the escaping matches what a test author expected it to look like.
/// </remarks>
public class StringLiteralRoundTripTests
{
    public static TheoryData<string> Corpus =>
        new TheoryData<string>
        {
            "",
            "plain",
            "he said \"hi\"",
            "ends with a quote\"",
            "\"starts with a quote",
            "\"\"\"three quotes\"\"\"",
            "back\\slash",
            "path\\to\\file",
            "line\nbreak",
            "carriage\r\nreturn",
            "tab\there",
            "null\0char",
            "bell\a and \bbackspace",
            "escape\u001bcharacter",
            "vertical\vtab and form\ffeed",
            "delete\u007fcharacter",
            "line\u2028separator",
            "paragraph\u2029separator",
            "emoji 😀 pair",
            "lone high \ud83d surrogate",
            "lone low \ude00 surrogate",
            "éèê accents",
            "braces {0} and {{1}}",
            "$interpolation-looking",
            "@verbatim-looking",
            "// not a comment",
            "/* not a comment either */"
        };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void AnEscapedLiteralReadsBackAsItself(string value)
    {
        AssertRoundTrips(value, StringLiteralStatement.Quote(value));
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void WhateverTheWriterChoosesReadsBackAsItself(string value)
    {
        // With raw literals preferred the writer picks between two forms per value. Both have to
        // give the value back, and the rule that decides between them - CanBeWrittenRaw - is what
        // this is really checking.
        var emitted = ProfileEmitter.Emit(
            new StringLiteralStatement(value),
            EmitProfile.Default.With(p => p.PreferRawStrings = true)).Code;

        AssertRoundTrips(value, emitted);
    }

    [Fact]
    public void ARawLiteralIsChosenWhereItIsWorthIt()
    {
        // Not just "the output is correct" - that the preference is honoured when it can be.
        var emitted = ProfileEmitter.Emit(
            new StringLiteralStatement("he said \"hi\" loudly"),
            EmitProfile.Default.With(p => p.PreferRawStrings = true)).Code;

        Assert.StartsWith("\"\"\"", emitted);
        AssertRoundTrips("he said \"hi\" loudly", emitted);
    }

    [Fact]
    public void AMultiLineRawLiteralGivesBackTheLinesItWasGiven()
    {
        const string value = "first\nsecond\nthird";

        var emitted = ProfileEmitter.Emit(
            new StringLiteralStatement(value),
            EmitProfile.Default.With(p => p.PreferRawStrings = true)).Code;

        Assert.Contains("\n", emitted);
        AssertRoundTrips(value, emitted);
    }

    [Fact]
    public void TheEscapedFormIsUsedWhereARawOneWouldChangeTheValue()
    {
        // A single-line raw literal whose content touches a quote at either end cannot be fenced.
        // The padding trick that looks like it works pads the content.
        var profile = EmitProfile.Default.With(p => p.PreferRawStrings = true);

        foreach (var value in new[] { "ends with a quote\"", "\"starts with a quote" })
        {
            var emitted = ProfileEmitter.Emit(new StringLiteralStatement(value), profile).Code;

            Assert.DoesNotContain("\"\"\"", emitted);
            AssertRoundTrips(value, emitted);
        }
    }

    private static void AssertRoundTrips(string value, string literalText)
    {
        var expression = SyntaxFactory.ParseExpression(literalText);

        var errors = expression.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Id + ": " + d.GetMessage())
            .ToList();

        Assert.True(errors.Count == 0, literalText + " does not parse: " + string.Join("; ", errors));

        var literal = Assert.IsType<LiteralExpressionSyntax>(expression);

        Assert.Equal(value, literal.Token.ValueText);
    }
}
