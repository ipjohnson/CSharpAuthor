using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class WrapStatement : BaseOutputComponent
{
    private readonly string _prefixString;
    private readonly string _postfixString;
    private readonly IOutputComponent _outputComponent;

    public WrapStatement(IOutputComponent outputComponent, string prefixString, string postfixString)
    {
        _outputComponent = outputComponent;
        _prefixString = prefixString;
        _postfixString = postfixString;
    }
        
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write(_prefixString);
        _outputComponent.WriteOutput(outputContext);
        outputContext.Write(_postfixString);
    }
}