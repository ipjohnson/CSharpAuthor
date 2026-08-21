using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSharpAuthor.Expressions;

/// <summary>
/// Turning a CLR value into C# source text. Every method here is culture-invariant and
/// every one escapes, because <c>1,5</c> on de-DE and <c>"he said "hi""</c> are the two
/// cheapest ways to emit code that does not compile.
/// </summary>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
static class CSharpText
{
    private static readonly HashSet<string> ReservedKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while",
    };

    /// <summary>True when <paramref name="name"/> is a reserved C# keyword.</summary>
    /// <remarks>
    /// Contextual keywords (<c>var</c>, <c>value</c>, <c>record</c>, <c>when</c> …) are
    /// deliberately absent: they are legal identifiers, and escaping them would emit
    /// <c>@value</c> in property setters, which is correct but gratuitous.
    /// </remarks>
    public static bool IsReservedKeyword(string name)
    {
        return name != null && ReservedKeywords.Contains(name);
    }

    /// <summary>
    /// An identifier, verbatim-escaped when it collides with a keyword. Anything that is
    /// not a simple identifier (a dotted path, an already-escaped <c>@name</c>) is passed
    /// through untouched.
    /// </summary>
    public static string Identifier(string name)
    {
        if (string.IsNullOrEmpty(name) || name[0] == '@')
        {
            return name;
        }

        return ReservedKeywords.Contains(name) ? "@" + name : name;
    }

    /// <summary>A double-quoted, fully escaped string literal.</summary>
    public static string StringLiteral(string value)
    {
        if (value == null)
        {
            return "null";
        }

        var builder = new StringBuilder(value.Length + 2);

        builder.Append('"');
        AppendEscaped(builder, value, insideCharLiteral: false);
        builder.Append('"');

        return builder.ToString();
    }

    /// <summary>
    /// A verbatim string literal, <c>@"…"</c>. Only <c>"</c> needs doubling; backslashes
    /// and newlines are content. Returns a regular literal instead when the value contains
    /// a carriage return, because a bare CR inside a verbatim literal is invisible and
    /// survives no round trip through a text editor.
    /// </summary>
    public static string VerbatimStringLiteral(string value)
    {
        if (value == null)
        {
            return "null";
        }

        if (value.IndexOf('\r') >= 0)
        {
            return StringLiteral(value);
        }

        return "@\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>A single-quoted, fully escaped character literal.</summary>
    public static string CharLiteral(char value)
    {
        var builder = new StringBuilder(6);

        builder.Append('\'');
        AppendEscaped(builder, value.ToString(CultureInfo.InvariantCulture), insideCharLiteral: true);
        builder.Append('\'');

        return builder.ToString();
    }

    private static void AppendEscaped(StringBuilder builder, string value, bool insideCharLiteral)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            switch (ch)
            {
                case '\\':
                    builder.Append("\\\\");
                    continue;
                case '"':
                    builder.Append(insideCharLiteral ? "\"" : "\\\"");
                    continue;
                case '\'':
                    builder.Append(insideCharLiteral ? "\\'" : "'");
                    continue;
                case '\0':
                    builder.Append("\\0");
                    continue;
                case '\a':
                    builder.Append("\\a");
                    continue;
                case '\b':
                    builder.Append("\\b");
                    continue;
                case '\f':
                    builder.Append("\\f");
                    continue;
                case '\n':
                    builder.Append("\\n");
                    continue;
                case '\r':
                    builder.Append("\\r");
                    continue;
                case '\t':
                    builder.Append("\\t");
                    continue;
                case '\v':
                    builder.Append("\\v");
                    continue;
            }

            // A lone surrogate is not a character; \uXXXX keeps it addressable and keeps
            // the file valid UTF-8. A well-formed pair is left alone so the source stays
            // readable.
            if (char.IsSurrogate(ch))
            {
                var wellFormedPair =
                    char.IsHighSurrogate(ch) &&
                    i + 1 < value.Length &&
                    char.IsLowSurrogate(value[i + 1]);

                if (wellFormedPair)
                {
                    builder.Append(ch);
                    builder.Append(value[i + 1]);
                    i++;
                    continue;
                }

                AppendUnicodeEscape(builder, ch);
                continue;
            }

            if (char.IsControl(ch))
            {
                AppendUnicodeEscape(builder, ch);
                continue;
            }

            builder.Append(ch);
        }
    }

    private static void AppendUnicodeEscape(StringBuilder builder, char ch)
    {
        builder.Append("\\u");
        builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
    }

    /// <summary><c>42</c></summary>
    public static string Int32Literal(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary><c>42U</c></summary>
    public static string UInt32Literal(uint value)
    {
        return value.ToString(CultureInfo.InvariantCulture) + "U";
    }

    /// <summary><c>42L</c></summary>
    public static string Int64Literal(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture) + "L";
    }

    /// <summary><c>42UL</c></summary>
    public static string UInt64Literal(ulong value)
    {
        return value.ToString(CultureInfo.InvariantCulture) + "UL";
    }

    /// <summary><c>1.5f</c> — the suffix is mandatory or the literal is a double (CS0664).</summary>
    public static string SingleLiteral(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return NonFiniteSingle(value);
        }

        return value.ToString("R", CultureInfo.InvariantCulture) + "F";
    }

    /// <summary><c>1.5D</c></summary>
    public static string DoubleLiteral(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return NonFiniteDouble(value);
        }

        return value.ToString("R", CultureInfo.InvariantCulture) + "D";
    }

    /// <summary><c>1.5M</c></summary>
    public static string DecimalLiteral(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture) + "M";
    }

    private static string NonFiniteSingle(float value)
    {
        if (float.IsNaN(value))
        {
            return "float.NaN";
        }

        return float.IsPositiveInfinity(value) ? "float.PositiveInfinity" : "float.NegativeInfinity";
    }

    private static string NonFiniteDouble(double value)
    {
        if (double.IsNaN(value))
        {
            return "double.NaN";
        }

        return double.IsPositiveInfinity(value) ? "double.PositiveInfinity" : "double.NegativeInfinity";
    }
}
