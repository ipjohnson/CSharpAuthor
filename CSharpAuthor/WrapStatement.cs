using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class WrapStatement : BaseOutputComponent
{
    private readonly IOutputComponent? _prefixString;
    private readonly IOutputComponent? _postfixString;
    private readonly IOutputComponent? _outputComponent;

    public WrapStatement(IOutputComponent outputComponent, string prefixString, string postfixString) : 
        this(outputComponent, CodeOutputComponent.Get(prefixString), CodeOutputComponent.Get(postfixString))
    {
        
    }

    public WrapStatement(IOutputComponent? outputComponent, IOutputComponent? prefixString, IOutputComponent? postfixString)
    {
        _outputComponent = outputComponent;
        _prefixString = prefixString;
        _postfixString = postfixString;
    }
        
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        _prefixString?.WriteOutput(outputContext);
        _outputComponent?.WriteOutput(outputContext);
        _postfixString?.WriteOutput(outputContext);
    }
}