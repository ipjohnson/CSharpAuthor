using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class BaseStatement : BaseOutputComponent
{
    private readonly IReadOnlyList<IOutputComponent> _components;

    public BaseStatement(IReadOnlyList<IOutputComponent> components)
    {
        _components = components;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write("base(");
        _components.OutputCommaSeparatedList(outputContext);
        outputContext.Write(")");
    }
}