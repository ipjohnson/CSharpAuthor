using Xunit;

namespace CSharpAuthor.Tests.PropertyDefinitionTests;

public class IndexerPropertyTests
{
    [Fact]
    public void SingleIndexIndexer()
    {
        var classDefinition = new ClassDefinition("Row");

        var property = classDefinition.AddProperty(typeof(string), "this");
        property.IndexType = TypeDefinition.Get(typeof(int));
        property.Get.AddIndentedStatement("return _values[index]");
        property.Set!.AddIndentedStatement("_values[index] = value");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(SingleIndexOutput, context.Output());
    }

    private const string SingleIndexOutput =
        @"public class Row
{
    public string this[int index]
    {
        get
        {
            return _values[index];
        }
        set
        {
            _values[index] = value;
        }
    }
}
";

    [Fact]
    public void IndexerWithSeveralIndices()
    {
        var classDefinition = new ClassDefinition("Grid");

        var property = classDefinition.AddProperty(typeof(int), "this");
        property.AddIndexParameter(TypeDefinition.Get(typeof(int)), "row");
        property.AddIndexParameter(TypeDefinition.Get(typeof(int)), "column");
        property.Get.AddIndentedStatement("return _cells[row, column]");
        property.Set!.AddIndentedStatement("_cells[row, column] = value");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(SeveralIndicesOutput, context.Output());
    }

    private const string SeveralIndicesOutput =
        @"public class Grid
{
    public int this[int row, int column]
    {
        get
        {
            return _cells[row, column];
        }
        set
        {
            _cells[row, column] = value;
        }
    }
}
";

    /// <summary>
    /// An indexer has no auto-property form, so a read-only one still writes its accessor out.
    /// </summary>
    [Fact]
    public void ReadOnlyIndexer()
    {
        var classDefinition = new ClassDefinition("Row");

        var property = classDefinition.AddProperty(typeof(string), "this");
        property.AddIndexParameter(TypeDefinition.Get(typeof(int)), "index");
        property.Set = null;
        property.Get.AddIndentedStatement("return _values[index]");

        var context = new OutputContext();
        classDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(ReadOnlyOutput, context.Output());
    }

    private const string ReadOnlyOutput =
        @"public class Row
{
    public string this[int index]
    {
        get
        {
            return _values[index];
        }
    }
}
";
}
