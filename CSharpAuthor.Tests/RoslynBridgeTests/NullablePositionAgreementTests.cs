using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// The bridge has always known where an annotation sits - <see cref="ArrayTypeDefinition"/> keeps
/// one per level. The type model now knows too, so the two have to say the same thing about the
/// same source rather than each being right in its own dialect.
/// </summary>
public class NullablePositionAgreementTests
{
    private const string Arrays = @"
        public int?[] elementNullable;
        public string?[] referenceElementNullable;
        public int?[]? bothNullable;
        public int[]? arrayNullable;
        public string[]?[] nullableArrayOfArrays;
        public int?[][] jaggedElementNullable;
        public int?[,] elementNullableTwoDimensional;
";

    /// <summary>
    /// What the compiler wrote, read back off the symbol. These are the four rows the model could
    /// not hold: the third dropped an annotation outright, and the fifth needs the <c>?</c> to close
    /// a run of specifiers rather than to end the type.
    /// </summary>
    [Theory]
    [InlineData("elementNullable", "int?[]")]
    [InlineData("referenceElementNullable", "string?[]")]
    [InlineData("bothNullable", "int?[]?")]
    [InlineData("arrayNullable", "int[]?")]
    [InlineData("nullableArrayOfArrays", "string[]?[]")]
    [InlineData("jaggedElementNullable", "int?[][]")]
    [InlineData("elementNullableTwoDimensional", "int?[,]")]
    public void TheBridgeWritesThePosition(string field, string expected)
    {
        var typeDefinition = TestCompilation.FieldType(Arrays, field).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// The same type built by hand out of the type model writes the same text as the bridged one.
    /// A generator that mixes the two - a symbol for a parameter, a hand-built type for what it
    /// generates around it - would otherwise emit two spellings of one type.
    /// </summary>
    [Theory]
    [InlineData("elementNullable")]
    [InlineData("referenceElementNullable")]
    [InlineData("bothNullable")]
    [InlineData("arrayNullable")]
    [InlineData("nullableArrayOfArrays")]
    [InlineData("jaggedElementNullable")]
    [InlineData("elementNullableTwoDimensional")]
    public void TheHandBuiltShapeAgrees(string field)
    {
        var bridged = TestCompilation.FieldType(Arrays, field).GetTypeDefinition();

        var handBuilt = HandBuild(field);

        Assert.Equal(TestCompilation.Write(bridged), TestCompilation.Write(handBuilt));
        Assert.Equal(bridged.ArrayRanks, handBuilt.ArrayRanks);
        Assert.Equal(bridged.NullableAnnotations, handBuilt.NullableAnnotations);
        Assert.Equal(bridged.IsNullable, handBuilt.IsNullable);
    }

    private static ITypeDefinition HandBuild(string field)
    {
        var @int = TypeDefinition.Get(typeof(int));
        var @string = TypeDefinition.Get(typeof(string));

        switch (field)
        {
            case "elementNullable":
                return @int.MakeNullable().MakeArray();
            case "referenceElementNullable":
                return @string.MakeNullable().MakeArray();
            case "bothNullable":
                return @int.MakeNullable().MakeArray().MakeNullable();
            case "arrayNullable":
                return @int.MakeArray().MakeNullable();
            case "nullableArrayOfArrays":
                return @string.MakeArray().MakeNullable().MakeArray();
            case "jaggedElementNullable":
                return @int.MakeNullable().MakeArray().MakeArray();
            case "elementNullableTwoDimensional":
                return @int.MakeNullable().MakeArray(2);
            default:
                throw new System.ArgumentOutOfRangeException(nameof(field), field, "no hand-built shape");
        }
    }

    /// <summary>
    /// The annotation list is the same length on both sides - one per array level plus one for the
    /// element - so a reader can index it without asking which implementation produced it.
    /// </summary>
    [Fact]
    public void BothSidesSizeTheListTheSameWay()
    {
        foreach (var pair in TestCompilation.FieldTypes(Arrays))
        {
            var typeDefinition = pair.Value.GetTypeDefinition();

            Assert.Equal(
                typeDefinition.ArrayRanks.Count + 1,
                typeDefinition.NullableAnnotations.Count);
        }
    }
}
