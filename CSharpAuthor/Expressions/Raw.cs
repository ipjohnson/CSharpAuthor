using System;
using System.Collections.Generic;

namespace CSharpAuthor.Expressions;

/// <summary>
/// The escape hatch, and the reason it is safe to have one: literal text that is still a
/// node.
/// </summary>
/// <remarks>
/// <para>
/// <c>Raw</c> stands wherever an expression, a statement or a pattern is expected, so
/// dropping to text costs one node rather than the file. Two properties make that true:
/// </para>
/// <list type="number">
/// <item><description>
/// It holds <see cref="ITypeDefinition"/> references <b>unrendered</b> and writes them
/// through <see cref="IOutputContext.Write(ITypeDefinition)"/>. A type inside a
/// <c>Raw</c> therefore still contributes its namespace, still honours
/// <see cref="TypeOutputMode"/>, and still participates in whatever name plan the context
/// builds — see <see cref="TypeReferences"/>, which is that plan's input.
/// </description></item>
/// <item><description>
/// It works out its own <see cref="Precedence"/> from the shape of the text, so a
/// <c>Raw</c> used as an operand is bracketed when it needs to be. <c>Raw("a + b")</c>
/// multiplied by <c>c</c> emits <c>(a + b) * c</c>, not <c>a + b * c</c>. When the shape
/// cannot be proven the answer is <see cref="ExPrecedence.Lowest"/>, which brackets: the
/// failure mode is a redundant pair of parentheses, never a changed program.
/// </description></item>
/// </list>
/// <para>
/// Declared <c>partial</c> so the generated grammar's <c>IExpression</c> /
/// <c>IStatement</c> / <c>IPattern</c> interfaces can be attached at integration without
/// this file being edited.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
sealed partial class Raw : IExpressionNode, IStatementNode, IPatternNode
{
    private readonly object?[] _parts;
    private readonly int? _assertedPrecedence;

    private bool _shapeComputed;
    private int _shapePrecedence;
    private ExFlags _shapeFlags;

    /// <summary>Literal text, type references and nested components, in order.</summary>
    public Raw(params object?[] parts)
        : this(null, parts)
    {
    }

    private Raw(int? assertedPrecedence, object?[]? parts)
    {
        _assertedPrecedence = assertedPrecedence;
        _parts = parts ?? new object?[0];
    }

    /// <summary>A fragment whose precedence you assert rather than infer.</summary>
    public static Raw At(int precedence, params object?[] parts) => new Raw(precedence, parts);

    /// <summary>A fragment asserted to be a primary expression — an identifier or a member chain.</summary>
    public static Raw Primary(params object?[] parts) => new Raw(ExPrecedence.Primary, parts);

    /// <summary>A single value, rendered by <see cref="object.ToString"/> unless it is a node or a type.</summary>
    public static Raw From(object? value) => new Raw(null, new[] { value });

    /// <summary>The parts, in order, as supplied.</summary>
    public IReadOnlyList<object?> Parts => _parts;

    /// <inheritdoc />
    public int Precedence
    {
        get
        {
            if (_assertedPrecedence.HasValue)
            {
                return _assertedPrecedence.Value;
            }

            EnsureShape();

            return _shapePrecedence;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A raw fragment is a leaf as far as patterns go; combinators bracket it.
    /// </remarks>
    public int PatternPrecedence => _assertedPrecedence ?? PatPrecedence.Lowest;

    internal ExFlags Flags
    {
        get
        {
            if (_assertedPrecedence.HasValue)
            {
                return ExFlags.None;
            }

            EnsureShape();

            return _shapeFlags;
        }
    }

    /// <summary>
    /// Every unrendered type this fragment carries, including those inside nested
    /// fragments. This is what lets a <c>Raw</c> take part in the name plan: the types are
    /// visible to the context before anything is written, and they are still types, not
    /// text.
    /// </summary>
    public IEnumerable<ITypeDefinition> TypeReferences
    {
        get
        {
            foreach (var part in _parts)
            {
                if (part is ITypeDefinition type)
                {
                    yield return type;
                }
                else if (part is Raw nested)
                {
                    foreach (var nestedType in nested.TypeReferences)
                    {
                        yield return nestedType;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public void AddUsingNamespace(string ns)
    {
        // Invariant 1: a type reaches the output only through IOutputContext.Write, and
        // namespaces are derived from that. A Raw announces nothing.
    }

    /// <inheritdoc />
    public void WriteOutput(IOutputContext outputContext)
    {
        foreach (var part in _parts)
        {
            switch (part)
            {
                case null:
                    continue;

                // Still deferred: the context decides short name, full name or global::,
                // and records the namespace, at serialization time.
                case ITypeDefinition type:
                    outputContext.Write(type);
                    continue;

                case IOutputComponent component:
                    component.WriteOutput(outputContext);
                    continue;

                case string text:
                    outputContext.Write(text);
                    continue;

                default:
                    outputContext.Write(Convert.ToString(part, System.Globalization.CultureInfo.InvariantCulture) ?? "");
                    continue;
            }
        }
    }

    /// <summary>This fragment as an <see cref="Ex"/>, carrying the precedence it could prove.</summary>
    public Ex ToExpression()
    {
        return new Ex(Precedence, WriteOutput, Flags);
    }

    /// <summary>Renders to a string. A debugging and testing convenience.</summary>
    public string Render(OutputContextOptions? options = null)
    {
        var context = new OutputContext(options);

        WriteOutput(context);

        return context.Output();
    }

    private void EnsureShape()
    {
        if (_shapeComputed)
        {
            return;
        }

        _shapePrecedence = RawShape.Classify(BuildShapeText(), out _shapeFlags);
        _shapeComputed = true;
    }

    /// <summary>
    /// The text as the classifier sees it. Parts that are not literal text are stood in for
    /// by a placeholder: a type is an identifier, and a node contributes an identifier when
    /// it is at least primary and an unparsable character otherwise, which forces the
    /// conservative answer.
    /// </summary>
    private string BuildShapeText()
    {
        var builder = new System.Text.StringBuilder();

        foreach (var part in _parts)
        {
            switch (part)
            {
                case null:
                    continue;
                case string text:
                    builder.Append(text);
                    continue;
                case ITypeDefinition _:
                    builder.Append("X");
                    continue;
                case Ex expression:
                    // "#" cannot start or continue any expression, so a loose node forces the
                    // classifier to give up - which brackets.
                    builder.Append(expression.Precedence >= ExPrecedence.NullChain ? "X" : "#");
                    continue;
                case Raw nested:
                    builder.Append(nested.Precedence >= ExPrecedence.NullChain ? "X" : "#");
                    continue;
                case IOutputComponent _:
                    // A V1 component: opaque, and by V1's own convention an atom. This is
                    // the one place the classifier trusts rather than proves; assert a
                    // precedence with Raw.At if that is wrong.
                    builder.Append("X");
                    continue;
                default:
                    builder.Append(Convert.ToString(part, System.Globalization.CultureInfo.InvariantCulture) ?? "");
                    continue;
            }
        }

        return builder.ToString();
    }
}
