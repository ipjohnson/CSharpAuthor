using Xunit;

namespace CSharpAuthor.Tests.SyntaxTests;

/// <summary>
/// The forms a real generator reached for and had to write as raw strings, because there was no
/// node for them. A raw string carries no type reference, so every one of these escapes also opted
/// its fragment out of import derivation.
/// </summary>
public class ExpressionFormTests
{
    private static string Write(IOutputComponent component)
    {
        var context = new OutputContext();

        component.WriteOutput(context);

        return context.Output();
    }

    [Fact]
    public void MemberAccess()
    {
        Assert.Equal("target.Handle", Write(SyntaxHelpers.MemberAccess(CodeOutputComponent.Get("target"), "Handle")));
    }

    [Fact]
    public void NullTests()
    {
        Assert.Equal("value is null", Write(SyntaxHelpers.IsNull(CodeOutputComponent.Get("value"))));
        Assert.Equal("value is not null", Write(SyntaxHelpers.IsNotNull(CodeOutputComponent.Get("value"))));
    }

    [Fact]
    public void ArgumentModifiers()
    {
        Assert.Equal("ref reader", Write(SyntaxHelpers.Ref(CodeOutputComponent.Get("reader"))));
        Assert.Equal("out result", Write(SyntaxHelpers.Out(CodeOutputComponent.Get("result"))));
        Assert.Equal("in options", Write(SyntaxHelpers.In(CodeOutputComponent.Get("options"))));
    }

    [Fact]
    public void OutVariableDeclaration()
    {
        Assert.Equal("out var value", Write(SyntaxHelpers.OutVar("value")));
        Assert.Equal("out Widget value", Write(SyntaxHelpers.OutVar("value", TypeDefinition.Get("Sample", "Widget"))));
    }

    /// <summary>
    /// The declared type reaches import derivation, which is the whole reason not to write it as a
    /// string.
    /// </summary>
    [Fact]
    public void OutVariableTypeIsImported()
    {
        var context = new OutputContext();

        SyntaxHelpers.OutVar("value", TypeDefinition.Get("Sample.Models", "Widget")).WriteOutput(context);
        context.GenerateUsingStatements();

        Assert.Equal("using Sample.Models;\n\nout Widget value", context.Output());
    }

    [Fact]
    public void NameOf()
    {
        Assert.Equal("nameof(target)", Write(SyntaxHelpers.NameOf(CodeOutputComponent.Get("target"))));
        Assert.Equal("nameof(Widget)", Write(SyntaxHelpers.NameOf(TypeDefinition.Get("Sample", "Widget"))));
    }

    [Fact]
    public void Conditional()
    {
        Assert.Equal(
            "(hasValue ? value : fallback)",
            Write(SyntaxHelpers.Conditional(
                CodeOutputComponent.Get("hasValue"),
                CodeOutputComponent.Get("value"),
                CodeOutputComponent.Get("fallback"))));
    }

    [Fact]
    public void ConditionalDropsItsParenthesesOnRequest()
    {
        var conditional = SyntaxHelpers.Conditional(
            CodeOutputComponent.Get("hasValue"),
            CodeOutputComponent.Get("value"),
            CodeOutputComponent.Get("fallback"));

        conditional.PrintParentheses = false;

        Assert.Equal("hasValue ? value : fallback", Write(conditional));
    }

    /// <summary>
    /// The condition sits inside the operator's own brackets, so it does not add its own.
    /// </summary>
    [Fact]
    public void ConditionalDoesNotDoubleTheConditionsParentheses()
    {
        Assert.Equal(
            "(a && b ? value : fallback)",
            Write(SyntaxHelpers.Conditional(
                SyntaxHelpers.And(CodeOutputComponent.Get("a"), CodeOutputComponent.Get("b")),
                CodeOutputComponent.Get("value"),
                CodeOutputComponent.Get("fallback"))));
    }

    [Fact]
    public void Default()
    {
        Assert.Equal("default", Write(SyntaxHelpers.Default()));
        Assert.Equal("default(Widget)", Write(SyntaxHelpers.Default(TypeDefinition.Get("Sample", "Widget"))));
    }
}
