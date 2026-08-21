using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// The shape the bridge's types present to the type model, rather than to a writer.
/// </summary>
/// <remarks>
/// <c>ArrayRanks</c> and <c>ContainingType</c> are the two facts the model needs from a converted
/// type that its name does not carry, and they are answered here in the same terms the model asks
/// them in: ranks outermost-first, container as a type rather than as a string.
/// </remarks>
public class TypeModelContractTests
{
    private const string Fields = @"
        public int[] one;
        public int[,] two;
        public int[,][] twoOfOne;
        public int[][,] oneOfTwo;
        public Outer<int>.Inner<string>.Deepest deepest;
        public Outer<int>.Inner<string> inner;
        public (int a, string b) tuple;
";

    /// <summary>
    /// Outermost first, which is the order the specifiers are written in - and the opposite of the
    /// order reflection names them in.
    /// </summary>
    [Fact]
    public void ArrayRanksAreOutermostFirst()
    {
        var types = TestCompilation.FieldTypes(Fields);

        Assert.Equal(new[] { 2, 1 }, RanksOf(types["twoOfOne"].GetTypeDefinition()));
        Assert.Equal(new[] { 1, 2 }, RanksOf(types["oneOfTwo"].GetTypeDefinition()));
        Assert.Equal(new[] { 2 }, RanksOf(types["two"].GetTypeDefinition()));
    }

    /// <summary>The container keeps its own arguments, so it qualifies in every mode.</summary>
    [Fact]
    public void ContainingTypeIsATypeAndNotAName()
    {
        var typeDefinition = TestCompilation.FieldType(Fields, "deepest").GetTypeDefinition();

        var container = Assert.IsType<NestedTypeDefinition>(typeDefinition).ContainingType;

        Assert.NotNull(container);
        Assert.Equal("Outer<int>.Inner<string>", TestCompilation.Write(container!));
        Assert.Equal(
            "global::BridgeTestNamespace.Outer<int>.Inner<string>",
            TestCompilation.Write(container!, TypeOutputMode.Global));
    }

    [Fact]
    public void TheOutermostContainerHasNoContainer()
    {
        var typeDefinition = TestCompilation.FieldType(Fields, "inner").GetTypeDefinition();

        var container = Assert.IsType<NestedTypeDefinition>(typeDefinition).ContainingType;

        Assert.NotNull(container);
        Assert.Null(Assert.IsType<NestedTypeDefinition>(container).ContainingType);
    }

    /// <summary>
    /// An array is declared inside nothing, which is what an array symbol reports. A tuple likewise.
    /// </summary>
    [Fact]
    public void ArraysAndTuplesAreDeclaredInsideNothing()
    {
        var types = TestCompilation.FieldTypes(Fields);

        Assert.Null(Assert.IsType<ArrayTypeDefinition>(types["two"].GetTypeDefinition()).ContainingType);
        Assert.Null(Assert.IsType<TupleTypeDefinition>(types["tuple"].GetTypeDefinition()).ContainingType);
        Assert.Empty(Assert.IsType<TupleTypeDefinition>(types["tuple"].GetTypeDefinition()).ArrayRanks);
    }

    [Fact]
    public void MakeArrayWithARankWrapsOnTheOutside()
    {
        var typeDefinition = TestCompilation.FieldType(Fields, "two").GetTypeDefinition();

        var wrapped = Assert.IsType<ArrayTypeDefinition>(typeDefinition).MakeArray(3);

        Assert.Equal("int[,,][,]", TestCompilation.Write(wrapped));
        Assert.Equal(new[] { 3, 2 }, RanksOf(wrapped));
    }

    private static int[] RanksOf(ITypeDefinition typeDefinition)
    {
        var ranks = Assert.IsType<ArrayTypeDefinition>(typeDefinition).ArrayRanks;

        var result = new int[ranks.Count];

        for (var i = 0; i < ranks.Count; i++)
        {
            result[i] = ranks[i];
        }

        return result;
    }
}
