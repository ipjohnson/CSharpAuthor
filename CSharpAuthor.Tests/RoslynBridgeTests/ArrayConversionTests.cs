using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// Rank and jaggedness, which the one-bit array flag cannot hold.
/// </summary>
public class ArrayConversionTests
{
    private const string Arrays = @"
        public int[] one;
        public int[,] two;
        public int[][] jagged;
        public int[,][] twoOfOne;
        public int[][,] oneOfTwo;
        public int[,,] three;
        public List<int>[] genericArray;
        public List<int>[,] genericTwo;
        public Val[] structArray;
";

    [Theory]
    [InlineData("one", "int[]")]
    [InlineData("two", "int[,]")]
    [InlineData("jagged", "int[][]")]
    [InlineData("twoOfOne", "int[,][]")]
    [InlineData("oneOfTwo", "int[][,]")]
    [InlineData("three", "int[,,]")]
    [InlineData("genericArray", "List<int>[]")]
    [InlineData("genericTwo", "List<int>[,]")]
    [InlineData("structArray", "Val[]")]
    public void ArrayShapeSurvives(string field, string expected)
    {
        var typeDefinition = TestCompilation.FieldType(Arrays, field).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// <c>int[,][]</c> and <c>int[][,]</c> are different types, and the difference is the order the
    /// ranks are written in - which is the one thing a flattened array cannot record.
    /// </summary>
    [Fact]
    public void MixedRanksAreNotTheSameType()
    {
        var types = TestCompilation.FieldTypes(Arrays);

        var twoOfOne = types["twoOfOne"].GetTypeDefinition();
        var oneOfTwo = types["oneOfTwo"].GetTypeDefinition();

        Assert.NotEqual(TestCompilation.Write(twoOfOne), TestCompilation.Write(oneOfTwo));
        Assert.False(twoOfOne.Equals(oneOfTwo));
    }

    [Fact]
    public void RankAndElementAreReadable()
    {
        var types = TestCompilation.FieldTypes(Arrays);

        var twoOfOne = Assert.IsType<ArrayTypeDefinition>(types["twoOfOne"].GetTypeDefinition());

        Assert.Equal(2, twoOfOne.Rank);
        Assert.Equal("int[]", TestCompilation.Write(twoOfOne.ElementType));
        Assert.Equal("int", TestCompilation.Write(twoOfOne.RootElementType));
        Assert.True(twoOfOne.IsArray);
    }

    /// <summary>
    /// A plain <c>T[]</c> still converts to the shape the type model has always used, so a bridged
    /// type keeps comparing equal to a hand-built one.
    /// </summary>
    [Fact]
    public void SingleRankArrayKeepsTheOriginalShape()
    {
        var typeDefinition = TestCompilation.FieldType(Arrays, "one").GetTypeDefinition();

        Assert.IsType<TypeDefinition>(typeDefinition);
        Assert.True(typeDefinition.IsArray);
        Assert.Equal("int", typeDefinition.Name);
    }

    /// <summary>Making an array of an array adds a rank rather than replacing one.</summary>
    [Fact]
    public void MakeArrayAddsARank()
    {
        var twoDimensional = TestCompilation.FieldType(Arrays, "two").GetTypeDefinition();

        Assert.Equal("int[][,]", TestCompilation.Write(twoDimensional.MakeArray()));
        Assert.Equal("int[][][,]", TestCompilation.Write(twoDimensional.MakeArray().MakeArray()));
    }

    [Fact]
    public void ArrayOfGenericQualifiesInGlobalMode()
    {
        var typeDefinition = TestCompilation.FieldType(Arrays, "genericTwo").GetTypeDefinition();

        Assert.Equal(
            "global::System.Collections.Generic.List<int>[,]",
            TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
    }

    [Fact]
    public void ArrayContributesItsElementNamespace()
    {
        var typeDefinition = TestCompilation.FieldType(Arrays, "genericArray").GetTypeDefinition();

        Assert.Contains("System.Collections.Generic", typeDefinition.KnownNamespaces);
    }

    private const string NullableArrays = @"
        public string[]? nullableArray;
        public string?[] arrayOfNullable;
        public string[]?[] arrayOfNullableArray;
        public int[,]?[] arrayOfNullableRankTwo;
        public string?[]?[]? nullableEverywhere;
        public int?[] arrayOfNullableInt;
        public List<Dictionary<string,int?>>?[][] theAdversaryCase;
";

    /// <summary>
    /// A nullable annotation closes off the ranks to its left, so the levels are not one run.
    /// </summary>
    [Theory]
    [InlineData("nullableArray", "string[]?")]
    [InlineData("arrayOfNullable", "string?[]")]
    [InlineData("arrayOfNullableArray", "string[]?[]")]
    [InlineData("arrayOfNullableRankTwo", "int[,]?[]")]
    [InlineData("nullableEverywhere", "string?[]?[]?")]
    [InlineData("arrayOfNullableInt", "int?[]")]
    [InlineData("theAdversaryCase", "List<Dictionary<string,int?>>?[][]")]
    public void NullableArrayAnnotationsLandWhereTheyWereWritten(string field, string expected)
    {
        var typeDefinition = TestCompilation.FieldType(NullableArrays, field).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// <c>T?[]</c> is an array of nullables and <c>T[]?</c> is a nullable array. The model's
    /// <c>MakeArray</c> turns the first into the second.
    /// </summary>
    [Fact]
    public void ArrayOfNullableIsNotANullableArray()
    {
        var types = TestCompilation.FieldTypes(NullableArrays);

        Assert.Equal("string?[]", TestCompilation.Write(types["arrayOfNullable"].GetTypeDefinition()));
        Assert.Equal("string[]?", TestCompilation.Write(types["nullableArray"].GetTypeDefinition()));
    }
}
