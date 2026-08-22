using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class InvokeGenericDefinition : BaseOutputComponent
{
    private readonly string _instance;
    private readonly string _methodName;
    private readonly IReadOnlyList<ITypeDefinition> _genericArguments;
    private readonly List<IOutputComponent> _arguments = new();

    public InvokeGenericDefinition(string instance, string methodName, IReadOnlyList<ITypeDefinition> genericArguments, params object[] arguments) 
    {
        _instance = instance;
        _methodName = methodName;
        _genericArguments = genericArguments;

        foreach (var argument in arguments)
        {
            AddArgument(argument);
        }
    }

    public void AddArgument(object argument)
    {
        AddArgument(CodeOutputComponent.Get(argument));
    }

    public void AddArgument(IOutputComponent argument)
    {
        _arguments.Add(argument);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (Indented)
        {
            outputContext.WriteIndent();
        }

        if (!string.IsNullOrEmpty(_instance))
        {
            // The receiver is an identifier and needs the same escaping the declaration got.
            // Written raw, a parameter named `event` was declared `@event` and then invoked as
            // `event.Go()` - the same name, escaped in one place and not the other, in one file.
            // EscapeReference walks a dotted path and leaves `this`, `base` and indexers alone.
            outputContext.Write(CSharpIdentifier.EscapeReference(_instance));
            outputContext.Write(".");
        }

        outputContext.Write(CSharpIdentifier.EscapeQualified(_methodName));
        outputContext.Write("<");
        _genericArguments.OutputCommaSeparatedList(outputContext, outputContext.Options.BreakInvokeLines);
        outputContext.Write(">");
        outputContext.Write("(");

        _arguments.OutputCommaSeparatedList(outputContext, outputContext.Options.BreakInvokeLines);

        outputContext.Write(")");
    }
}