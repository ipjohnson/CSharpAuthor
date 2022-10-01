using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class StaticInvokeStatement : BaseOutputComponent
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly string _methodName;
    private readonly IReadOnlyList<IOutputComponent> _parameters;

    public StaticInvokeStatement(ITypeDefinition typeDefinition, string methodName, IReadOnlyList<IOutputComponent> parameters)
    {
        _typeDefinition = typeDefinition;
        _methodName = methodName;
        _parameters = parameters;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (Indented)
        {
            outputContext.WriteIndent();
        }
            
        outputContext.Write(_typeDefinition);
        outputContext.Write(".");
        outputContext.Write(_methodName);
        outputContext.Write("(");
        _parameters.OutputCommaSeparatedList(outputContext);
        outputContext.Write(")");
    }
}