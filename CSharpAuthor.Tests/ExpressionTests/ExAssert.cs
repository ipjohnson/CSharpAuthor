using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.ExpressionTests;

/// <summary>
/// Shared helpers for the expression tests. Every assertion here is on the exact emitted
/// string: the property being defended is that the text re-parses to the tree it came
/// from, and a wrong bracket is invisible in any looser check.
/// </summary>
public static class ExAssert
{
    /// <summary>Asserts the exact text an expression emits.</summary>
    public static void Emits(string expected, Ex expression, OutputContextOptions? options = null)
    {
        var context = new OutputContext(options);

        expression.WriteOutput(context);

        Assert.Equal(expected, context.Output());
    }

    /// <summary>Asserts the exact text a component emits.</summary>
    public static void Emits(string expected, IOutputComponent component, OutputContextOptions? options = null)
    {
        var context = new OutputContext(options);

        component.WriteOutput(context);

        Assert.Equal(expected, context.Output());
    }

    /// <summary>A type in the test namespace.</summary>
    public static ITypeDefinition Type(string name) => TypeDefinition.Get("TestNamespace", name);

    /// <summary>A shorthand for an identifier.</summary>
    public static Ex Id(string name) => Ex.Id(name);
}
