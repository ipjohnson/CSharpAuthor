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
        : this(name, isNullable, false, arrayRanks)
    {
    }

    internal TypeParameterDefinition(
        string name, bool isNullable, bool isElementNullable, IReadOnlyList<int>? arrayRanks)
    {
        Name = name;
        IsNullable = isNullable;
        ArrayRanks = BaseTypeDefinition.NormalizeRanks(arrayRanks);
        IsElementNullable = isElementNullable && ArrayRanks.Count > 0;
    }

    public string Name { get; }

    public string Namespace => "";

    public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;

    public bool IsNullable { get; }

    public bool IsArray => ArrayRanks.Count > 0;

    /// <inheritdoc cref="BaseTypeDefinition.IsElementNullable" />
    public bool IsElementNullable { get; }

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

        // T?[] is an array of nullable T; T[]? is a nullable array. The ? goes on the side the
        // caller asked for it.
        if (IsElementNullable)
        {
            builder.Append('?');
        }

        for (var i = 0; i < ArrayRanks.Count; i++)
        {
            builder.Append('[');

            for (var dimension = 1; dimension < ArrayRanks[i]; dimension++)
            {
                builder.Append(',');
            }

            builder.Append(']');
        }

        if (IsNullable)
        {
            builder.Append("?");
        }
    }

    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new TypeParameterDefinition(Name, nullable, IsElementNullable, ArrayRanks);
    }

    public ITypeDefinition MakeArray()
    {
        return MakeArray(1);
    }

    /// <inheritdoc cref="TypeDefinition.MakeArray(int)" />
    public ITypeDefinition MakeArray(int rank)
    {
        return new TypeParameterDefinition(
            Name, IsNullable, IsElementNullable, BaseTypeDefinition.WithOuterRank(ArrayRanks, rank));
    }

    /// <summary>
    /// The shared ordering. Answering -1 to everything that was not another type parameter made
    /// this smaller than every type and, read the other way, larger than none of them.
    /// </summary>
    public int CompareTo(ITypeDefinition other)
    {
        return TypeDefinitionOrder.Compare(this, other);
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
