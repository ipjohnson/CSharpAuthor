using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace CSharpAuthor;

public interface ITypeDefinition : IComparable<ITypeDefinition>
{
    TypeDefinitionEnum TypeDefinitionEnum { get; }

    bool IsNullable { get; }

    bool IsArray { get; }

    string Name { get; }

    string Namespace { get; }

    IEnumerable<string> KnownNamespaces { get; }

    void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);

    ITypeDefinition MakeNullable(bool nullable = true);

    ITypeDefinition MakeArray();

    IReadOnlyList<ITypeDefinition> TypeArguments { get; }
}

public static class ITypeDefinitionExtensions
{
    public static string GetShortName(this ITypeDefinition typeDefinition)
    {
        var stringBuilder = new StringBuilder();

        typeDefinition.WriteTypeName(stringBuilder);

        return stringBuilder.ToString();
    }

    /// <summary>
    /// A multidimensional array of this type - <c>int[,]</c> at rank 2.
    /// </summary>
    /// <remarks>
    /// An extension rather than a member of <see cref="ITypeDefinition"/>, so that rank works for
    /// every implementation without the interface gaining a member each one has to add. Jagged
    /// arrays need nothing extra: <c>MakeArray().MakeArray()</c> is <c>int[][]</c>.
    /// </remarks>
    public static ITypeDefinition MakeArray(this ITypeDefinition typeDefinition, int rank)
    {
        return new ArrayTypeDefinition(typeDefinition, rank);
    }

    /// <summary>
    /// What an array holds, unwrapping one level - <c>int[]</c> for <c>int[][]</c>. Null when the
    /// type is not an array.
    /// </summary>
    public static ITypeDefinition? GetElementType(this ITypeDefinition typeDefinition)
    {
        return typeDefinition is ArrayTypeDefinition arrayType ? arrayType.ElementType : null;
    }
}