using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// One member of an <see cref="EnumDefinition"/>.
/// </summary>
public class EnumValueDefinition : BaseOutputComponent
{
    private readonly string _enumValueName;

    /// <summary>
    /// A member named <paramref name="enumValueName"/>. Prefer
    /// <see cref="EnumDefinition.AddValue(string)"/>, which builds one and attaches it.
    /// </summary>
    public EnumValueDefinition(string enumValueName)
    {
        _enumValueName = enumValueName;
    }

    /// <summary>
    /// The explicit value, or null to let the compiler number it.
    /// </summary>
    /// <remarks>
    /// Null means "no <c>= n</c>", not the value zero - a member that should be zero has to say so.
    /// </remarks>
    public object? Value { get; set; }

    /// <summary>
    /// The member's own documentation, which is where a specification's description of a single
    /// enum value belongs. Silently dropped before this existed.
    /// </summary>
    protected override void WriteComment(IOutputContext outputContext)
    {
        DocumentationComment.WriteSummary(outputContext.WriteIndentedLine, Comment);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent();
        outputContext.Write(CSharpIdentifier.Escape(_enumValueName));

        if (Value != null)
        {
            outputContext.Write(" = ");
            outputContext.Write(LiteralFormatter.Format(Value));
        }

        outputContext.WriteLine(",");
    }
}