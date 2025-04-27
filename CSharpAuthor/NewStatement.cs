using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class NewStatement : InstanceDefinition
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly List<IOutputComponent> _arguments = new ();
    private readonly List<IOutputComponent> _initValues = new ();

    public NewStatement(ITypeDefinition typeDefinition, params object[] arguments) : base("")
    {
        _typeDefinition = typeDefinition;
        _arguments.AddRange(CodeOutputComponent.GetAll(arguments));
    }
        
    public void AddArgument(object argument)
    {
        AddArgument(CodeOutputComponent.Get(argument));
    }

    public void AddArgument(IOutputComponent argument)
    {
        _arguments.Add(argument);
    }

    public void AddInitValue(object initValue)
    {
        _initValues.Add(CodeOutputComponent.Get(initValue));
    }
        
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write("new ");
        outputContext.Write(_typeDefinition);
        outputContext.Write("(");
            
        _arguments.OutputCommaSeparatedList(outputContext, outputContext.Options.BreakInvokeLines);

        outputContext.Write(")");

        if (_initValues.Count > 0)
        {
            outputContext.Write("{ ");
            _initValues.OutputCommaSeparatedList(outputContext, outputContext.Options.BreakInvokeLines);
            outputContext.Write(" }");
        }
    }
}