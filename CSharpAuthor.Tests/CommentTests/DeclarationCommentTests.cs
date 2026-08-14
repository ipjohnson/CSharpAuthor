using Xunit;

namespace CSharpAuthor.Tests.CommentTests;

/// <summary>
/// Declarations whose comments used to go nowhere.
/// </summary>
/// <remarks>
/// <c>Comment</c> is inherited from <see cref="BaseOutputComponent"/> by everything, and the base
/// <c>WriteComment</c> does nothing - so a component that never overrode it accepted a comment,
/// read as documented at the call site, and emitted none. Enums and their members were both in
/// that position, and a positional record could document itself but not the properties it declares
/// in its header.
/// </remarks>
public class DeclarationCommentTests
{
    [Fact]
    public void AnEnumWritesItsSummary()
    {
        var enumDefinition = new EnumDefinition("Status") { Comment = "How far along a pet is." };

        enumDefinition.AddValue("Available");

        AssertEqual.ContainsWithoutNewLine(
            "/// <summary>\n/// How far along a pet is.\n/// </summary>\npublic enum Status",
            Write(enumDefinition));
    }

    [Fact]
    public void AnEnumMemberWritesItsSummary()
    {
        var enumDefinition = new EnumDefinition("Status");

        enumDefinition.AddValue("Available").Comment = "Ready to be adopted.";
        enumDefinition.AddValue("Pending");

        var output = Write(enumDefinition);

        AssertEqual.ContainsWithoutNewLine("    /// <summary>\n    /// Ready to be adopted.\n    /// </summary>\n    Available,", output);

        // The undocumented member is untouched.
        AssertEqual.ContainsWithoutNewLine("    Pending,", output);
    }

    /// <summary>
    /// A positional record's properties are documented with <c>&lt;param&gt;</c> on the type,
    /// which is the only place the compiler accepts it.
    /// </summary>
    [Fact]
    public void ARecordDocumentsItsPositionalProperties()
    {
        var classDefinition = new ClassDefinition("Pet") {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true,
            Modifiers = ComponentModifier.Public,
            Comment = "A pet."
        };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id").Comment = "Opaque identifier.";
        constructor.AddParameter(typeof(string), "Name").Comment = "Display name.";

        AssertEqual.WithoutNewLine(
            "/// <summary>\n" +
            "/// A pet.\n" +
            "/// </summary>\n" +
            "/// <param name=\"Id\">Opaque identifier.</param>\n" +
            "/// <param name=\"Name\">Display name.</param>\n" +
            "public record Pet(string Id, string Name);\n",
            Write(classDefinition));
    }

    /// <summary>
    /// An undocumented parameter contributes no element, so a partly-documented record does not
    /// emit empty ones.
    /// </summary>
    [Fact]
    public void UndocumentedParametersAreSkipped()
    {
        var classDefinition = new ClassDefinition("Pet") {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true,
            Modifiers = ComponentModifier.Public,
            Comment = "A pet."
        };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id").Comment = "Opaque identifier.";
        constructor.AddParameter(typeof(string), "Name");

        var output = Write(classDefinition);

        AssertEqual.ContainsWithoutNewLine("""<param name="Id">""", output);
        Assert.DoesNotContain("""<param name="Name">""", output);
    }

    /// <summary>
    /// A class with no summary writes nothing, even where its parameters carry comments — the
    /// elements have no summary to hang from.
    /// </summary>
    [Fact]
    public void NoSummaryMeansNoComment()
    {
        var classDefinition = new ClassDefinition("Pet") {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true,
            Modifiers = ComponentModifier.Public
        };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id").Comment = "Opaque identifier.";

        Assert.DoesNotContain("///", Write(classDefinition));
    }

    private static string Write(IOutputComponent component)
    {
        var outputContext = new OutputContext();

        component.WriteOutput(outputContext);

        return outputContext.Output();
    }
}
