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
    private string? _key;

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
        : this(typeDefinitionEnum, ns, name, arrayRanks, isNullable, false, containingType)
    {
    }

    private protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable, bool isElementNullable, ITypeDefinition? containingType)
    {
        Name = name;
        Namespace = ns;
        IsNullable = isNullable;
        ArrayRanks = NormalizeRanks(arrayRanks);
        IsElementNullable = isElementNullable && ArrayRanks.Count > 0;
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

    /// <summary>
    /// Whether the innermost element of an array type is nullable - <c>string?[]</c> rather than
    /// <c>string[]?</c>. False for anything that is not an array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>string?[]</c> and <c>string[]?</c> are different types and both compile, so writing the
    /// <c>?</c> and the <c>[]</c> in a fixed order does not fail - it hands the caller the other
    /// type. <c>MakeNullable().MakeArray()</c> is the array of a nullable element;
    /// <c>MakeArray().MakeNullable()</c> is the nullable array. Which one was asked for is the
    /// order the calls were made in.
    /// </para>
    /// <para>
    /// Nullability at a level between the two - <c>string[]?[]</c> - is not expressible. Making an
    /// array of a nullable array carries the <c>?</c> outward, which is what version 1 did.
    /// </para>
    /// <para>
    /// Deliberately not on <see cref="ITypeDefinition"/>. Adding a member to that interface breaks
    /// every implementation of it outside this assembly, and one exists in this repository's own
    /// test project - which is the evidence that they exist elsewhere too. Handbook rule 8.4: where
    /// the spec is silent, keep version 1 source-compatible.
    /// </para>
    /// </remarks>
    public bool IsElementNullable { get; }

    /// <inheritdoc />
    public IReadOnlyList<int> ArrayRanks { get; }

    /// <inheritdoc />
    public bool IsArray => ArrayRanks.Count > 0;

    /// <inheritdoc />
    public ITypeDefinition? ContainingType { get; }

    public abstract int CompareTo(ITypeDefinition other);

    /// <summary>
    /// The type's identity: what makes it the same type as another definition of it, whichever class
    /// that one happens to be. See <see cref="TypeDefinitionIdentity"/>.
    /// </summary>
    /// <remarks>
    /// Built once and kept. The definition is immutable in every way the key reads - the array ranks
    /// are copied behind a read-only view at construction for exactly this reason - so the answer
    /// cannot go stale.
    /// </remarks>
    internal string TypeKey => _key ??= TypeDefinitionIdentity.Build(this);

    /// <summary>
    /// Two type definitions are equal when they denote the same type: the same kind, the same fully
    /// qualified name, the same container, generic arguments and array shape.
    /// </summary>
    /// <remarks>
    /// Not "the same class as this one". <see cref="ITypeDefinition"/> is an interface, and the
    /// bridge implements it several more times over for the shapes this class cannot hold - a
    /// nullable value type, a ranked array, a nested type whose containers are generic. A bridged
    /// <c>int?</c> and <c>TypeDefinition.Get(typeof(int)).MakeNullable()</c> are the same type, and a
    /// generator matching a parameter against a registration is comparing exactly those two.
    /// </remarks>
    public override bool Equals(object? obj)
    {
        return TypeDefinitionIdentity.KeyEquals(TypeKey, obj);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= TypeKey.GetHashCode();
    }

    /// <summary>
    /// Orders this type against another, zero exactly when <see cref="Equals(object)"/> says they are
    /// the same type.
    /// </summary>
    /// <remarks>
    /// This used to compare the properties one at a time and stop, which is why it was not safe to
    /// remove the class check from <see cref="Equals(object)"/>. Name, namespace, container, ranks
    /// and nullability are shared by <c>int</c> and <c>int*</c>, by <c>Ns.List</c> and
    /// <c>Ns.List&lt;int&gt;</c>, and by <c>System.ValueTuple</c> and <c>(int a, string b)</c>: it
    /// reported each of those pairs equal, while the other side of the pair reported them different
    /// and hashed them differently. A subclass calling this still gets a comparison that stops at
    /// what it renders, and is free to break a remaining tie on anything it does not.
    /// </remarks>
    protected int BaseCompareTo(ITypeDefinition other)
    {
        return TypeDefinitionIdentity.KeyCompare(TypeKey, other);
    }

    /// <summary>
    /// The type's own name as it has to be written: prefixed with <c>@</c> when it is a reserved
    /// word, so a type read out of a schema and called <c>event</c> names itself rather than
    /// producing CS1001 at every reference.
    /// </summary>
    /// <remarks>
    /// Only a type that has somewhere to live - a namespace or a containing type - is escaped. A
    /// type with neither is the shape the keyword aliases take: <c>int</c> and <c>string</c> are
    /// held as a name with no namespace, and <c>@int</c> is not the same type as <c>int</c>.
    /// </remarks>
    private protected string WrittenName()
    {
        return Namespace.Length > 0 || ContainingType != null
            ? CSharpIdentifier.Escape(Name)
            : Name;
    }

    /// <summary>
    /// Writes the array specifiers, outermost first, so ranks <c>[2, 1]</c> read as <c>[,][]</c> -
    /// preceded by the element's <c>?</c> where it has one, because <c>string?[]</c> and
    /// <c>string[]?</c> are different types.
    /// </summary>
    private protected void WriteArrayRanks(StringBuilder builder)
    {
        if (IsElementNullable)
        {
            builder.Append('?');
        }

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
