using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CSharpAuthor;

/// <summary>
/// Reads one .editorconfig option. Shaped to match Roslyn's
/// <c>AnalyzerConfigOptions.TryGetValue</c> exactly, so the Roslyn-gated bridge is a method group
/// and the core library needs no Roslyn reference to accept one.
/// </summary>
public delegate bool EditorConfigOptionLookup(string key, out string? value);

public sealed partial class EmitProfile
{
    /// <summary>
    /// A profile that formats the way the host project's .editorconfig says to.
    /// </summary>
    /// <remarks>
    /// Formatting only. There is no .editorconfig key for the language version - that comes from
    /// the project, which is why <see cref="Target"/> is left alone here and set from
    /// <c>CSharpParseOptions.LanguageVersion</c> by the Roslyn-gated bridge.
    /// </remarks>
    public static EmitProfile FromEditorConfig(EditorConfigOptionLookup lookup)
    {
        if (lookup == null)
        {
            throw new ArgumentNullException(nameof(lookup));
        }

        var profile = Default.Clone();

        ApplyIndent(profile, lookup);
        ApplyNewLine(profile, lookup);
        ApplyBraces(profile, lookup);
        ApplyNamespaceStyle(profile, lookup);
        ApplyVar(profile, lookup);
        ApplyExpressionBodies(profile, lookup);

        return profile;
    }

    /// <summary>
    /// <see cref="FromEditorConfig(EditorConfigOptionLookup)"/> from options already read.
    /// </summary>
    public static EmitProfile FromEditorConfig(IReadOnlyDictionary<string, string> options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        return FromEditorConfig((string key, out string? value) =>
        {
            var found = options.TryGetValue(key, out var raw);

            value = raw;

            return found;
        });
    }

    /// <summary>
    /// <see cref="FromEditorConfig(EditorConfigOptionLookup)"/> from the text of an .editorconfig
    /// file, resolved for one file name.
    /// </summary>
    /// <remarks>
    /// The text, not a path: reading files is not something a source generator may do, and this
    /// code is compiled into source generators.
    /// </remarks>
    public static EmitProfile FromEditorConfigText(string editorConfigText, string fileName = "Generated.cs") =>
        FromEditorConfig(ParseEditorConfig(editorConfigText, fileName));

    /// <summary>
    /// The options an .editorconfig applies to one file name, later sections winning.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseEditorConfig(string editorConfigText, string fileName)
    {
        if (editorConfigText == null)
        {
            throw new ArgumentNullException(nameof(editorConfigText));
        }

        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sectionApplies = true;

        foreach (var rawLine in editorConfigText.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line[0] == '#' || line[0] == ';')
            {
                continue;
            }

            if (line[0] == '[' && line[line.Length - 1] == ']')
            {
                sectionApplies = SectionMatches(line.Substring(1, line.Length - 2), fileName);

                continue;
            }

            if (!sectionApplies)
            {
                continue;
            }

            var separator = line.IndexOf('=');

            if (separator <= 0)
            {
                continue;
            }

            var key = line.Substring(0, separator).Trim();
            var value = line.Substring(separator + 1).Trim();

            options[key] = value;
        }

