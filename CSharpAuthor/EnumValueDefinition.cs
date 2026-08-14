using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class EnumValueDefinition : BaseOutputComponent
{
    private readonly string _enumValueName;

    public EnumValueDefinition(string enumValueName)
    {
        _enumValueName = enumValueName;
    }

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
        outputContext.Write(_enumValueName);

        if (Value != null)
        {
            outputContext.Write(" = ");
            outputContext.Write(Value.ToString());
        }

        outputContext.WriteLine(",");
    }
}