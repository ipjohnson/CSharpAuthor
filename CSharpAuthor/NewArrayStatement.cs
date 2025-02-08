using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class NewArrayStatement : BaseOutputComponent
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly int? _length;
    private readonly IOutputComponent[] _components;

    public NewArrayStatement(ITypeDefinition typeDefinition, int length)
    {
        _typeDefinition = typeDefinition;
        _length = length;
        _components = Array.Empty<IOutputComponent>();
    }
    
    public NewArrayStatement(ITypeDefinition typeDefinition, params IOutputComponent[] components)
    {
        _typeDefinition = typeDefinition;
        _length = null;
        _components = components;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write("new ");
        outputContext.Write(_typeDefinition);
        outputContext.Write("[");
        outputContext.Write(_length?.ToString() ?? "");
        outputContext.Write("]");

        if (_components is { Length: > 0 })
        {
            outputContext.Write(" { ");
            _components.OutputCommaSeparatedList(outputContext);
            outputContext.Write(" }");
        }
    }
}