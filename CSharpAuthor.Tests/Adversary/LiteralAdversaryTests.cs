using System;
using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Values on their way to becoming literals. Every one of these arrives from outside - a schema
/// default, an attribute argument read off a symbol, a path - so none of them can be assumed to be
/// free of the characters that end a literal.
/// </summary>
public class LiteralAdversaryTests
{
    private static string FieldOf(Type type, string name, IOutputComponent value)
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(type, name).InitializeValue = value;

        return Emit.Component(classDefinition);
    }

    [Fact(Skip = "ADVERSARY GAP (§7 'String literals unescaped'): QuoteString concatenates quotes, so a value containing \" ends the literal early")]
    public void StringContainingAQuote()
    {
        RoslynAssert.Compiles(
            FieldOf(typeof(string), "f", CodeOutputComponent.Get(QuoteString("he said \"hi\""))));
    }

    /// <summary>
    /// A backslash is the case that compiles. <c>"C:\temp\new"</c> parses, because <c>\t</c> and
    /// <c>\n</c> are valid escapes - it just silently becomes a tab and a newline, and the path is
    /// wrong at run time rather than at build time.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: a backslash is not escaped, so \"C:\\temp\\new\" compiles into a string containing a tab and a newline - silently the wrong value, with no diagnostic anywhere")]
    public void StringContainingBackslashes()
    {
        var quoted = QuoteString(@"C:\temp\new");

        Assert.Equal(@"""C:\\temp\\new""", quoted);
    }

    [Fact(Skip = "ADVERSARY GAP: a newline in a value is written literally into a non-verbatim literal - CS1010, newline in constant")]
    public void StringContainingANewline()
    {
        RoslynAssert.Compiles(
            FieldOf(typeof(string), "f", CodeOutputComponent.Get(QuoteString("line1\nline2"))));
    }

    [Fact(Skip = "ADVERSARY GAP: a NUL is written as a raw U+0000 byte into the source rather than \\0")]
    public void StringContainingNul()
    {
        Assert.Equal("\"a\\0b\"", QuoteString("a\0b"));
    }

    [Fact(Skip = "ADVERSARY GAP: a control character is written raw - an ESC lands in the source as U+001B, which no editor or diff will show")]
    public void StringContainingAnEscapeCharacter()
    {
        Assert.Equal("\"esc \\u001b[0m\"", QuoteString("esc \u001b[0m"));
    }

    /// <summary>
    /// A surrogate pair survives, because the source file is UTF-8 and an emoji is an ordinary
    /// literal character. Unskipped: an escaping fix must not mangle it into two lone surrogates.
    /// </summary>
    [Fact]
    public void StringContainingASurrogatePair()
    {
        RoslynAssert.Compiles(
            FieldOf(typeof(string), "f", CodeOutputComponent.Get(QuoteString("emoji \U0001F600 done"))));
    }

    [Fact(Skip = "ADVERSARY GAP: AddCode quotes a string argument by concatenation, so {arg1} with an embedded quote produces var x = \"he said \"hi\"\";")]
    public void AddCodeStringArgumentContainingAQuote()
    {
        var method = new MethodDefinition("M");

        method.AddCode("var x = {arg1};", "he said \"hi\"");

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact(Skip = "ADVERSARY GAP: CodeOutputComponent.Get(string[]) quotes each element by concatenation - new string[] { \"a\"b\", \"c\\d\" }")]
    public void StringArrayElementsContainingQuotes()
    {
        RoslynAssert.ExpressionCompiles(
            Emit.Component(CodeOutputComponent.Get(new[] { "a\"b", "c\\d" })));
    }

    [Fact(Skip = "ADVERSARY GAP (§7 'char literal'): emits = a, which is CS0103 - an unknown identifier, not a character")]
    public void CharLiteral()
    {
        RoslynAssert.Compiles(FieldOf(typeof(char), "f", CodeOutputComponent.Get('a')));
    }

    [Fact(Skip = "ADVERSARY GAP: a char that is itself a quote emits = ', which cannot become a literal at all")]
    public void CharLiteralThatIsAQuote()
    {
        RoslynAssert.Compiles(FieldOf(typeof(char), "f", CodeOutputComponent.Get('\'')));
    }

    [Fact(Skip = "ADVERSARY GAP (§7 'float literal'): emits = 1.5, which is a double - CS0664 without the f suffix")]
    public void FloatLiteral()
    {
        RoslynAssert.Compiles(FieldOf(typeof(float), "f", CodeOutputComponent.Get(1.5f)));
    }

    /// <summary>
    /// The same defect as <c>float</c>, at a type §7 does not name. <c>decimal</c> has no implicit
    /// conversion from <c>double</c> at all, so this is CS0664 too.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: decimal needs an m suffix and does not get one - emits = 1.5, CS0664")]
    public void DecimalLiteral()
    {
        RoslynAssert.Compiles(FieldOf(typeof(decimal), "f", CodeOutputComponent.Get(1.5m)));
    }

    /// <summary>
    /// The worst literal in the set. <c>double.PositiveInfinity.ToString()</c> is <c>"∞"</c> on
    /// .NET Core, and U+221E is a math symbol - not a letter - so it cannot even be an identifier.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: double.PositiveInfinity emits the character ∞ into the source (CS1056) and double.NaN emits the bare word NaN (CS0103) - neither is a literal C# has")]
    public void NonFiniteDoubleLiterals()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(double), "n").InitializeValue =
            CodeOutputComponent.Get(double.NaN);
        classDefinition.AddField(typeof(double), "p").InitializeValue =
            CodeOutputComponent.Get(double.PositiveInfinity);
        classDefinition.AddField(typeof(double), "m").InitializeValue =
            CodeOutputComponent.Get(double.NegativeInfinity);

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    /// <summary>
    /// <c>CodeOutputComponent.Get(null)</c> returns an empty component, so an initializer written
    /// from a null value emits <c>= ;</c>.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: CodeOutputComponent.Get(null) emits the empty string, so a null value becomes 'private string f = ;' - CS1525")]
    public void NullValueBecomesTheNullLiteral()
    {
        RoslynAssert.Compiles(FieldOf(typeof(string), "f", CodeOutputComponent.Get(null)));
    }

    /// <summary>
    /// Two entry points disagree about what a <see cref="string"/> means.
    /// <c>AddCode("{arg1}", "hello")</c> quotes it; <c>CodeOutputComponent.Get("hello")</c> writes
    /// it as code. A caller that reaches for the wrong one gets an identifier reference where it
    /// wanted a literal - and where an identifier of that name happens to exist, it compiles.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: CodeOutputComponent.Get(\"hello\") emits hello unquoted while AddCode's {argN} emits \"hello\" - one of the two is silently wrong for any given caller")]
    public void StringValueIsQuotedConsistently()
    {
        Assert.Equal(
            "\"hello\"",
            Emit.Component(CodeOutputComponent.Get("hello")));
    }

    /// <summary>
    /// The values that do reach output correctly. Unskipped, because a literal fix touches all of
    /// them and these are the ones consumers already depend on.
    /// </summary>
    [Fact]
    public void BoolIntAndDoubleLiteralsAreCorrect()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(bool), "b").InitializeValue = CodeOutputComponent.Get(true);
        classDefinition.AddField(typeof(int), "i").InitializeValue = CodeOutputComponent.Get(42);
        classDefinition.AddField(typeof(double), "d").InitializeValue = CodeOutputComponent.Get(1.5d);
        classDefinition.AddField(typeof(long), "l").InitializeValue =
            CodeOutputComponent.Get(9999999999L);

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void PlainStringLiteralIsCorrect()
    {
        RoslynAssert.Compiles(
            FieldOf(typeof(string), "f", CodeOutputComponent.Get(QuoteString("hello"))));
    }

    /// <summary>
    /// A raw string literal ending in a quote needs a longer fence than three. There is no raw
    /// string API to get this wrong with yet, which is worth recording before one is added - CS8998
    /// is the error it produces, and it was found once already.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: no API - there is no raw-string literal emitter, so the fence-length rule (content ending in a quote needs a longer fence, else CS8998) has nowhere to live yet")]
    public void RawStringLiteralFenceLength()
    {
        Assert.True(false, "no API");
    }
}
