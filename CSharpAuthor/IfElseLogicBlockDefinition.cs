using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// An <c>if</c> and the <c>else if</c> and <c>else</c> arms after it.
/// </summary>
/// <remarks>
/// Statements added to this object are the <c>if</c> body; each of <see cref="ElseIf(string)"/> and
/// <see cref="Else"/> returns a block of its own. Built by
/// <see cref="BaseBlockDefinition.If(string)"/>.
/// </remarks>
public class IfElseLogicBlockDefinition : BaseBlockDefinition
{
    private readonly IOutputComponent _ifStatement;
    private List<IOutputComponent>? _elseStatements;
    private ElseBlockDefinition? _elseStatement;

    /// <summary>
    /// An <c>if</c> over <paramref name="ifStatement"/>. Prefer
    /// <see cref="BaseBlockDefinition.If(string)"/>, which builds one and attaches it to a block.
    /// </summary>
    /// <remarks>
    /// A <see cref="LogicStatement"/> passed here drops the parentheses it would print on its own,
    /// because the <c>if</c> supplies them.
    /// </remarks>
    public IfElseLogicBlockDefinition(IOutputComponent ifStatement)
    {
        _ifStatement = ifStatement;

        if (ifStatement is LogicStatement logicStatement)
        {
            logicStatement.PrintParentheses = false;
        }
    }

    /// <summary>
    /// An <c>else if</c> arm. Statements go on the block this returns.
    /// </summary>
    /// <remarks>
    /// Arms are written in the order they were added, before the <see cref="Else"/> whichever order
    /// the two were asked for in. The condition takes no parentheses of its own.
    /// </remarks>
    public BaseBlockDefinition ElseIf(string ifStatement)
    {
        return ElseIf(new CodeOutputComponent(ifStatement) { Indented = false });
    }

    /// <inheritdoc cref="ElseIf(string)" />
    /// <remarks>
    /// The overload for a condition built out of components - <see cref="SyntaxHelpers.And(object[])"/>,
    /// <see cref="SyntaxHelpers.Is"/> - so any type it mentions reaches the file as a type.
    /// </remarks>
    public BaseBlockDefinition ElseIf(IOutputComponent ifStatement)
    {
        var elseIf = new ElseIfBlockDefinition(ifStatement);

        _elseStatements ??= new List<IOutputComponent>();
        _elseStatements.Add(elseIf);

        return elseIf;
    }
    /// <summary>
    /// The <c>else</c> arm, written last. Statements go on the block this returns.
    /// </summary>
    /// <remarks>
    /// A second call returns a second, empty block and silently discards the first, so ask once and
    /// keep what comes back. That is the opposite of
    /// <see cref="SwitchBlockDefinition.AddDefault"/>, which reuses.
    /// </remarks>
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