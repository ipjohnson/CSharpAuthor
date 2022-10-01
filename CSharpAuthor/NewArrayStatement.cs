using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class NewArrayStatement : BaseOutputComponent
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly int _length;

    public NewArrayStatement(ITypeDefinition typeDefinition, int length)
    {
        _typeDefinition = typeDefinition;
        _length = length;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write("new ");
        outputContext.Write(_typeDefinition);
        outputContext.Write("[");
        outputContext.Write(_length.ToString());
        outputContext.Write("]");
    }
}