using System;
using System.Collections.Generic;

namespace CSharpAuthor.Expressions;

/// <summary>
/// A C# expression that knows its own precedence and parenthesises its operands for you.
/// </summary>
/// <remarks>
/// <para>
/// Concrete rather than an interface, so it can carry implicit conversions and operator
/// overloads — that is what makes the typed path shorter to write than string
/// interpolation, not merely safer.
/// </para>
/// <para>
/// <b>The invariant:</b> the text an <see cref="Ex"/> emits re-parses to the tree it was
/// built from. Parentheses are added exactly where dropping them would change the
/// program, and nowhere else. Wrong parenthesisation does not throw and does not fail to
/// compile; it silently computes something else. That is why the operand rules live in
/// one place and are tested one trap at a time.
/// </para>
/// <para>
/// <b>Deferred by construction:</b> an <see cref="Ex"/> is a write action, not a string.
/// Types travel as unrendered <see cref="ITypeDefinition"/> and reach the output only
/// through <see cref="IOutputContext.Write(ITypeDefinition)"/>, so a single option can
/// still flip a whole file between short names and <c>global::</c>. Nothing here calls
/// <c>AddImportNamespace</c>; namespaces are derived from what was written.
/// </para>
/// </remarks>
public sealed partial class Ex : IExpressionNode
{
    private readonly Action<IOutputContext> _write;

    internal Ex(int precedence, Action<IOutputContext> write, ExFlags flags = ExFlags.None)
    {
        Precedence = precedence;
        _write = write;
        Flags = flags;
    }

    /// <summary>Where this expression sits on <see cref="ExPrecedence"/>'s ladder.</summary>
    public int Precedence { get; }

    internal ExFlags Flags { get; }

    /// <inheritdoc />
    public void AddUsingNamespace(string ns)
    {
        // Invariant 1: namespaces are derived from the types that were written, never
        // announced by the writer.
    }

    /// <inheritdoc />
    public void WriteOutput(IOutputContext outputContext)
    {
        _write(outputContext);
    }

    /// <summary>
    /// Renders to a string. A debugging and testing convenience — real output goes
    /// through the file's own <see cref="IOutputContext"/>, which is the only context that
    /// knows the whole file and can therefore resolve names.
    /// </summary>
    public string Render(OutputContextOptions? options = null)
    {
        var context = new OutputContext(options);

        WriteOutput(context);

        return context.Output();
    }

    // ---------------------------------------------------------------------------------
    // Conversions. A bare string is an IDENTIFIER; literals are always explicit, because
    // getting that backwards is the single most common code-generation bug.
    // ---------------------------------------------------------------------------------

    /// <summary>A bare string is an identifier, keyword-escaped. Use <see cref="Str"/> for a literal.</summary>
    public static implicit operator Ex(string identifier) => Id(identifier);

    /// <summary>An <see cref="int"/> literal.</summary>
    public static implicit operator Ex(int value) => Int(value);

    /// <summary>A <see cref="bool"/> literal.</summary>
    public static implicit operator Ex(bool value) => Bool(value);

    /// <summary>A type in expression position, still unrendered.</summary>
    public static implicit operator Ex(TypeDefinition type) => Type(type);

    /// <summary>A <see cref="Raw"/> fragment, carrying whatever precedence it could prove.</summary>
    public static implicit operator Ex(Expressions.Raw raw) => raw.ToExpression();

    // ---------------------------------------------------------------------------------
    // Atoms
    // ---------------------------------------------------------------------------------

    /// <summary>An identifier, keyword-escaped: <c>class</c> becomes <c>@class</c>.</summary>
    public static Ex Id(string name)
    {
        var text = CSharpText.Identifier(name);

        return new Ex(ExPrecedence.Primary, c => c.Write(text));
    }

    /// <summary>A type used in expression position — <c>Foo.Bar</c> in <c>Foo.Bar.Baz()</c>.</summary>
    public static Ex Type(ITypeDefinition type)
    {
        return new Ex(ExPrecedence.Primary, c => c.Write(type));
    }

