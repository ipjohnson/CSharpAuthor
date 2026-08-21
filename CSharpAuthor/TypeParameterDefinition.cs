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
        : this(name, isNullable, isArray ? new[] { 1 } : null)
    {
    }

    /// <remarks>
    /// Internal: an array shape is reached through <see cref="MakeArray(int)"/>, which is the part
    /// of this the model needs. Widening it later is not a breaking change.
    /// </remarks>
    internal TypeParameterDefinition(string name, bool isNullable, IReadOnlyList<int>? arrayRanks)
    {
        Name = name;
        ArrayRanks = BaseTypeDefinition.NormalizeRanks(arrayRanks);
        NullableAnnotations = BaseTypeDefinition.OuterAnnotationOnly(ArrayRanks.Count + 1, isNullable);
        IsNullable = isNullable;
    }

    /// <remarks>
    /// <c>T?[]</c> is an array of nullable <c>T</c> and <c>T[]?</c> is a nullable array of
    /// <c>T</c>; a type parameter reaches both the same way a named type does.
    /// </remarks>
    internal TypeParameterDefinition(string name, IReadOnlyList<int>? arrayRanks, IReadOnlyList<bool>? nullableAnnotations)
    {
        Name = name;
        ArrayRanks = BaseTypeDefinition.NormalizeRanks(arrayRanks);
        NullableAnnotations = BaseTypeDefinition.NormalizeAnnotations(nullableAnnotations, ArrayRanks.Count + 1);
        IsNullable = NullableAnnotations[0];
    }

    public string Name { get; }

    public string Namespace => "";

    public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;

    public bool IsNullable { get; }

    public bool IsArray => ArrayRanks.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<bool> NullableAnnotations { get; }

    public IReadOnlyList<int> ArrayRanks { get; }

    /// <summary>
    /// Always null: a type parameter is declared by a type or a method, never nested inside one.
    /// </summary>
    public ITypeDefinition? ContainingType => null;

    public IEnumerable<string> KnownNamespaces => Enumerable.Empty<string>();

    public IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        builder.Append(Name);

        BaseTypeDefinition.WriteArraySuffix(builder, ArrayRanks, NullableAnnotations);
    }

    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new TypeParameterDefinition(Name, ArrayRanks, BaseTypeDefinition.WithOuterAnnotation(NullableAnnotations, nullable));
    }

    public ITypeDefinition MakeArray()
    {
        return MakeArray(1);
    }

    public ITypeDefinition MakeArray(int rank)
    {
        return new TypeParameterDefinition(
            Name,
            BaseTypeDefinition.WithOuterRank(ArrayRanks, rank),
            BaseTypeDefinition.WithOuterLevel(NullableAnnotations));
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

        if (ArrayRanks.Count != typeParameter.ArrayRanks.Count)
        {
            return ArrayRanks.Count - typeParameter.ArrayRanks.Count;
        }

        for (var i = 0; i < ArrayRanks.Count; i++)
        {
            if (ArrayRanks[i] != typeParameter.ArrayRanks[i])
            {
                return ArrayRanks[i] - typeParameter.ArrayRanks[i];
            }
        }

        // Positional, not merely counted: T?[] and T[]? have one annotation each and are not the
        // same type.
        for (var i = 0; i < NullableAnnotations.Count && i < typeParameter.NullableAnnotations.Count; i++)
        {
            if (NullableAnnotations[i] != typeParameter.NullableAnnotations[i])
            {
                return NullableAnnotations[i] ? 1 : -1;
            }
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

            foreach (var annotation in NullableAnnotations)
            {
                hash = hash * 31 + annotation.GetHashCode();
            }

            foreach (var rank in ArrayRanks)
            {
                hash = hash * 31 + rank;
            }

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
