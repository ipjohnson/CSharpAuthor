using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace CSharpAuthor;

public class IfElseLogicBlockDefinition : BaseBlockDefinition
{
    private readonly IOutputComponent _ifStatement;
    private List<IOutputComponent>? _elseStatements;
    private ElseBlockDefinition? _elseStatement;

    public IfElseLogicBlockDefinition(IOutputComponent ifStatement)
    {
        _ifStatement = ifStatement;

        if (ifStatement is LogicStatement logicStatement)
        {
            logicStatement.PrintParentheses = false;
        }
    }

    public BaseBlockDefinition ElseIf(string ifStatement)
    {
        return ElseIf(new CodeOutputComponent(ifStatement) { Indented = false });
    }

    public BaseBlockDefinition ElseIf(IOutputComponent ifStatement)
    {
        var elseIf = new ElseIfBlockDefinition(ifStatement);

        _elseStatements ??= new List<IOutputComponent>();
        _elseStatements.Add(elseIf);

        return elseIf;
    }
    public BaseBlockDefinition Else()
    {
        return _elseStatement = new ElseBlockDefinition();
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.WriteIndent("if (");
        _ifStatement.WriteOutput(outputContext);
        outputContext.WriteLine(")");
        WriteBlock(outputContext);

        if (_elseStatements != null)
        {
            foreach (var outputComponent in _elseStatements)
            {
                outputComponent.WriteOutput(outputContext);
            }
        }

        if (_elseStatement != null)
        {
            _elseStatement.WriteOutput(outputContext);
        }
    }

    private class ElseIfBlockDefinition : BaseBlockDefinition
    {
        private readonly IOutputComponent _ifStatement;

        public ElseIfBlockDefinition(IOutputComponent ifStatement)
        {
            _ifStatement = ifStatement;

            if (_ifStatement is LogicStatement logicStatement)
            {
                logicStatement.PrintParentheses = false;
            }
        }

        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent("else if (");
            _ifStatement.WriteOutput(outputContext);
            outputContext.WriteLine(")");

            WriteBlock(outputContext);
        }
    }

    private class ElseBlockDefinition : BaseBlockDefinition
    {
        protected override void WriteComponentOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndentedLine("else");
                
            WriteBlock(outputContext);
        }
    }
}