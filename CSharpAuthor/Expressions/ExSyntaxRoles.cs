namespace CSharpAuthor.Expressions;

/// <summary>
/// Marker for anything that can stand where C# expects an expression.
/// </summary>
/// <remarks>
/// Deliberately named <c>…Node</c> rather than <c>IExpression</c>: the generated
/// grammar layer declares <c>CSharpAuthor.Syntax.IExpression</c>, and two identically
/// named interfaces in two imported namespaces would make every call site ambiguous.
/// Every type here is <c>partial</c>, so the generated interfaces can be attached at
/// integration time without editing this file.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
interface IExpressionNode : IOutputComponent
{
    /// <summary>Where this expression sits on <see cref="ExPrecedence"/>'s ladder.</summary>
    int Precedence { get; }
}

/// <summary>Marker for anything that can stand where C# expects a statement.</summary>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
interface IStatementNode : IOutputComponent
{
}

/// <summary>Marker for anything that can stand where C# expects a pattern.</summary>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
interface IPatternNode : IOutputComponent
{
    /// <summary>Where this pattern sits on <see cref="PatPrecedence"/>'s ladder.</summary>
    int PatternPrecedence { get; }
}

/// <summary>
/// Pattern combinators have their own, much shorter, ladder:
/// <c>or</c> is looser than <c>and</c>, which is looser than <c>not</c>.
/// </summary>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
static class PatPrecedence
{
    /// <summary>Unknown shape. Always parenthesised.</summary>
    public const int Lowest = 0;

    /// <summary><c>a or b</c></summary>
    public const int Or = 1;

    /// <summary><c>a and b</c></summary>
    public const int And = 2;

    /// <summary><c>not a</c></summary>
    public const int Not = 3;

    /// <summary>A type, constant, relational, property, list or var pattern.</summary>
    public const int Primary = 4;
}
