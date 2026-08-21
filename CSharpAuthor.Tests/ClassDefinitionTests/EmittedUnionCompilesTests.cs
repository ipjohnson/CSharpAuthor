using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

// The declaration this file asserts against, written by hand exactly as CSharpAuthor emits it.
//
// This is the assertion the rest of the suite cannot make. Every other test compares the emitted
// text to a string, which proves the writer produced what somebody expected - not that what it
// produced is C#. A union is a declaration form nothing else here writes, so a plausible-looking
// wrong shape would pass a string comparison and fail in every consumer.
//
// Compiled by this project rather than by a Roslyn invocation inside a test, which is why the test
// project moved to net11.0 with LangVersion preview: if the shape below is not valid C#, this file
// does not build and no test in the suite runs.
public sealed record Pet(int Id);

public sealed record GetPetNotFound(string Body);

public sealed record GetPetServiceUnavailable;

public union GetPetResponse(Pet, GetPetNotFound, GetPetServiceUnavailable);

/// <summary>
/// What CSharpAuthor writes for a union, against a union the compiler has accepted.
/// </summary>
public class EmittedUnionCompilesTests
{
    /// <summary>
    /// The emitted declaration, character for character, against the one above.
    /// </summary>
    /// <remarks>
    /// The pairing is the point: the hand-written one is known to compile because this file did,
    /// and the emitted one is known to match it because of this assertion. Either alone proves
    /// nothing about the other.
    /// </remarks>
    [Fact]
    public void TheEmittedDeclarationMatchesOneTheCompilerAccepted()
    {
        var union = new ClassDefinition("GetPetResponse");

        union.TypeKeyword = ClassKeyword.Union;
        union.Modifiers |= ComponentModifier.Public;

        union.AddUnionCase(TypeDefinition.Get("CSharpAuthor.Tests.ClassDefinitionTests", "Pet"));
        union.AddUnionCase(
            TypeDefinition.Get("CSharpAuthor.Tests.ClassDefinitionTests", "GetPetNotFound"));
        union.AddUnionCase(
            TypeDefinition.Get("CSharpAuthor.Tests.ClassDefinitionTests", "GetPetServiceUnavailable"));

        var context = new OutputContext();
        union.WriteOutput(context);

        Assert.Contains(
            "public union GetPetResponse(Pet, GetPetNotFound, GetPetServiceUnavailable);",
            context.Output());
    }

    /// <summary>
    /// The members the compiler synthesises from that declaration, which is the whole reason a union
    /// needs no body: a constructor and an implicit conversion per case, and a public
    /// <c>object? Value</c>.
    /// </summary>
    /// <remarks>
    /// Asserted through the compiler rather than through reflection, so a change in what the
    /// language synthesises shows up as this file failing to build.
    /// </remarks>
    [Fact]
    public void TheCompilerSynthesisesTheBasicUnionPattern()
    {
        GetPetResponse fromSuccess = new Pet(7);
        GetPetResponse fromError = new GetPetNotFound("no pet");
        GetPetResponse fromBodyless = new GetPetServiceUnavailable();

        Assert.Equal(200, Status(fromSuccess));
        Assert.Equal(404, Status(fromError));
        Assert.Equal(503, Status(fromBodyless));
        Assert.Equal(500, Status(default));
    }

    /// <summary>
    /// A switch over <c>Value</c>, which is the dispatch a generator emits against this type.
    /// </summary>
    /// <remarks>
    /// <c>default</c> is reachable - it bypasses every constructor and leaves <c>Value</c> null -
    /// so the fallback arm is not decoration.
    /// </remarks>
    private static int Status(GetPetResponse response) => response.Value switch
    {
        Pet => 200,
        GetPetNotFound => 404,
        GetPetServiceUnavailable => 503,
        _ => 500
    };
}
