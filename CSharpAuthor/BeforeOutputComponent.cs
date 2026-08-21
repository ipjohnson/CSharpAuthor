using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class PrefixOutputComponent : BaseOutputComponent, IPrecedenceComponent
{
    private readonly string _prefix;
    private readonly IOutputComponent _awaitableOutputComponent;

    public PrefixOutputComponent(string prefix, IOutputComponent awaitableOutputComponent)
    {
        _prefix = prefix;
        _awaitableOutputComponent = awaitableOutputComponent;
    }

    /// <summary>
    /// Every prefix this carries - <c>await</c>, and the unary operators - binds looser than a
    /// member access, so composing one onto it needs parentheses: <c>await x.Y</c> awaits
    /// <c>x.Y</c>, not <c>x</c>.
    /// </summary>
    int IPrecedenceComponent.Precedence => Expressions.ExPrecedence.Unary;

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write(_prefix);
        _awaitableOutputComponent.WriteOutput(outputContext);
    }
}