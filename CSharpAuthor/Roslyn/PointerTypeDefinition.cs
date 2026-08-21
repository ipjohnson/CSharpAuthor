using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor.Roslyn;

/// <summary>A pointer type — <c>int*</c>, <c>void*</c>, <c>int**</c>.</summary>
/// <remarks>
/// The pointed-at type stays a type definition rather than a name, so <c>SomeStruct*</c> still
/// qualifies and still contributes its namespace. A pointer is never nullable and never generic;
/// nesting one inside an array is what produces <c>int*[]</c>.
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
sealed class PointerTypeDefinition : ITypeDefinition
{
    private int? _hashCode;
    private string? _key;

    public PointerTypeDefinition(ITypeDefinition pointedAtType)
    {
        PointedAtType = pointedAtType ?? throw new ArgumentNullException(nameof(pointedAtType));
    }

    public ITypeDefinition PointedAtType { get; }

    public TypeDefinitionEnum TypeDefinitionEnum => PointedAtType.TypeDefinitionEnum;

    public bool IsNullable => false;

    public bool IsArray => false;

    public string Name => PointedAtType.Name;

    public string Namespace => PointedAtType.Namespace;

    public IEnumerable<string> KnownNamespaces => PointedAtType.KnownNamespaces;

    public IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        PointedAtType.WriteTypeName(builder, typeOutputMode);

        builder.Append('*');
    }

    /// <summary>A pointer cannot be nullable; the request is answered by the pointer itself.</summary>
    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return this;
    }

    public ITypeDefinition MakeArray()
    {
        return new ArrayTypeDefinition(this);
    }

    /// <summary>
    /// The rank of each array wrapping this type, outermost first. Empty: this type is not an array.
    /// </summary>
    /// <remarks>
    /// Present so the bridge's types satisfy the type model's array-rank contract without a change
    /// at merge time.
    /// </remarks>
    public IReadOnlyList<int> ArrayRanks => Array.Empty<int>();

    /// <summary>
    /// Where each <c>?</c> sits: one flag per array level, then one for the element. This type is
    /// not an array, so it is the single flag <see cref="IsNullable"/>.
    /// </summary>
    /// <remarks>
    /// Present so the bridge's types satisfy the type model's nullable-position contract, the same
    /// way <see cref="ArrayRanks"/> answers its array-rank contract.
    /// </remarks>
    public IReadOnlyList<bool> NullableAnnotations => BaseTypeDefinition.OuterAnnotationOnly(1, IsNullable);

    /// <summary>The type this one is declared inside. A pointer is declared inside nothing, which is what a pointer symbol reports.</summary>
    public ITypeDefinition? ContainingType => null;

    /// <summary>An array of this type with the given rank.</summary>
    public ITypeDefinition MakeArray(int rank)
    {
        return new ArrayTypeDefinition(this, rank);
    }

    /// <inheritdoc cref="BaseTypeDefinition.TypeKey" />
    private string TypeKey => _key ??= TypeDefinitionIdentity.KeyOf(this);

    public int CompareTo(ITypeDefinition? other)
    {
        return TypeDefinitionIdentity.KeyCompare(TypeKey, other);
    }

    public override bool Equals(object? obj)
    {
        return TypeDefinitionIdentity.KeyEquals(TypeKey, obj);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= TypeKey.GetHashCode();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder, TypeOutputMode.FullName);

        return builder.ToString();
    }
}
