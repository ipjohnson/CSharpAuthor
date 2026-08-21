using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CSharpAuthor;

public abstract class BaseTypeDefinition : ITypeDefinition
{
    private static readonly IReadOnlyList<int> _notAnArray = new ReadOnlyCollection<int>(Array.Empty<int>());
    private static readonly IReadOnlyList<int> _oneDimensional = new ReadOnlyCollection<int>(new[] { 1 });

    private int? _hashCode;

    protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable)
        : this(typeDefinitionEnum, ns, name, isArray ? _oneDimensional : _notAnArray, isNullable, null)
    {
    }

    /// <remarks>
    /// The rank-carrying constructors and the write helpers below are <c>private protected</c>: they
    /// are how this assembly's own type definitions are built and written, not surface a consumer
    /// needs. The 1.x <c>protected</c> constructor above is untouched, so an outside subclass still
    /// has the entry point it always had. Widening one of these later is not a breaking change;
    /// narrowing it would be.
    /// </remarks>
    private protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable)
        : this(typeDefinitionEnum, ns, name, arrayRanks, isNullable, null)
    {
    }

    private protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable, ITypeDefinition? containingType)
    {
        Name = name;
        Namespace = ns;
        IsNullable = isNullable;
        ArrayRanks = NormalizeRanks(arrayRanks);
        ContainingType = containingType;
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

    /// <inheritdoc />
    public ITypeDefinition? ContainingType { get; }

    public abstract int CompareTo(ITypeDefinition other);

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
        return _hashCode ??= HashKey().GetHashCode();
    }

    /// <summary>
    /// A string that separates every type this model can now tell apart: the fully qualified C# name,
    /// containers, generic arguments and array shape included.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="object.ToString"/>. That stays in its 1.x form, which a consumer
    /// asserts on directly, and which cannot tell <c>int</c> from <c>int[]</c> or one nested
    /// <c>Inner</c> from another - fine for equal values, which always agree on it, but it would put
    /// every one of those newly distinguishable types in the same bucket.
    /// </remarks>
    private string HashKey()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }

    /// <summary>
    /// The shared ordering, which every implementation of <see cref="ITypeDefinition"/> answers
    /// with so that the two directions of a comparison agree. See <see cref="TypeDefinitionOrder"/>.
    /// </summary>
    protected int BaseCompareTo(ITypeDefinition other)
    {
        return TypeDefinitionOrder.Compare(this, other);
    }

    /// <summary>
    /// Writes the array specifiers, outermost first, so ranks <c>[2, 1]</c> read as <c>[,][]</c>.
    /// </summary>
    private protected void WriteArrayRanks(StringBuilder builder)
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

    /// <summary>
    /// Writes everything that comes before the type's own name: the containing type if it has one,
    /// the namespace otherwise.
    /// </summary>
    /// <remarks>
    /// A nested type is qualified by its container, not by its namespace - <c>Ns.Outer.Inner</c>, never
    /// <c>Ns.Inner</c>. The container writes itself in the same mode, so it picks up <c>global::</c> or
    /// the namespace exactly once, at the outermost type, and the chain below it stays plain.
    /// </remarks>
    private protected void WriteQualifier(StringBuilder builder, TypeOutputMode typeOutputMode)
    {
        if (ContainingType != null)
        {
            ContainingType.WriteTypeName(builder, typeOutputMode);
            builder.Append('.');

            return;
        }

        WriteNamespacePrefix(builder, typeOutputMode);
    }

    private protected void WriteNamespacePrefix(StringBuilder builder, TypeOutputMode typeOutputMode)
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
    private protected IReadOnlyList<int> ArrayRanksWithOuterRank(int rank)
    {
        return WithOuterRank(ArrayRanks, rank);
    }

    /// <inheritdoc cref="ArrayRanksWithOuterRank" />
    internal static IReadOnlyList<int> WithOuterRank(IReadOnlyList<int> ranks, int rank)
    {
        CheckRank(rank);

        if (ranks.Count == 0)
        {
            return rank == 1 ? _oneDimensional : new ReadOnlyCollection<int>(new[] { rank });
        }

        var result = new int[ranks.Count + 1];

        result[0] = rank;

        for (var i = 0; i < ranks.Count; i++)
        {
            result[i + 1] = ranks[i];
        }

        return new ReadOnlyCollection<int>(result);
    }

    private int CompareContainingTypes(ITypeDefinition other)
    {
        var container = ContainingType;
        var otherContainer = other.ContainingType;

        if (container == null)
        {
            return otherContainer == null ? 0 : -1;
        }

        if (otherContainer == null)
        {
            return 1;
        }

        return container.CompareTo(otherContainer);
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

    /// <summary>
    /// Takes a copy behind a read-only view, so the shape cannot change under a type definition that
    /// has already cached a hash from it.
    /// </summary>
    internal static IReadOnlyList<int> NormalizeRanks(IReadOnlyList<int>? arrayRanks)
    {
        if (arrayRanks == null || arrayRanks.Count == 0)
        {
            return _notAnArray;
        }

        if (arrayRanks.Count == 1)
        {
            CheckRank(arrayRanks[0]);

            return arrayRanks[0] == 1 ? _oneDimensional : new ReadOnlyCollection<int>(new[] { arrayRanks[0] });
        }

        var copy = new int[arrayRanks.Count];

        for (var i = 0; i < arrayRanks.Count; i++)
        {
            CheckRank(arrayRanks[i]);

            copy[i] = arrayRanks[i];
        }

        return new ReadOnlyCollection<int>(copy);
    }

    private static void CheckRank(int rank)
    {
        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "An array rank is at least 1.");
        }
    }
}
