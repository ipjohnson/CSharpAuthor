using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class DeclarationStatement : BaseOutputComponent
{
    private ITypeDefinition _typeDefinition;
    private IOutputComponent _outputComponent;

    public DeclarationStatement(ITypeDefinition typeDefinition, IOutputComponent outputComponent)
    {
        _typeDefinition = typeDefinition;
        _outputComponent = outputComponent;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (Indented)
        {
            outputContext.WriteIndent();
        }

        outputContext.Write(_typeDefinition);
        outputContext.WriteSpace();
        _outputComponent.WriteOutput(outputContext);
    }
}