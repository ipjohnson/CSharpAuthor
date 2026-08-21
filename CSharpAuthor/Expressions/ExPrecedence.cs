using System;

namespace CSharpAuthor.Expressions;

/// <summary>
/// The C# precedence ladder, densely numbered so that "one level tighter" is
/// always <c>level + 1</c>. Higher binds tighter.
/// </summary>
/// <remarks>
/// <para>
/// The ordering is the language's, not a convenience: it was verified against the
/// compiler rather than copied from memory. Two entries surprise people and are
/// worth stating out loud:
/// </para>
/// <list type="bullet">
/// <item><description><c>x switch { }</c> and <c>x with { }</c> bind TIGHTER than
/// <c>*</c>. <c>2 * x switch { 1 =&gt; 10, 2 =&gt; 30, _ =&gt; 99 }</c> evaluates as
/// <c>2 * (x switch ...)</c>.</description></item>
/// <item><description><c>a..b</c> binds tighter than <c>+</c>. <c>-1 + 2..^2</c> is
/// <c>-1 + (2..^2)</c> — which is a compile error, not an off-by-one.</description></item>
/// </list>
/// <para>
/// <see cref="NullChain"/> sits between <see cref="Unary"/> and <see cref="Primary"/>.
/// It is not a language level; it exists because <c>a?.b.c</c> and <c>(a?.b).c</c> are
/// different programs — the first yields <c>null</c> when <c>a</c> is null, the second
/// throws. Anything that requires a genuine primary operand (member access, invocation,
/// element access) asks for <see cref="Primary"/> and therefore parenthesises a
/// null-conditional chain instead of silently extending it.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
static class ExPrecedence
{
    /// <summary>Unknown shape. Always parenthesised in an operand position.</summary>
    public const int Lowest = 0;

    /// <summary><c>=</c> <c>+=</c> … and <c>=&gt;</c>. Right associative.</summary>
    public const int Assignment = 1;

    /// <summary><c>c ? t : f</c>. Right associative.</summary>
    public const int Conditional = 2;

    /// <summary><c>??</c>. Right associative.</summary>
    public const int Coalesce = 3;

    /// <summary><c>||</c></summary>
    public const int ConditionalOr = 4;

    /// <summary><c>&amp;&amp;</c></summary>
    public const int ConditionalAnd = 5;

    /// <summary><c>|</c></summary>
    public const int BitwiseOr = 6;

    /// <summary><c>^</c></summary>
    public const int BitwiseXor = 7;

    /// <summary><c>&amp;</c></summary>
    public const int BitwiseAnd = 8;

    /// <summary><c>==</c> <c>!=</c></summary>
    public const int Equality = 9;

    /// <summary><c>&lt;</c> <c>&gt;</c> <c>&lt;=</c> <c>&gt;=</c> <c>is</c> <c>as</c></summary>
    public const int Relational = 10;

    /// <summary><c>&lt;&lt;</c> <c>&gt;&gt;</c> <c>&gt;&gt;&gt;</c></summary>
    public const int Shift = 11;

    /// <summary><c>+</c> <c>-</c></summary>
    public const int Additive = 12;

    /// <summary><c>*</c> <c>/</c> <c>%</c></summary>
    public const int Multiplicative = 13;

    /// <summary><c>x switch { }</c>, <c>x with { }</c>. Tighter than <c>*</c>.</summary>
    public const int SwitchWith = 14;

    /// <summary><c>a..b</c>. Tighter than <c>+</c>.</summary>
    public const int Range = 15;

    /// <summary><c>-x</c> <c>!x</c> <c>~x</c> <c>++x</c> <c>--x</c> <c>^x</c> <c>(T)x</c> <c>await x</c> <c>&amp;x</c> <c>*x</c></summary>
    public const int Unary = 16;

    /// <summary>
    /// A null-conditional chain, <c>a?.b</c>. Primary for every purpose except being
    /// the target of a further <c>.</c>, <c>(</c> or <c>[</c>, which would extend the
    /// chain and change the program.
    /// </summary>
    public const int NullChain = 17;

    /// <summary><c>a.b</c> <c>f(x)</c> <c>a[i]</c> <c>x++</c> <c>x!</c> <c>new</c> <c>typeof</c> … and every literal.</summary>
    public const int Primary = 18;

    /// <summary>Operand requirement for the left side of a binary operator at <paramref name="precedence"/>.</summary>
    public static int LeftRequirement(int precedence, bool rightAssociative)
    {
        return rightAssociative ? precedence + 1 : precedence;
    }

    /// <summary>Operand requirement for the right side of a binary operator at <paramref name="precedence"/>.</summary>
    public static int RightRequirement(int precedence, bool rightAssociative)
    {
        return rightAssociative ? precedence : precedence + 1;
    }
}

/// <summary>
/// Lexical hazards that precedence alone does not describe. They exist because
/// <c>- -a</c> and <c>--a</c> are both valid C# and mean different things.
/// </summary>
[Flags]
internal enum ExFlags
{
    None = 0,

    /// <summary>Rendered text begins with <c>-</c> or <c>--</c>.</summary>
    LeadsWithMinus = 1,

    /// <summary>Rendered text begins with <c>+</c> or <c>++</c>.</summary>
    LeadsWithPlus = 2,

    /// <summary>
    /// Parenthesising this node is a compile error, not merely noise — <c>throw</c>
    /// expressions (CS8115) and argument modifiers such as <c>out x</c>.
    /// </summary>
    NeverParenthesize = 4,
}
