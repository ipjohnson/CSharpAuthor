using System.Linq;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// Converting from a syntax node, which is the shape a generator usually has one in.
/// </summary>
public class SyntaxNodeConversionTests
{
    private const string Fields = @"
        public List<string?> annotatedArgument;
        public string? annotated;
        public int? nullableValue;
        public Outer<int>.Inner<string> nested;
";

    private static (ITypeDefinition? Definition, string Text) FromSyntax(string fieldName)
    {
        var compilation = TestCompilation.CompileClean(TestCompilation.Wrap(Fields));

        var tree = compilation.SyntaxTrees.First();

        var model = compilation.GetSemanticModel(tree);

        foreach (var field in tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (field.Declaration.Variables[0].Identifier.ValueText == fieldName)
            {
                return (field.Declaration.Type.GetTypeDefinition(model), field.Declaration.Type.ToString());
            }
        }

        Assert.True(false, "no field named " + fieldName);

        return (null, "");
    }

    [Theory]
    [InlineData("annotatedArgument", "List<string?>")]
    [InlineData("annotated", "string?")]
    [InlineData("nullableValue", "int?")]
    [InlineData("nested", "Outer<int>.Inner<string>")]
    public void ATypeReferenceConvertsThroughTheSemanticModel(string field, string expected)
    {
        var result = FromSyntax(field);

        Assert.NotNull(result.Definition);
        Assert.Equal(expected, TestCompilation.Write(result.Definition!));
    }

    /// <summary>
    /// The annotation comes off the model, not off the text. Asking whether the source ended in "?"
    /// says yes for <c>List&lt;string?&gt;</c>, which is not a nullable list.
    /// </summary>
    [Fact]
    public void NullabilityIsNotReadFromTheSourceText()
    {
        var result = FromSyntax("annotatedArgument");

        Assert.EndsWith("?>", result.Text);
        Assert.False(result.Definition!.IsNullable);
        Assert.Equal("List<string?>", TestCompilation.Write(result.Definition!));
    }

    [Fact]
    public void ASymbolInfoConvertsWhenItNamesAType()
    {
        var compilation = TestCompilation.CompileClean(TestCompilation.Wrap(Fields));

        var tree = compilation.SyntaxTrees.First();

        var model = compilation.GetSemanticModel(tree);

        var typeSyntax = tree.GetRoot().DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .First(field => field.Declaration.Variables[0].Identifier.ValueText == "nested")
            .Declaration.Type;

        var symbolInfo = model.GetSymbolInfo(typeSyntax);

        Assert.Equal("Outer<int>.Inner<string>", TestCompilation.Write(symbolInfo.GetTypeDefinition()!));
    }
}
