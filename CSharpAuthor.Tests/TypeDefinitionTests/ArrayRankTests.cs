using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// An array's shape is a list of ranks, not a flag. <c>int[,]</c> is one two-dimensional array;
/// <c>int[][]</c> is two one-dimensional ones. A single <c>IsArray</c> bool cannot tell them apart, so
/// it wrote both wrongly and silently.
/// </summary>
/// <remarks>
/// Each expectation here is written twice: once as the C# the compiler parsed into the
/// <see cref="Type"/>, and once as the string the emitter has to produce. That makes the compiler the
/// oracle - the test cannot agree with a wrong renderer, because the <c>typeof</c> and the expected
/// text are the same source text.
/// <para>
/// The order matters and is easy to get backwards: reflection names <c>typeof(int[,][])</c> as
/// <c>Int32[][,]</c>, reversing the specifiers, because it names the element type first.
/// </para>
/// </remarks>
public class ArrayRankTests
{
    public static TheoryData<Type, string> ArrayShapes => new()
    {
        { typeof(int[]), "int[]" },
        { typeof(int[,]), "int[,]" },
        { typeof(int[,,]), "int[,,]" },
        { typeof(int[][]), "int[][]" },
        { typeof(int[][][]), "int[][][]" },
        { typeof(int[,][]), "int[,][]" },
        { typeof(int[][,]), "int[][,]" },
        { typeof(int[,,][]), "int[,,][]" },
        { typeof(int[][,,]), "int[][,,]" },
        { typeof(int[,][,,]), "int[,][,,]" },
        { typeof(string[][]), "string[][]" },
        { typeof(float[,]), "float[,]" },
    };

    [Theory]
    [MemberData(nameof(ArrayShapes))]
    public void WritesTheShapeTheCompilerParsed(Type type, string expected)
    {
        var builder = new StringBuilder();

        TypeDefinition.Get(type).WriteTypeName(builder);

        Assert.Equal(expected, builder.ToString());
    }

    /// <summary>
    /// The two shapes the old model collapsed into one, kept apart explicitly.
    /// </summary>
    [Fact]
    public void MultidimensionalIsNotJagged()
    {
        Assert.Equal("int[,]", TypeDefinition.Get(typeof(int[,])).GetShortName());
        Assert.Equal("int[][]", TypeDefinition.Get(typeof(int[][])).GetShortName());

        Assert.NotEqual(
            TypeDefinition.Get(typeof(int[,])),
            TypeDefinition.Get(typeof(int[][])));

        Assert.NotEqual(
            TypeDefinition.Get(typeof(int[,])).GetHashCode(),
            TypeDefinition.Get(typeof(int[][])).GetHashCode());
    }

    /// <summary>
    /// <c>MakeArray</c> means "an array of this", so applying it twice has to nest rather than
    /// re-set a flag and lose a dimension.
    /// </summary>
    [Fact]
    public void MakeArrayComposes()
    {
        var element = TypeDefinition.Get(typeof(int));

        Assert.Equal("int[]", element.MakeArray().GetShortName());
        Assert.Equal("int[][]", element.MakeArray().MakeArray().GetShortName());
        Assert.Equal("int[][][]", element.MakeArray().MakeArray().MakeArray().GetShortName());

        Assert.Equal(
            TypeDefinition.Get(typeof(int[][])),
            element.MakeArray().MakeArray());
    }

    [Fact]
    public void MakeArrayTakesARank()
    {
        var element = TypeDefinition.Get(typeof(int));

        Assert.Equal("int[,]", element.MakeArray(2).GetShortName());
        Assert.Equal("int[,,]", element.MakeArray(3).GetShortName());

        // A new array always goes on the outside: an array of int[,] is int[][,].
        Assert.Equal("int[][,]", element.MakeArray(2).MakeArray().GetShortName());
        Assert.Equal("int[,][]", element.MakeArray().MakeArray(2).GetShortName());

        Assert.Equal(TypeDefinition.Get(typeof(int[][,])), element.MakeArray(2).MakeArray());
        Assert.Equal(TypeDefinition.Get(typeof(int[,][])), element.MakeArray().MakeArray(2));
    }

