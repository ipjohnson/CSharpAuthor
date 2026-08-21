using System;

namespace CSharpAuthor;

/// <summary>
/// One ordering for every <see cref="ITypeDefinition"/>, whatever implements it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IComparable{T}"/> promises that <c>a.CompareTo(b)</c> and <c>b.CompareTo(a)</c> are
/// opposite signs. Each implementation used to answer for itself, and they disagreed: a plain
/// <c>Ns.List</c> reported itself <em>equal</em> to <c>Ns.List&lt;int&gt;</c> because it knows
/// nothing about type arguments, while the generic one reported them different. A
/// <see cref="TypeParameterDefinition"/> answered -1 to everything that was not another type
/// parameter, which made it smaller than every type and also larger than none of them.
/// </para>
/// <para>
/// A comparator that is not a total order does not merely sort oddly - <c>List.Sort</c> is entitled
/// to throw "IComparer.Compare() method returns inconsistent results", and which elements it
/// compares depends on the input order, so the throw is a function of the data. This matters beyond
/// sorting: section 7 asks for <c>EquatableArray&lt;T&gt;</c> so an incremental generator's model
/// caches across runs, and a model caches on the equality of what is inside it.
/// </para>
/// <para>
/// The key, in order: kind, name, namespace, containing type, array shape, nullability, type
/// arguments. Every part of it is asked of the interface, so a third implementation orders
/// correctly against the two here without knowing about either.
/// </para>
/// </remarks>
internal static class TypeDefinitionOrder
{
    public static int Compare(ITypeDefinition left, ITypeDefinition right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (ReferenceEquals(right, null))
        {
            return 1;
        }

        if (ReferenceEquals(left, null))
        {
            return -1;
        }

        // A type parameter names nothing outside its declaration, so it is a different kind of
        // thing from a type and sorts before every one of them - consistently in both directions,
        // which is the part that was missing.
        var kindCompare = KindRank(left) - KindRank(right);

        if (kindCompare != 0)
        {
            return kindCompare;
        }

        if (left.TypeDefinitionEnum != right.TypeDefinitionEnum)
        {
            return left.TypeDefinitionEnum - right.TypeDefinitionEnum;
        }

        var nameCompare = string.Compare(left.Name, right.Name, StringComparison.Ordinal);

        if (nameCompare != 0)
        {
            return nameCompare;
        }

        var namespaceCompare =
            string.Compare(left.Namespace, right.Namespace, StringComparison.Ordinal);

        if (namespaceCompare != 0)
        {
            return namespaceCompare;
        }

        var containerCompare = CompareContainers(left.ContainingType, right.ContainingType);

        if (containerCompare != 0)
        {
            return containerCompare;
        }

        var rankCompare = CompareArrayRanks(left, right);

        if (rankCompare != 0)
        {
            return rankCompare;
        }

        if (left.IsNullable != right.IsNullable)
        {
            return left.IsNullable ? 1 : -1;
        }

        return CompareTypeArguments(left, right);
    }

    private static int KindRank(ITypeDefinition typeDefinition) =>
        typeDefinition is TypeParameterDefinition ? 0 : 1;

    private static int CompareContainers(ITypeDefinition? left, ITypeDefinition? right)
    {
        if (left == null)
        {
            return right == null ? 0 : -1;
        }

        return right == null ? 1 : Compare(left, right);
    }

    private static int CompareArrayRanks(ITypeDefinition left, ITypeDefinition right)
    {
        var ranks = left.ArrayRanks;
        var otherRanks = right.ArrayRanks;

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

    private static int CompareTypeArguments(ITypeDefinition left, ITypeDefinition right)
    {
        var arguments = left.TypeArguments;
        var otherArguments = right.TypeArguments;

        if (arguments.Count != otherArguments.Count)
        {
            return arguments.Count - otherArguments.Count;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            var argumentCompare = Compare(arguments[i], otherArguments[i]);

            if (argumentCompare != 0)
            {
                return argumentCompare;
            }
        }

        return 0;
    }
}
