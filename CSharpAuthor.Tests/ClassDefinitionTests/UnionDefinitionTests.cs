using CSharpAuthor.Profiles;
using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

/// <summary>
/// A C# 15 union declares its cases in its header, as bare types.
/// </summary>
/// <remarks>
/// Brought forward from the 1.2.0 release, which shipped after this branch was cut. The one thing
/// that could not come with it is the <c>AddImportNamespace</c> call that sat beside the write:
/// V2 derives the namespace from the type it recorded, so the case types below reach the using
/// list without anyone asking for them. That is invariant 1, and it is what these tests pin.
/// </remarks>
public class UnionDefinitionTests
{
    private static ClassDefinition Union(CSharpFileDefinition file, string name)
    {
        var union = file.AddClass(name);

        union.TypeKeyword = ClassKeyword.Union;

        return union;
    }

    [Fact]
    public void TheCasesAreWrittenAsBareTypes()
    {
        var file = new CSharpFileDefinition("Shapes");
        var union = Union(file, "Shape");

        union.AddUnionCase(TypeDefinition.Get("Shapes.Kinds", "Circle"));
        union.AddUnionCase(TypeDefinition.Get("Shapes.Kinds", "Square"));

        var context = new OutputContext();

        file.WriteOutput(context);

        var output = context.Output();

        Assert.Contains("public union Shape(Circle, Square);", output);
        Assert.DoesNotContain("case0", output);
        Assert.DoesNotContain("case1", output);
    }

    /// <summary>
    /// The V2 half: nobody asked for this using, and it is there because a type was written.
    /// </summary>
    [Fact]
    public void TheCaseTypesNamespaceIsDerivedNotDeclared()
    {
        var file = new CSharpFileDefinition("Shapes");
        var union = Union(file, "Shape");

        union.AddUnionCase(TypeDefinition.Get("Shapes.Kinds", "Circle"));

        var context = new OutputContext();

        file.WriteOutput(context);

        Assert.Contains("using Shapes.Kinds;", context.Output());
    }

    [Fact]
    public void AQualifyingModeWritesTheCasesOutInFull()
    {
        var file = new CSharpFileDefinition("Shapes");
        var union = Union(file, "Shape");

        union.AddUnionCase(TypeDefinition.Get("Shapes.Kinds", "Circle"));

        var context = new OutputContext(
            new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        file.WriteOutput(context);

        var output = context.Output();

        Assert.Contains("union Shape(global::Shapes.Kinds.Circle);", output);
        Assert.DoesNotContain("using Shapes.Kinds;", output);
    }

    [Fact]
    public void ChoosingTheKeywordTerminatesTheDeclarationWithASemicolon()
    {
        var file = new CSharpFileDefinition("Shapes");
        var union = Union(file, "Shape");

        Assert.True(union.TerminateWithSemicolon);
    }

    /// <summary>
    /// A union has no downlevel form, so a profile that cannot reach C# 15 refuses rather than
    /// emitting something that is not a union.
    /// </summary>
    [Fact]
    public void ATargetBelowCSharp15CannotEmitOne()
    {
        var info = LanguageFeatures.Get(LanguageFeature.Unions);

        Assert.Equal(LanguageVersion.CSharp15, info.MinimumVersion);
        Assert.Equal(FeatureCategory.Impossible, info.Category);
    }
}
