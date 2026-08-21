using System.Collections.Generic;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// A nested type used to be indistinguishable from a top-level one, so it wrote <c>Inner</c> alone
/// and bound to whatever <c>Inner</c> was in scope where it was used.
/// </summary>
public class NestedTypeTests
{
    private class Outer
    {
        internal class Inner
        {
        }
    }

    private static string Write(ITypeDefinition type, TypeOutputMode mode = TypeOutputMode.ShortName)
    {
        var builder = new System.Text.StringBuilder();

        type.WriteTypeName(builder, mode);

        return builder.ToString();
    }

    [Fact]
    public void NestedTypeCarriesItsContainer()
    {
        var outer = TypeDefinition.Get("Sample.Models", "Outer");
        var inner = TypeDefinition.GetNested(outer, "Inner");

        Assert.Equal("Outer.Inner", Write(inner));
    }

    [Fact]
    public void ContainerIsQualifiedByTheOutputMode()
    {
        var outer = TypeDefinition.Get("Sample.Models", "Outer");
        var inner = TypeDefinition.GetNested(outer, "Inner");

        Assert.Equal("global::Sample.Models.Outer.Inner", Write(inner, TypeOutputMode.Global));
        Assert.Equal("Sample.Models.Outer.Inner", Write(inner, TypeOutputMode.FullName));
    }

    /// <summary>
    /// The container renders its own type arguments, which a dotted name could not have carried.
    /// </summary>
    [Fact]
    public void GenericContainerWritesItsTypeArguments()
    {
        var outer = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "Sample.Models",
            "Outer",
            new List<ITypeDefinition> { TypeDefinition.Get(typeof(string)) });

        var inner = TypeDefinition.GetNested(outer, "Inner");

        Assert.Equal("Outer<string>.Inner", Write(inner));
    }

    /// <summary>
    /// Every level, not just the innermost - <c>Outer</c> is itself nested in this test class.
    /// </summary>
    [Fact]
    public void ReflectedNestedTypeKeepsItsWholeContainerChain()
    {
        Assert.Equal("NestedTypeTests.Outer.Inner", Write(TypeDefinition.Get(typeof(Outer.Inner))));

        Assert.Equal(
            "global::CSharpAuthor.Tests.TypeDefinitionTests.NestedTypeTests.Outer.Inner",
            Write(TypeDefinition.Get(typeof(Outer.Inner)), TypeOutputMode.Global));
    }

    [Fact]
    public void NestedTypeImportsTheContainersNamespace()
    {
        var outer = TypeDefinition.Get("Sample.Models", "Outer");
        var context = new OutputContext();

        context.Write(TypeDefinition.GetNested(outer, "Inner"));
        context.GenerateUsingStatements();

        Assert.Equal("using Sample.Models;\n\nOuter.Inner", context.Output());
    }

    [Fact]
    public void SameNameInDifferentContainersAreDifferentTypes()
    {
        var first = TypeDefinition.GetNested(TypeDefinition.Get("Sample", "First"), "Inner");
        var second = TypeDefinition.GetNested(TypeDefinition.Get("Sample", "Second"), "Inner");

        Assert.NotEqual(first, second);
        Assert.NotEqual(0, first.CompareTo(second));
    }

    [Fact]
    public void ArrayOfNestedTypeKeepsTheContainer()
    {
        var inner = TypeDefinition.GetNested(TypeDefinition.Get("Sample", "Outer"), "Inner");

        Assert.Equal("Outer.Inner[]", Write(inner.MakeArray()));
    }
}
