using CSharpAuthor.Expressions;

namespace CSharpAuthor;

/// <summary>
/// A component that knows how tightly its rendered text binds.
/// </summary>
/// <remarks>
/// <para>
/// Only components that bind <em>looser</em> than a primary expression need to say so. Everything
/// else - a literal, a name, a member access, an invocation, <c>x++</c>, <c>x?.y</c> - is already a
/// primary and needs no parentheses in any operand position, so the absence of this interface is
/// the correct answer for almost every component in the library.
/// </para>
/// <para>
/// Implemented explicitly, so it adds nothing to the public surface of the component that carries
/// it. The scale is <see cref="ExPrecedence"/>'s, shared with the expression layer rather than
/// invented a second time here.
/// </para>
/// </remarks>
internal interface IPrecedenceComponent
{
    int Precedence { get; }
}

/// <summary>
/// Parenthesising an operand that would otherwise be read as part of something else.
/// </summary>
/// <remarks>
/// <para>
/// <c>.</c>, <c>(</c> and <c>[</c> all require a <em>primary</em> expression on their left. A cast
/// and an <c>await</c> are unary, one level looser, so composing a member access onto either of
/// them without parentheses moves the operator to the far side of the access:
/// <c>(Dog)animal.Breed</c> is <c>(Dog)(animal.Breed)</c>, and <c>await GetAsync().Length</c> is
/// <c>await (GetAsync().Length)</c>. Both are different expressions from the one the caller built,
/// and both compile whenever the wrong reading happens to be well-typed - which is what makes this
/// the defect class in V2-HANDOFF.md section 1 rather than a syntax error someone would have caught.
/// </para>
/// </remarks>
internal static class ExpressionPrecedence
{
    /// <summary>
    /// <paramref name="component"/> in a form that can carry a <c>.</c>, a <c>(</c> or a <c>[</c>,
    /// parenthesised only when it would otherwise bind looser than the operator applied to it.
    /// </summary>
    public static IOutputComponent AsPrimary(IOutputComponent component)
    {
        if (component is IPrecedenceComponent precedence &&
            precedence.Precedence < ExPrecedence.Primary)
        {
            return new WrapStatement(component, "(", ")");
        }

        return component;
    }
}
