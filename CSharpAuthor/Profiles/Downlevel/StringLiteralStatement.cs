using System;
using System.Globalization;
using System.Text;

namespace CSharpAuthor.Profiles;

/// <summary>
/// A string, written the way the target can read it: a raw literal where that is available and
/// worth it, an escaped literal otherwise.
/// </summary>
/// <remarks>
/// The value is held as a value, not as source text, which is what lets the same node choose
/// between the two forms. It also means the escaping happens exactly once, here, rather than at
/// every call site that happened to remember - <c>"he said "hi""</c> is a verified V1 defect and
/// it is a defect of the second kind.
/// </remarks>
public class StringLiteralStatement : BaseOutputComponent
{
    private readonly string _value;

    /// <summary>A string literal for this value.</summary>
    public StringLiteralStatement(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The value, unescaped.</summary>
    public string Value => _value;

    /// <summary>What the literal is for, named in any diagnostic it produces.</summary>
    public string? Context { get; set; }

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        // Only ask about raw literals when one would actually help. A string with nothing to
        // escape gains nothing from three quotes, and asking anyway would record a downlevel
        // decision that was never really taken.
        if (WouldBenefitFromRaw(_value) &&
            CanBeWrittenRaw(_value) &&
            session.MayEmit(LanguageFeature.RawStringLiterals, outputContext, Context))
        {
            WriteRaw(outputContext, _value);

            return;
        }

        outputContext.Write(Quote(_value));
    }

    /// <summary>
    /// The value as an escaped C# string literal, quotes included.
    /// </summary>
    /// <remarks>
    /// Supersedes <see cref="SyntaxHelpers.QuoteString"/>, which wraps the value in quotes and
    /// escapes nothing.
    /// </remarks>
    public static string Quote(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var builder = new StringBuilder(value.Length + 2);

        builder.Append('"');

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    continue;
                case '\\':
                    builder.Append("\\\\");
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

            if (char.IsSurrogate(character))
            {
                // A matched pair is one character to a reader and is left alone. A lone surrogate
                // is not valid text at all, and writing it into the file would produce a source
                // file no editor round-trips.
                if (char.IsHighSurrogate(character) &&
                    index + 1 < value.Length &&
                    char.IsLowSurrogate(value[index + 1]))
                {
                    builder.Append(character);
                    builder.Append(value[index + 1]);
                    index++;

                    continue;
                }

                AppendUnicodeEscape(builder, character);

                continue;
            }

            if (character < ' ' || character == '\u007f' ||
                (character >= '\u0080' && character <= '\u009f') ||
                character == '\u2028' || character == '\u2029')
            {
                AppendUnicodeEscape(builder, character);

                continue;
            }

            builder.Append(character);
        }

        builder.Append('"');

        return builder.ToString();
    }

    /// <summary>Whether a raw literal would save any escaping.</summary>
    public static bool WouldBenefitFromRaw(string value) =>
        value.IndexOf('"') >= 0 || value.IndexOf('\\') >= 0 || value.IndexOf('\n') >= 0;

    /// <summary>
    /// Whether this value can be written as a raw literal at all.
    /// </summary>
    /// <remarks>
    /// A raw literal has no escapes, so anything it cannot show literally rules it out: control
    /// characters, and a single-line literal whose content touches a quote at either end, which
    /// is CS8998. Carriage returns are excluded too - the content of a raw literal is the source
    /// text between the fences, and a value whose line endings are not the file's would come back
    /// out changed.
    /// </remarks>
    public static bool CanBeWrittenRaw(string value)
    {
        if (value.Length == 0 || value.IndexOf('\r') >= 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character != '\n' && character != '\t' &&
                (character < ' ' || character == '\u007f' ||
                 character == '\u2028' || character == '\u2029'))
            {
                return false;
            }
        }

        if (value.IndexOf('\n') >= 0)
        {
            return true;
        }

        return value[0] != '"' && value[value.Length - 1] != '"';
    }

    private static void WriteRaw(IOutputContext outputContext, string value)
    {
        var fence = new string('"', Math.Max(3, LongestQuoteRun(value) + 1));

        if (value.IndexOf('\n') < 0)
        {
            outputContext.Write(fence);
            outputContext.Write(value);
            outputContext.Write(fence);

            return;
        }

        // Multi-line: the closing fence's indentation is stripped from every line, so writing
        // each line at exactly that indentation gives the value back unchanged.
        outputContext.Write(fence);
        outputContext.WriteLine();

        foreach (var line in value.Split('\n'))
        {
            outputContext.WriteIndent(line);
            outputContext.WriteLine();
        }

        outputContext.WriteIndent(fence);
    }

    private static int LongestQuoteRun(string value)
    {
        var longest = 0;
        var run = 0;

        foreach (var character in value)
        {
            run = character == '"' ? run + 1 : 0;

            if (run > longest)
            {
                longest = run;
            }
        }

        return longest;
    }

    private static void AppendUnicodeEscape(StringBuilder builder, char character)
    {
        builder.Append("\\u");
        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
    }
}
