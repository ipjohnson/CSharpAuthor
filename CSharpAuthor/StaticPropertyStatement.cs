using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class StaticPropertyStatement : BaseOutputComponent
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly string _propertyName;

    public StaticPropertyStatement(ITypeDefinition typeDefinition, string propertyName)
    {
        _typeDefinition = typeDefinition;
        _propertyName = propertyName;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (Indented)
        {
            outputContext.WriteIndent();
        }

        outputContext.Write(_typeDefinition);
        outputContext.Write(".");
        outputContext.Write(_propertyName);
    }
}
