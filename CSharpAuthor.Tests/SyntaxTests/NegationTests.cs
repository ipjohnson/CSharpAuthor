using Xunit;

namespace CSharpAuthor.Tests.SyntaxTests;

public class NegationTests
{
    [Fact]
    public void NotWritesThePrefixOperator()
    {
        Assert.Equal("!flag", Write(SyntaxHelpers.Not(CodeOutputComponent.Get("flag"))));
    }

    /// <summary>
    /// <c>Bang</c> is the null-forgiving operator. Both forms are legal in a condition under
    /// <c>#nullable enable</c>, so nothing but this test distinguishes them.
    /// </summary>
    [Fact]
    public void BangWritesThePostfixOperator()
    {
        Assert.Equal("flag!", Write(SyntaxHelpers.Bang(CodeOutputComponent.Get("flag"))));
    }

    [Fact]
    public void NegatingALogicStatementKeepsItsParentheses()
    {
        var statement = SyntaxHelpers.Not(
            SyntaxHelpers.And(CodeOutputComponent.Get("a"), CodeOutputComponent.Get("b")));

        Assert.Equal("!(a && b)", Write(statement));
    }

    private static string Write(IOutputComponent component)
    {
        var context = new OutputContext();

        component.WriteOutput(context);

        return context.Output();
    }
}
