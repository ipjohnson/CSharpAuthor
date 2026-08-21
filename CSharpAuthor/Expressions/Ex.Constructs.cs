using System;
using System.Collections.Generic;

namespace CSharpAuthor.Expressions;

#if CSHARPAUTHOR_PUBLIC_API
public
#endif
sealed partial class Ex
{
    // ---------------------------------------------------------------------------------
    // Literals. Explicit, always — a bare string is an identifier, and that asymmetry is
    // the point.
    // ---------------------------------------------------------------------------------

    private static Ex Literal(string text) => new Ex(ExPrecedence.Primary, c => c.Write(text));

    /// <summary>An escaped string literal: <c>"he said \"hi\""</c>.</summary>
    public static Ex Str(string value) => Literal(CSharpText.StringLiteral(value));

    /// <summary>A verbatim string literal: <c>@"C:\path"</c>.</summary>
    public static Ex VerbatimStr(string value) => Literal(CSharpText.VerbatimStringLiteral(value));

    /// <summary>A character literal: <c>'a'</c>. Not <c>a</c>, which is an identifier (CS0103).</summary>
    public static Ex Char(char value) => Literal(CSharpText.CharLiteral(value));

    /// <summary><c>42</c></summary>
    public static Ex Int(int value) => Literal(CSharpText.Int32Literal(value));

    /// <summary><c>42U</c></summary>
    public static Ex UInt(uint value) => Literal(CSharpText.UInt32Literal(value));

    /// <summary><c>42L</c></summary>
    public static Ex Long(long value) => Literal(CSharpText.Int64Literal(value));

    /// <summary><c>42UL</c></summary>
    public static Ex ULong(ulong value) => Literal(CSharpText.UInt64Literal(value));

    /// <summary><c>1.5F</c> — with the suffix, because <c>float f = 1.5;</c> is CS0664.</summary>
    public static Ex Float(float value) => Literal(CSharpText.SingleLiteral(value));

    /// <summary><c>1.5D</c></summary>
    public static Ex Double(double value) => Literal(CSharpText.DoubleLiteral(value));

    /// <summary><c>1.5M</c></summary>
    public static Ex Decimal(decimal value) => Literal(CSharpText.DecimalLiteral(value));

    /// <summary><c>true</c> or <c>false</c>.</summary>
    public static Ex Bool(bool value) => value ? True : False;

    /// <summary>
    /// The right literal for a CLR value, culture-invariantly. <c>null</c> becomes
    /// <c>null</c>; an <see cref="ITypeDefinition"/> becomes a deferred type reference.
    /// </summary>
    public static Ex Value(object? value)
    {
        switch (value)
        {
            case null: return Null;
            case Ex expression: return expression;
            case Expressions.Raw raw: return raw.ToExpression();
            case ITypeDefinition type: return Type(type);
            case string text: return Str(text);
            case char character: return Char(character);
            case bool flag: return Bool(flag);
            case int number: return Int(number);
            case uint number: return UInt(number);
            case long number: return Long(number);
            case ulong number: return ULong(number);
            case short number: return Int(number);
            case ushort number: return Int(number);
            case byte number: return Int(number);
            case sbyte number: return Int(number);
            case float number: return Float(number);
            case double number: return Double(number);
            case decimal number: return Decimal(number);
            default: return Expressions.Raw.From(value).ToExpression();
        }
    }

    // ---------------------------------------------------------------------------------
    // Keyword-shaped primaries
    // ---------------------------------------------------------------------------------

