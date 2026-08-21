using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// <c>int?</c> and <c>string?</c> both render a trailing <c>?</c> and are not the same thing.
/// </summary>
public class NullableConversionTests
{
    private const string Nullables = @"
        public int? nullableInt;
        public string? nullableString;
        public string plainString;
        public Val? nullableStruct;
        public Color? nullableEnum;
        public KeyValuePair<string, int>? nullableGeneric;
        public List<string?> listOfNullableString;
        public List<int?> listOfNullableInt;
        public T? nullableTypeParameter;
        public IThing? nullableInterface;
";

    [Theory]
    [InlineData("nullableInt", "int?")]
    [InlineData("nullableString", "string?")]
    [InlineData("plainString", "string")]
    [InlineData("nullableStruct", "Val?")]
    [InlineData("nullableEnum", "Color?")]
    [InlineData("nullableGeneric", "KeyValuePair<string,int>?")]
    [InlineData("listOfNullableString", "List<string?>")]
    [InlineData("listOfNullableInt", "List<int?>")]
    [InlineData("nullableTypeParameter", "T?")]
    [InlineData("nullableInterface", "IThing?")]
    public void NullabilityIsWritten(string field, string expected)
    {
        var typeDefinition = TestCompilation.FieldType(Nullables, field).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// The distinction the type model's one bit loses. <c>typeof(int?)</c> compiles and
    /// <c>typeof(string?)</c> is CS8639, so an emitter has to be able to tell them apart.
    /// </summary>
    [Fact]
    public void ValueTypeNullabilityIsNotAnnotationNullability()
    {
        var types = TestCompilation.FieldTypes(Nullables);

        var nullableInt = types["nullableInt"].GetTypeDefinition();
        var nullableString = types["nullableString"].GetTypeDefinition();

        Assert.True(nullableInt.IsNullable);
        Assert.True(nullableString.IsNullable);

        Assert.True(nullableInt.IsNullableValueType());
        Assert.False(nullableString.IsNullableValueType());
    }

    [Fact]
    public void NullableValueTypeKeepsItsUnderlyingType()
    {
        var typeDefinition = TestCompilation.FieldType(Nullables, "nullableInt").GetTypeDefinition();

        var nullable = Assert.IsType<NullableValueTypeDefinition>(typeDefinition);

        Assert.Equal("int", TestCompilation.Write(nullable.UnderlyingType));
        Assert.Equal("int", Assert.Single(nullable.TypeArguments).GetShortName());
    }

    /// <summary>
    /// Removing the nullability of a <c>Nullable&lt;T&gt;</c> gives back <c>T</c>, which is what an
    /// emitter targeting a version without nullable value types would need.
    /// </summary>
    [Fact]
    public void MakeNullableFalseUnwrapsTheValueType()
    {
        var typeDefinition = TestCompilation.FieldType(Nullables, "nullableInt").GetTypeDefinition();

        Assert.Equal("int", TestCompilation.Write(typeDefinition.MakeNullable(false)));
    }

    /// <summary>
    /// A bridged <c>int?</c> has to keep comparing equal to a hand-built one, in both directions and
    /// with the same hash - that comparison is how a generator matches a parameter against a
    /// registration.
    /// </summary>
    [Fact]
    public void BridgedNullableEqualsAHandBuiltOne()
    {
        var bridged = TestCompilation.FieldType(Nullables, "nullableInt").GetTypeDefinition();

        var handBuilt = TypeDefinition.Get(typeof(int)).MakeNullable();

        Assert.True(bridged.Equals(handBuilt));
        Assert.True(handBuilt.Equals(bridged));
        Assert.Equal(handBuilt.GetHashCode(), bridged.GetHashCode());
    }

    [Fact]
    public void NullableStructQualifiesInGlobalMode()
    {
        var typeDefinition = TestCompilation.FieldType(Nullables, "nullableStruct").GetTypeDefinition();

        Assert.Equal("global::BridgeTestNamespace.Val?", TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
        Assert.Contains("BridgeTestNamespace", typeDefinition.KnownNamespaces);
    }

    /// <summary>Two closings of the same nullable generic are not the same type.</summary>
    [Fact]
    public void NullableGenericsCompareByTheirArguments()
    {
        var source = @"
        public KeyValuePair<string, int>? first;
        public KeyValuePair<int, string>? second;
";

        var types = TestCompilation.FieldTypes(source);

        var first = types["first"].GetTypeDefinition();
        var second = types["second"].GetTypeDefinition();

        Assert.False(first.Equals(second));
        Assert.True(first.Equals(types["first"].GetTypeDefinition()));
    }
}
