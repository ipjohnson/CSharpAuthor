using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class AppendStatement : BaseOutputComponent
{
    private readonly string _appendString;
    private readonly IOutputComponent _outputComponent;

    public AppendStatement(string appendString, IOutputComponent outputComponent)
    {
        _appendString = appendString;
        _outputComponent = outputComponent;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write(_appendString);
        _outputComponent.WriteOutput(outputContext);
    }
}