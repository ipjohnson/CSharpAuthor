using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// Writing a name that happens to be a C# keyword.
/// </summary>
/// <remarks>
/// <para>
/// A generator naming things after something it read - a database column called <c>class</c>, a
/// JSON property called <c>event</c>, a namespace segment called <c>base</c> - produces a name that
/// is a keyword, and <c>void M(string class)</c> is CS1001. C# has one answer, the <c>@</c> prefix:
/// <c>@class</c> is an identifier spelled with the letters of a keyword, and it is the same
/// identifier as <c>@class</c> anywhere else, so escaping the declaration and every reference to it
/// agree.
/// </para>
/// <para>
/// Only the reserved words are escaped. C#'s contextual keywords - <c>value</c>, <c>var</c>,
/// <c>record</c>, <c>async</c>, <c>where</c>, <c>nint</c> and the rest - are legal identifiers as
/// they stand, and prefixing them would be noise in the output for no gain.
/// </para>
/// <para>
/// Internal: the consumers source-include this library, so they can still use it, but it is not
/// public API anyone has to support forever. V2-HANDOFF.md section 3 already establishes this for
/// generated nodes - "mark generated node types internal when source-included so they don't leak
/// into consumer API surface" - and an incidental helper is the same case.
/// </para>
/// </remarks>
internal static class CSharpIdentifier
{
    /// <summary>
    /// C#'s reserved words. A reserved word cannot be used as an identifier without the prefix.
    /// </summary>
    private static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    /// <summary>
    /// The reserved words that are also complete expressions, which a caller writing an expression
    /// may reasonably hand to something that holds a name.
    /// </summary>
    /// <remarks>
    /// <c>this</c> as the receiver of a call is <c>this</c>, not <c>@this</c>. These are left alone
    /// at reference sites and escaped at declaration sites, where a parameter named <c>this</c>
    /// really does need the prefix.
    /// </remarks>
    private static readonly HashSet<string> ExpressionKeywords =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "this", "base", "null", "true", "false", "default"
        };

    /// <summary>
    /// Whether <paramref name="value"/> is one of C#'s reserved words.
    /// </summary>
    public static bool IsReservedKeyword(string? value)
    {
        return value != null && Reserved.Contains(value);
    }

    /// <summary>
    /// <paramref name="name"/> as it must be written where a name is declared, prefixed with
    /// <c>@</c> if it is a reserved word.
    /// </summary>
    public static string Escape(string? name)
    {
        if (string.IsNullOrEmpty(name) || name![0] == '@')
        {
            return name ?? "";
        }

        return Reserved.Contains(name) ? "@" + name : name;
    }

    /// <summary>
    /// <paramref name="name"/> as it must be written where a name is used, with each dotted segment
    /// escaped.
    /// </summary>
    /// <remarks>
    /// Names arrive here already joined - <c>InstanceDefinition.Property</c> builds <c>a.b.c</c> -
    /// so each segment is considered separately. Anything that is not a plain dotted name is
    /// returned untouched: this is handed expressions as well as names, and mangling one would be a
    /// worse failure than leaving a keyword unescaped.
    /// </remarks>
    public static string EscapeReference(string? name)
    {
        return EscapeQualified(name, ExpressionKeywords);
    }

    /// <summary>
    /// <paramref name="name"/> with each dotted segment escaped, for a namespace or a qualified
    /// declaration name.
    /// </summary>
    public static string EscapeQualified(string? name)
    {
        return EscapeQualified(name, null);
    }

    private static string EscapeQualified(string? name, HashSet<string>? exclusions)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name ?? "";
        }

        if (name!.IndexOf('.') < 0)
        {
            return NeedsPrefix(name, exclusions) ? "@" + name : name;
        }

        var segments = name.Split('.');

        // Only a plain dotted name is rewritten. Anything else - a call, an index, a generic
        // argument list - is an expression that happens to hold a dot, and is left as it is.
        foreach (var segment in segments)
        {
            if (!IsSimpleIdentifier(segment))
            {
                return name;
            }
        }

        var builder = new StringBuilder(name.Length + 2);

        for (var i = 0; i < segments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            if (NeedsPrefix(segments[i], exclusions))
            {
                builder.Append('@');
            }

            builder.Append(segments[i]);
        }

        return builder.ToString();
    }

    private static bool NeedsPrefix(string segment, HashSet<string>? exclusions)
    {
        if (segment.Length == 0 || segment[0] == '@')
        {
            return false;
        }

        if (exclusions != null && exclusions.Contains(segment))
        {
            return false;
        }

        return Reserved.Contains(segment);
    }

    private static bool IsSimpleIdentifier(string segment)
    {
        if (segment.Length == 0)
        {
            return false;
        }

        var start = segment[0] == '@' ? 1 : 0;

        if (start >= segment.Length)
        {
            return false;
        }

        if (segment[start] != '_' && !char.IsLetter(segment[start]))
        {
            return false;
        }

        for (var i = start + 1; i < segment.Length; i++)
        {
            if (segment[i] != '_' && !char.IsLetterOrDigit(segment[i]))
            {
                return false;
            }
        }

        return true;
    }
}
