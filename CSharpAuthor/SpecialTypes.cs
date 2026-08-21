using System;
using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// The C# keywords for the framework types that have one - <c>int</c> for <c>System.Int32</c>.
/// </summary>
/// <remarks>
/// <para>
/// This table used to be private and keyed on <see cref="Type"/>, which is precisely what a source
/// generator does not have: it has an <c>ITypeSymbol</c>, and a namespace and name. So every
/// generator rewrote the table, and every one of them rewrote its gaps too - <c>float</c>,
/// <c>char</c>, <c>sbyte</c>, <c>nint</c> and <c>nuint</c> were all missing, which is how
/// <c>Single</c> and <c>SByte</c> reached generated output.
/// </para>
/// <para>
/// Keyed on namespace and name so it can be reached from either direction, and public because the
/// first thing written against this library needs it.
/// </para>
/// </remarks>
public static class SpecialTypes
{
    /// <remarks>
    /// Every keyword here is C# 1, so it is safe to emit without knowing what version the
    /// consumer compiles at. <c>nint</c> and <c>nuint</c> are deliberately absent: they are C# 9,
    /// and nothing in the output context carries a target version yet, so adding them would emit
    /// a spelling a C# 8 consumer cannot compile and give them no way to opt out.
    /// <c>System.IntPtr</c> renders as <c>IntPtr</c>, which is valid everywhere. Add them once
    /// the target version reaches rendering.
    /// </remarks>
    private static readonly Dictionary<string, string> _keywords = new()
    {
        { "System.Object", "object" },
        { "System.String", "string" },
        { "System.Boolean", "bool" },
        { "System.Char", "char" },
        { "System.SByte", "sbyte" },
        { "System.Byte", "byte" },
        { "System.Int16", "short" },
        { "System.UInt16", "ushort" },
        { "System.Int32", "int" },
        { "System.UInt32", "uint" },
        { "System.Int64", "long" },
        { "System.UInt64", "ulong" },
        { "System.Single", "float" },
        { "System.Double", "double" },
        { "System.Decimal", "decimal" },
        { "System.Void", "void" },
    };

    private static readonly Dictionary<string, ITypeDefinition> _definitions = BuildDefinitions();

    private static Dictionary<string, ITypeDefinition> BuildDefinitions()
    {
        var definitions = new Dictionary<string, ITypeDefinition>(_keywords.Count);

        foreach (var pair in _keywords)
        {
            // No namespace, because a keyword needs no qualifying and no using - which is also
            // what keeps it out of import derivation in every output mode.
            definitions.Add(
                pair.Key,
                new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "", pair.Value, false));
        }

        return definitions;
    }

    /// <summary>
    /// The keyword for a framework type, or null where it has none.
    /// </summary>
    public static string? GetKeyword(string? ns, string name)
    {
        return _keywords.TryGetValue(QualifiedName(ns, name), out var keyword) ? keyword : null;
    }

    /// <summary>
    /// The keyword form of a framework type as a type definition, or null where it has none.
    /// </summary>
    public static ITypeDefinition? Get(string? ns, string name)
    {
        return _definitions.TryGetValue(QualifiedName(ns, name), out var definition) ? definition : null;
    }

    /// <inheritdoc cref="Get(string,string)"/>
    public static ITypeDefinition? Get(Type type)
    {
        return type == null ? null : Get(type.Namespace, type.Name);
    }

    /// <summary>
    /// Every framework type with a keyword, as full name to keyword.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> Keywords => _keywords;

    private static string QualifiedName(string? ns, string name)
    {
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }
}
