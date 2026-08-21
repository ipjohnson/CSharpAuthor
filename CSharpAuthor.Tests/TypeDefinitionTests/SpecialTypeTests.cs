using System;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

public class SpecialTypeTests
{
    private static string Write(ITypeDefinition type)
    {
        var builder = new System.Text.StringBuilder();

        type.WriteTypeName(builder);

        return builder.ToString();
    }

    /// <summary>
    /// The keyword table used to omit these, so they reached generated output as
    /// <c>Single</c>, <c>Char</c> and <c>SByte</c>.
    /// </summary>
    [Theory]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(char), "char")]
    [InlineData(typeof(sbyte), "sbyte")]
    [InlineData(typeof(void), "void")]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(uint), "uint")]
    [InlineData(typeof(short), "short")]
    [InlineData(typeof(ushort), "ushort")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(ulong), "ulong")]
    [InlineData(typeof(byte), "byte")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(decimal), "decimal")]
    [InlineData(typeof(bool), "bool")]
    [InlineData(typeof(string), "string")]
    [InlineData(typeof(object), "object")]
    public void FrameworkTypesRenderAsKeywords(Type type, string expected)
    {
        Assert.Equal(expected, Write(TypeDefinition.Get(type)));
    }

    /// <summary>
    /// The keywords in the table are all C# 1, so they are safe without a target version.
    /// <c>nint</c> and <c>nuint</c> are C# 9 and stay out until one reaches rendering - emitting
    /// them now would break a C# 8 consumer with no way to opt out.
    /// </summary>
    [Theory]
    [InlineData(typeof(IntPtr), "IntPtr")]
    [InlineData(typeof(UIntPtr), "UIntPtr")]
    public void NativeIntegersKeepTheirFrameworkNames(Type type, string expected)
    {
        Assert.Equal(expected, Write(TypeDefinition.Get(type)));
    }

    /// <summary>
    /// The table used to be private and keyed on <see cref="Type"/>, which a source generator does
    /// not have.
    /// </summary>
    [Fact]
    public void TableIsReachableByNamespaceAndName()
    {
        Assert.Equal("int", SpecialTypes.GetKeyword("System", "Int32"));
        Assert.Equal("float", SpecialTypes.GetKeyword("System", "Single"));
        Assert.Null(SpecialTypes.GetKeyword("System", "Guid"));
        Assert.Null(SpecialTypes.GetKeyword("Sample", "Widget"));

        Assert.Equal("int", Write(SpecialTypes.Get("System", "Int32")!));
    }

    /// <summary>
    /// A keyword needs no qualifying and no using, in any output mode.
    /// </summary>
    [Fact]
    public void KeywordsContributeNoImports()
    {
        var context = new OutputContext();

        context.Write(TypeDefinition.Get(typeof(int)));
        context.GenerateUsingStatements();

        Assert.Equal("int", context.Output());
    }

    [Fact]
    public void NullableValueTypesUseTheShorthand()
    {
        Assert.Equal("int?", Write(TypeDefinition.Get(typeof(int?))));
        Assert.Equal("DateTime?", Write(TypeDefinition.Get(typeof(DateTime?))));
    }

    [Fact]
    public void NullableValueTypeArraysCompose()
    {
        Assert.Equal("int?[]", Write(TypeDefinition.Get(typeof(int?[]))));
    }
}
