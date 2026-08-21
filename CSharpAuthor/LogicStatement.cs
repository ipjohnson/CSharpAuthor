using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public class LogicStatement : BaseOutputComponent
{
    private readonly string _logicStatement;
    private readonly IReadOnlyList<IOutputComponent> _outputComponents;

    public LogicStatement(string logicStatement, params object[] outputComponents)
        : this(logicStatement, CodeOutputComponent.GetAll(outputComponents).ToList())
    {
    }

    public LogicStatement(string logicStatement, IReadOnlyList<IOutputComponent> outputComponents)
    {
        _logicStatement = logicStatement;
        _outputComponents = outputComponents;
    }

    public bool PrintParentheses { get; set; } = true;

    /// <summary>
    /// Stops a condition writing the parentheses that the construct enclosing it already writes,
    /// so <c>if</c>, <c>else if</c> and <c>while</c> produce <c>if (a &amp;&amp; b)</c> rather than
    /// <c>if ((a &amp;&amp; b))</c>.
    /// </summary>
    /// <remarks>
    /// Only the outermost statement is touched. Nested ones keep their parentheses, because that
    /// is where they carry precedence rather than repeat it.
    /// </remarks>
    internal static void SuppressEnclosedParentheses(IOutputComponent condition)
    {
        if (condition is LogicStatement logicStatement)
        {
            logicStatement.PrintParentheses = false;
        }
    }

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (PrintParentheses)
        {
            outputContext.Write("(");
        }

        _outputComponents.OutputSeparatedList(outputContext, (context, component) => component.WriteOutput(context), _logicStatement);
            
        if (PrintParentheses)
        {
            outputContext.Write(")");
        }
    }
}