    /// <summary><c>this</c></summary>
    public static readonly Ex This = new Ex(ExPrecedence.Primary, c => c.Write("this"));

    /// <summary><c>base</c></summary>
    public static readonly Ex Base = new Ex(ExPrecedence.Primary, c => c.Write("base"));

    /// <summary><c>null</c></summary>
    public static readonly Ex Null = new Ex(ExPrecedence.Primary, c => c.Write("null"));

    /// <summary><c>true</c></summary>
    public static readonly Ex True = new Ex(ExPrecedence.Primary, c => c.Write("true"));

    /// <summary><c>false</c></summary>
    public static readonly Ex False = new Ex(ExPrecedence.Primary, c => c.Write("false"));

    /// <summary><c>_</c> — a discard.</summary>
    public static readonly Ex Discard = new Ex(ExPrecedence.Primary, c => c.Write("_"));

    /// <summary>
    /// The empty expression a null-conditional chain hangs from. Writes nothing; it exists
    /// so <c>root.Dot("b").Dot("c")</c> renders <c>.b.c</c> for <see cref="NullAccess"/>.
    /// </summary>
    public static readonly Ex ChainRoot = new Ex(ExPrecedence.Primary, c => { });

    /// <summary>Explicit parentheses, preserved even when precedence does not require them.</summary>
    public static Ex Paren(Ex inner)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write("(");
            inner.WriteOutput(c);
            c.Write(")");
        });
    }

    // ---------------------------------------------------------------------------------
    // The one place parentheses are decided
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Writes <paramref name="operand"/>, wrapping it when its precedence is looser than
    /// the position demands.
    /// </summary>
    internal static void WriteOperand(IOutputContext context, Ex operand, int required)
    {
        var needsParens =
            operand.Precedence < required &&
            (operand.Flags & ExFlags.NeverParenthesize) == 0;

        if (needsParens)
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

    /// <summary>
    /// Writes the target of a member access, invocation or element access. Demands a true
    /// primary, so a null-conditional chain is parenthesised rather than extended —
    /// <c>(a?.b).c</c>, never <c>a?.b.c</c>, unless the author asked for the chain.
    /// </summary>
    private static void WriteTarget(IOutputContext context, Ex target)
    {
        WriteOperand(context, target, ExPrecedence.Primary);
    }

    // ---------------------------------------------------------------------------------
    // Binary operators
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// A binary operator at an explicit precedence. Left-associative unless told otherwise.
    /// </summary>
    /// <remarks>
    /// The whole associativity story is these two requirements. For a left-associative
    /// operator the right operand must be one level tighter, so <c>a - (b - c)</c> keeps
    /// its parentheses while <c>(a - b) - c</c> does not need any. For a right-associative
    /// operator the requirement swaps, so <c>a ?? b ?? c</c> is bare and
    /// <c>(a ?? b) ?? c</c> is not.
    /// </remarks>
    public static Ex Binary(string op, int precedence, Ex left, Ex right, bool rightAssociative = false)
    {
        var leftRequirement = ExPrecedence.LeftRequirement(precedence, rightAssociative);
        var rightRequirement = ExPrecedence.RightRequirement(precedence, rightAssociative);

        return new Ex(precedence, c =>
        {
            WriteOperand(c, left, leftRequirement);
            c.Write(" ");
            c.Write(op);
            c.Write(" ");
            WriteOperand(c, right, rightRequirement);
        });
    }

    /// <summary><c>a + b</c></summary>
    public static Ex Add(Ex left, Ex right) => Binary("+", ExPrecedence.Additive, left, right);

    /// <summary><c>a - b</c></summary>
    public static Ex Subtract(Ex left, Ex right) => Binary("-", ExPrecedence.Additive, left, right);

    /// <summary><c>a * b</c></summary>
    public static Ex Multiply(Ex left, Ex right) => Binary("*", ExPrecedence.Multiplicative, left, right);

    /// <summary><c>a / b</c></summary>
    public static Ex Divide(Ex left, Ex right) => Binary("/", ExPrecedence.Multiplicative, left, right);

    /// <summary><c>a % b</c></summary>
    public static Ex Modulo(Ex left, Ex right) => Binary("%", ExPrecedence.Multiplicative, left, right);

    /// <summary><c>a &lt;&lt; b</c></summary>
    public static Ex ShiftLeft(Ex left, Ex right) => Binary("<<", ExPrecedence.Shift, left, right);

    /// <summary><c>a &gt;&gt; b</c></summary>
    public static Ex ShiftRight(Ex left, Ex right) => Binary(">>", ExPrecedence.Shift, left, right);

    /// <summary><c>a &gt;&gt;&gt; b</c> — unsigned right shift (C# 11).</summary>
    public static Ex UnsignedShiftRight(Ex left, Ex right) => Binary(">>>", ExPrecedence.Shift, left, right);

    /// <summary><c>a &amp;&amp; b</c></summary>
    public static Ex AndAlso(Ex left, Ex right) => Binary("&&", ExPrecedence.ConditionalAnd, left, right);

    /// <summary><c>a || b</c></summary>
    public static Ex OrElse(Ex left, Ex right) => Binary("||", ExPrecedence.ConditionalOr, left, right);

    /// <summary><c>a &amp; b</c></summary>
    public static Ex BitAnd(Ex left, Ex right) => Binary("&", ExPrecedence.BitwiseAnd, left, right);

    /// <summary><c>a | b</c></summary>
    public static Ex BitOr(Ex left, Ex right) => Binary("|", ExPrecedence.BitwiseOr, left, right);

    /// <summary><c>a ^ b</c></summary>
    public static Ex BitXor(Ex left, Ex right) => Binary("^", ExPrecedence.BitwiseXor, left, right);

    /// <summary><c>a == b</c></summary>
    public static Ex Equal(Ex left, Ex right) => Binary("==", ExPrecedence.Equality, left, right);

    /// <summary><c>a != b</c></summary>
    public static Ex NotEqual(Ex left, Ex right) => Binary("!=", ExPrecedence.Equality, left, right);

    /// <summary><c>a &lt; b</c></summary>
    public static Ex LessThan(Ex left, Ex right) => Binary("<", ExPrecedence.Relational, left, right);

    /// <summary><c>a &gt; b</c></summary>
    public static Ex GreaterThan(Ex left, Ex right) => Binary(">", ExPrecedence.Relational, left, right);

    /// <summary><c>a &lt;= b</c></summary>
    public static Ex LessThanOrEqual(Ex left, Ex right) => Binary("<=", ExPrecedence.Relational, left, right);

    /// <summary><c>a &gt;= b</c></summary>
    public static Ex GreaterThanOrEqual(Ex left, Ex right) => Binary(">=", ExPrecedence.Relational, left, right);

    /// <summary><c>a ?? b</c> — right associative.</summary>
    public static Ex Coalesce(Ex left, Ex right) =>
        Binary("??", ExPrecedence.Coalesce, left, right, rightAssociative: true);

    /// <summary><c>a = b</c> — right associative.</summary>
    public static Ex Assign(Ex target, Ex value) => AssignOperator("=", target, value);

    /// <summary><c>a += b</c>, <c>a ??= b</c>, and every other compound assignment.</summary>
    public static Ex AssignOperator(string op, Ex target, Ex value)
    {
        // The grammar's left side is a unary_expression, not an expression: `a + b = c`
        // is not an assignment to a sum, it is a syntax error.
        return new Ex(ExPrecedence.Assignment, c =>
        {
            WriteOperand(c, target, ExPrecedence.Unary);
            c.Write(" ");
            c.Write(op);
            c.Write(" ");
            WriteOperand(c, value, ExPrecedence.Assignment);
        });
    }

    /// <summary><c>a ??= b</c></summary>
    public static Ex CoalesceAssign(Ex target, Ex value) => AssignOperator("??=", target, value);

    /// <summary><c>c ? t : f</c> — right associative.</summary>
    public static Ex Conditional(Ex condition, Ex whenTrue, Ex whenFalse)
    {
        return new Ex(ExPrecedence.Conditional, c =>
        {
            // The condition must be a null_coalescing_expression; the two branches accept
            // a full expression, so a nested conditional in either arm needs nothing.
            WriteOperand(c, condition, ExPrecedence.Coalesce);
            c.Write(" ? ");
            WriteOperand(c, whenTrue, ExPrecedence.Assignment);
            c.Write(" : ");
            WriteOperand(c, whenFalse, ExPrecedence.Assignment);
        });
    }

    /// <summary><c>a is B</c> — a type test.</summary>
    public static Ex Is(Ex target, ITypeDefinition type)
    {
        return new Ex(ExPrecedence.Relational, c =>
        {
            WriteOperand(c, target, ExPrecedence.Relational);
            c.Write(" is ");
            c.Write(type);
        });
    }

    /// <summary><c>a is <i>pattern</i></c></summary>
    public static Ex Is(Ex target, Pat pattern)
    {
        return new Ex(ExPrecedence.Relational, c =>
        {
            WriteOperand(c, target, ExPrecedence.Relational);
            c.Write(" is ");
            pattern.WriteOutput(c);
        });
    }

    /// <summary><c>a is not B</c></summary>
    public static Ex IsNot(Ex target, ITypeDefinition type) => Is(target, Pat.Not(Pat.Type(type)));

    /// <summary><c>a as B</c></summary>
    public static Ex As(Ex target, ITypeDefinition type)
    {
        return new Ex(ExPrecedence.Relational, c =>
        {
            WriteOperand(c, target, ExPrecedence.Relational);
            c.Write(" as ");
            c.Write(type);
        });
    }

    // ---------------------------------------------------------------------------------
    // Unary operators
    // ---------------------------------------------------------------------------------

    private static Ex Prefix(string op, Ex operand, bool spaceAfterOperator = false)
    {
        var leadFlag =
            op[0] == '-' ? ExFlags.LeadsWithMinus :
            op[0] == '+' ? ExFlags.LeadsWithPlus :
            ExFlags.None;

        return new Ex(ExPrecedence.Unary, c =>
        {
            c.Write(op);

            if (spaceAfterOperator)
            {
                c.Write(" ");
            }

            // `- -a` and `--a` are both C# and mean different things, so a minus in front
            // of something that already starts with a minus gets brackets rather than a
            // hopeful space. Same for plus. Nothing else can collide.
            var lexicalHazard =
                (leadFlag != ExFlags.None && (operand.Flags & leadFlag) != 0) &&
                !spaceAfterOperator;

            if (lexicalHazard)
            {
                c.Write("(");
                operand.WriteOutput(c);
                c.Write(")");
            }
            else
            {
                WriteOperand(c, operand, ExPrecedence.Unary);
            }
        }, leadFlag);
    }

    /// <summary><c>!a</c></summary>
    public static Ex Not(Ex operand) => Prefix("!", operand);

    /// <summary><c>-a</c></summary>
    public static Ex Negate(Ex operand) => Prefix("-", operand);

    /// <summary><c>+a</c></summary>
    public static Ex UnaryPlus(Ex operand) => Prefix("+", operand);

    /// <summary><c>~a</c></summary>
    public static Ex Complement(Ex operand) => Prefix("~", operand);

    /// <summary><c>++a</c></summary>
    public static Ex PreIncrement(Ex operand) => Prefix("++", operand);

    /// <summary><c>--a</c></summary>
    public static Ex PreDecrement(Ex operand) => Prefix("--", operand);

    /// <summary><c>&amp;a</c></summary>
    public static Ex AddressOf(Ex operand) => Prefix("&", operand);

    /// <summary><c>*a</c></summary>
    public static Ex Dereference(Ex operand) => Prefix("*", operand);

    /// <summary><c>^a</c> — an index from the end.</summary>
    public static Ex FromEnd(Ex operand) => Prefix("^", operand);

    /// <summary><c>await a</c></summary>
    public static Ex Await(Ex operand) => Prefix("await", operand, spaceAfterOperator: true);

    /// <summary>
    /// <c>throw new T()</c> in expression position — the right of <c>??</c>, either arm of
    /// <c>?:</c>, or a lambda body.
    /// </summary>
    /// <remarks>
    /// Marked so it is never parenthesised: <c>a ?? (throw new T())</c> is CS8115, not
    /// merely redundant.
    /// </remarks>
    public static Ex Throw(Ex operand)
    {
        return new Ex(ExPrecedence.Assignment, c =>
        {
            c.Write("throw ");
            operand.WriteOutput(c);
        }, ExFlags.NeverParenthesize);
    }

    /// <summary><c>(T)a</c></summary>
    /// <remarks>
    /// The operand requirement is a unary expression, which is why <c>(int)-x</c> comes out
    /// bare while <c>(int)(a + b)</c> keeps its brackets. Note also what this method cannot
    /// emit: because the target is an <see cref="ITypeDefinition"/> and never a
    /// parenthesised expression, the <c>(A)(b)</c> shape — a cast to the compiler, a call
    /// to the reader — is unreachable by accident.
    /// </remarks>
    public static Ex Cast(ITypeDefinition type, Ex operand)
    {
        return new Ex(ExPrecedence.Unary, c =>
        {
            c.Write("(");
            c.Write(type);
            c.Write(")");
            WriteOperand(c, operand, ExPrecedence.Unary);
        });
    }

    /// <summary><c>a++</c></summary>
    public static Ex PostIncrement(Ex operand) => Postfix("++", operand);

    /// <summary><c>a--</c></summary>
    public static Ex PostDecrement(Ex operand) => Postfix("--", operand);

    private static Ex Postfix(string op, Ex operand)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            WriteTarget(c, operand);
            c.Write(op);
        });
    }

    /// <summary><c>a!</c> — the null-forgiving operator.</summary>
    /// <remarks>
    /// Accepts a null-conditional chain unbracketed (<c>a?.b!</c>) because suppression does
    /// not break the chain, but brackets anything looser: <c>(a as B)!</c>.
    /// </remarks>
    public static Ex SuppressNullWarning(Ex operand)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            WriteOperand(c, operand, ExPrecedence.NullChain);
            c.Write("!");
        });
    }

    // ---------------------------------------------------------------------------------
    // Member access, invocation, element access
    // ---------------------------------------------------------------------------------

    /// <summary><c>target.member</c></summary>
    public Ex Dot(string member)
    {
        var name = CSharpText.Identifier(member);

        return new Ex(ExPrecedence.Primary, c =>
        {
            WriteTarget(c, this);
            c.Write(".");
            c.Write(name);
        });
    }

    /// <summary><c>Type.member</c> — the type stays unrendered.</summary>
    public static Ex On(ITypeDefinition type, string member) => Type(type).Dot(member);

    /// <summary><c>target.method(args)</c></summary>
    public Ex Call(string method, params Ex[] args)
    {
        return Dot(method).Invoke(args);
    }

    /// <summary><c>target.method&lt;T&gt;(args)</c></summary>
    public Ex CallGeneric(string method, IReadOnlyList<ITypeDefinition> typeArguments, params Ex[] args)
    {
        return DotGeneric(method, typeArguments).Invoke(args);
    }

    /// <summary><c>target.member&lt;T&gt;</c> — a generic name, not yet invoked.</summary>
    public Ex DotGeneric(string member, IReadOnlyList<ITypeDefinition> typeArguments)
    {
        var name = CSharpText.Identifier(member);

        return new Ex(ExPrecedence.Primary, c =>
        {
            WriteTarget(c, this);
            c.Write(".");
            c.Write(name);
            WriteTypeArguments(c, typeArguments);
        });
    }

    /// <summary><c>Type.method(args)</c> — the type stays unrendered.</summary>
    public static Ex Call(ITypeDefinition type, string method, params Ex[] args) =>
        Type(type).Call(method, args);

    /// <summary><c>Type.method&lt;T&gt;(args)</c></summary>
    public static Ex CallGeneric(ITypeDefinition type, string method, IReadOnlyList<ITypeDefinition> typeArguments, params Ex[] args) =>
        Type(type).CallGeneric(method, typeArguments, args);

    /// <summary><c>target(args)</c> — invoke the expression itself.</summary>
    public Ex Invoke(params Ex[] args)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            WriteTarget(c, this);
            WriteArgumentList(c, args);
        });
    }

    /// <summary><c>target[index]</c></summary>
    public Ex Index(params Ex[] indices)
    {
        return new Ex(ExPrecedence.Primary, c =>
        {
            WriteTarget(c, this);
            WriteBracketedList(c, indices);
        });
    }

    // ---------------------------------------------------------------------------------
    // Null-conditional access
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// <c>target?<i>chain</i></c> — a null-conditional chain, built from
    /// <see cref="ChainRoot"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The chain is one node, not two, and that distinction is load-bearing. Given
    /// <c>node</c> is null, <c>node?.B.C</c> evaluates to null and <c>(node?.B).C</c>
    /// throws — verified against the compiler, not assumed. Everything built through
    /// <see cref="Dot"/> on an existing chain takes the second reading, so the only way to
    /// get the first is to say so here:
    /// </para>
    /// <code>
    /// Ex.Id("node").NullAccess(root =&gt; root.Dot("B").Dot("C"))   // node?.B.C
    /// Ex.Id("node").NullDot("B").Dot("C")                          // (node?.B).C
    /// </code>
    /// </remarks>
    public Ex NullAccess(Func<Ex, Ex> chain)
    {
        var body = chain(ChainRoot);

        return new Ex(ExPrecedence.NullChain, c =>
        {
            WriteTarget(c, this);
            c.Write("?");
            body.WriteOutput(c);
        });
    }

    /// <summary><c>target?.member</c></summary>
    public Ex NullDot(string member) => NullAccess(root => root.Dot(member));

    /// <summary><c>target?.method(args)</c></summary>
    public Ex NullCall(string method, params Ex[] args) => NullAccess(root => root.Call(method, args));

    /// <summary><c>target?[index]</c></summary>
    public Ex NullIndex(params Ex[] indices) => NullAccess(root => root.Index(indices));

    // ---------------------------------------------------------------------------------
    // Operators — the ergonomics the typed path is for
    // ---------------------------------------------------------------------------------

    /// <summary><c>!a</c></summary>
    public static Ex operator !(Ex operand) => Not(operand);

    /// <summary><c>-a</c></summary>
    public static Ex operator -(Ex operand) => Negate(operand);

    /// <summary><c>+a</c></summary>
    public static Ex operator +(Ex operand) => UnaryPlus(operand);

    /// <summary><c>~a</c></summary>
    public static Ex operator ~(Ex operand) => Complement(operand);

    /// <summary><c>a &amp;&amp; b</c> — the short-circuiting form, which is what generated code almost always wants.</summary>
    public static Ex operator &(Ex left, Ex right) => AndAlso(left, right);

    /// <summary><c>a || b</c></summary>
    public static Ex operator |(Ex left, Ex right) => OrElse(left, right);

    /// <summary><c>a ^ b</c></summary>
    public static Ex operator ^(Ex left, Ex right) => BitXor(left, right);

    /// <summary><c>a &lt; b</c></summary>
    public static Ex operator <(Ex left, Ex right) => LessThan(left, right);

    /// <summary><c>a &gt; b</c></summary>
    public static Ex operator >(Ex left, Ex right) => GreaterThan(left, right);

    /// <summary><c>a &lt;= b</c></summary>
    public static Ex operator <=(Ex left, Ex right) => LessThanOrEqual(left, right);

    /// <summary><c>a &gt;= b</c></summary>
    public static Ex operator >=(Ex left, Ex right) => GreaterThanOrEqual(left, right);

    /// <summary><c>a + b</c></summary>
    public static Ex operator +(Ex left, Ex right) => Add(left, right);

    /// <summary><c>a - b</c></summary>
    public static Ex operator -(Ex left, Ex right) => Subtract(left, right);

    /// <summary><c>a * b</c></summary>
    public static Ex operator *(Ex left, Ex right) => Multiply(left, right);

    /// <summary><c>a / b</c></summary>
    public static Ex operator /(Ex left, Ex right) => Divide(left, right);

    /// <summary><c>a % b</c></summary>
    public static Ex operator %(Ex left, Ex right) => Modulo(left, right);

    // ---------------------------------------------------------------------------------
    // Fluent forms of the binary operators, for the ones C# will not let us overload
    // ---------------------------------------------------------------------------------

    /// <summary><c>this == other</c></summary>
    public Ex Eq(Ex other) => Equal(this, other);

    /// <summary><c>this != other</c></summary>
    public Ex NotEq(Ex other) => NotEqual(this, other);

    /// <summary><c>this is <i>pattern</i></c></summary>
    public Ex Is(Pat pattern) => Is(this, pattern);

    /// <summary><c>this is T</c></summary>
    public Ex Is(ITypeDefinition type) => Is(this, type);

    /// <summary><c>this as T</c></summary>
    public Ex As(ITypeDefinition type) => As(this, type);

    /// <summary><c>this ?? other</c></summary>
    public Ex Coalesce(Ex other) => Coalesce(this, other);

    /// <summary><c>this = value</c></summary>
    public Ex Assign(Ex value) => Assign(this, value);

    /// <summary><c>this ? whenTrue : whenFalse</c></summary>
    public Ex Then(Ex whenTrue, Ex whenFalse) => Conditional(this, whenTrue, whenFalse);

    /// <summary><c>this!</c></summary>
    public Ex Bang() => SuppressNullWarning(this);

    /// <summary><c>this++</c></summary>
    public Ex PlusPlus() => PostIncrement(this);

    /// <summary><c>this--</c></summary>
    public Ex MinusMinus() => PostDecrement(this);

    /// <summary>Folds a list with <c>&amp;&amp;</c>. An empty list is <c>true</c>.</summary>
    public static Ex All(IReadOnlyList<Ex> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            return True;
        }

        var accumulator = parts[0];

        for (var i = 1; i < parts.Count; i++)
        {
            accumulator = AndAlso(accumulator, parts[i]);
        }

        return accumulator;
    }

    /// <summary>Folds a list with <c>||</c>. An empty list is <c>false</c>.</summary>
    public static Ex Any(IReadOnlyList<Ex> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            return False;
        }

        var accumulator = parts[0];

        for (var i = 1; i < parts.Count; i++)
        {
            accumulator = OrElse(accumulator, parts[i]);
        }

        return accumulator;
    }

    // ---------------------------------------------------------------------------------
    // Shared writers
    // ---------------------------------------------------------------------------------

    internal static void WriteArgumentList(IOutputContext context, IReadOnlyList<Ex> args)
    {
        context.Write("(");
        WriteSeparated(context, args);
        context.Write(")");
    }

    internal static void WriteBracketedList(IOutputContext context, IReadOnlyList<Ex> items)
    {
        context.Write("[");
        WriteSeparated(context, items);
        context.Write("]");
    }

    internal static void WriteSeparated(IOutputContext context, IReadOnlyList<Ex> items)
    {
        if (items == null)
        {
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                context.Write(", ");
            }

            // An argument list is a comma-separated sequence of full expressions: a lambda
            // or an assignment needs no brackets here.
            WriteOperand(context, items[i], ExPrecedence.Assignment);
        }
    }

    internal static void WriteTypeArguments(IOutputContext context, IReadOnlyList<ITypeDefinition> typeArguments)
    {
        if (typeArguments == null || typeArguments.Count == 0)
        {
            return;
        }

        context.Write("<");

        for (var i = 0; i < typeArguments.Count; i++)
        {
            if (i > 0)
            {
                context.Write(", ");
            }

            context.Write(typeArguments[i]);
        }

        context.Write(">");
    }
}
