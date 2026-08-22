using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class IndentedStatementComponent : BaseOutputComponent
{
    private readonly IOutputComponent _component;

    public IndentedStatementComponent(IOutputComponent component)
    {
        _component = component;
    }

    /// <summary>The statement this indents and terminates.</summary>
    internal IOutputComponent Inner => _component;

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent();
        _component.WriteOutput(outputContext);
        outputContext.WriteLine(";");
    }
}