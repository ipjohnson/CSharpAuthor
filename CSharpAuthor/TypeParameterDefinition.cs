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

    /// <summary>
    /// A type parameter named <paramref name="name"/> - the <c>T</c> a generic declaration and its
    /// members both refer to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Build one per use rather than sharing an instance; two with the same name are equal, so
    /// nothing depends on it being the same object. <see cref="ClassDefinition.AddGenericParameter(string)"/>
    /// makes one for a type declaration, and a member that mentions it needs one of its own:
    /// </para>
    /// <example>
    /// <code>
    /// var create = greeter.AddMethod("Create");
    /// create.AddGenericParameter(new TypeParameterDefinition("T"));
    /// create.SetReturnType(new TypeParameterDefinition("T"));
    /// create.AddConstraint("T").DefaultConstructor();
    /// create.Return("new T()");
    /// </code>
    /// which is <c>public T Create&lt;T&gt;() where T : new()</c>.
    /// </example>
    /// <para>
    /// <paramref name="isNullable"/> gives <c>T?</c> and <paramref name="isArray"/> gives
    /// <c>T[]</c>. For anything past those two, ask for the shape:
    /// <see cref="MakeArray(int)"/> and <see cref="MakeNullable"/> compose the same way they do on
    /// a named type, and <see cref="ITypeDefinitionExtensions.MakeArrayOfNullable"/> gives
    /// <c>T?[]</c>.
    /// </para>
    /// </remarks>
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

    /// <summary>The parameter's name, as declared.</summary>
    public string Name { get; }

    /// <summary>
    /// Always empty: a type parameter names nothing outside the declaration it belongs to, so
    /// there is no namespace to import and nothing to qualify.
    /// </summary>
    public string Namespace => "";

    /// <summary>
    /// Always <see cref="TypeDefinitionEnum.ClassDefinition"/>. A type parameter's real kind is
    /// whatever it is constrained to, which this cannot know and does not need to - it does not
    /// change how the name is written.
    /// </summary>
    public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;

    /// <inheritdoc />
    public bool IsNullable { get; }

    /// <inheritdoc />
    public bool IsArray => ArrayRanks.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<bool> NullableAnnotations { get; }

    /// <inheritdoc />
    public IReadOnlyList<int> ArrayRanks { get; }

    /// <summary>
    /// Always null: a type parameter is declared by a type or a method, never nested inside one.
    /// </summary>
    public ITypeDefinition? ContainingType => null;

    /// <summary>
    /// Always empty - a type parameter needs no <c>using</c>. It is what stops a generic member
    /// from pulling a namespace into a file that does not need one.
    /// </summary>
    public IEnumerable<string> KnownNamespaces => Enumerable.Empty<string>();

    /// <summary>
    /// Always empty: a type parameter is not itself a constructed generic, even where it is
    /// constrained to one.
    /// </summary>
    public IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

    /// <summary>
    /// Writes the parameter as its name, plus any array specifiers and nullable annotations.
    /// </summary>
    /// <remarks>
    /// <paramref name="typeOutputMode"/> is accepted and ignored, which is the point: qualifying a
    /// type parameter would render it as the type that declared it. <c>T</c> reads as <c>T</c> in
    /// every mode.
    /// </remarks>
    public void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName)
    {
        // Always escaped: a type parameter's name is only ever an identifier the caller chose, so
        // there is no keyword alias to confuse it with. `class Box<int>` is CS1001.
        builder.Append(CSharpIdentifier.Escape(Name));

        // T?[] is an array of nullable T; T[]? is a nullable array. WriteArraySuffix places each
        // annotation at the level that carries it, so there is nothing to append here.
        BaseTypeDefinition.WriteArraySuffix(builder, ArrayRanks, NullableAnnotations);
    }

    /// <inheritdoc />
    public ITypeDefinition MakeNullable(bool nullable = true)
    {
        return new TypeParameterDefinition(Name, ArrayRanks, BaseTypeDefinition.WithOuterAnnotation(NullableAnnotations, nullable));
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Orders by the same identity <see cref="Equals(object)"/> uses, so a sorted collection of
    /// type references is stable across runs.
    /// </summary>
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

    /// <inheritdoc cref="Equals(object)" />
    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= TypeKey.GetHashCode();
    }

    /// <summary>
    /// The parameter as C# writes it - <c>T</c>, <c>T?[]</c>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="TypeDefinition.ToString"/>, which keeps a 1.x identity shape, this is the
    /// real name: a type parameter has no namespace for that shape to differ over.
    /// </remarks>
    public override string ToString()
    {
        var builder = new StringBuilder();

        WriteTypeName(builder);

        return builder.ToString();
    }
}
