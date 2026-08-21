using System;
using System.Collections.Generic;

namespace CSharpAuthor.Expressions;

public sealed partial class Ex
{
    // ---------------------------------------------------------------------------------
    // Lambdas
    // ---------------------------------------------------------------------------------

    /// <summary><c>x =&gt; body</c></summary>
    public static Ex Lambda(string parameter, Ex body) => ExLambda.Of(parameter).Body(body);

    /// <summary><c>(x, y) =&gt; body</c>, or <c>() =&gt; body</c> when there are no parameters.</summary>
    public static Ex Lambda(string[] parameters, Ex body) => ExLambda.Of(parameters).Body(body);

    /// <summary><c>(T x) =&gt; body</c></summary>
    public static Ex Lambda(KeyValuePair<ITypeDefinition, string>[] parameters, Ex body) =>
        ExLambda.Typed(parameters).Body(body);

    /// <summary><c>x =&gt; { … }</c></summary>
    public static Ex LambdaBlock(string[] parameters, params IOutputComponent[] statements) =>
        ExLambda.Of(parameters).Block(statements);

    /// <summary><c>(T x) =&gt; { … }</c></summary>
    public static Ex LambdaBlock(KeyValuePair<ITypeDefinition, string>[] parameters, params IOutputComponent[] statements) =>
        ExLambda.Typed(parameters).Block(statements);

    /// <summary>
    /// <c>delegate (T x) { … }</c> — an anonymous method. Pass a null parameter list for
    /// the bare <c>delegate { … }</c> form, which matches any signature.
    /// </summary>
    public static Ex AnonymousMethod(
        IReadOnlyList<KeyValuePair<ITypeDefinition, string>>? parameters,
        params IOutputComponent[] statements)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("delegate");

            if (parameters != null)
            {
                c.Write(" (");

                for (var i = 0; i < parameters.Count; i++)
                {
                    if (i > 0)
                    {
                        c.Write(", ");
                    }

                    c.Write(parameters[i].Key);
                    c.Write(" ");
                    c.Write(CSharpText.Identifier(parameters[i].Value));
                }

                c.Write(")");
            }

            ExBlock.Write(c, statements);
        });
    }

    // ---------------------------------------------------------------------------------
    // Switch expressions
    // ---------------------------------------------------------------------------------

    /// <summary>One arm of a switch expression: <c>pattern =&gt; result</c>.</summary>
    public static ExSwitchArm Arm(Pat pattern, Ex result) => new ExSwitchArm(pattern, null, result);

    /// <summary>A guarded arm: <c>pattern when guard =&gt; result</c>.</summary>
    public static ExSwitchArm Arm(Pat pattern, Ex guard, Ex result) => new ExSwitchArm(pattern, guard, result);

    /// <summary>
    /// <c>governing switch { … }</c>, one arm per line.
    /// </summary>
    /// <remarks>
    /// The governing expression binds tighter than it reads: <c>switch</c> is above
    /// multiplicative on the ladder, so <c>a + b switch { … }</c> means
    /// <c>a + (b switch { … })</c> and this method brackets the sum.
    /// </remarks>
    public static Ex Switch(Ex governing, params ExSwitchArm[] arms)
    {
        return new Ex(ExPrecedence.SwitchWith, c =>
        {
            WriteOperand(c, governing, ExPrecedence.SwitchWith);
            c.Write(" switch");
            c.WriteLine();
            c.WriteIndent("{");
            c.WriteLine();
            c.IncrementIndent();

            for (var i = 0; i < arms.Length; i++)
            {
                c.WriteIndent();
                arms[i].WriteOutput(c);

                if (i < arms.Length - 1)
                {
                    c.Write(",");
                }

                c.WriteLine();
            }

            c.DecrementIndent();
            c.WriteIndent("}");
        });
    }

    /// <summary><c>governing switch { a =&gt; b, _ =&gt; c }</c> on a single line.</summary>
    public static Ex SwitchInline(Ex governing, params ExSwitchArm[] arms)
    {
        return new Ex(ExPrecedence.SwitchWith, c =>
        {
            WriteOperand(c, governing, ExPrecedence.SwitchWith);
            c.Write(" switch { ");

            for (var i = 0; i < arms.Length; i++)
            {
                if (i > 0)
                {
                    c.Write(", ");
                }

                arms[i].WriteOutput(c);
            }

            c.Write(" }");
        });
    }
}

/// <summary>One arm of a switch expression.</summary>
public sealed class ExSwitchArm : IOutputComponent
{
    private readonly Pat _pattern;
    private readonly Ex? _guard;
    private readonly Ex _result;

    internal ExSwitchArm(Pat pattern, Ex? guard, Ex result)
    {
        _pattern = pattern;
        _guard = guard;
        _result = result;
    }

    /// <inheritdoc />
    public void AddUsingNamespace(string ns)
    {
    }

    /// <inheritdoc />
    public void WriteOutput(IOutputContext outputContext)
    {
        _pattern.WriteOutput(outputContext);

        if (_guard != null)
        {
            outputContext.Write(" when ");
            Ex.WriteOperand(outputContext, _guard, ExPrecedence.Assignment);
        }

        outputContext.Write(" => ");

        // An arm result is a full expression; a nested switch or lambda needs no brackets.
        Ex.WriteOperand(outputContext, _result, ExPrecedence.Assignment);
    }
}
