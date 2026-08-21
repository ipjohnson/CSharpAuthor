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
    private int? _hashCode;
    private string? _key;

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
        // Always escaped: a type parameter's name is only ever an identifier the caller chose, so
        // there is no keyword alias to confuse it with. `class Box<int>` is CS1001.
        builder.Append(CSharpIdentifier.Escape(Name));

        // T?[] is an array of nullable T; T[]? is a nullable array. WriteArraySuffix places each
        // annotation at the level that carries it, so there is nothing to append here.
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

    /// <inheritdoc cref="TypeDefinition.MakeArray(int)" />
    public ITypeDefinition MakeArray(int rank)
    {
        return new TypeParameterDefinition(
            Name,
            BaseTypeDefinition.WithOuterRank(ArrayRanks, rank),
            BaseTypeDefinition.WithOuterLevel(NullableAnnotations));
    }

    /// <inheritdoc cref="BaseTypeDefinition.TypeKey" />
    internal string TypeKey => _key ??= TypeDefinitionIdentity.Build(this);

    public int CompareTo(ITypeDefinition other)
    {
        return TypeDefinitionIdentity.KeyCompare(TypeKey, other);
    }

    /// <summary>
    /// Value equality, so a model holding one compares equal across runs. A source generator caches
    /// on its models, and reference equality would miss that cache on every edit.
    /// </summary>
    /// <remarks>
    /// A type parameter writes itself as its name in every output mode, so what it is equal to is
    /// anything that writes the same name - a <c>T</c> read off a symbol and a <c>T</c> a caller
    /// built with <see cref="TypeDefinition.Get(string,string,bool,bool)"/> name the same thing in
    /// the declaration they appear in.
    /// </remarks>
    public override bool Equals(object obj)
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

        WriteTypeName(builder);

        return builder.ToString();
    }
}
