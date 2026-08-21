#nullable enable

using System.Globalization;
using System.Text;

namespace CSharpAuthor.Syntax;

/// <summary>
/// The escape hatch, and it composes.
/// </summary>
/// <remarks>
/// <para>
/// Implements every slot interface a caller is likely to need, so dropping to text costs
/// one node rather than the file. Parts that are <see cref="ITypeDefinition"/> still go
/// through the context unrendered, so a raw fragment carrying a type still participates in
/// the name plan and still gets its namespace derived.
/// </para>
/// <para>
/// Spacing around a raw part is left alone: the writer records it as
/// <see cref="TokenRole.Raw"/>, which neither claims a space nor forbids one.
/// </para>
/// </remarks>
#if !CSHARPAUTHOR_SOURCE
public
#endif
sealed class Raw : SyntaxNode, IExpression, IStatement, IPattern, IMemberDeclaration, IType
{
    private readonly object?[] _parts;

    public Raw(params object?[] parts) => _parts = parts;

    /// <inheritdoc />
    public override void WriteOutput(IOutputContext outputContext)
    {
        var writer = SyntaxWriter.For(outputContext);

        foreach (var part in _parts)
        {
            switch (part)
            {
                case null:
                    break;
                case ITypeDefinition type:
                    writer.Type(TypeRef.Of(type));
                    break;
                case ISyntax node:
                    writer.Node(node);
                    break;
                case IOutputComponent component:
                    component.WriteOutput(outputContext);
                    break;
                default:
                    writer.Token(TokenRole.Raw, part.ToString());
                    break;
            }
        }
    }
}

/// <summary>
/// Builds the <see cref="LiteralExpression"/> nodes whose text is a value rather than a
/// token, with the quoting and the culture handled once.
/// </summary>
/// <remarks>
/// The grammar gives <c>LiteralExpressionSyntax</c> a token slot and no spelling, so the
/// generator emits it with a string argument. What it cannot generate is what belongs in
/// that string - escaping a quote, or writing <c>1.5</c> rather than <c>1,5</c> on a
/// de-DE machine.
/// </remarks>
#if !CSHARPAUTHOR_SOURCE
public
#endif
static class Literal
{
    /// <summary>A quoted, escaped string literal.</summary>
    public static LiteralExpression String(string value) => new(Quote(value));

    /// <summary>A quoted, escaped character literal.</summary>
    public static LiteralExpression Char(char value) => new("'" + Escape(value, '\'') + "'");

    public static LiteralExpression Int(int value) => new(value.ToString(CultureInfo.InvariantCulture));

    public static LiteralExpression Long(long value) => new(value.ToString(CultureInfo.InvariantCulture) + "L");

    public static LiteralExpression Double(double value) => new(value.ToString("R", CultureInfo.InvariantCulture));

    public static LiteralExpression Float(float value) => new(value.ToString("R", CultureInfo.InvariantCulture) + "f");

    public static LiteralExpression Decimal(decimal value) => new(value.ToString(CultureInfo.InvariantCulture) + "m");

    public static LiteralExpression Bool(bool value) => new(value ? "true" : "false");

    public static LiteralExpression Null() => new("null");

    /// <summary>Quote and escape a string the way the C# lexer will read back.</summary>
    public static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);

        builder.Append('"');

        foreach (var c in value)
        {
            builder.Append(Escape(c, '"'));
        }

        return builder.Append('"').ToString();
    }

    private static string Escape(char c, char quote)
    {
        switch (c)
        {
            case '\\': return "\\\\";
            case '\0': return "\\0";
            case '\a': return "\\a";
            case '\b': return "\\b";
            case '\f': return "\\f";
            case '\n': return "\\n";
            case '\r': return "\\r";
            case '\t': return "\\t";
            case '\v': return "\\v";
        }

        if (c == quote)
        {
            return "\\" + c;
        }

        // Anything the lexer would not read back as itself becomes an escape. Surrogates
        // are emitted as their own \u escapes, which round-trips a pair correctly.
        if (c < ' ' || c == '\u007F' || c == '\u0085' || c == '\u2028' || c == '\u2029' || char.IsSurrogate(c))
        {
            return "\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
        }

        return c.ToString();
    }
}
