using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// Checks the conversion against the compiler's own spelling of the same type.
/// </summary>
/// <remarks>
/// Every other test here asserts a string a person wrote, which proves only that the conversion
/// agrees with what its author expected. Roslyn will print the type it bound, and it is not guessing
/// - so a row that disagrees is the conversion being wrong about a type the compiler already knows
/// the answer for. The only accommodations are spacing, which is not semantic and which the type
/// model has always written without a space after a comma, and the global namespace, where the type
/// model spells "no namespace" as the empty string and therefore cannot write <c>global::</c>.
/// </remarks>
public class RoslynDisplayOracleTests
{
    private static readonly SymbolDisplayFormat FullyQualified =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat ShortName = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    [Theory]
    [InlineData("int")]
    [InlineData("float")]
    [InlineData("char")]
    [InlineData("sbyte")]
    [InlineData("nint")]
    [InlineData("nuint")]
    [InlineData("string")]
    [InlineData("object")]
    [InlineData("decimal")]
    [InlineData("dynamic")]
    [InlineData("int[]")]
    [InlineData("int[,]")]
    [InlineData("int[,,]")]
    [InlineData("int[][]")]
    [InlineData("int[,][]")]
    [InlineData("int[][,]")]
    [InlineData("int?")]
    [InlineData("string?")]
    [InlineData("int?[]")]
    [InlineData("string?[]")]
    [InlineData("string[]?")]
    [InlineData("string[]?[]")]
    [InlineData("int[,]?[]")]
    [InlineData("string?[]?[]?")]
    [InlineData("List<int>")]
    [InlineData("List<int>[]")]
    [InlineData("List<int>[,]")]
    [InlineData("Dictionary<string, int>")]
    [InlineData("List<Dictionary<string, int?>>")]
    [InlineData("List<Dictionary<string, int?>>?[][]")]
    [InlineData("Outer<int>.PlainInner")]
    [InlineData("Outer<int>.Inner<string>")]
    [InlineData("Outer<int>.Inner<string>.Deepest")]
    [InlineData("Outer<Outer<int>.PlainInner>.Inner<Color>")]
    [InlineData("Plain.Middle.Deepest")]
    [InlineData("Val?")]
    [InlineData("Color")]
    [InlineData("IThing")]
    [InlineData("IThing?")]
    [InlineData("(int a, string b)")]
    [InlineData("(int, string)")]
    [InlineData("(int a, (string x, bool y) b)")]
    [InlineData("(int a, string b)?")]
    [InlineData("(int, string)[]")]
    [InlineData("int*")]
    [InlineData("void*")]
    [InlineData("int**")]
    [InlineData("int*[]")]
    [InlineData("delegate*<int, void>")]
    [InlineData("delegate* unmanaged[Cdecl]<int, int>")]
    [InlineData("@event")]
    [InlineData("@event.@void")]
    [InlineData("T")]
    [InlineData("T?")]
    [InlineData("T[]")]
    [InlineData("List<T>")]
    public void GlobalModeMatchesRoslyn(string typeExpression)
    {
        var symbol = TestCompilation.FieldType("public " + typeExpression + " probe;", "probe");

        Assert.Equal(
            Normalize(symbol.ToDisplayString(FullyQualified)),
            Normalize(TestCompilation.Write(symbol.GetTypeDefinition(), TypeOutputMode.Global)));
    }

    [Theory]
    [InlineData("int[,][]")]
    [InlineData("int[][,]")]
    [InlineData("string[]?[]")]
    [InlineData("List<Dictionary<string, int?>>?[][]")]
    [InlineData("Outer<int>.Inner<string>.Deepest")]
    [InlineData("Outer<Outer<int>.PlainInner>.Inner<Color>")]
    [InlineData("(int a, (string x, bool y) b)")]
    [InlineData("delegate* unmanaged[Cdecl]<int, int>")]
    [InlineData("@event.@void")]
    [InlineData("Val?")]
    public void ShortModeMatchesRoslyn(string typeExpression)
    {
        var symbol = TestCompilation.FieldType("public " + typeExpression + " probe;", "probe");

        Assert.Equal(
            Normalize(symbol.ToDisplayString(ShortName)),
            Normalize(TestCompilation.Write(symbol.GetTypeDefinition())));
    }

    /// <summary>Spacing after a comma is not part of the type.</summary>
    private static string Normalize(string typeName)
    {
        return typeName.Replace(", ", ",");
    }
}
