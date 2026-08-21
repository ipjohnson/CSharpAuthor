using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public abstract class BaseTypeDefinition : ITypeDefinition
{
    private static readonly int[] _notAnArray = Array.Empty<int>();
    private static readonly int[] _oneDimensional = { 1 };

    private int? _hashCode;

    protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable)
        : this(typeDefinitionEnum, ns, name, isArray ? _oneDimensional : _notAnArray, isNullable)
    {
    }

    protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable)
    {
        Name = name;
        Namespace = ns;
        IsNullable = isNullable;
        ArrayRanks = NormalizeRanks(arrayRanks);
        TypeDefinitionEnum = typeDefinitionEnum;
    }

    public string Name { get; }

    public string Namespace { get; }

    public abstract IEnumerable<string> KnownNamespaces { get; }

    public abstract void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);

    public abstract ITypeDefinition MakeNullable(bool nullable = true);

    public ITypeDefinition MakeArray()
    {
        return MakeArray(1);
    }

    public abstract ITypeDefinition MakeArray(int rank);

    public abstract IReadOnlyList<ITypeDefinition> TypeArguments { get; }

    public TypeDefinitionEnum TypeDefinitionEnum { get; }

    public bool IsNullable { get; }

    /// <inheritdoc />
    public IReadOnlyList<int> ArrayRanks { get; }

    /// <inheritdoc />
    public bool IsArray => ArrayRanks.Count > 0;

    public abstract int CompareTo(ITypeDefinition other);

    /// <summary>
    /// The fully qualified C# name of the type, which is what makes it a usable dictionary key: it
    /// tells <c>int</c> from <c>int[]</c> and one nested type from another.
    /// </summary>
    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }

    /// <summary>
    /// Two type definitions are equal when they are the same kind of definition and name the same
    /// type - the same container, the same generic arguments, the same array shape.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is ITypeDefinition other && obj.GetType() == GetType() && CompareTo(other) == 0;
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= ToString().GetHashCode();
    }

    protected int BaseCompareTo(ITypeDefinition other)
    {
        if (ReferenceEquals(null, other))
        {
            return 1;
        }

        if (TypeDefinitionEnum != other.TypeDefinitionEnum)
        {
            return TypeDefinitionEnum - other.TypeDefinitionEnum;
        }

        var nameCompare = string.Compare(Name, other.Name, StringComparison.Ordinal);

        if (nameCompare != 0)
        {
            return nameCompare;
        }

        var namespaceCompare = string.Compare(Namespace, other.Namespace, StringComparison.Ordinal);

        if (namespaceCompare != 0)
        {
            return namespaceCompare;
        }

        var rankCompare = CompareArrayRanks(other);

        if (rankCompare != 0)
        {
            return rankCompare;
        }

        if (IsNullable != other.IsNullable)
        {
            return IsNullable ? 1 : -1;
        }

        return 0;
    }

    /// <summary>
    /// Writes the array specifiers, outermost first, so ranks <c>[2, 1]</c> read as <c>[,][]</c>.
    /// </summary>
    protected void WriteArrayRanks(StringBuilder builder)
    {
        var ranks = ArrayRanks;

        for (var i = 0; i < ranks.Count; i++)
        {
            builder.Append('[');

            for (var dimension = 1; dimension < ranks[i]; dimension++)
            {
                builder.Append(',');
            }

            builder.Append(']');
        }
    }

    protected void WriteNamespacePrefix(StringBuilder builder, TypeOutputMode typeOutputMode)
    {
        if (string.IsNullOrEmpty(Namespace))
        {
            return;
        }

        if (typeOutputMode == TypeOutputMode.Global)
        {
            builder.Append("global::");
            builder.Append(Namespace);
            builder.Append('.');
        }
        else if (typeOutputMode == TypeOutputMode.FullName)
        {
            builder.Append(Namespace);
            builder.Append('.');
        }
    }

    /// <summary>
    /// An array of this type: the new rank goes on the outside, so making an array of <c>int[]</c>
    /// gives <c>int[][]</c> rather than losing a dimension.
    /// </summary>
    protected IReadOnlyList<int> ArrayRanksWithOuterRank(int rank)
    {
        CheckRank(rank);

        var ranks = ArrayRanks;

        if (ranks.Count == 0)
        {
            return rank == 1 ? _oneDimensional : new[] { rank };
        }

        var result = new int[ranks.Count + 1];

        result[0] = rank;

        for (var i = 0; i < ranks.Count; i++)
        {
            result[i + 1] = ranks[i];
        }

        return result;
    }

    private int CompareArrayRanks(ITypeDefinition other)
    {
        var ranks = ArrayRanks;
        var otherRanks = other.ArrayRanks;

        if (ranks.Count != otherRanks.Count)
        {
            return ranks.Count - otherRanks.Count;
        }

        for (var i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] != otherRanks[i])
            {
                return ranks[i] - otherRanks[i];
            }
        }

        return 0;
    }

    private static IReadOnlyList<int> NormalizeRanks(IReadOnlyList<int>? arrayRanks)
    {
        if (arrayRanks == null || arrayRanks.Count == 0)
        {
            return _notAnArray;
        }

        if (arrayRanks.Count == 1)
        {
            CheckRank(arrayRanks[0]);

            return arrayRanks[0] == 1 ? _oneDimensional : new[] { arrayRanks[0] };
        }

        var copy = new int[arrayRanks.Count];

        for (var i = 0; i < arrayRanks.Count; i++)
        {
            CheckRank(arrayRanks[i]);

            copy[i] = arrayRanks[i];
        }

        return copy;
    }

    private static void CheckRank(int rank)
    {
        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "An array rank is at least 1.");
        }
    }
}
