using System;
using System.Collections.Generic;

namespace CSharpAuthor.Expressions;

/// <summary>
/// A C# pattern, with its own precedence ladder: <c>or</c> is looser than <c>and</c>,
/// which is looser than <c>not</c>.
/// </summary>
/// <remarks>
/// The same invariant as <see cref="Ex"/> applies, and it bites in the same way:
/// <c>a or b and c</c> is <c>a or (b and c)</c>, so a tree that meant
/// <c>(a or b) and c</c> has to say so with brackets or it silently becomes a different
/// test. Both combinators are left-associative.
/// </remarks>
public sealed partial class Pat : IPatternNode
{
    private readonly Action<IOutputContext> _write;

    internal Pat(int precedence, Action<IOutputContext> write)
    {
        PatternPrecedence = precedence;
        _write = write;
    }

    /// <inheritdoc />
    public int PatternPrecedence { get; }

    /// <inheritdoc />
    public void AddUsingNamespace(string ns)
    {
    }

    /// <inheritdoc />
    public void WriteOutput(IOutputContext outputContext)
    {
        _write(outputContext);
    }

    /// <summary>Renders to a string. A debugging and testing convenience.</summary>
    public string Render(OutputContextOptions? options = null)
    {
        var context = new OutputContext(options);

        WriteOutput(context);

        return context.Output();
    }

    /// <summary>A type used as a pattern is a type pattern.</summary>
    public static implicit operator Pat(TypeDefinition type) => Type(type);

    /// <summary>A raw fragment used as a pattern.</summary>
    public static implicit operator Pat(Raw raw) =>
        new Pat(raw.PatternPrecedence, raw.WriteOutput);

    internal static void WriteOperand(IOutputContext context, Pat operand, int required)
    {
        if (operand.PatternPrecedence < required)
        {
            context.Write("(");
            operand.WriteOutput(context);
            context.Write(")");
        }
        else
        {
            operand.WriteOutput(context);
        }
    }

    // ---------------------------------------------------------------------------------
    // Primary patterns
    // ---------------------------------------------------------------------------------

    /// <summary><c>_</c></summary>
    public static readonly Pat Discard = new Pat(PatPrecedence.Primary, c => c.Write("_"));

    /// <summary><c>null</c></summary>
    public static readonly Pat Null = new Pat(PatPrecedence.Primary, c => c.Write("null"));

    /// <summary><c>not null</c></summary>
    public static Pat NotNull() => Not(Null);

    /// <summary>A constant pattern: <c>42</c>, <c>"text"</c>, <c>Colour.Red</c>.</summary>
    public static Pat Constant(Ex value)
    {
        return new Pat(PatPrecedence.Primary, c => Ex.WriteOperand(c, value, ExPrecedence.Unary));
    }

    /// <summary>A type pattern: <c>string</c>.</summary>
    public static Pat Type(ITypeDefinition type)
    {
        return new Pat(PatPrecedence.Primary, c => c.Write(type));
    }

    /// <summary>A declaration pattern: <c>string text</c>.</summary>
    public static Pat Declaration(ITypeDefinition type, string name)
    {
        var identifier = CSharpText.Identifier(name);

        return new Pat(PatPrecedence.Primary, c =>
        {
            c.Write(type);
            c.Write(" ");
            c.Write(identifier);
        });
    }

    /// <summary>A var pattern: <c>var text</c>.</summary>
    public static Pat Var(string name)
    {
        var identifier = CSharpText.Identifier(name);

        return new Pat(PatPrecedence.Primary, c =>
        {
            c.Write("var ");
            c.Write(identifier);
        });
    }

    /// <summary>A var pattern with a tuple designation: <c>var (x, y)</c>.</summary>
    public static Pat VarTuple(params string[] names)
    {
        return new Pat(PatPrecedence.Primary, c =>
        {
            c.Write("var (");

            for (var i = 0; i < names.Length; i++)
            {
                if (i > 0)
                {
                    c.Write(", ");
                }

                c.Write(CSharpText.Identifier(names[i]));
            }

            c.Write(")");
        });
    }

    /// <summary>A relational pattern: <c>&gt; 5</c>.</summary>
    public static Pat Relational(string op, Ex value)
    {
        return new Pat(PatPrecedence.Primary, c =>
        {
            c.Write(op);
            c.Write(" ");
            Ex.WriteOperand(c, value, ExPrecedence.Unary);
        });
    }

    /// <summary><c>&lt; value</c></summary>
    public static Pat LessThan(Ex value) => Relational("<", value);

    /// <summary><c>&gt; value</c></summary>
    public static Pat GreaterThan(Ex value) => Relational(">", value);

    /// <summary><c>&lt;= value</c></summary>
    public static Pat LessThanOrEqual(Ex value) => Relational("<=", value);

    /// <summary><c>&gt;= value</c></summary>
    public static Pat GreaterThanOrEqual(Ex value) => Relational(">=", value);

