using System;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// When two <see cref="ITypeDefinition"/> values are the same type, and the order they sort in.
/// </summary>
/// <remarks>
/// <para>
/// The model has more than one class for the same idea. A plain <see cref="TypeDefinition"/>, a
/// bridged nullable value type, a bridged array and a bridged nested type can each denote
/// <c>int?</c>, <c>int[]</c> or <c>Ns.Outer.Inner</c>, and a generator matching a symbol against a
/// registration is comparing two of them that were built by different halves of the library.
/// Identity is therefore what a type <em>is</em>, not which class is holding it: the C# name it
/// writes in <see cref="TypeOutputMode.FullName"/>. That is the one thing every implementation has
/// to be able to produce - including one this assembly has never seen - and it is the thing that
/// makes two values interchangeable at a call site.
/// </para>
/// <para>
/// The structural facts on the interface cannot stand in for it, which is why comparing them was
/// never safe across implementations. Name, namespace, container, array ranks and nullability are
/// the same for <c>int</c> and <c>int*</c>; for <c>Ns.List</c> and <c>Ns.List&lt;int&gt;</c>; for
/// <c>System.ValueTuple</c> and <c>(int a, string b)</c>; and for <c>delegate*</c> and
/// <c>delegate*&lt;int, void&gt;</c>. Each of those pairs differs only in something the rendering
/// carries and the properties do not, and each used to compare equal one way round and unequal the
/// other.
/// </para>
/// <para>
/// The key is not <see cref="object.ToString"/>. On <see cref="TypeDefinition"/> that stays in its
/// 1.x form - namespace and name, which a consumer reads and asserts on - and it cannot tell
/// <c>int</c> from <c>int[]</c> or one nested <c>Inner</c> from another.
/// </para>
/// </remarks>
public static class TypeDefinitionIdentity
{
    /// <summary>
    /// Prefixes the key of an <see cref="AttributeTypeReference"/>, which is deliberately not the
    /// same type as the one it wraps.
    /// </summary>
    /// <remarks>
    /// It writes <c>[Obsolete]</c> where the type it stands for is <c>ObsoleteAttribute</c>, so its
    /// rendering already separates the two - but that rendering is also what a real type called
    /// <c>Obsolete</c> writes, and those are two different types. A name can never start with this
    /// character, and every other key starts with a digit, so the two spaces do not overlap.
    /// </remarks>
    private const char AttributeReferenceMarker = '@';

    /// <summary>
    /// The identity of <paramref name="type"/>: equal keys mean the same type, and ordering the keys
    /// orders the types.
    /// </summary>
    /// <remarks>
    /// Implementations in this assembly hold on to their key, so asking twice costs one field read.
    /// Any other implementation is rendered on demand, which is what lets a type this assembly has
    /// never seen take part in the same equality.
    /// </remarks>
    public static string KeyOf(ITypeDefinition type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        switch (type)
        {
            case BaseTypeDefinition baseTypeDefinition:
                return baseTypeDefinition.TypeKey;

            case TypeParameterDefinition typeParameter:
                return typeParameter.TypeKey;

            case AttributeTypeReference attributeReference:
                return attributeReference.TypeKey;

            default:
                return Build(type);
        }
    }

    /// <summary>
    /// Whether <paramref name="other"/> is a type definition denoting the type <paramref name="key"/>
    /// identifies. The overload every <c>Equals</c> in the model is written in terms of.
    /// </summary>
    public static bool KeyEquals(string key, object? other)
    {
        if (other is not ITypeDefinition typeDefinition)
        {
            return false;
        }

        return string.Equals(key, KeyOf(typeDefinition), StringComparison.Ordinal);
    }

    /// <summary>
    /// Orders the type <paramref name="key"/> identifies against <paramref name="other"/>, zero
    /// exactly when they are the same type. The overload every <c>CompareTo</c> in the model is
    /// written in terms of, which is what makes the ordering a total one: it is a string comparison,
    /// so it is symmetric and transitive whichever pair of implementations it is handed.
    /// </summary>
    public static int KeyCompare(string key, ITypeDefinition? other)
    {
        if (other is null)
        {
            return 1;
        }

        return string.CompareOrdinal(key, KeyOf(other));
    }

    /// <summary>
    /// Enough for a namespace, a name and a little punctuation, which is what almost every key is.
    /// </summary>
    /// <remarks>
    /// The default builder starts at 16 characters and grows in chunks, so a key of ordinary length
    /// costs three allocations and a walk to gather them. Asking for the room up front is the
    /// difference between building a key and building it three times.
    /// </remarks>
    private const int TypicalKeyLength = 64;

    /// <summary>
    /// Builds the key. Called once per type definition in this assembly, and on every ask for one
    /// that does not cache it.
    /// </summary>
    /// <remarks>
    /// The kind goes in front of the rendering because two types cannot share a fully qualified name,
    /// so a disagreement about the kind is a disagreement about which type is meant.
    /// </remarks>
    internal static string Build(ITypeDefinition type)
    {
        var builder = new StringBuilder(TypicalKeyLength);

        builder.Append((int)type.TypeDefinitionEnum);
        builder.Append(':');

        type.WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }

    /// <summary>
    /// The key of an attribute reference: the key of the type it stands for, marked as the reference
    /// rather than the type.
    /// </summary>
    internal static string BuildAttributeReference(ITypeDefinition attributeType)
    {
        return AttributeReferenceMarker + KeyOf(attributeType);
    }
}
