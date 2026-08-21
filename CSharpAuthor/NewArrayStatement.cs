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
        outputContext.Write(_length.HasValue ? LiteralFormatter.FormatNumeric(_length.Value) : "");
        outputContext.Write("]");

        if (_components is { Length: > 0 })
        {
            outputContext.Write(" { ");
            _components.OutputCommaSeparatedList(outputContext);
            outputContext.Write(" }");
        }
        else if (!_length.HasValue)
        {
            // `new int[]` is CS1586 - an array creation needs a size or an initializer, and this
            // form has neither. An empty initializer is the one that says "no elements", which is
            // what a caller who handed over an empty collection asked for. The sized form is
            // untouched: `new int[0]` was always well formed.
            outputContext.Write(" { }");
        }
    }
}