    [Fact]
    public void ARankIsAtLeastOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TypeDefinition.Get(typeof(int)).MakeArray(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TypeDefinition.Get(typeof(int)).MakeArray(-1));
    }

    [Fact]
    public void RanksAreReadableOutermostFirst()
    {
        Assert.Equal(new[] { 2, 1 }, TypeDefinition.Get(typeof(int[,][])).ArrayRanks);
        Assert.Equal(new[] { 1, 2 }, TypeDefinition.Get(typeof(int[][,])).ArrayRanks);
        Assert.Equal(new[] { 1, 1, 1 }, TypeDefinition.Get(typeof(int[][][])).ArrayRanks);
        Assert.Empty(TypeDefinition.Get(typeof(int)).ArrayRanks);
    }

    /// <summary>
    /// <c>IsArray</c> keeps its old meaning for the callers that read it.
    /// </summary>
    [Fact]
    public void IsArrayStillAnswersTheOldQuestion()
    {
        Assert.False(TypeDefinition.Get(typeof(int)).IsArray);
        Assert.True(TypeDefinition.Get(typeof(int[])).IsArray);
        Assert.True(TypeDefinition.Get(typeof(int[,])).IsArray);
        Assert.True(TypeDefinition.Get(typeof(int[][])).IsArray);
        Assert.True(TypeDefinition.Get(typeof(int)).MakeArray().IsArray);
    }

    [Fact]
    public void GenericTypesTakeArrayShapesToo()
    {
        Assert.Equal("Task<string>[][]", TypeDefinition.Get(typeof(Task<string>[][])).GetShortName());
        Assert.Equal("Task<string>[,]", TypeDefinition.Get(typeof(Task<string>[,])).GetShortName());
        Assert.Equal("List<int>[][]", TypeDefinition.Get(typeof(List<int>)).MakeArray().MakeArray().GetShortName());
        Assert.Equal("List<int>[,][]", TypeDefinition.Get(typeof(List<int>[,][])).GetShortName());
    }

    [Fact]
    public void TypeParametersTakeArrayShapesToo()
    {
        var parameter = new TypeParameterDefinition("T");

        Assert.Equal("T[]", parameter.MakeArray().GetShortName());
        Assert.Equal("T[][]", parameter.MakeArray().MakeArray().GetShortName());
        Assert.Equal("T[,]", parameter.MakeArray(2).GetShortName());
        Assert.Equal("T[][,]", parameter.MakeArray(2).MakeArray().GetShortName());

        Assert.NotEqual(parameter.MakeArray(), parameter.MakeArray().MakeArray());
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, "Task<string>[,][]")]
    [InlineData(TypeOutputMode.FullName, "System.Threading.Tasks.Task<string>[,][]")]
    [InlineData(TypeOutputMode.Global, "global::System.Threading.Tasks.Task<string>[,][]")]
    public void TheShapeSurvivesEveryOutputMode(TypeOutputMode mode, string expected)
    {
        var builder = new StringBuilder();

        TypeDefinition.Get(typeof(Task<string>[,][])).WriteTypeName(builder, mode);

        Assert.Equal(expected, builder.ToString());
    }

    /// <summary>
    /// Nullability applies to the outermost array, which is where it was written before.
    /// </summary>
    [Fact]
    public void NullableGoesAfterTheShape()
    {
        Assert.Equal("int[][]?", TypeDefinition.Get(typeof(int[][])).MakeNullable().GetShortName());
        Assert.Equal("int[,]?", TypeDefinition.Get(typeof(int)).MakeArray(2).MakeNullable().GetShortName());
        Assert.Equal("int[][]?", TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray().MakeArray().GetShortName());
    }

    /// <summary>
    /// The element type keeps its keyword: an array is unwrapped to it, rather than looked up shape by
    /// shape in a table that only listed twelve of them.
    /// </summary>
    [Fact]
    public void ElementKeywordsSurviveTheUnwrap()
    {
        Assert.Equal("float[]", TypeDefinition.Get(typeof(float[])).GetShortName());
        Assert.Equal("char[][]", TypeDefinition.Get(typeof(char[][])).GetShortName());
        Assert.Equal("sbyte[,]", TypeDefinition.Get(typeof(sbyte[,])).GetShortName());
        Assert.Equal("string[]", TypeDefinition.Get(typeof(string[])).GetShortName());
        Assert.Equal("byte[]", TypeDefinition.Get(typeof(byte[])).GetShortName());

        Assert.Empty(TypeDefinition.Get(typeof(float[])).Namespace);
    }

    [Fact]
    public void ArraysOfDifferentShapeAreDifferentValues()
    {
        var shapes = new[]
        {
            TypeDefinition.Get(typeof(int)),
            TypeDefinition.Get(typeof(int[])),
            TypeDefinition.Get(typeof(int[,])),
            TypeDefinition.Get(typeof(int[][])),
            TypeDefinition.Get(typeof(int[,][])),
            TypeDefinition.Get(typeof(int[][,])),
        };

        for (var i = 0; i < shapes.Length; i++)
        {
            for (var j = 0; j < shapes.Length; j++)
            {
                if (i == j)
                {
                    Assert.Equal(0, shapes[i].CompareTo(shapes[j]));
                }
                else
                {
                    Assert.NotEqual(0, shapes[i].CompareTo(shapes[j]));
                }
            }
        }
    }
}
