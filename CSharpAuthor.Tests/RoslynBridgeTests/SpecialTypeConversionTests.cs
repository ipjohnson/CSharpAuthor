using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// The types C# has a keyword for. <c>Single</c> is not <c>float</c> at a use site:
/// <c>Single f = 1.5f</c> needs a using for <c>System</c>, and the defect list has the keyword list
/// because four of them were missing.
/// </summary>
public class SpecialTypeConversionTests
{
    private const string Specials = @"
        public object objectField;
        public bool boolField;
        public char charField;
        public sbyte sbyteField;
        public byte byteField;
        public short shortField;
        public ushort ushortField;
        public int intField;
        public uint uintField;
        public long longField;
        public ulong ulongField;
        public decimal decimalField;
        public float floatField;
        public double doubleField;
        public string stringField;
        public nint nintField;
        public nuint nuintField;
        public System.IntPtr intPtrField;
        public void VoidMethod() { }
";

    [Theory]
    [InlineData("objectField", "object")]
    [InlineData("boolField", "bool")]
    [InlineData("charField", "char")]
    [InlineData("sbyteField", "sbyte")]
    [InlineData("byteField", "byte")]
    [InlineData("shortField", "short")]
    [InlineData("ushortField", "ushort")]
    [InlineData("intField", "int")]
    [InlineData("uintField", "uint")]
    [InlineData("longField", "long")]
    [InlineData("ulongField", "ulong")]
    [InlineData("decimalField", "decimal")]
    [InlineData("floatField", "float")]
    [InlineData("doubleField", "double")]
    [InlineData("stringField", "string")]
    [InlineData("nintField", "nint")]
    [InlineData("nuintField", "nuint")]
    [InlineData("intPtrField", "nint")]
    public void SpecialTypesAreWrittenAsKeywords(string field, string expected)
    {
        var typeDefinition = TestCompilation.FieldType(Specials, field).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// A keyword is a keyword in every output mode, and it needs no import - the type model spells
    /// that as an empty namespace, which is also what <c>TypeDefinition.Get(typeof(int))</c> does.
    /// </summary>
    [Fact]
    public void KeywordTypesNeedNoImport()
    {
        var typeDefinition = TestCompilation.FieldType(Specials, "floatField").GetTypeDefinition();

        Assert.Equal("", typeDefinition.Namespace);
        Assert.Equal("float", TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
        Assert.Equal("float", TestCompilation.Write(typeDefinition, TypeOutputMode.FullName));
        Assert.DoesNotContain("System", typeDefinition.KnownNamespaces);
    }

    [Fact]
    public void BridgedKeywordTypeEqualsAHandBuiltOne()
    {
        var bridged = TestCompilation.FieldType(Specials, "intField").GetTypeDefinition();

        var handBuilt = TypeDefinition.Get(typeof(int));

        Assert.True(bridged.Equals(handBuilt));
        Assert.True(handBuilt.Equals(bridged));
    }

    /// <summary>
    /// <c>void</c> keeps the identity the type model already gives it, so a caller comparing a
    /// return type against <c>TypeDefinition.Get(typeof(void))</c> still matches.
    /// </summary>
    [Fact]
    public void VoidIsWrittenAsAKeywordAndKeepsItsIdentity()
    {
        var typeDefinition = TestCompilation.MethodReturnType(Specials, "VoidMethod").GetTypeDefinition();

        Assert.Equal("void", TestCompilation.Write(typeDefinition));
        Assert.Equal("void", TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
        Assert.True(typeDefinition.Equals(TypeDefinition.Get(typeof(void))));
    }
}
