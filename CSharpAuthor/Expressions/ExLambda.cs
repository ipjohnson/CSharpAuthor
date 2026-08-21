using System;
using System.Collections.Generic;

namespace CSharpAuthor.Expressions;

/// <summary>
/// Builds all four lambda shapes — implicit or explicit parameters, expression or block
/// body — plus <c>async</c>, <c>static</c> and an explicit return type.
/// </summary>
/// <remarks>
/// A lambda sits at the bottom of the precedence ladder, alongside assignment, which is
/// why <c>(x =&gt; x)(3)</c> needs its brackets and why a lambda passed as an argument
/// does not.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
sealed class ExLambda
{
    private readonly List<KeyValuePair<ITypeDefinition?, string>> _parameters =
        new List<KeyValuePair<ITypeDefinition?, string>>();

    private bool _explicitParentheses;
    private bool _isAsync;
    private bool _isStatic;
    private ITypeDefinition? _returnType;

    private ExLambda()
    {
    }

    /// <summary>A lambda with implicitly typed parameters: <c>x =&gt; …</c>, <c>(x, y) =&gt; …</c>.</summary>
    public static ExLambda Of(params string[] parameters)
    {
        var lambda = new ExLambda();

        foreach (var parameter in parameters)
        {
            lambda._parameters.Add(new KeyValuePair<ITypeDefinition?, string>(null, parameter));
        }

        // A single implicit parameter is the only shape that may drop its brackets.
        lambda._explicitParentheses = parameters.Length != 1;

        return lambda;
    }

    /// <summary>A lambda with explicitly typed parameters: <c>(int x) =&gt; …</c>.</summary>
    public static ExLambda Typed(params KeyValuePair<ITypeDefinition, string>[] parameters)
    {
        var lambda = new ExLambda { _explicitParentheses = true };

        foreach (var parameter in parameters)
        {
            lambda._parameters.Add(new KeyValuePair<ITypeDefinition?, string>(parameter.Key, parameter.Value));
        }

        return lambda;
    }

    /// <summary>Keeps the brackets even around a single implicit parameter.</summary>
    public ExLambda Parenthesized()
    {
        _explicitParentheses = true;
        return this;
    }

    /// <summary><c>async</c></summary>
    public ExLambda Async()
    {
        _isAsync = true;
        return this;
    }

    /// <summary><c>static</c></summary>
    public ExLambda Static()
    {
        _isStatic = true;
        return this;
    }

    /// <summary>An explicit return type (C# 10): <c>int (x) =&gt; …</c>.</summary>
    public ExLambda Returns(ITypeDefinition returnType)
    {
        _returnType = returnType;
        _explicitParentheses = true;
        return this;
    }

    /// <summary>An expression-bodied lambda: <c>x =&gt; body</c>.</summary>
    public Ex Body(Ex body)
    {
        return new Ex(ExPrecedence.Assignment, c =>
        {
            WriteHeader(c);

            // The body is a full expression: a nested lambda or an assignment needs nothing.
            Ex.WriteOperand(c, body, ExPrecedence.Assignment);
        });
    }

    /// <summary>A statement-bodied lambda: <c>x =&gt; { … }</c>.</summary>
    public Ex Block(params IOutputComponent[] statements)
    {
        return new Ex(ExPrecedence.Assignment, c =>
        {
            WriteHeader(c, trailingSpace: false);
            ExBlock.Write(c, statements);
        });
    }

    private void WriteHeader(IOutputContext context, bool trailingSpace = true)
    {
        if (_isStatic)
        {
            context.Write("static ");
        }

        if (_isAsync)
        {
            context.Write("async ");
        }

        if (_returnType != null)
        {
            context.Write(_returnType);
            context.Write(" ");
        }

        WriteParameters(context);

        context.Write(trailingSpace ? " => " : " =>");
    }

    private void WriteParameters(IOutputContext context)
    {
        if (!_explicitParentheses && _parameters.Count == 1 && _parameters[0].Key == null)
        {
            context.Write(CSharpText.Identifier(_parameters[0].Value));
            return;
        }

        context.Write("(");

        for (var i = 0; i < _parameters.Count; i++)
        {
            if (i > 0)
            {
                context.Write(", ");
            }

            if (_parameters[i].Key != null)
            {
                context.Write(_parameters[i].Key!);
                context.Write(" ");
            }

            context.Write(CSharpText.Identifier(_parameters[i].Value));
        }

        context.Write(")");
    }
}

/// <summary>Writes a braced statement block at the context's current indent.</summary>
internal static class ExBlock
{
    public static void Write(IOutputContext context, IReadOnlyList<IOutputComponent> statements)
    {
        context.WriteLine();
        context.WriteIndent("{");
        context.WriteLine();
        context.IncrementIndent();

        if (statements != null)
        {
            foreach (var statement in statements)
            {
                statement.WriteOutput(context);
            }
        }

        context.DecrementIndent();
        context.WriteIndent("}");
    }
}
