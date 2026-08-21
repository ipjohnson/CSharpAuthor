using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// Puts an expression in statement position: on its own line, indented, terminated.
/// </summary>
/// <remarks>
/// Expression nodes write no terminator, because none of them knows whether it is being used as a
/// statement or as part of one. An <c>Invoke</c> added straight to a block therefore emits
/// <c>writer.WriteStartObject()</c> with nothing after it, and the generated file does not
/// compile. This is the wrapper that makes the difference, reached through
/// <see cref="BaseBlockDefinition.AddStatement{T}"/>.
/// </remarks>
public class IndentedStatementComponent : BaseOutputComponent
{
    private readonly IOutputComponent _component;

    public IndentedStatementComponent(IOutputComponent component)
    {
        _component = component;

        // This wrapper writes the indent, so anything inside it writing its own would indent the
        // line twice.
        if (component is BaseOutputComponent baseComponent)
        {
            baseComponent.Indented = false;
        }
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent();
        _component.WriteOutput(outputContext);
        outputContext.WriteLine(";");
    }
}