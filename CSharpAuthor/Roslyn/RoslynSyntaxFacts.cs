using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// The two facts about C# the bridge needs and Roslyn will not hand over as a string: which
/// identifiers have to be escaped, and which types are written as keywords.
/// </summary>
/// <remarks>
/// Both are derived from a symbol, never from source text. <see cref="ISymbol.Name"/> is the
/// unescaped identifier — a type declared <c>class @event</c> reports <c>event</c>, and writing
/// that verbatim produces CS1001 at the use site. <see cref="ITypeSymbol.SpecialType"/> carries the
/// keyword mapping that <c>Name</c> loses: <c>Single</c> is <c>float</c>, and the two are not
/// interchangeable in a literal (<c>float f = 1.5</c> is CS0664).
/// </remarks>
internal static class RoslynSyntaxFacts
{
    /// <summary>
    /// The reserved keywords. Contextual keywords are deliberately absent: <c>var</c>, <c>record</c>
    /// and friends are legal identifiers and escaping them would be wrong.
    /// </summary>
    private static readonly HashSet<string> Keywords = new HashSet<string>
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
    /// The identifier as it must be written in source.
    /// </summary>
    public static string Escape(string name)
    {
        if (string.IsNullOrEmpty(name) || !Keywords.Contains(name))
        {
            return name;
        }

        return "@" + name;
    }

    public static bool IsKeyword(string name) => Keywords.Contains(name);

    /// <summary>
    /// The C# keyword for a special type, or null when the type has no keyword.
    /// </summary>
    /// <remarks>
    /// <see cref="SpecialType.System_IntPtr"/> answers <c>nint</c> rather than <c>IntPtr</c>, which
    /// is what Roslyn's own <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/> produces for it
    /// on a runtime where the two are unified, and what the defect list asks for. On a target where
    /// they are not unified the keyword is still legal C# for the same type from C# 9 onwards.
    /// <see cref="SpecialType.System_Void"/> is absent on purpose: the type model already renders
    /// <c>System.Void</c> as <c>void</c>, and answering here would change the identity of a type
    /// callers compare against <c>TypeDefinition.Get(typeof(void))</c>.
    /// </remarks>
    public static string? Keyword(SpecialType specialType)
    {
        switch (specialType)
        {
            case SpecialType.System_Object: return "object";
            case SpecialType.System_Boolean: return "bool";
            case SpecialType.System_Char: return "char";
            case SpecialType.System_SByte: return "sbyte";
            case SpecialType.System_Byte: return "byte";
            case SpecialType.System_Int16: return "short";
            case SpecialType.System_UInt16: return "ushort";
            case SpecialType.System_Int32: return "int";
            case SpecialType.System_UInt32: return "uint";
            case SpecialType.System_Int64: return "long";
            case SpecialType.System_UInt64: return "ulong";
            case SpecialType.System_Decimal: return "decimal";
            case SpecialType.System_Single: return "float";
            case SpecialType.System_Double: return "double";
            case SpecialType.System_String: return "string";
            case SpecialType.System_IntPtr: return "nint";
            case SpecialType.System_UIntPtr: return "nuint";
            default: return null;
        }
    }
}
