using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class StaticInvokeGenericStatement : BaseOutputComponent
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly IReadOnlyList<ITypeDefinition> _genericArguments;
    private readonly string _methodName;
    private readonly IReadOnlyList<IOutputComponent> _parameters;

    public StaticInvokeGenericStatement(
        ITypeDefinition typeDefinition, 
        string methodName,
        IReadOnlyList<ITypeDefinition> genericArguments,
        IReadOnlyList<IOutputComponent> parameters)
    {
        _typeDefinition = typeDefinition;
        _genericArguments = genericArguments;
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
        outputContext.Write("<");
        _genericArguments.OutputCommaSeparatedList(outputContext);
        outputContext.Write(">(");
        _parameters.OutputCommaSeparatedList(outputContext);
        outputContext.Write(")");
    }
}