    /// <summary>Explicit brackets around a pattern (C# 10), preserved as written.</summary>
    public static Pat Parenthesized(Pat inner)
    {
        return new Pat(PatPrecedence.Primary, c =>
        {
            c.Write("(");
            inner.WriteOutput(c);
            c.Write(")");
        });
    }

    // ---------------------------------------------------------------------------------
    // Combinators
    // ---------------------------------------------------------------------------------

    /// <summary><c>not p</c></summary>
    public static Pat Not(Pat pattern)
    {
        return new Pat(PatPrecedence.Not, c =>
        {
            c.Write("not ");
            WriteOperand(c, pattern, PatPrecedence.Not);
        });
    }

    /// <summary><c>a and b</c> — left associative.</summary>
    public static Pat And(Pat left, Pat right) => Combine("and", PatPrecedence.And, left, right);

    /// <summary><c>a or b</c> — left associative.</summary>
    public static Pat Or(Pat left, Pat right) => Combine("or", PatPrecedence.Or, left, right);

    private static Pat Combine(string keyword, int precedence, Pat left, Pat right)
    {
        return new Pat(precedence, c =>
        {
            WriteOperand(c, left, precedence);
            c.Write(" ");
            c.Write(keyword);
            c.Write(" ");
            WriteOperand(c, right, precedence + 1);
        });
    }

    // ---------------------------------------------------------------------------------
    // Recursive patterns
    // ---------------------------------------------------------------------------------

    /// <summary>Names a sub-pattern inside a property pattern: <c>Length: &gt; 0</c>.</summary>
    public static KeyValuePair<string, Pat> Prop(string name, Pat pattern)
    {
        return new KeyValuePair<string, Pat>(name, pattern);
    }

    /// <summary>
    /// A property pattern: <c>{ Length: &gt; 0 }</c>, or <c>Foo { Length: &gt; 0 } f</c>
    /// with a type and a designation.
    /// </summary>
    public static Pat Property(ITypeDefinition? type, IReadOnlyList<KeyValuePair<string, Pat>> properties, string? designation = null)
    {
        return Recursive(type, null, properties, designation);
    }

    /// <summary>
    /// The general recursive pattern: an optional type, an optional positional list, an
    /// optional property list and an optional designation —
    /// <c>Point (0, var y) { Length: &gt; 0 } p</c>.
    /// </summary>
    public static Pat Recursive(
        ITypeDefinition? type,
        IReadOnlyList<Pat>? positional,
        IReadOnlyList<KeyValuePair<string, Pat>>? properties,
        string? designation = null)
    {
        var identifier = designation == null ? null : CSharpText.Identifier(designation);

        return new Pat(PatPrecedence.Primary, c =>
        {
            var wroteSomething = false;

            if (type != null)
            {
                c.Write(type);
                wroteSomething = true;
            }

            if (positional != null)
            {
                if (wroteSomething)
                {
                    c.Write(" ");
                }

                c.Write("(");

                for (var i = 0; i < positional.Count; i++)
                {
                    if (i > 0)
                    {
                        c.Write(", ");
                    }

                    positional[i].WriteOutput(c);
                }

                c.Write(")");
                wroteSomething = true;
            }

            if (properties != null)
            {
                if (wroteSomething)
                {
                    c.Write(" ");
                }

                if (properties.Count == 0)
                {
                    c.Write("{ }");
                }
                else
                {
                    c.Write("{ ");

                    for (var i = 0; i < properties.Count; i++)
                    {
                        if (i > 0)
                        {
                            c.Write(", ");
                        }

                        c.Write(CSharpText.Identifier(properties[i].Key));
                        c.Write(": ");
                        properties[i].Value.WriteOutput(c);
                    }

                    c.Write(" }");
                }

                wroteSomething = true;
            }

            if (identifier != null)
            {
                if (wroteSomething)
                {
                    c.Write(" ");
                }

                c.Write(identifier);
            }
        });
    }

    /// <summary>A positional pattern: <c>Point (0, var y)</c>, or <c>(0, var y)</c>.</summary>
    public static Pat Positional(ITypeDefinition? type, params Pat[] elements)
    {
        return Recursive(type, elements, null);
    }

    // ---------------------------------------------------------------------------------
    // List patterns
    // ---------------------------------------------------------------------------------

    /// <summary>A list pattern: <c>[1, 2, ..]</c>.</summary>
    public static Pat List(params Pat[] elements)
    {
        return new Pat(PatPrecedence.Primary, c =>
        {
            c.Write("[");

            for (var i = 0; i < elements.Length; i++)
            {
                if (i > 0)
                {
                    c.Write(", ");
                }

                elements[i].WriteOutput(c);
            }

            c.Write("]");
        });
    }

    /// <summary>A slice pattern: <c>..</c>, or <c>..var rest</c> when given a sub-pattern.</summary>
    public static Pat Slice(Pat? inner = null)
    {
        return new Pat(PatPrecedence.Primary, c =>
        {
            c.Write("..");

            if (inner != null)
            {
                c.Write(" ");
                inner.WriteOutput(c);
            }
        });
    }
}
