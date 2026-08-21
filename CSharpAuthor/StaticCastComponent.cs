using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public class StaticCastComponent : BaseOutputComponent, IPrecedenceComponent
{
    private readonly ITypeDefinition _typeDefinition;
    private readonly IOutputComponent _value;

    public StaticCastComponent(ITypeDefinition typeDefinition, object value)
    {
        _typeDefinition = typeDefinition;
        _value = CodeOutputComponent.Get(value);
    }

    /// <summary>
    /// A cast is a unary operator, so a member access, an invocation or an index built on top of
    /// one has to parenthesise it: <c>(Dog)animal.Breed</c> reads as <c>(Dog)(animal.Breed)</c>.
    /// </summary>
    int IPrecedenceComponent.Precedence => Expressions.ExPrecedence.Unary;

    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        outputContext.Write("(");
        outputContext.Write(_typeDefinition);
        outputContext.Write(")");
        _value.WriteOutput(outputContext);
    }
}