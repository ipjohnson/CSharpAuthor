using System;
using System.Globalization;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// Turns a runtime value into the C# literal text that denotes it.
/// </summary>
/// <remarks>
/// <para>
/// Every path through here is culture invariant. <c>value.ToString()</c> honours
/// <see cref="CultureInfo.CurrentCulture"/>, so a generator running on a machine set to de-DE wrote
/// <c>1,5</c> for <c>1.5</c> and sv-SE wrote a U+2212 minus sign for a negative number - output
/// that does not compile, produced only on someone else's machine.
/// </para>
/// <para>
/// Suffixes are part of denoting the value, not decoration. <c>float f = 1.5;</c> is CS0664 because
/// <c>1.5</c> is a <c>double</c>; only <c>1.5f</c> is the float. The same applies to
/// <c>decimal</c>, and to <c>uint</c>/<c>ulong</c> values above <see cref="int.MaxValue"/>.
/// </para>
/// </remarks>
public static class LiteralFormatter
{
    /// <summary>
    /// The literal text for <paramref name="value"/>, quoted and suffixed as its type requires.
    /// </summary>
    /// <remarks>
    /// <see cref="string"/> is deliberately not quoted. Throughout this library a string is a
    /// fragment of code - <c>AddCode("Foo()")</c>, <c>CodeOutputComponent.Get("Lifetime.Scoped")</c>
    /// - and quoting it here would turn every one of them into text. Callers that mean a string
    /// literal ask for one through <see cref="QuoteString"/>.
    /// </remarks>
    public static string Format(object? value)
    {
        switch (value)
        {
            case null:
                return "null";
            case bool boolValue:
                return boolValue ? "true" : "false";
            case char charValue:
                return QuoteChar(charValue);
            case string stringValue:
                return stringValue;
            default:
                return FormatNumeric(value);
        }
    }

    /// <summary>
    /// The invariant text for a numeric value, with the suffix that fixes its type.
    /// </summary>
    /// <remarks>
    /// Anything that is not a known numeric type falls through to its own <c>ToString</c>, asked for
    /// invariantly where the type knows how to honour that.
    /// </remarks>
    public static string FormatNumeric(object value)
    {
        switch (value)
        {
            // Suffix required: the bare digits denote a value of a different type.
            case float floatValue:
                return FormatSingle(floatValue);
            case double doubleValue:
                return FormatDouble(doubleValue);
            case decimal decimalValue:
                return decimalValue.ToString(CultureInfo.InvariantCulture) + "m";
            case long longValue:
                return longValue.ToString(CultureInfo.InvariantCulture) + "L";
            case ulong ulongValue:
                return ulongValue.ToString(CultureInfo.InvariantCulture) + "UL";
            case uint uintValue:
                return uintValue.ToString(CultureInfo.InvariantCulture) + "U";

            // No suffix needed - an int literal converts implicitly - but still invariant, because
            // the negative sign is not "-" in every culture.
            case int intValue:
                return intValue.ToString(CultureInfo.InvariantCulture);
            case short shortValue:
                return shortValue.ToString(CultureInfo.InvariantCulture);
            case sbyte sbyteValue:
                return sbyteValue.ToString(CultureInfo.InvariantCulture);
            case byte byteValue:
                return byteValue.ToString(CultureInfo.InvariantCulture);
            case ushort ushortValue:
                return ushortValue.ToString(CultureInfo.InvariantCulture);

            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            default:
                return value.ToString();
        }
    }

    /// <summary>
    /// <paramref name="value"/> as a regular string literal, escaped and in quotes.
    /// </summary>
    public static string QuoteString(string? value)
    {
        if (value == null)
        {
            return "null";
        }

        var builder = new StringBuilder(value.Length + 2);

        builder.Append('"');
        AppendEscaped(builder, value, '"');
        builder.Append('"');

        return builder.ToString();
    }

