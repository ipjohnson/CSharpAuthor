using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// The conditional operator, <c>condition ? whenTrue : whenFalse</c>.
/// </summary>
/// <remarks>
/// Parenthesised by default, on the same reasoning as <see cref="LogicStatement"/>: the tree does
/// not track precedence, so the only choice that is never wrong is to bracket. Set
/// <see cref="PrintParentheses"/> to false where the position already makes the grouping clear.
/// </remarks>
public class ConditionalStatement : BaseOutputComponent
{
    private readonly IOutputComponent _condition;
    private readonly IOutputComponent _whenTrue;
    private readonly IOutputComponent _whenFalse;

    public ConditionalStatement(object condition, object whenTrue, object whenFalse)
    {
        _condition = CodeOutputComponent.Get(condition);
        _whenTrue = CodeOutputComponent.Get(whenTrue);
        _whenFalse = CodeOutputComponent.Get(whenFalse);

        // The branches sit inside this operator's own brackets, so their brackets would only
        // repeat what these already say.
        LogicStatement.SuppressEnclosedParentheses(_condition);
    }

    public bool PrintParentheses { get; set; } = true;

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        if (PrintParentheses)
        {
            outputContext.Write("(");
        }

        _condition.WriteOutput(outputContext);
        outputContext.Write(" ? ");
        _whenTrue.WriteOutput(outputContext);
        outputContext.Write(" : ");
        _whenFalse.WriteOutput(outputContext);

        if (PrintParentheses)
        {
            outputContext.Write(")");
        }
    }
}
