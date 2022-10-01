using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class StaticCastComponent : BaseOutputComponent
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly IOutputComponent _value;

    public StaticCastComponent(ITypeDefinition typeDefinition, object value)
    {
        _typeDefinition = typeDefinition;
        _value = CodeOutputComponent.Get(value);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write("(");
        outputContext.Write(_typeDefinition);
        outputContext.Write(")");
        _value.WriteOutput(outputContext);
    }
}