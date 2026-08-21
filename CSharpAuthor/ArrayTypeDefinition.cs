using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// An array, as its element type plus a rank.
/// </summary>
/// <remarks>
/// <para>
/// Array-ness used to be a single <c>bool IsArray</c> carried by the element itself, which can
/// express <c>string[]</c> and nothing further. <c>MakeArray()</c> on something already an array
/// returned a copy that was already an array, so <c>string[][]</c> silently came back as
/// <c>string[]</c>; rank had nowhere to live at all, so <c>int[,]</c> was unreachable.
/// </para>
/// <para>
/// Wrapping instead of flagging makes jagged arrays fall out of nesting - <c>string[][]</c> is an
/// <see cref="ArrayTypeDefinition"/> whose element is another - and gives rank somewhere to sit.
/// The element stays unrendered, so an array of a generic still resolves its own namespaces.
/// </para>
/// </remarks>
public class ArrayTypeDefinition : BaseTypeDefinition
{
    /// <param name="elementType">What the array holds - itself an array, for a jagged one.</param>
    /// <param name="rank">
    /// The number of dimensions. Rank 2 is <c>[,]</c>; a jagged array is rank 1 nested inside
    /// rank 1, not rank 2.
    /// </param>
    public ArrayTypeDefinition(ITypeDefinition elementType, int rank = 1, bool isNullable = false)
        : base(elementType.TypeDefinitionEnum, elementType.Namespace, elementType.Name, true, isNullable)
    {
        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "An array has at least one dimension.");
        }

        ElementType = elementType;
        Rank = rank;
    }

    public ITypeDefinition ElementType { get; }

    public int Rank { get; }

    /// <remarks>
    /// The element's, because that is what actually needs importing - an array introduces no name
    /// of its own.
    /// </remarks>
    public override IEnumerable<string> KnownNamespaces => ElementType.KnownNamespaces;

    /// <remarks>
    /// Empty. An array is not a constructed generic; the arguments of <c>List&lt;int&gt;[]</c>
    /// belong to <see cref="ElementType"/>.
    /// </remarks>
    public override IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    public override void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        ElementType.WriteTypeName(builder, typeOutputMode);

        builder.Append('[');
        builder.Append(new string(',', Rank - 1));
        builder.Append(']');

        if (IsNullable)
        {
            builder.Append('?');
        }
    }

    public override ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new ArrayTypeDefinition(ElementType, Rank, nullable);
    }

    /// <remarks>
    /// Wraps rather than flattens, so this is the jagged step: <c>string[]</c> becomes
    /// <c>string[][]</c> rather than staying <c>string[]</c>.
    /// </remarks>
    public override ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    public override int CompareTo(ITypeDefinition? other)
    {
        if (other is not ArrayTypeDefinition arrayType)
        {
            return BaseCompareTo(other);
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
        unchecked
        {
            var hash = ElementType.GetHashCode();

            hash = hash * 31 + Rank;
            hash = hash * 31 + IsNullable.GetHashCode();

            return hash;
        }
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }
}
