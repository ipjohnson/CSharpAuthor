using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A generic type parameter, such as the T in <c>class Box&lt;T&gt;</c> or <c>T Get&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// A type parameter names nothing outside the declaration it belongs to: it has no namespace, and
/// qualifying it the way a real type is qualified would render it as the type that declared it.
/// It is written as itself in every output mode.
/// </remarks>
public class TypeParameterDefinition : ITypeDefinition
{
    public TypeParameterDefinition(string name, bool isNullable = false, bool isArray = false)
    {
        Name = name;
        IsNullable = isNullable;
        IsArray = isArray;
    }

    public string Name { get; }

    public string Namespace => "";

    public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;

    public bool IsNullable { get; }

    public bool IsArray { get; }

    public IEnumerable<string> KnownNamespaces => Enumerable.Empty<string>();

    public IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        builder.Append(Name);

        if (IsArray)
        {
            builder.Append("[]");
        }

        if (IsNullable)
        {
            builder.Append("?");
        }
    }

    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new TypeParameterDefinition(Name, nullable, IsArray);
    }

    public ITypeDefinition MakeArray()
    {
        return new TypeParameterDefinition(Name, IsNullable, true);
    }

    public int CompareTo(ITypeDefinition other)
    {
        if (other is not TypeParameterDefinition typeParameter)
        {
            return -1;
        }

        var nameCompare = string.Compare(Name, typeParameter.Name, StringComparison.Ordinal);

        if (nameCompare != 0)
        {
            return nameCompare;
        }

        if (IsArray != typeParameter.IsArray)
        {
            return IsArray ? 1 : -1;
        }

        if (IsNullable != typeParameter.IsNullable)
        {
            return IsNullable ? 1 : -1;
        }

        return 0;
    }

    /// <summary>
    /// Value equality, so a model holding one compares equal across runs. A source generator caches
    /// on its models, and reference equality would miss that cache on every edit.
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is TypeParameterDefinition other && CompareTo(other) == 0;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Name.GetHashCode();

            hash = hash * 31 + IsNullable.GetHashCode();
            hash = hash * 31 + IsArray.GetHashCode();

            return hash;
        }
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder);

        return builder.ToString();
    }
}
