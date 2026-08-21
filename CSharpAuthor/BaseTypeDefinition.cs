using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CSharpAuthor;

public abstract class BaseTypeDefinition : ITypeDefinition
{
    private static readonly IReadOnlyList<int> _notAnArray = new ReadOnlyCollection<int>(Array.Empty<int>());
    private static readonly IReadOnlyList<int> _oneDimensional = new ReadOnlyCollection<int>(new[] { 1 });

    /// <summary>
    /// The two annotation lists a type that is not an array can have. Almost every type in a
    /// generator's model is one of these, so they are shared rather than allocated per type.
    /// </summary>
    private static readonly IReadOnlyList<bool> _plain = new ReadOnlyCollection<bool>(new[] { false });
    private static readonly IReadOnlyList<bool> _annotated = new ReadOnlyCollection<bool>(new[] { true });

    private int? _hashCode;
    private string? _key;

    protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, bool isArray, bool isNullable)
        : this(typeDefinitionEnum, ns, name, isArray ? _oneDimensional : _notAnArray, isNullable, null)
    {
    }

    /// <remarks>
    /// The rank-carrying constructors and the write helpers below are <c>private protected</c>: they
    /// are how this assembly's own type definitions are built and written, not surface a consumer
    /// needs. The 1.x <c>protected</c> constructor above is untouched, so an outside subclass still
    /// has the entry point it always had. Widening one of these later is not a breaking change;
    /// narrowing it would be.
    /// </remarks>
    private protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable)
        : this(typeDefinitionEnum, ns, name, arrayRanks, isNullable, null)
    {
    }

    private protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable, ITypeDefinition? containingType)
        : this(typeDefinitionEnum, ns, name, arrayRanks, isNullable, false, containingType)
    {
    }

    private protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, bool isNullable, bool isElementNullable, ITypeDefinition? containingType)
    {
        Name = name;
        Namespace = ns;
        ArrayRanks = NormalizeRanks(arrayRanks);
        // The 1.x meaning of the flag, kept exactly: it is the type's own annotation, and nothing
        // inside an array it wraps is annotated.
        NullableAnnotations = OuterAnnotationOnly(ArrayRanks.Count + 1, isNullable);
        IsNullable = isNullable;
        ContainingType = containingType;
        TypeDefinitionEnum = typeDefinitionEnum;
    }

    /// <remarks>
    /// The annotation-carrying constructor. Nullability has a position - <c>int?[]</c> and
    /// <c>int[]?</c> are different types - so the list says where each <c>?</c> is rather than
    /// leaving the emitter to put one wherever it writes them.
    /// </remarks>
    private protected BaseTypeDefinition(TypeDefinitionEnum typeDefinitionEnum, string ns, string name, IReadOnlyList<int>? arrayRanks, IReadOnlyList<bool>? nullableAnnotations, ITypeDefinition? containingType)
    {
        Name = name;
        Namespace = ns;
        ArrayRanks = NormalizeRanks(arrayRanks);
        NullableAnnotations = NormalizeAnnotations(nullableAnnotations, ArrayRanks.Count + 1);
        IsNullable = NullableAnnotations[0];
        ContainingType = containingType;
        TypeDefinitionEnum = typeDefinitionEnum;
    }

    public string Name { get; }

    public string Namespace { get; }

    public abstract IEnumerable<string> KnownNamespaces { get; }

    public abstract void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);

    public abstract ITypeDefinition MakeNullable(bool nullable = true);

    public ITypeDefinition MakeArray()
    {
        return MakeArray(1);
    }

    public abstract ITypeDefinition MakeArray(int rank);

    public abstract IReadOnlyList<ITypeDefinition> TypeArguments { get; }

    public TypeDefinitionEnum TypeDefinitionEnum { get; }

    /// <inheritdoc />
    public bool IsNullable { get; }

    /// <summary>
    /// Whether the innermost element of an array type is nullable - <c>string?[]</c> rather than
    /// <c>string[]?</c>. False for anything that is not an array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>string?[]</c> and <c>string[]?</c> are different types and both compile, so writing the
    /// <c>?</c> and the <c>[]</c> in a fixed order does not fail - it hands the caller the other
    /// type. <c>MakeNullable().MakeArray()</c> is the array of a nullable element;
    /// <c>MakeArray().MakeNullable()</c> is the nullable array. Which one was asked for is the
    /// order the calls were made in.
    /// </para>
    /// <para>
    /// Nullability at a level between the two - <c>string[]?[]</c> - is not expressible. Making an
    /// array of a nullable array carries the <c>?</c> outward, which is what version 1 did.
    /// </para>
    /// <para>
    /// Deliberately not on <see cref="ITypeDefinition"/>. Adding a member to that interface breaks
    /// every implementation of it outside this assembly, and one exists in this repository's own
    /// test project - which is the evidence that they exist elsewhere too. Handbook rule 8.4: where
    /// the spec is silent, keep version 1 source-compatible.
    /// </para>
    /// </remarks>
    public bool IsElementNullable { get; }

    /// <inheritdoc />
    public IReadOnlyList<bool> NullableAnnotations { get; }

    /// <inheritdoc />
    public IReadOnlyList<int> ArrayRanks { get; }

    /// <inheritdoc />
    public bool IsArray => ArrayRanks.Count > 0;

    /// <inheritdoc />
    public ITypeDefinition? ContainingType { get; }

    public abstract int CompareTo(ITypeDefinition other);

    /// <summary>
    /// The type's identity: what makes it the same type as another definition of it, whichever class
    /// that one happens to be. See <see cref="TypeDefinitionIdentity"/>.
    /// </summary>
    /// <remarks>
    /// Built once and kept. The definition is immutable in every way the key reads - the array ranks
    /// are copied behind a read-only view at construction for exactly this reason - so the answer
    /// cannot go stale.
    /// </remarks>
    internal string TypeKey => _key ??= TypeDefinitionIdentity.Build(this);

    /// <summary>
    /// Two type definitions are equal when they denote the same type: the same kind, the same fully
    /// qualified name, the same container, generic arguments and array shape.
    /// </summary>
    /// <remarks>
    /// Not "the same class as this one". <see cref="ITypeDefinition"/> is an interface, and the
    /// bridge implements it several more times over for the shapes this class cannot hold - a
    /// nullable value type, a ranked array, a nested type whose containers are generic. A bridged
    /// <c>int?</c> and <c>TypeDefinition.Get(typeof(int)).MakeNullable()</c> are the same type, and a
    /// generator matching a parameter against a registration is comparing exactly those two.
    /// </remarks>
    public override bool Equals(object? obj)
    {
        return TypeDefinitionIdentity.KeyEquals(TypeKey, obj);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode ??= TypeKey.GetHashCode();
    }

    /// <summary>
    /// Orders this type against another, zero exactly when <see cref="Equals(object)"/> says they are
    /// the same type.
    /// </summary>
    /// <remarks>
    /// This used to compare the properties one at a time and stop, which is why it was not safe to
    /// remove the class check from <see cref="Equals(object)"/>. Name, namespace, container, ranks
    /// and nullability are shared by <c>int</c> and <c>int*</c>, by <c>Ns.List</c> and
    /// <c>Ns.List&lt;int&gt;</c>, and by <c>System.ValueTuple</c> and <c>(int a, string b)</c>: it
    /// reported each of those pairs equal, while the other side of the pair reported them different
    /// and hashed them differently. A subclass calling this still gets a comparison that stops at
    /// what it renders, and is free to break a remaining tie on anything it does not.
    /// </remarks>
    protected int BaseCompareTo(ITypeDefinition other)
    {
        return TypeDefinitionIdentity.KeyCompare(TypeKey, other);
    }

    /// <summary>
    /// The type's own name as it has to be written: prefixed with <c>@</c> when it is a reserved
    /// word, so a type read out of a schema and called <c>event</c> names itself rather than
    /// producing CS1001 at every reference.
    /// </summary>
    /// <remarks>
    /// Only a type that has somewhere to live - a namespace or a containing type - is escaped. A
    /// type with neither is the shape the keyword aliases take: <c>int</c> and <c>string</c> are
    /// held as a name with no namespace, and <c>@int</c> is not the same type as <c>int</c>.
    /// </remarks>
    private protected string WrittenName()
    {
        return Namespace.Length > 0 || ContainingType != null
            ? CSharpIdentifier.Escape(Name)
            : Name;
    }

    /// <summary>
    /// Writes the array specifiers, outermost first, so ranks <c>[2, 1]</c> read as <c>[,][]</c> -
    /// preceded by the element's <c>?</c> where it has one, because <c>string?[]</c> and
    /// <c>string[]?</c> are different types.
    /// </summary>
    private protected void WriteArraySuffix(StringBuilder builder)
    {
        WriteArraySuffix(builder, ArrayRanks, NullableAnnotations);
    }

    /// <summary>
    /// Ranks <c>[2, 1]</c> with no annotations read as <c>[,][]</c> - outermost first, the order C#
    /// writes them in.
    /// </summary>
    /// <remarks>
    /// An annotation breaks that run. <c>string[]?[]</c> is an array of nullable arrays: the
    /// <c>?</c> closes off every specifier to its left before the next one wraps them, so the
    /// specifiers come out in groups, each group ending at the annotated level that closes it and
    /// each group written outermost-first within itself. This is the same walk
    /// <c>CSharpAuthor.Roslyn.ArrayTypeDefinition</c> does over its nested levels; a flat pair of
    /// lists is the same information in the shape the rest of the model already stores.
    /// </remarks>
    internal static void WriteArraySuffix(StringBuilder builder, IReadOnlyList<int> ranks, IReadOnlyList<bool> annotations)
    {
        var levels = ranks.Count;

        // The last entry belongs to the element, and the element has already been written.
        if (annotations.Count > levels && annotations[levels])
        {
            builder.Append('?');
        }

        var innerEnd = levels - 1;

        while (innerEnd >= 0)
        {
            var outerStart = innerEnd;

            while (outerStart > 0 && !Annotated(annotations, outerStart))
            {
                outerStart--;
            }

            for (var i = outerStart; i <= innerEnd; i++)
            {
                builder.Append('[');

                for (var dimension = 1; dimension < ranks[i]; dimension++)
                {
                    builder.Append(',');
                }

                builder.Append(']');
            }

            if (Annotated(annotations, outerStart))
            {
                builder.Append('?');
            }

            innerEnd = outerStart - 1;
        }
    }

    private static bool Annotated(IReadOnlyList<bool> annotations, int level)
    {
        return level < annotations.Count && annotations[level];
    }

    /// <summary>
    /// Writes everything that comes before the type's own name: the containing type if it has one,
    /// the namespace otherwise.
    /// </summary>
    /// <remarks>
    /// A nested type is qualified by its container, not by its namespace - <c>Ns.Outer.Inner</c>, never
    /// <c>Ns.Inner</c>. The container writes itself in the same mode, so it picks up <c>global::</c> or
    /// the namespace exactly once, at the outermost type, and the chain below it stays plain.
    /// </remarks>
    private protected void WriteQualifier(StringBuilder builder, TypeOutputMode typeOutputMode)
    {
        if (ContainingType != null)
        {
            ContainingType.WriteTypeName(builder, typeOutputMode);
            builder.Append('.');

            return;
        }

        WriteNamespacePrefix(builder, typeOutputMode);
    }

    private protected void WriteNamespacePrefix(StringBuilder builder, TypeOutputMode typeOutputMode)
    {
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

    /// <summary>
    /// An array of this type: the new rank goes on the outside, so making an array of <c>int[]</c>
    /// gives <c>int[][]</c> rather than losing a dimension.
    /// </summary>
    private protected IReadOnlyList<int> ArrayRanksWithOuterRank(int rank)
    {
        return WithOuterRank(ArrayRanks, rank);
    }

    /// <summary>
    /// The annotations for an array built around this type: a new, unannotated level on the
    /// outside, and everything that was already there one step further in.
    /// </summary>
    private protected IReadOnlyList<bool> AnnotationsWithOuterLevel()
    {
        return WithOuterLevel(NullableAnnotations);
    }

    /// <summary>This type's annotations with its own - outermost - flag set to <paramref name="nullable"/>.</summary>
    private protected IReadOnlyList<bool> AnnotationsWithOuterAnnotation(bool nullable)
    {
        return WithOuterAnnotation(NullableAnnotations, nullable);
    }

    /// <inheritdoc cref="AnnotationsWithOuterLevel" />
    internal static IReadOnlyList<bool> WithOuterLevel(IReadOnlyList<bool> annotations)
    {
        var result = new bool[annotations.Count + 1];

        for (var i = 0; i < annotations.Count; i++)
        {
            result[i + 1] = annotations[i];
        }

        return new ReadOnlyCollection<bool>(result);
    }

    /// <inheritdoc cref="AnnotationsWithOuterAnnotation" />
    internal static IReadOnlyList<bool> WithOuterAnnotation(IReadOnlyList<bool> annotations, bool nullable)
    {
        if (annotations.Count <= 1)
        {
            return nullable ? _annotated : _plain;
        }

        if (annotations[0] == nullable)
        {
            return annotations;
        }

        var result = new bool[annotations.Count];

        result[0] = nullable;

        for (var i = 1; i < annotations.Count; i++)
        {
            result[i] = annotations[i];
        }

        return new ReadOnlyCollection<bool>(result);
    }

    /// <summary>
    /// The 1.x flag read as a list: the type's own annotation, and nothing inside it annotated.
    /// </summary>
    internal static IReadOnlyList<bool> OuterAnnotationOnly(int levelCount, bool isNullable)
    {
        if (levelCount <= 1)
        {
            return isNullable ? _annotated : _plain;
        }

        var result = new bool[levelCount];

        result[0] = isNullable;

        return new ReadOnlyCollection<bool>(result);
    }

    /// <summary>
    /// Takes a copy behind a read-only view, and refuses a list that does not have one flag per
    /// array level plus one for the element.
    /// </summary>
    /// <remarks>
    /// A wrong length is not recoverable by guessing: padding it would decide, silently, which
    /// level the caller meant to annotate, and getting that wrong produces a different type that
    /// still compiles. That is the failure this whole property exists to remove.
    /// </remarks>
    internal static IReadOnlyList<bool> NormalizeAnnotations(IReadOnlyList<bool>? nullableAnnotations, int levelCount)
    {
        if (nullableAnnotations == null)
        {
            return OuterAnnotationOnly(levelCount, false);
        }

        if (nullableAnnotations.Count != levelCount)
        {
            throw new ArgumentException(
                $"A type with {levelCount - 1} array level(s) has {levelCount} nullable annotations - one per level, outermost first, then one for the element. Got {nullableAnnotations.Count}.",
                nameof(nullableAnnotations));
        }

        if (levelCount == 1)
        {
            return nullableAnnotations[0] ? _annotated : _plain;
        }

        var copy = new bool[levelCount];

        for (var i = 0; i < levelCount; i++)
        {
            copy[i] = nullableAnnotations[i];
        }

        return new ReadOnlyCollection<bool>(copy);
    }

    /// <inheritdoc cref="ArrayRanksWithOuterRank" />
    internal static IReadOnlyList<int> WithOuterRank(IReadOnlyList<int> ranks, int rank)
    {
        CheckRank(rank);

        if (ranks.Count == 0)
        {
            return rank == 1 ? _oneDimensional : new ReadOnlyCollection<int>(new[] { rank });
        }

        var result = new int[ranks.Count + 1];

        result[0] = rank;

        for (var i = 0; i < ranks.Count; i++)
        {
            result[i + 1] = ranks[i];
        }

        return new ReadOnlyCollection<int>(result);
    }

    /// <summary>
    /// Takes a copy behind a read-only view, so the shape cannot change under a type definition that
    /// has already cached a hash from it.
    /// </summary>
    internal static IReadOnlyList<int> NormalizeRanks(IReadOnlyList<int>? arrayRanks)
    {
        if (arrayRanks == null || arrayRanks.Count == 0)
        {
            return _notAnArray;
        }

        if (arrayRanks.Count == 1)
        {
            CheckRank(arrayRanks[0]);

            return arrayRanks[0] == 1 ? _oneDimensional : new ReadOnlyCollection<int>(new[] { arrayRanks[0] });
        }

        var copy = new int[arrayRanks.Count];

        for (var i = 0; i < arrayRanks.Count; i++)
        {
            CheckRank(arrayRanks[i]);

            copy[i] = arrayRanks[i];
        }

        return new ReadOnlyCollection<int>(copy);
    }

    private static void CheckRank(int rank)
    {
        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "An array rank is at least 1.");
        }
    }
}
