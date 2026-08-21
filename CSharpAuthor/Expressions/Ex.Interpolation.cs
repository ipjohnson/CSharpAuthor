using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSharpAuthor.Expressions;

public sealed partial class Ex
{
    /// <summary>
    /// An interpolated string, <c>$"…"</c>. Strings are literal text; everything else is a
    /// hole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Literal text and hole text obey different rules, and conflating them is the whole
    /// bug class here. In literal text <c>"</c> and <c>\</c> are escaped and <c>{</c> and
    /// <c>}</c> are doubled; inside a hole none of that applies, so
    /// <c>$"{table["key"]}"</c> keeps its inner quotes exactly as written — which is legal
    /// C# in a non-verbatim interpolated string, and was verified as such.
    /// </para>
    /// <para>
    /// A hole holding a conditional expression is bracketed, because the <c>:</c> would
    /// otherwise start a format specifier: <c>$"{(flag ? 1 : 2)}"</c>.
    /// </para>
    /// </remarks>
    public static Ex Interpolate(params object?[] parts)
    {
        return InterpolateCore(parts, verbatim: false);
    }

    /// <summary>
    /// A verbatim interpolated string, <c>$@"…"</c>, for content with newlines or
    /// backslashes.
    /// </summary>
    /// <remarks>
    /// Before C# 11 a hole in a verbatim interpolated string may not contain a <c>"</c>.
    /// Nothing here can detect that for you; prefer <see cref="Interpolate"/> unless the
    /// literal text genuinely needs the verbatim form.
    /// </remarks>
    public static Ex InterpolateVerbatim(params object?[] parts)
    {
        return InterpolateCore(parts, verbatim: true);
    }

    private static Ex InterpolateCore(object?[] parts, bool verbatim)
    {
        var prefix = verbatim ? "$@\"" : "$\"";

        return new Ex(ExPrecedence.Primary, c =>
        {
            c.Write(prefix);

            foreach (var part in parts)
            {
                switch (part)
                {
                    case null:
                        continue;

                    case string text:
                        c.Write(EscapeInterpolatedText(text, verbatim));
                        continue;

                    case ExInterpolationHole hole:
                        hole.WriteOutput(c);
                        continue;

                    case ITypeDefinition type:
                        c.Write("{");
                        c.Write(type);
                        c.Write("}");
                        continue;

                    case Ex expression:
                        WriteHole(c, expression, null, null);
                        continue;

                    case Expressions.Raw raw:
                        WriteHole(c, raw.ToExpression(), null, null);
                        continue;

                    default:
                        WriteHole(c, Value(part), null, null);
                        continue;
                }
            }

            c.Write("\"");
        });
    }

    /// <summary>
    /// A hole with an alignment and/or a format specifier: <c>{value,10:N2}</c>.
    /// </summary>
    public static ExInterpolationHole Hole(Ex value, int? alignment = null, string? format = null)
    {
        return new ExInterpolationHole(value, alignment, format);
    }

    internal static void WriteHole(IOutputContext context, Ex value, int? alignment, string? format)
    {
        context.Write("{");

        // Anything at or below the conditional operator is bracketed: its `:` would be read
        // as the start of a format specifier, and its `,` as an alignment.
        WriteOperand(context, value, ExPrecedence.Coalesce);

        if (alignment.HasValue)
        {
            context.Write(",");
            context.Write(alignment.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(format))
        {
            context.Write(":");
            context.Write(format!);
        }

        context.Write("}");
    }

    /// <summary>
    /// Escapes literal text for an interpolated string: braces double, and in the
    /// non-verbatim form quotes, backslashes and control characters escape as usual.
    /// </summary>
    internal static string EscapeInterpolatedText(string text, bool verbatim)
    {
        if (text == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);

        if (verbatim)
        {
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '"':
                        builder.Append("\"\"");
                        continue;
                    case '{':
                        builder.Append("{{");
                        continue;
                    case '}':
                        builder.Append("}}");
                        continue;
                    default:
                        builder.Append(ch);
                        continue;
                }
            }

            return builder.ToString();
        }

        // Reuse the ordinary literal escaper, then double the braces. Going the other way
        // round would double the backslashes of an escape sequence.
        var quoted = CSharpText.StringLiteral(text);
        var body = quoted.Substring(1, quoted.Length - 2);

        foreach (var ch in body)
        {
            switch (ch)
            {
                case '{':
                    builder.Append("{{");
                    continue;
                case '}':
                    builder.Append("}}");
                    continue;
                default:
                    builder.Append(ch);
                    continue;
            }
        }

        return builder.ToString();
    }
}

/// <summary>A hole in an interpolated string, with an optional alignment and format.</summary>
public sealed class ExInterpolationHole : IOutputComponent
{
    private readonly Ex _value;
    private readonly int? _alignment;
    private readonly string? _format;

    internal ExInterpolationHole(Ex value, int? alignment, string? format)
    {
        _value = value;
        _alignment = alignment;
        _format = format;
    }

    /// <inheritdoc />
    public void AddUsingNamespace(string ns)
    {
    }

    /// <inheritdoc />
    public void WriteOutput(IOutputContext outputContext)
    {
        Ex.WriteHole(outputContext, _value, _alignment, _format);
    }
}
