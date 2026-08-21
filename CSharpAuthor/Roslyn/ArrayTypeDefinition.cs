using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor.Roslyn;

/// <summary>
/// An array type that keeps its rank and its element type, so <c>int[,]</c>, <c>int[][]</c>,
/// <c>int[,][]</c> and <c>int[][,]</c> stay four different types.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ITypeDefinition.IsArray"/> is one bit and <c>MakeArray()</c> sets it, so the type
/// model on its own can say "array of" exactly once and only at rank one: every one of those four
/// collapses to <c>int[]</c> or grows a rank that was never asked for. A rank and an element type
/// are the two facts an array has, and <c>IArrayTypeSymbol</c> carries both.
/// </para>
/// <para>
/// The rendering is not a fold. In C# the rank specifiers read outermost-first — <c>int[,][]</c> is
/// a two-dimensional array whose elements are <c>int[]</c>, which is also how Roslyn nests the
/// symbols — so the element is written first and then the ranks from the outside in. A nullable
/// annotation breaks that run: <c>string[]?[]</c> is an array of nullable arrays, and the
/// annotation closes off the ranks to its left before the next one wraps them. Levels are therefore
/// emitted in groups, each group ending at the annotated level that closes it.
/// </para>
/// </remarks>
public sealed class ArrayTypeDefinition : ITypeDefinition
{
    private int? _hashCode;

    /// <param name="elementType">The immediate element type. May itself be an array.</param>
    /// <param name="rank">Dimensions of this array. 1 for <c>T[]</c>, 2 for <c>T[,]</c>.</param>
    /// <param name="isNullable">Whether this array level carries a <c>?</c> annotation.</param>
    public ArrayTypeDefinition(ITypeDefinition elementType, int rank = 1, bool isNullable = false)
    {
        if (elementType == null)
        {
            throw new ArgumentNullException(nameof(elementType));
        }

        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), "An array has at least one dimension");
        }

        ElementType = elementType;
        Rank = rank;
        IsNullable = isNullable;
    }

    public ITypeDefinition ElementType { get; }

    public int Rank { get; }

    public bool IsNullable { get; }

    public bool IsArray => true;

    /// <summary>
    /// The element's name, as <c>MakeArray()</c> on the type model leaves it — an array of
    /// <c>Byte</c> is still named <c>Byte</c>.
    /// </summary>
    public string Name => ElementType.Name;

    public string Namespace => ElementType.Namespace;

    public TypeDefinitionEnum TypeDefinitionEnum => ElementType.TypeDefinitionEnum;

    public IEnumerable<string> KnownNamespaces => ElementType.KnownNamespaces;

    public IReadOnlyList<ITypeDefinition> TypeArguments => ElementType.TypeArguments;

    /// <summary>
    /// The element type with every array level stripped: the <c>int</c> of <c>int[,][]</c>.
    /// </summary>
    public ITypeDefinition RootElementType
    {
        get
        {
            var current = ElementType;

            while (current is ArrayTypeDefinition array)
            {
                current = array.ElementType;
            }

            return current;
        }
    }

    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        var levels = new List<ArrayTypeDefinition>();

        ITypeDefinition current = this;

        while (current is ArrayTypeDefinition array)
        {
            levels.Add(array);
            current = array.ElementType;
        }

        current.WriteTypeName(builder, typeOutputMode);

        // levels[0] is the outermost. Walk inwards-out: a group runs from the level that closes it
        // (annotated, or the outermost level) down to the innermost level not yet written, and the
        // ranks inside a group are written outermost-first.
        var innerEnd = levels.Count - 1;

        while (innerEnd >= 0)
        {
            var outerStart = innerEnd;

            while (outerStart > 0 && !levels[outerStart].IsNullable)
            {
                outerStart--;
            }

            for (var i = outerStart; i <= innerEnd; i++)
            {
                WriteRank(builder, levels[i].Rank);
            }

            if (levels[outerStart].IsNullable)
            {
                builder.Append('?');
            }

            innerEnd = outerStart - 1;
        }
    }

    private static void WriteRank(StringBuilder builder, int rank)
    {
        builder.Append('[');

        for (var i = 1; i < rank; i++)
        {
            builder.Append(',');
        }

        builder.Append(']');
    }

    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new ArrayTypeDefinition(ElementType, Rank, nullable);
    }

    /// <summary>
    /// An array of this array — <c>int[,]</c> becomes <c>int[][,]</c>, never <c>int[,]</c> again.
    /// </summary>
    public ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    public int CompareTo(ITypeDefinition? other)
    {
        if (ReferenceEquals(other, null))
        {
            return 1;
        }

        if (other is not ArrayTypeDefinition arrayType)
        {
            var nameCompare = string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);

            return nameCompare != 0 ? nameCompare : -1;
        }

        if (Rank != arrayType.Rank)
        {
            return Rank - arrayType.Rank;
        }

        if (IsNullable != arrayType.IsNullable)
        {
            return IsNullable ? 1 : -1;
        }

        return ElementType.CompareTo(arrayType.ElementType);
    }

    public override bool Equals(object? obj)
    {
        return obj is ArrayTypeDefinition arrayType && CompareTo(arrayType) == 0;
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= ToString().GetHashCode();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }
}