    /// <summary>
    /// <paramref name="value"/> as a verbatim string literal - <c>@"..."</c>.
    /// </summary>
    /// <remarks>
    /// A verbatim literal escapes only the quote, by doubling it, and every other character stands
    /// for itself. That is what makes it the wrong choice for content holding control characters:
    /// there is no way to write them, so a newline in the value becomes a real line break in the
    /// output. This is offered for content a caller wants to read back unchanged - a path, a
    /// regular expression - and <see cref="QuoteString"/> is the safe default.
    /// </remarks>
    public static string QuoteVerbatimString(string? value)
    {
        if (value == null)
        {
            return "null";
        }

        return "@\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// The body of a regular string literal, escaped but without the surrounding quotes.
    /// </summary>
    public static string EscapeStringContent(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? "";
        }

        var builder = new StringBuilder(value!.Length);

        AppendEscaped(builder, value, '"');

        return builder.ToString();
    }

    /// <summary>
    /// <paramref name="value"/> as a character literal, escaped and in single quotes.
    /// </summary>
    public static string QuoteChar(char value)
    {
        var builder = new StringBuilder(4);

        builder.Append('\'');
        AppendEscapedChar(builder, value, '\'');
        builder.Append('\'');

        return builder.ToString();
    }

    /// <summary>
    /// The body of a character literal, escaped but without the surrounding quotes.
    /// </summary>
    public static string EscapeCharContent(char value)
    {
        var builder = new StringBuilder(2);

        AppendEscapedChar(builder, value, '\'');

        return builder.ToString();
    }

    private static string FormatSingle(float value)
    {
        if (float.IsNaN(value))
        {
            return "float.NaN";
        }

        if (float.IsPositiveInfinity(value))
        {
            return "float.PositiveInfinity";
        }

        if (float.IsNegativeInfinity(value))
        {
            return "float.NegativeInfinity";
        }

        return value.ToString("R", CultureInfo.InvariantCulture) + "f";
    }

    private static string FormatDouble(double value)
    {
        if (double.IsNaN(value))
        {
            return "double.NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "double.PositiveInfinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "double.NegativeInfinity";
        }

        return value.ToString("R", CultureInfo.InvariantCulture) + "d";
    }

    private static void AppendEscaped(StringBuilder builder, string value, char quote)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];

            // A high surrogate followed by its low surrogate is one real character. Writing the pair
            // through unchanged keeps it a character; escaping each half would still round-trip, but
            // reads as two numbers where the source said one letter.
            if (char.IsHighSurrogate(character) &&
                i + 1 < value.Length &&
                char.IsLowSurrogate(value[i + 1]))
            {
                builder.Append(character);
                builder.Append(value[i + 1]);
                i++;
                continue;
            }

            AppendEscapedChar(builder, character, quote);
        }
    }

    private static void AppendEscapedChar(StringBuilder builder, char character, char quote)
    {
        switch (character)
        {
            case '\\':
                builder.Append("\\\\");
                return;
            case '\0':
                builder.Append("\\0");
                return;
            case '\a':
                builder.Append("\\a");
                return;
            case '\b':
                builder.Append("\\b");
                return;
            case '\f':
                builder.Append("\\f");
                return;
            case '\n':
                builder.Append("\\n");
                return;
            case '\r':
                builder.Append("\\r");
                return;
            case '\t':
                builder.Append("\\t");
                return;
            case '\v':
                builder.Append("\\v");
                return;
        }

        if (character == quote)
        {
            builder.Append('\\');
            builder.Append(character);
            return;
        }

        // An unpaired surrogate is not a character and has no textual form; a control character has
        // one but it is invisible, and an invisible character in a literal is how a generator ends
        // up emitting a file nobody can diff. Both become their escape.
        if (char.IsControl(character) || char.IsSurrogate(character))
        {
            builder.Append("\\u");
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            return;
        }

        builder.Append(character);
    }
}
