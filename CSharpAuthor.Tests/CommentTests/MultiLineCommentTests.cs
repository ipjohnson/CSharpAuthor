using Xunit;

namespace CSharpAuthor.Tests.CommentTests;

/// <summary>
/// Comments containing line breaks.
/// </summary>
/// <remarks>
/// A comment used to be written as one <c>"/// " + Comment</c>, so a line break in it emitted a
/// continuation line carrying neither the indent nor the marker - output that does not compile.
/// Callers worked around it by flattening their prose, which is a poor answer for the one kind of
/// text that arrives with paragraphs in it.
/// </remarks>
public class MultiLineCommentTests
{
    [Fact]
    public void EveryLineOfASummaryCarriesTheMarker()
    {
        var classDefinition = new ClassDefinition("MyClass") {
            Comment = "First line.\nSecond line."
        };

        AssertEqual.WithoutNewLine(
            "/// <summary>\n/// First line.\n/// Second line.\n/// </summary>\npublic class MyClass\n{\n}\n",
            Write(classDefinition));
    }

    /// <summary>
    /// Windows line endings split the same way, rather than leaving a stray carriage return inside
    /// the comment.
    /// </summary>
    [Fact]
    public void CarriageReturnsAreNotCarriedThrough()
    {
        var classDefinition = new ClassDefinition("MyClass") {
            Comment = "First line.\r\nSecond line."
        };

        // Real line breaks first: AppendLine writes Environment.NewLine, so on Windows the output
        // carries carriage returns by design. What must not survive is one inside the comment's own
        // text, which is what is left once the line breaks are taken out.
        Assert.DoesNotContain("\r", Write(classDefinition).Replace("\r\n", "\n"));

        AssertEqual.ContainsWithoutNewLine("/// First line.\n/// Second line.", Write(classDefinition));
    }

    /// <summary>
    /// A blank line between paragraphs keeps its marker, and does not leave trailing whitespace
    /// behind it.
    /// </summary>
    [Fact]
    public void ABlankLineKeepsItsMarkerWithoutTrailingSpace()
    {
        var classDefinition = new ClassDefinition("MyClass") {
            Comment = "First paragraph.\n\nSecond paragraph."
        };

        AssertEqual.ContainsWithoutNewLine("/// First paragraph.\n///\n/// Second paragraph.", Write(classDefinition));
    }

    /// <summary>
    /// Indentation applies to every line, not only the first.
    /// </summary>
    [Fact]
    public void NestedComponentsIndentEveryLine()
    {
        var classDefinition = new ClassDefinition("MyClass");

        var method = classDefinition.AddMethod("MyMethod");
        method.Comment = "First line.\nSecond line.";

        AssertEqual.ContainsWithoutNewLine("    /// First line.\n    /// Second line.", Write(classDefinition));
    }

    /// <summary>
    /// A single-line comment is written exactly as it always was.
    /// </summary>
    [Fact]
    public void ASingleLineCommentIsUnchanged()
    {
        var classDefinition = new ClassDefinition("MyClass") { Comment = "One line." };

        AssertEqual.WithoutNewLine(
            "/// <summary>\n/// One line.\n/// </summary>\npublic class MyClass\n{\n}\n",
            Write(classDefinition));
    }

    /// <summary>
    /// A <c>&lt;param&gt;</c> stays on one line where it fits, and opens out where it does not.
    /// </summary>
    [Fact]
    public void ParamElementsOpenOutOnlyWhenTheyHaveTo()
    {
        var classDefinition = new ClassDefinition("MyClass");

        var method = classDefinition.AddMethod("MyMethod");
        method.Comment = "A method.";
        method.AddParameter(typeof(int), "single").Comment = "On one line.";
        method.AddParameter(typeof(int), "wrapped").Comment = "First line.\nSecond line.";

        var output = Write(classDefinition);

        AssertEqual.ContainsWithoutNewLine("""/// <param name="single">On one line.</param>""", output);
        AssertEqual.ContainsWithoutNewLine(
            "/// <param name=\"wrapped\">\n    /// First line.\n    /// Second line.\n    /// </param>",
            output);
    }

    private static string Write(ClassDefinition classDefinition)
    {
        var outputContext = new OutputContext();

        classDefinition.WriteOutput(outputContext);

        return outputContext.Output();
    }
}
