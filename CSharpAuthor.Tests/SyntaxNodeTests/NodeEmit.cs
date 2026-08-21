using CSharpAuthor.Syntax;

namespace CSharpAuthor.Tests.SyntaxNodeTests;

/// <summary>
/// Shared plumbing for the grammar-node tests. Each test builds a tree, serialises it, and
/// asserts the exact text - because the whole point of the spacing policy is that the exact
/// text is predictable.
/// </summary>
internal static class NodeEmit
{
    /// <summary>Serialise a node with the default options.</summary>
    public static string Emit(ISyntax node)
    {
        var context = new OutputContext();

        node.WriteOutput(context);

        return context.Output().Replace("\r\n", "\n");
    }

    /// <summary>A type reference that carries a namespace, so deferral is exercised.</summary>
    public static TypeRef Type(string ns, string name) =>
        TypeRef.Of(new TypeDefinition(TypeDefinitionEnum.ClassDefinition, ns, name, false));

    /// <summary>A bare identifier expression.</summary>
    public static IdentifierName Id(string name) => new(name);

    /// <summary>An expression statement: <c>expression;</c></summary>
    public static ExpressionStatement Statement(IExpression expression) => new(expression);

    /// <summary>An invocation with the given arguments.</summary>
    public static InvocationExpression Call(IExpression target, params IExpression[] arguments)
    {
        var list = new ArgumentList();

        foreach (var argument in arguments)
        {
            list.Arguments.Add(new Argument(argument));
        }

        return new InvocationExpression(target, list);
    }
}
