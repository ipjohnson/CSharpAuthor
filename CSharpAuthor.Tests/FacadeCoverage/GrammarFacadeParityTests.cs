using System;
using System.Collections.Generic;
using System.Linq;
using CSharpAuthor.Syntax;
using Xunit;

namespace CSharpAuthor.Tests.FacadeCoverage;

/// <summary>
/// Every kind of type the grammar can declare has to be reachable from the hand-written facade,
/// or be listed here as deliberately unreachable.
/// </summary>
/// <remarks>
/// <para>
/// The node layer under <c>CSharpAuthor.Syntax</c> is generated from Roslyn's <c>Syntax.xml</c>, so
/// it gains a node the moment C# gains a construct - for free, with no one deciding anything. The
/// facade - <see cref="ClassDefinition"/>, <see cref="ClassKeyword"/>, <see cref="CSharpFileDefinition"/>
/// and friends - is hand-written and gains nothing automatically. Those two facts together are a
/// trap: node coverage reads as 100% while the API a caller actually uses is missing the construct
/// entirely.
/// </para>
/// <para>
/// That is not hypothetical. <c>ClassKeyword.Union</c> shipped in 1.2.0, the 2.0 branch was cut
/// before it, and nothing failed - the grammar had <c>UnionDeclaration</c> the whole time. It was
/// found by migrating a consumer that used it, which is far too late and entirely by luck.
/// </para>
/// <para>
/// So this test is a ratchet, not a coverage metric. A new type-declaration node appearing in the
/// grammar fails it, and the fix is a deliberate choice: support the construct in the facade, or add
/// it below with the reason it is not supported. Either way somebody decides, rather than nobody
/// noticing.
/// </para>
/// </remarks>
public class GrammarFacadeParityTests
{
    /// <summary>
    /// Every type-declaration node the grammar emits, and how the facade reaches it.
    /// </summary>
    /// <remarks>
    /// Adding a row is a decision. Leaving one out is not an option the test allows.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> FacadeReach =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ClassDeclaration"] = "CSharpFileDefinition.AddClass, ClassKeyword.Class",
            ["StructDeclaration"] = "ClassKeyword.Struct and ClassKeyword.RecordStruct",
            ["InterfaceDeclaration"] = "CSharpFileDefinition.AddInterface",
            ["RecordDeclaration"] = "CSharpFileDefinition.AddRecord, ClassKeyword.Record",
            ["EnumDeclaration"] = "CSharpFileDefinition.AddEnum",
            ["UnionDeclaration"] = "ClassKeyword.Union and ClassDefinition.AddUnionCase",

            // Not reachable, on purpose, and recorded rather than forgotten.
            //
            // An extension block is not a type - it is a container for members that attach to some
            // other type - so it has no ClassKeyword and no Add* on the file. Reaching it needs a
            // shape the facade does not have, which is a feature decision and not an oversight.
            // The grammar can emit one today; the facade cannot.
            ["ExtensionBlockDeclaration"] = NotReachable
        };

    private const string NotReachable = "(deliberately not reachable from the facade)";

    private static IEnumerable<Type> GrammarTypeDeclarations() =>
        typeof(ClassDeclaration).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "CSharpAuthor.Syntax"
                        && !t.IsAbstract
                        && t.GetInterfaces().Any(i =>
                            i.Name == "ITypeDeclaration" || i.Name == "IBaseTypeDeclaration"))
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    /// <summary>
    /// The ratchet. A construct the grammar learned and the facade did not fails here.
    /// </summary>
    [Fact]
    public void EveryTypeTheGrammarCanDeclareIsAccountedFor()
    {
        var unaccounted = GrammarTypeDeclarations()
            .Select(t => t.Name)
            .Where(name => !FacadeReach.ContainsKey(name))
            .ToList();

        Assert.True(
            unaccounted.Count == 0,
            "The grammar can declare " + string.Join(", ", unaccounted) + ", and the facade has no "
            + "entry for it. This is the gap that let ClassKeyword.Union go missing for a whole "
            + "release. Either support it in the facade, or add it to FacadeReach with the reason "
            + "it is not supported - but decide, do not delete this assertion.");
    }

    /// <summary>
    /// And the reverse: a row here naming a node the grammar no longer has is stale.
    /// </summary>
    [Fact]
    public void NoRowDescribesANodeTheGrammarNoLongerHas()
    {
        var live = GrammarTypeDeclarations().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        var stale = FacadeReach.Keys.Where(k => !live.Contains(k)).ToList();

        Assert.True(
            stale.Count == 0,
            "FacadeReach still lists " + string.Join(", ", stale) + ", which the grammar no longer "
            + "declares. Regeneration changed the node set; this map has not kept up.");
    }

    /// <summary>
    /// The union case specifically, because it is the one that got away.
    /// </summary>
    [Fact]
    public void TheFacadeCanActuallyDeclareEachSupportedKind()
    {
        var file = new CSharpFileDefinition("Parity");

        Assert.NotNull(file.AddClass("AClass"));
        Assert.NotNull(file.AddInterface("AnInterface"));
        Assert.NotNull(file.AddEnum("AnEnum"));
        Assert.NotNull(file.AddRecord("ARecord"));

        var aStruct = file.AddClass("AStruct");
        aStruct.TypeKeyword = ClassKeyword.Struct;

        var aUnion = file.AddClass("AUnion");
        aUnion.TypeKeyword = ClassKeyword.Union;
        aUnion.AddUnionCase(TypeDefinition.Get("Parity", "ACase"));

        var context = new OutputContext();

        file.WriteOutput(context);

        var output = context.Output();

        Assert.Contains("class AClass", output);
        Assert.Contains("interface AnInterface", output);
        Assert.Contains("enum AnEnum", output);
        Assert.Contains("record ARecord", output);
        Assert.Contains("struct AStruct", output);
        Assert.Contains("union AUnion(ACase);", output);
    }
}
