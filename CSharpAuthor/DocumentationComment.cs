using System;

namespace CSharpAuthor;

/// <summary>
/// Writes the <c>///</c> lines of a documentation comment.
/// </summary>
/// <remarks>
/// <para>
/// Every component that had a comment wrote it as a single <c>"/// " + Comment</c>, so a comment
/// containing a line break emitted continuation lines carrying neither the indent nor the marker.
/// That is a syntax error rather than a formatting problem, which left callers collapsing their
/// prose to one line before handing it over - and prose is exactly the thing that arrives with
/// paragraphs in it.
/// </para>
/// <para>
/// The single-line shape is unchanged, so nothing that was already correct moves.
/// </para>
/// </remarks>
internal static class DocumentationComment
{
    /// <summary>
    /// A <c>&lt;summary&gt;</c> element, one <c>///</c> line per line of the comment.
    /// </summary>
    public static void WriteSummary(Action<string> writeLine, string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        writeLine("/// <summary>");

        WriteBody(writeLine, comment!);

        writeLine("/// </summary>");
    }

    /// <summary>
    /// An element that fits on one line where its content does - <c>&lt;param&gt;</c>,
    /// <c>&lt;returns&gt;</c> - and opens out onto its own lines where it does not.
    /// </summary>
    public static void WriteElement(
        Action<string> writeLine, string openTag, string closeTag, string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        if (!IsMultiLine(comment!))
        {
            writeLine("/// " + openTag + comment!.Trim() + closeTag);

            return;
        }

        writeLine("/// " + openTag);

        WriteBody(writeLine, comment!);

        writeLine("/// " + closeTag);
    }

    private static void WriteBody(Action<string> writeLine, string comment)
    {
        foreach (var line in comment.Split('\n'))
        {
            var text = line.TrimEnd('\r').TrimEnd();

            // A blank line keeps its marker but not a trailing space, which is what an editor
            // strips on save and what would otherwise show up as a whitespace-only diff.
            writeLine(text.Length == 0 ? "///" : "/// " + text);
        }
    }

    private static bool IsMultiLine(string comment) =>
        comment.IndexOf('\n') >= 0 || comment.IndexOf('\r') >= 0;
}