    /// <summary><c>typeof(T)</c> — the type stays unrendered until serialization.</summary>
    public static Ex TypeOf(ITypeDefinition type)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("typeof(");
            c.Write(type);
            c.Write(")");
        });
    }

    /// <summary><c>nameof(x)</c></summary>
    public static Ex NameOf(Ex target)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("nameof(");
            target.WriteOutput(c);
            c.Write(")");
        });
    }

    /// <summary><c>sizeof(T)</c></summary>
    public static Ex SizeOf(ITypeDefinition type)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("sizeof(");
            c.Write(type);
            c.Write(")");
        });
    }

    /// <summary><c>default</c> — the target-typed form.</summary>
    public static Ex Default() => Literal("default");

    /// <summary><c>default(T)</c></summary>
    public static Ex Default(ITypeDefinition type)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("default(");
            c.Write(type);
            c.Write(")");
        });
    }

    /// <summary><c>checked(x)</c></summary>
    public static Ex Checked(Ex operand) => KeywordCall("checked", operand);

    /// <summary><c>unchecked(x)</c></summary>
    public static Ex Unchecked(Ex operand) => KeywordCall("unchecked", operand);

    private static Ex KeywordCall(string keyword, Ex operand)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write(keyword);
            c.Write("(");
            operand.WriteOutput(c);
            c.Write(")");
        });
    }

    // ---------------------------------------------------------------------------------
    // Object and collection creation
    // ---------------------------------------------------------------------------------

    /// <summary><c>new T(args)</c></summary>
    public static Ex New(ITypeDefinition type, params Ex[] args)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("new ");
            c.Write(type);
            WriteArgumentList(c, args);
        });
    }

    /// <summary><c>new T&lt;U&gt;(args)</c> where the type arguments are supplied separately.</summary>
    public static Ex NewGeneric(ITypeDefinition type, IReadOnlyList<ITypeDefinition> typeArguments, params Ex[] args)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("new ");
            c.Write(type);
            WriteTypeArguments(c, typeArguments);
            WriteArgumentList(c, args);
        });
    }

    /// <summary>
    /// <c>new T(args) { X = 1 }</c>, or <c>new T { X = 1 }</c> when <paramref name="args"/>
    /// is null. Both are valid; the empty <c>()</c> is not always wanted.
    /// </summary>
    public static Ex NewWithInitializer(ITypeDefinition type, IReadOnlyList<Ex>? args, params Ex[] initializers)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("new ");
            c.Write(type);

            if (args != null)
            {
                WriteArgumentList(c, args);
            }

            WriteInitializerBlock(c, initializers);
        });
    }

    /// <summary><c>new(args)</c> — target-typed (C# 9).</summary>
    public static Ex NewTargetTyped(params Ex[] args)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("new");
            WriteArgumentList(c, args);
        });
    }

    /// <summary><c>new { X = 1, Y = 2 }</c> — an anonymous object.</summary>
    public static Ex NewAnonymous(params Ex[] initializers)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("new");
            WriteInitializerBlock(c, initializers);
        });
    }

    /// <summary><c>new T[] { a, b }</c></summary>
    public static Ex NewArray(ITypeDefinition elementType, params Ex[] elements)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("new ");
            c.Write(elementType);
            c.Write("[]");
            WriteInitializerBlock(c, elements);
        });
    }

    /// <summary><c>new T[n]</c>, or <c>new T[n, m]</c> for a rectangular array.</summary>
    public static Ex NewArraySized(ITypeDefinition elementType, params Ex[] lengths)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("new ");
            c.Write(elementType);
            WriteBracketedList(c, lengths);
        });
    }

    /// <summary><c>new[] { a, b }</c></summary>
    public static Ex NewArrayImplicit(params Ex[] elements)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("new[]");
            WriteInitializerBlock(c, elements);
        });
    }

    /// <summary><c>[a, b, ..rest]</c> — a collection expression (C# 12).</summary>
    public static Ex Collection(params Ex[] elements)
    {
        return new Ex(ExPrecedence.Primary, c => WriteBracketedList(c, elements));
    }

    /// <summary><c>..rest</c> — a spread element inside a collection expression.</summary>
    public static Ex Spread(Ex source)
    {
        return new Ex(ExPrecedence.Range, c =>
        {
            c.Write("..");
            WriteOperand(c, source, ExPrecedence.Unary);
        });
    }

    /// <summary><c>stackalloc T[n]</c></summary>
    public static Ex StackAlloc(ITypeDefinition elementType, Ex length)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("stackalloc ");
            c.Write(elementType);
            WriteBracketedList(c, new[] { length });
        });
    }

    /// <summary><c>stackalloc T[] { a, b }</c></summary>
    public static Ex StackAllocInit(ITypeDefinition elementType, params Ex[] elements)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("stackalloc ");
            c.Write(elementType);
            c.Write("[]");
            WriteInitializerBlock(c, elements);
        });
    }

    /// <summary><c>stackalloc[] { a, b }</c></summary>
    public static Ex StackAllocImplicit(params Ex[] elements)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("stackalloc[]");
            WriteInitializerBlock(c, elements);
        });
    }

    /// <summary><c>target with { X = 1 }</c> — a non-destructive mutation (C# 9).</summary>
    public static Ex With(Ex target, params Ex[] initializers)
    {
        return new Ex(ExPrecedence.SwitchWith, c =>
        {
            WriteOperand(c, target, ExPrecedence.SwitchWith);
            c.Write(" with");
            WriteInitializerBlock(c, initializers);
        });
    }

    private static void WriteInitializerBlock(IOutputContext context, IReadOnlyList<Ex> initializers)
    {
        if (initializers == null || initializers.Count == 0)
        {
            context.Write(" { }");
            return;
        }

        context.Write(" { ");
        WriteSeparated(context, initializers);
        context.Write(" }");
    }

    // ---------------------------------------------------------------------------------
    // Tuples, deconstruction, ranges
    // ---------------------------------------------------------------------------------

    /// <summary><c>(a, b)</c></summary>
    public static Ex Tuple(params Ex[] items)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("(");
            WriteSeparated(c, items);
            c.Write(")");
        });
    }

    /// <summary><c>name: value</c> — a named tuple element or a named argument.</summary>
    public static Ex Named(string name, Ex value)
    {
        var identifier = CSharpText.Identifier(name);

        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write(identifier);
            c.Write(": ");
            WriteOperand(c, value, ExPrecedence.Assignment);
        }, ExFlags.NeverParenthesize);
    }

    /// <summary><c>var (a, b)</c> — a deconstruction designation.</summary>
    public static Ex VarTuple(params string[] names)
    {
        return new Ex(ExPrecedence.Primary, c =>
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

    /// <summary><c>(int a, string b)</c> — a typed deconstruction designation.</summary>
    public static Ex TypedTuple(params KeyValuePair<ITypeDefinition, string>[] elements)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("(");

            for (var i = 0; i < elements.Length; i++)
            {
                if (i > 0)
                {
                    c.Write(", ");
                }

                c.Write(elements[i].Key);
                c.Write(" ");
                c.Write(CSharpText.Identifier(elements[i].Value));
            }

            c.Write(")");
        });
    }

    /// <summary>Pairs a type with a name, for <see cref="TypedTuple"/> and the typed lambdas.</summary>
    public static KeyValuePair<ITypeDefinition, string> Param(ITypeDefinition type, string name)
    {
        return new KeyValuePair<ITypeDefinition, string>(type, name);
    }

    /// <summary>
    /// <c>from..to</c>. Either end may be null for <c>..to</c>, <c>from..</c> or <c>..</c>.
    /// </summary>
    /// <remarks>
    /// The operands are unary expressions, which is tighter than it looks: <c>-1 + 2..^2</c>
    /// parses as <c>-1 + (2..^2)</c>, so <c>(a + b)..c</c> keeps its brackets.
    /// </remarks>
    public static Ex Range(Ex? from, Ex? to)
    {
        return new Ex(ExPrecedence.Range, c =>
        {
            if (from != null)
            {
                WriteOperand(c, from, ExPrecedence.Unary);
            }

            c.Write("..");

            if (to != null)
            {
                WriteOperand(c, to, ExPrecedence.Unary);
            }
        });
    }

    // ---------------------------------------------------------------------------------
    // Argument modifiers. Never parenthesised: `f((out x))` is not a thing.
    // ---------------------------------------------------------------------------------

    /// <summary><c>out x</c></summary>
    public static Ex OutArg(Ex value) => ArgumentModifier("out ", value);

    /// <summary><c>out T name</c> — an inline out variable.</summary>
    public static Ex OutVar(ITypeDefinition type, string name)
    {
        var identifier = CSharpText.Identifier(name);

        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("out ");
            c.Write(type);
            c.Write(" ");
            c.Write(identifier);
        }, ExFlags.NeverParenthesize);
    }

    /// <summary><c>out var name</c></summary>
    public static Ex OutVar(string name)
    {
        var identifier = CSharpText.Identifier(name);

        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("out var ");
            c.Write(identifier);
        }, ExFlags.NeverParenthesize);
    }

    /// <summary><c>out _</c></summary>
    public static Ex OutDiscard() => new Ex(ExPrecedence.Primary, c => c.Write("out _"), ExFlags.NeverParenthesize);

    /// <summary><c>ref x</c></summary>
    public static Ex RefArg(Ex value) => ArgumentModifier("ref ", value);

    /// <summary><c>in x</c></summary>
    public static Ex InArg(Ex value) => ArgumentModifier("in ", value);

    private static Ex ArgumentModifier(string keyword, Ex value)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write(keyword);
            WriteOperand(c, value, ExPrecedence.Unary);
        }, ExFlags.NeverParenthesize);
    }

    // ---------------------------------------------------------------------------------
    // Escape hatch
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Last resort: literal text that still composes. Types inside stay unrendered.
    /// </summary>
    /// <seealso cref="Expressions.Raw"/>
    public static Ex Raw(params object?[] parts) => new Expressions.Raw(parts).ToExpression();

    /// <summary>
    /// Literal text at a precedence you assert, for when the shape cannot be inferred and
    /// the conservative reading would over-bracket.
    /// </summary>
    public static Ex RawAt(int precedence, params object?[] parts) =>
        Expressions.Raw.At(precedence, parts).ToExpression();

    // ---------------------------------------------------------------------------------
    // Statements
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// This expression as a statement: indented, terminated with <c>;</c>, on its own line.
    /// </summary>
    public IStatementNode AsStatement() => new ExpressionStatement(this);

    private sealed class ExpressionStatement : IStatementNode
    {
        private readonly Ex _expression;

        public ExpressionStatement(Ex expression) => _expression = expression;

        public void AddUsingNamespace(string ns)
        {
        }

        public void WriteOutput(IOutputContext outputContext)
        {
            outputContext.WriteIndent();
            _expression.WriteOutput(outputContext);
            outputContext.Write(";");
            outputContext.WriteLine();
        }
    }
}
