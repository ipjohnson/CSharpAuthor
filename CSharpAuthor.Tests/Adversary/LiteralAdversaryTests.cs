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

    [Fact]
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
    [Fact]
    public void StringContainingBackslashes()
    {
        var quoted = QuoteString(@"C:\temp\new");

        Assert.Equal(@"""C:\\temp\\new""", quoted);
    }

    [Fact]
    public void StringContainingANewline()
    {
        RoslynAssert.Compiles(
            FieldOf(typeof(string), "f", CodeOutputComponent.Get(QuoteString("line1\nline2"))));
    }

    [Fact]
    public void StringContainingNul()
    {
        Assert.Equal("\"a\\0b\"", QuoteString("a\0b"));
    }

    /// <summary>
    /// A control character is escaped rather than written raw. The placeholder this replaces said an
    /// ESC landed in the source as U+001B; it does not - only the hex digits are upper case.
    /// </summary>
    [Fact]
    public void StringContainingAnEscapeCharacter()
    {
        var quoted = QuoteString("esc \u001b[0m");

        Assert.Equal("\"esc \\u001B[0m\"", quoted);
        RoslynAssert.ExpressionCompiles(quoted);
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

    [Fact]
    public void AddCodeStringArgumentContainingAQuote()
    {
        var method = new MethodDefinition("M");

        method.AddCode("var x = {arg1};", QuoteString("he said \"hi\""));

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void StringArrayElementsContainingQuotes()
    {
        RoslynAssert.ExpressionCompiles(
            Emit.Component(CodeOutputComponent.Get(new[] { "a\"b", "c\\d" })));
    }

    [Fact]
    public void CharLiteral()
    {
        RoslynAssert.Compiles(FieldOf(typeof(char), "f", CodeOutputComponent.Get('a')));
    }

    [Fact]
    public void CharLiteralThatIsAQuote()
    {
        RoslynAssert.Compiles(FieldOf(typeof(char), "f", CodeOutputComponent.Get('\'')));
    }

    [Fact]
    public void FloatLiteral()
    {
        RoslynAssert.Compiles(FieldOf(typeof(float), "f", CodeOutputComponent.Get(1.5f)));
    }

    /// <summary>
    /// The same defect as <c>float</c>, at a type §7 does not name. <c>decimal</c> has no implicit
    /// conversion from <c>double</c> at all, so this is CS0664 too.
    /// </summary>
    [Fact]
    public void DecimalLiteral()
    {
        RoslynAssert.Compiles(FieldOf(typeof(decimal), "f", CodeOutputComponent.Get(1.5m)));
    }

    /// <summary>
    /// The worst literal in the set. <c>double.PositiveInfinity.ToString()</c> is <c>"∞"</c> on
    /// .NET Core, and U+221E is a math symbol - not a letter - so it cannot even be an identifier.
    /// </summary>
    [Fact]
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
    [Fact]
    public void NullValueBecomesTheNullLiteral()
    {
        RoslynAssert.Compiles(FieldOf(typeof(string), "f", CodeOutputComponent.Get(null)));
    }

    /// <summary>
    /// Both entry points agree about what a <see cref="string"/> means: code.
    /// <c>AddCode("{arg1}", "hello")</c> and <c>CodeOutputComponent.Get("hello")</c> each write
    /// <c>hello</c>, and a caller that means a literal asks for one through
    /// <see cref="SyntaxHelpers.QuoteString"/>.
    /// </summary>
    /// <remarks>
    /// This was the section 1 inconsistency, and it was resolved toward code rather than toward
    /// literals because that is the rule the rest of the library already followed - stated
    /// outright in <see cref="LiteralFormatter.Format"/>, and relied on by every call site that
    /// passes a member access or a <c>nameof</c> as a string. <c>{argN}</c> was the single place
    /// that quoted, so it was the single place that changed.
    /// </remarks>
    [Fact]
    public void StringValueMeansCodeConsistently()
    {
        var method = new MethodDefinition("M");

        method.AddCode("var x = {arg1};", "hello");

        Assert.Contains("var x = hello;", Emit.Component(method));
        Assert.Equal("hello", Emit.Component(CodeOutputComponent.Get("hello")));

        Assert.Equal(
            "\"hello\"",
            Emit.Component(CodeOutputComponent.Get(QuoteString("hello"))));
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

}
