using Xunit;

namespace CSharpAuthor.Tests.NamespaceTests;

/// <summary>
/// <see cref="NamespaceDefinition.AddNamespace"/> returns the namespace that is already there.
/// </summary>
/// <remarks>
/// A file is usually assembled by several independent pieces of code, and more than one of them will
/// want the same namespace. Appending a second definition produced a file with two
/// <c>namespace Models { }</c> blocks - legal, and not what anyone meant.
/// </remarks>
public class NamespaceReuseTests
{
    [Fact]
    public void AddNamespaceReturnsTheExistingDefinition()
    {
        var namespaceDefinition = new NamespaceDefinition("Base");

        var first = namespaceDefinition.AddNamespace("Models");
        var second = namespaceDefinition.AddNamespace("Models");

        Assert.Same(first, second);
    }

    [Fact]
    public void TypesAddedThroughEitherCallLandInOneBlock()
    {
        var namespaceDefinition = new NamespaceDefinition("Base");

        namespaceDefinition.AddNamespace("Models").AddInterface("IFirst");
        namespaceDefinition.AddNamespace("Models").AddInterface("ISecond");

        var context = new OutputContext();

        namespaceDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(OneBlockOutput, context.Output());
    }

    private const string OneBlockOutput = @"namespace Base
{
    namespace Models
    {
        public interface IFirst
        {
        }

        public interface ISecond
        {
        }
    }
}
";

    [Fact]
    public void DifferentNamesStillGetTheirOwnBlock()
    {
        var namespaceDefinition = new NamespaceDefinition("Base");

        var models = namespaceDefinition.AddNamespace("Models");
        var services = namespaceDefinition.AddNamespace("Services");

        Assert.NotSame(models, services);
    }

    /// <summary>
    /// The match is by name, so nesting the same name at two depths is two namespaces.
    /// </summary>
    [Fact]
    public void ReuseDoesNotReachIntoNestedNamespaces()
    {
        var namespaceDefinition = new NamespaceDefinition("Base");

        var outer = namespaceDefinition.AddNamespace("Models");
        var inner = outer.AddNamespace("Models");

        Assert.NotSame(outer, inner);
    }

    [Fact]
    public void TheNamespaceItDeclaresIsReadable()
    {
        Assert.Equal("Models", new NamespaceDefinition("Models").Namespace);
    }

    /// <summary>
    /// A caller that genuinely wants a second block for the same name can still build one and add it
    /// as a component; reuse is what <see cref="NamespaceDefinition.AddNamespace"/> does, not a rule
    /// the type enforces.
    /// </summary>
    [Fact]
    public void AddComponentStillAppendsADistinctBlock()
    {
        var namespaceDefinition = new NamespaceDefinition("Base");

        namespaceDefinition.AddNamespace("Models");
        namespaceDefinition.AddComponent(new NamespaceDefinition("Models"));

        var context = new OutputContext();

        namespaceDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(TwoBlockOutput, context.Output());
    }

    private const string TwoBlockOutput = @"namespace Base
{
    namespace Models
    {
    }

    namespace Models
    {
    }
}
";
}
