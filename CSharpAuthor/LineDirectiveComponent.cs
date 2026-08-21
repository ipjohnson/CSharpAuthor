using System.Globalization;

namespace CSharpAuthor;

/// <summary>
/// A <c>#line</c> directive, which maps what follows back to the file it was generated from.
/// </summary>
/// <remarks>
/// <para>
/// Without one, a diagnostic in generated code points at the generated file - a place the user
/// cannot edit and did not write. With one, it points at the source that caused it, which is what
/// makes a generator's errors actionable.
/// </para>
/// <para>
/// The file name is written as a quoted literal and escaped, because a Windows path is full of
/// backslashes and an unescaped one would end the string early - the same defect
/// <see cref="SyntaxHelpers.QuoteString"/> exists to prevent.
/// </para>
/// </remarks>
public class LineDirectiveComponent : BaseOutputComponent
{
    private readonly string _text;

    private LineDirectiveComponent(string text)
    {
        _text = text;
    }

    /// <summary>
    /// <c>#line N "file"</c> - what follows came from line <paramref name="lineNumber"/> of
    /// <paramref name="fileName"/>.
    /// </summary>
    /// <param name="lineNumber">A 1-based line number.</param>
    /// <param name="fileName">
    /// The originating file. Omitted to keep whatever file was previously in force.
    /// </param>
    public static LineDirectiveComponent At(int lineNumber, string? fileName = null)
    {
        // Invariant: on a de-DE machine a grouped ToString() would write `#line 1.234`, which is
        // not a line number.
        var text = "#line " + lineNumber.ToString(CultureInfo.InvariantCulture);

        return new LineDirectiveComponent(
            fileName == null ? text : text + " " + SyntaxHelpers.QuoteString(fileName));
    }

    /// <summary>
    /// <c>#line default</c> - stop remapping, and go back to the real file and line.
    /// </summary>
    public static LineDirectiveComponent Default() => new("#line default");

    /// <summary>
    /// <c>#line hidden</c> - hide what follows from the debugger, so stepping walks over it.
    /// </summary>
    public static LineDirectiveComponent Hidden() => new("#line hidden");

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndentedLine(_text);
    }

    /// <inheritdoc cref="RegionComponent.ProcessLeadingTraits"/>
    protected override void ProcessLeadingTraits(IOutputContext outputContext)
    {
    }

    /// <inheritdoc cref="RegionComponent.ProcessLeadingTraits"/>
    protected override void ProcessTrailingTraits(IOutputContext outputContext)
    {
    }
}
