using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

/// <summary>
/// The C# 15 union declaration.
/// </summary>
/// <remarks>
/// A union declares everything in its header: the cases are the primary constructor's parameters
/// written as bare types, and the compiler synthesises a constructor and an implicit conversion per
/// case plus a public <c>object? Value</c>. So the whole of what this has to get right is the
/// header - a name written after a case type does not compile, and neither does a body where the
/// declaration should have ended.
/// </remarks>
public class UnionDefinitionTests
{
    [Fact]
    public void UnionWritesItsCasesAsBareTypes()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        file.FileScopedNamespace = true;

        var union = file.AddClass("Shape");
        union.TypeKeyword = ClassKeyword.Union;

        union.AddUnionCase(TypeDefinition.Get("TestNamespace", "Circle"));
        union.AddUnionCase(TypeDefinition.Get("TestNamespace", "Square"));

        // The redundant `using TestNamespace;` is what every type reference in this library emits
        // for its own namespace, not something the union keyword introduces. Asserted rather than
        // trimmed, so a change to that behaviour is visible here too.

        var context = new OutputContext();
        file.WriteOutput(context);

        AssertEqual.WithoutNewLine(UnionOutput, context.Output());
    }

    private const string UnionOutput =
        @"using TestNamespace;

namespace TestNamespace;

public union Shape(Circle, Square);
";

    /// <summary>
    /// Choosing the keyword is enough. A union has nothing to put in a body, and an empty
    /// <c>{ }</c> after the header is not what the declaration means.
    /// </summary>
    [Fact]
    public void UnionTerminatesWithASemicolonWithoutBeingAsked()
    {
        var union = new ClassDefinition("Shape");

        Assert.False(union.TerminateWithSemicolon);

        union.TypeKeyword = ClassKeyword.Union;

        Assert.True(union.TerminateWithSemicolon);
    }

    /// <summary>
    /// And it stays the caller's choice afterwards, for a union that declares members of its own.
    /// </summary>
    [Fact]
    public void SemicolonTerminationRemainsSettable()
    {
        var union = new ClassDefinition("Shape") { TypeKeyword = ClassKeyword.Union };

        union.TerminateWithSemicolon = false;

        Assert.False(union.TerminateWithSemicolon);
    }

    /// <summary>
    /// Cases from another namespace are imported, so the declaration names them unqualified.
    /// </summary>
    [Fact]
    public void UnionImportsTheNamespacesOfItsCases()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        file.FileScopedNamespace = true;

        var union = file.AddClass("Result");
        union.TypeKeyword = ClassKeyword.Union;

        union.AddUnionCase(TypeDefinition.Get("Other.Models", "Pet"));
        union.AddUnionCase(TypeDefinition.Get("Other.Errors", "NotFound"));

        var context = new OutputContext();
        file.WriteOutput(context);

        AssertEqual.WithoutNewLine(ImportOutput, context.Output());
    }

    private const string ImportOutput =
        @"using Other.Errors;
using Other.Models;

namespace TestNamespace;

public union Result(Pet, NotFound);
";

    /// <summary>
    /// Order is the order cases were added, which on a union is the order a switch over the value is
    /// checked in.
    /// </summary>
    [Fact]
    public void UnionKeepsTheOrderCasesWereAdded()
    {
        var union = new ClassDefinition("Shape") { TypeKeyword = ClassKeyword.Union };

        union.AddUnionCase(TypeDefinition.Get("N", "C"));
        union.AddUnionCase(TypeDefinition.Get("N", "B"));
        union.AddUnionCase(TypeDefinition.Get("N", "A"));

        var context = new OutputContext();
        union.WriteOutput(context);

        Assert.Contains("union Shape(C, B, A);", context.Output());
    }

    /// <summary>
    /// A modifier is written where it is on any other type, so a union can be public, internal or
    /// partial like anything else.
    /// </summary>
    [Fact]
    public void UnionCarriesItsModifiers()
    {
        var union = new ClassDefinition("Shape") { TypeKeyword = ClassKeyword.Union };

        union.Modifiers |= ComponentModifier.Public | ComponentModifier.Partial;
        union.AddUnionCase(TypeDefinition.Get("N", "Circle"));

        var context = new OutputContext();
        union.WriteOutput(context);

        Assert.Contains("public partial union Shape(Circle);", context.Output());
    }

    /// <summary>
    /// Generic unions are declared the same way, with the parameters between the name and the cases.
    /// </summary>
    [Fact]
    public void UnionCanBeGeneric()
    {
        var union = new ClassDefinition("Result") { TypeKeyword = ClassKeyword.Union };

        union.AddGenericParameter(TypeDefinition.Get("", "T"));
        union.AddUnionCase(TypeDefinition.Get("", "T"));
        union.AddUnionCase(TypeDefinition.Get("N", "Error"));

        var context = new OutputContext();
        union.WriteOutput(context);

        Assert.Contains("union Result<T>(T, Error);", context.Output());
    }

    /// <summary>
    /// Every other keyword still writes its parameters with names - the type-only form is the
    /// union's alone, and a record that lost its parameter names would compile to something else
    /// entirely.
    /// </summary>
    [Fact]
    public void ARecordStillWritesNamedParameters()
    {
        var record = new ClassDefinition("Pet") { TypeKeyword = ClassKeyword.Record };

        record.TerminateWithSemicolon = true;

        var constructor = record.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(TypeDefinition.Get(typeof(string)), "Name");

        var context = new OutputContext();
        record.WriteOutput(context);

        Assert.Contains("record Pet(string Name);", context.Output());
    }
}
