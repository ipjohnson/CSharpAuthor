using System;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// A generic definition closed over nothing.
/// </summary>
/// <remarks>
/// <c>Thing&lt;&gt;</c> is only legal inside <c>typeof</c>; in a field, a parameter or a base list
/// it is CS1031. The open form is still reachable - <see cref="GenericTypeDefinition.MakeOpenType"/>
/// builds one argument per parameter with no name, which is what writes <c>Thing&lt;,&gt;</c> - so
/// the two cases are told apart by whether there are arguments at all, not by how they render.
/// </remarks>
public class EmptyGenericArgumentTests
{
    [Fact]
    public void NoArgumentsWritesNoArgumentList()
    {
        var type = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Probe", "Thing", Array.Empty<ITypeDefinition>());

        Assert.Equal("Thing", type.GetShortName());
    }

    [Fact]
    public void TheShapeIsStillCarried()
    {
        var type = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Probe", "Thing", Array.Empty<ITypeDefinition>());

        Assert.Equal("Thing[]", type.MakeArray().GetShortName());
        Assert.Equal("Thing?", type.MakeNullable().GetShortName());
    }

    /// <summary>
    /// The open form keeps its brackets: it has arguments, they are just nameless.
    /// </summary>
    [Fact]
    public void AnOpenTypeStillWritesItsBrackets()
    {
        var closed = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "Probe",
            "Thing",
            new[] { TypeDefinition.Get(typeof(int)), TypeDefinition.Get(typeof(string)) });

        Assert.Equal("Thing<,>", closed.MakeOpenType().GetShortName());
    }

    [Fact]
    public void AClosedTypeIsUnchanged()
    {
        var closed = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "Probe",
            "Thing",
            new[] { TypeDefinition.Get(typeof(int)) });

        Assert.Equal("Thing<int>", closed.GetShortName());
    }
}
