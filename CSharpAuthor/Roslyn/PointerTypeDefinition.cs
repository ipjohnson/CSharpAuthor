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
public sealed class PointerTypeDefinition : ITypeDefinition
{
    private int? _hashCode;

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

    public int CompareTo(ITypeDefinition? other)
    {
        if (ReferenceEquals(other, null))
        {
            return 1;
        }

        if (other is not PointerTypeDefinition pointer)
        {
            var otherCompare = string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);

            return otherCompare != 0 ? otherCompare : -1;
        }

        return PointedAtType.CompareTo(pointer.PointedAtType);
    }

    public override bool Equals(object? obj)
    {
        return obj is PointerTypeDefinition pointer && CompareTo(pointer) == 0;
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
