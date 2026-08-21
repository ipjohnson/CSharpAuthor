using System.Collections.Generic;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

public class ArrayTypeTests
{
    private static string Write(ITypeDefinition type, TypeOutputMode mode = TypeOutputMode.ShortName)
    {
        var builder = new System.Text.StringBuilder();

        type.WriteTypeName(builder, mode);

        return builder.ToString();
    }

    [Fact]
    public void SingleDimension()
    {
        Assert.Equal("string[]", Write(TypeDefinition.Get(typeof(string)).MakeArray()));
    }

    /// <summary>
    /// Array-ness was a single bool carried by the element, so the second call returned a copy
    /// that was already an array and the rank was silently dropped.
    /// </summary>
    [Fact]
    public void JaggedArrayNests()
    {
        Assert.Equal("string[][]", Write(TypeDefinition.Get(typeof(string)).MakeArray().MakeArray()));
    }

    [Fact]
    public void MultidimensionalArrayHasRank()
    {
        Assert.Equal("int[,]", Write(TypeDefinition.Get(typeof(int)).MakeArray(2)));
        Assert.Equal("int[,,]", Write(TypeDefinition.Get(typeof(int)).MakeArray(3)));
    }

    [Theory]
    [InlineData(typeof(int[]), "int[]")]
    [InlineData(typeof(int[][]), "int[][]")]
    [InlineData(typeof(int[,]), "int[,]")]
    [InlineData(typeof(int[,,]), "int[,,]")]
    [InlineData(typeof(int[][,]), "int[,][]")]
    [InlineData(typeof(string[]), "string[]")]
    public void ReflectedArraysRoundTrip(System.Type type, string expected)
    {
        Assert.Equal(expected, Write(TypeDefinition.Get(type)));
    }

    [Fact]
    public void ArrayOfGenericKeepsTheElementsNamespaces()
    {
        var listOfString = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "System.Collections.Generic",
            "List",
            new List<ITypeDefinition> { TypeDefinition.Get(typeof(string)) });

        var context = new OutputContext();

        context.Write(listOfString.MakeArray());
        context.GenerateUsingStatements();

        Assert.Equal("using System.Collections.Generic;\n\nList<string>[]", context.Output());
    }

    [Fact]
    public void ArrayQualifiesItsElementInGlobalMode()
    {
        var type = TypeDefinition.Get("Sample.Models", "Widget").MakeArray().MakeArray();

        Assert.Equal("global::Sample.Models.Widget[][]", Write(type, TypeOutputMode.Global));
    }

    [Fact]
    public void NullableArrayIsDistinctFromArrayOfNullable()
    {
        var element = TypeDefinition.Get("Sample", "Widget");

        Assert.Equal("Widget[]?", Write(element.MakeArray().MakeNullable()));
        Assert.Equal("Widget?[]", Write(element.MakeNullable().MakeArray()));
    }

    [Fact]
    public void ElementTypeUnwrapsOneLevel()
    {
        var jagged = TypeDefinition.Get(typeof(int)).MakeArray().MakeArray();

        Assert.Equal("int[]", Write(jagged.GetElementType()!));
        Assert.Null(TypeDefinition.Get(typeof(int)).GetElementType());
    }

    [Fact]
    public void ArraysOfDifferentRankAreNotEqual()
    {
        var element = TypeDefinition.Get(typeof(int));

        Assert.Equal(element.MakeArray(), element.MakeArray());
        Assert.NotEqual(element.MakeArray(), element.MakeArray(2));
        Assert.NotEqual(element.MakeArray(), element.MakeArray().MakeArray());
    }
}
