using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public abstract class BaseTypeDefinition : ITypeDefinition
{
    protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable, ITypeDefinition? containingType = null)
    {
        Name = name;
        Namespace = ns;
        IsNullable = isNullable;
        IsArray = isArray;
        TypeDefinitionEnum = typeDefinitionEnum;
        ContainingType = containingType;
    }

    public string Name { get; }

    public string Namespace { get; }

    /// <summary>
    /// The type this one is declared inside, for a nested type - <c>Outer</c> for
    /// <c>Outer.Inner</c>. Null for a top-level type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A nested type used to be indistinguishable from a top-level one, so it wrote its own name
    /// alone. <c>Inner</c> then bound to whatever <c>Inner</c> happened to be in scope at the point
    /// of use, or to nothing - and either way the container was gone.
    /// </para>
    /// <para>
    /// Held as a type definition rather than a dotted name so it renders in whatever mode the
    /// file is in, and so a generic container writes its own type arguments.
    /// </para>
    /// </remarks>
    public ITypeDefinition? ContainingType { get; }

    /// <summary>
    /// Writes the qualification in front of a type's own name: its container where it has one,
    /// otherwise whatever prefix the output mode calls for.
    /// </summary>
    protected void WriteQualification(StringBuilder builder, TypeOutputMode typeOutputMode)
    {
        if (ContainingType != null)
        {
            ContainingType.WriteTypeName(builder, typeOutputMode);
            builder.Append('.');

            return;
        }

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

    public abstract IEnumerable<string> KnownNamespaces { get; }

    public abstract void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);

    public abstract ITypeDefinition MakeNullable(bool nullable = true);

    public abstract ITypeDefinition MakeArray();
    public abstract IReadOnlyList<ITypeDefinition> TypeArguments { get; }

    public TypeDefinitionEnum TypeDefinitionEnum { get; }

    public bool IsNullable { get; }
        
    public bool IsArray { get; }

    public abstract int CompareTo(ITypeDefinition? other);

    protected int BaseCompareTo(ITypeDefinition? other)
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

        if (IsArray != other.IsArray)
        {
            return IsArray ? 1 : -1;
        }

        if (IsNullable != other.IsNullable)
        {
            return IsNullable ? 1 : -1;
        }

        // Two types named Inner in the same namespace are different types when they are nested in
        // different containers, so the container is part of identity.
        var otherContainingType = (other as BaseTypeDefinition)?.ContainingType;

        if (ContainingType == null || otherContainingType == null)
        {
            return ContainingType == otherContainingType ? 0 : ContainingType == null ? -1 : 1;
        }

        return ContainingType.CompareTo(otherContainingType);
    }
}