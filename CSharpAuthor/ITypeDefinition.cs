using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public interface ITypeDefinition : IComparable<ITypeDefinition>
{
    TypeDefinitionEnum TypeDefinitionEnum { get; }

    bool IsNullable { get; }

    bool IsArray { get; }

    /// <summary>
    /// The rank of each array wrapping this type, outermost first - the order the specifiers are
    /// written in. <c>int[,][]</c> is <c>[2, 1]</c>; <c>int[][,]</c> is <c>[1, 2]</c>. Empty when the
    /// type is not an array.
    /// </summary>
    /// <remarks>
    /// A single flag cannot tell <c>int[]</c> from <c>int[][]</c> from <c>int[,]</c>, and all three
    /// are different types. Reflection names them in the opposite order to C# — <c>typeof(int[,][])</c>
    /// is named <c>Int32[][,]</c> — so the list is the order the emitter needs, not the order
    /// <see cref="Type.Name"/> gives.
    /// </remarks>
    IReadOnlyList<int> ArrayRanks { get; }

    string Name { get; }

    string Namespace { get; }

    IEnumerable<string> KnownNamespaces { get; }

    void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);

    ITypeDefinition MakeNullable(bool nullable = true);

    /// <summary>
    /// A one-dimensional array of this type.
    /// </summary>
    ITypeDefinition MakeArray();

    /// <summary>
    /// An array of this type with the given rank. The new array goes on the outside, so
    /// <c>Get(typeof(int)).MakeArray().MakeArray()</c> is <c>int[][]</c>.
    /// </summary>
    ITypeDefinition MakeArray(int rank);

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
}