        return options;
    }

    // ---- the individual mappings ------------------------------------------------------------

    private static void ApplyIndent(EmitProfile profile, EditorConfigOptionLookup lookup)
    {
        var style = Read(lookup, "indent_style");
        var tabWidth = ReadInt(lookup, "tab_width");
        var indentSize = Read(lookup, "indent_size");

        if (string.Equals(style, "tab", StringComparison.OrdinalIgnoreCase))
        {
            // One tab is one level. Taking indent_size literally would give four tabs a level,
            // which is not what a file indented with tabs looks like.
            profile.IndentChar = '\t';
            profile.IndentWidth = 1;

            return;
        }

        if (style != null)
        {
            profile.IndentChar = ' ';
        }

        if (string.Equals(indentSize, "tab", StringComparison.OrdinalIgnoreCase))
        {
            if (tabWidth.HasValue)
            {
                profile.IndentWidth = tabWidth.Value;
            }

            return;
        }

        var size = ToInt(indentSize) ?? tabWidth;

        if (size.HasValue && size.Value > 0)
        {
            profile.IndentWidth = size.Value;
        }
    }

    private static void ApplyNewLine(EmitProfile profile, EditorConfigOptionLookup lookup)
    {
        var value = Read(lookup, "end_of_line");

        if (value == null)
        {
            return;
        }

        switch (value.ToLowerInvariant())
        {
            case "lf":
                profile.NewLine = "\n";
                break;
            case "crlf":
                profile.NewLine = "\r\n";
                break;
            case "cr":
                profile.NewLine = "\r";
                break;
        }
    }

    private static void ApplyBraces(EmitProfile profile, EditorConfigOptionLookup lookup)
    {
        var value = Read(lookup, "csharp_new_line_before_open_brace");

        if (value == null)
        {
            return;
        }

        value = value.ToLowerInvariant();

        if (value == "all")
        {
            profile.Braces = BraceStyle.Allman;

            return;
        }

        if (value == "none")
        {
            profile.Braces = BraceStyle.KAndR;

            return;
        }

        // A partial list. Generated files are types and members before they are anything else, so
        // those two decide it.
        profile.Braces = value.IndexOf("types", StringComparison.Ordinal) >= 0 ||
                         value.IndexOf("methods", StringComparison.Ordinal) >= 0
            ? BraceStyle.Allman
            : BraceStyle.KAndR;
    }

    private static void ApplyNamespaceStyle(EmitProfile profile, EditorConfigOptionLookup lookup)
    {
        var value = Read(lookup, "csharp_style_namespace_declarations");

        if (value == null)
        {
            return;
        }

        if (string.Equals(value, "file_scoped", StringComparison.OrdinalIgnoreCase))
        {
            profile.FileScopedNamespace = true;
        }
        else if (string.Equals(value, "block_scoped", StringComparison.OrdinalIgnoreCase))
        {
            profile.FileScopedNamespace = false;
        }
    }

    private static void ApplyVar(EmitProfile profile, EditorConfigOptionLookup lookup)
    {
        // Three keys, one flag. Generated code declares locals where the type is apparent far more
        // often than anywhere else, so that key decides when it is present.
        var value = ReadBool(lookup, "csharp_style_var_when_type_is_apparent") ??
                    ReadBool(lookup, "csharp_style_var_elsewhere") ??
                    ReadBool(lookup, "csharp_style_var_for_built_in_types");

        if (value.HasValue)
        {
            profile.PreferVar = value.Value;
        }
    }

    private static void ApplyExpressionBodies(EmitProfile profile, EditorConfigOptionLookup lookup)
    {
        var value = ReadBool(lookup, "csharp_style_expression_bodied_methods") ??
                    ReadBool(lookup, "csharp_style_expression_bodied_properties") ??
                    ReadBool(lookup, "csharp_style_expression_bodied_accessors");

        if (value.HasValue)
        {
            profile.PreferExpressionBodied = value.Value;
        }
    }

    // ---- reading ----------------------------------------------------------------------------

    private static string? Read(EditorConfigOptionLookup lookup, string key)
    {
        if (!lookup(key, out var value) || value == null)
        {
            return null;
        }

        // Most .editorconfig style values carry a severity: `file_scoped:error`, `true:suggestion`.
        var severity = value.IndexOf(':');

        return (severity < 0 ? value : value.Substring(0, severity)).Trim();
    }

    private static bool? ReadBool(EditorConfigOptionLookup lookup, string key)
    {
        var value = Read(lookup, key);

        if (value == null)
        {
            return null;
        }

        switch (value.ToLowerInvariant())
        {
            case "true":
            // The style is wanted, with a condition this library has no way to evaluate before it
            // has written the member. Wanting it is the half that is being asked about.
            case "when_on_single_line":
            case "when_possible":
                return true;
            case "false":
            case "never":
                return false;
            default:
                return null;
        }
    }

    private static int? ReadInt(EditorConfigOptionLookup lookup, string key) => ToInt(Read(lookup, key));

    private static int? ToInt(string? value) =>
        value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (int?)null;

    /// <summary>
    /// Whether an .editorconfig section header applies to a file name.
    /// </summary>
    private static bool SectionMatches(string section, string fileName)
    {
        var pattern = new StringBuilder("^");

        for (var index = 0; index < section.Length; index++)
        {
            var character = section[index];

            switch (character)
            {
                case '*':
                    if (index + 1 < section.Length && section[index + 1] == '*')
                    {
                        pattern.Append(".*");
                        index++;
                    }
                    else
                    {
                        pattern.Append("[^/]*");
                    }

                    break;
                case '?':
                    pattern.Append('.');
                    break;
                case '{':
                    pattern.Append("(?:");
                    break;
                case '}':
                    pattern.Append(')');
                    break;
                case ',':
                    pattern.Append('|');
                    break;
                default:
                    pattern.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }

        pattern.Append('$');

        return Regex.IsMatch(
            fileName,
            pattern.ToString(),
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
