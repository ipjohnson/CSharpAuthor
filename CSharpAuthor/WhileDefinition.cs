using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class WhileDefinition : BaseBlockDefinition
{
    private readonly IOutputComponent _testStatement;

    public WhileDefinition(object testStatement)
    {
        _testStatement = CodeOutputComponent.Get(testStatement);
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent("while(");
        _testStatement.WriteOutput(outputContext);
        outputContext.WriteLine(")");

        WriteBlock(outputContext);
    }
}
