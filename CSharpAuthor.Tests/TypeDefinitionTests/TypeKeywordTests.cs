using System;
using System.Text;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// Every predefined type has a C# keyword, and the keyword is what the compiler and every style
/// guide expect to see. Reflection names the same types <c>Single</c>, <c>Char</c>, <c>SByte</c> and
/// <c>IntPtr</c>; emitting those is legal C# but reads as a different type and drags in a namespace
/// import that the keyword does not need.
/// </summary>
public class TypeKeywordTests
{
    public static TheoryData<Type, string> PredefinedTypes => new()
    {
        { typeof(object), "object" },
        { typeof(string), "string" },
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(sbyte), "sbyte" },
        { typeof(char), "char" },
        { typeof(short), "short" },
        { typeof(ushort), "ushort" },
        { typeof(int), "int" },
        { typeof(uint), "uint" },
        { typeof(long), "long" },
        { typeof(ulong), "ulong" },
        { typeof(float), "float" },
        { typeof(double), "double" },
        { typeof(decimal), "decimal" },
        { typeof(IntPtr), "nint" },
        { typeof(UIntPtr), "nuint" },
    };

    [Theory]
    [MemberData(nameof(PredefinedTypes))]
    public void WrittenAsTheKeyword(Type type, string expected)
    {
        var builder = new StringBuilder();

        TypeDefinition.Get(type).WriteTypeName(builder);

        Assert.Equal(expected, builder.ToString());
    }

    /// <summary>
    /// A keyword names no namespace, so it must read the same qualified as it does short - and it must
    /// not cause a <c>using System;</c> that the file has no other reason to hold.
    /// </summary>
    [Theory]
    [MemberData(nameof(PredefinedTypes))]
    public void TheKeywordIsTheSameInEveryOutputMode(Type type, string expected)
    {
        foreach (var mode in new[] { TypeOutputMode.ShortName, TypeOutputMode.FullName, TypeOutputMode.Global })
        {
            var builder = new StringBuilder();

            TypeDefinition.Get(type).WriteTypeName(builder, mode);

            Assert.Equal(expected, builder.ToString());
        }

        Assert.Empty(TypeDefinition.Get(type).Namespace);
    }

    [Fact]
    public void FloatIsNotSingle()
    {
        Assert.Equal("float", TypeDefinition.Get(typeof(float)).GetShortName());
    }

    [Fact]
    public void CharIsNotChar()
    {
        Assert.Equal("char", TypeDefinition.Get(typeof(char)).GetShortName());
    }

    [Fact]
    public void SByteIsNotSByte()
    {
        Assert.Equal("sbyte", TypeDefinition.Get(typeof(sbyte)).GetShortName());
    }

    [Fact]
    public void NintIsNotIntPtr()
    {
        Assert.Equal("nint", TypeDefinition.Get(typeof(IntPtr)).GetShortName());
        Assert.Equal("nuint", TypeDefinition.Get(typeof(UIntPtr)).GetShortName());
    }

    [Fact]
    public void VoidIsStillTheKeyword()
    {
        Assert.Equal("void", TypeDefinition.Get(typeof(void)).GetShortName());
    }

    /// <summary>
    /// A keyword still takes the markers that apply to any other type.
    /// </summary>
    [Fact]
    public void KeywordsTakeNullableAndArrayMarkers()
    {
        Assert.Equal("float?", TypeDefinition.Get(typeof(float)).MakeNullable().GetShortName());
        Assert.Equal("char[]", TypeDefinition.Get(typeof(char)).MakeArray().GetShortName());
    }
}
