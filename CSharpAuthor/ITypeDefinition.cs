using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public interface ITypeDefinition : IComparable<ITypeDefinition>
{
    TypeDefinitionEnum TypeDefinitionEnum { get; }

    /// <summary>
    /// Whether the type <em>itself</em> carries a <c>?</c> - the outermost annotation, which is
    /// <see cref="NullableAnnotations"/>[0]. <c>int?</c> is nullable; so is <c>int[]?</c>; but
    /// <c>int?[]</c> is an array, and an array is not nullable for having a nullable element.
    /// </summary>
    bool IsNullable { get; }

    bool IsArray { get; }

    /// <summary>
    /// Where each <c>?</c> sits. One entry per array level in <see cref="ArrayRanks"/> order -
    /// outermost first - followed by one for the element type itself, so it is always exactly one
    /// longer than <see cref="ArrayRanks"/> and never empty. <c>int?[]</c> is
    /// <c>[false, true]</c>; <c>int[]?</c> is <c>[true, false]</c>; <c>int?[]?</c> is
    /// <c>[true, true]</c>. For a type that is not an array it is the single flag
    /// <see cref="IsNullable"/>.
    /// </summary>
    /// <remarks>
    /// A single positionless flag cannot tell <c>int?[]</c> from <c>int[]?</c>, and it loses one of
    /// the two <c>?</c> in <c>int?[]?</c> without complaining. Those are three different types, and
    /// the one a positionless flag always picks - the annotation on the outside of the array - is
    /// what turned <c>new string?[] { null }</c> into <c>new string[]? { null }</c>, which is not a
    /// worse spelling of an array creation but a different node kind entirely.
    /// </remarks>
    IReadOnlyList<bool> NullableAnnotations { get; }

    /// <summary>
    /// The rank of each array wrapping this type, outermost first - the order the specifiers are
    /// written in. <c>int[,][]</c> is <c>[2, 1]</c>; <c>int[][,]</c> is <c>[1, 2]</c>. Empty when the
    /// type is not an array.
    /// </summary>
    /// <remarks>
    /// A single flag cannot tell <c>int[]</c> from <c>int[][]</c> from <c>int[,]</c>, and all three
    /// are different types. Reflection names them in the opposite order to C# — <c>typeof(int[,][])</c>
    /// is named <c>Int32[][,]</c> — so the list is the order the emitter needs, not the order
    /// <see cref="Type.Name"/> gives.
    /// </remarks>
    IReadOnlyList<int> ArrayRanks { get; }

    string Name { get; }

    string Namespace { get; }

    /// <summary>
    /// The type this one is declared inside, or null when it is declared directly in a namespace.
    /// </summary>
    /// <remarks>
    /// A nested type is named through its container - <c>Outer.Inner</c> - and dropping the container
    /// produces a name that resolves to a different type or to nothing at all. The container is held
    /// as a type definition rather than a string so a generic one keeps its arguments unrendered:
    /// <c>Outer&lt;T&gt;.Inner</c> qualifies the same way in every <see cref="TypeOutputMode"/>.
    /// </remarks>
    ITypeDefinition? ContainingType { get; }

    IEnumerable<string> KnownNamespaces { get; }

    void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);

    /// <summary>
    /// This type with its own - outermost - annotation set or cleared. Everything inside keeps its
    /// own: <c>int?[]</c>.MakeNullable() is <c>int?[]?</c>, never <c>int[]?</c>.
    /// </summary>
    ITypeDefinition MakeNullable(bool nullable = true);

    /// <summary>
    /// A one-dimensional array of this type.
    /// </summary>
    ITypeDefinition MakeArray();

    /// <summary>
    /// An array of this type with the given rank. The new array goes on the outside, so
    /// <c>Get(typeof(int)).MakeArray().MakeArray()</c> is <c>int[][]</c>.
    /// </summary>
    /// <remarks>
    /// The new level is the one that is now outermost, and it is not annotated: an array of
    /// <c>int?</c> is <c>int?[]</c>. The annotation belongs to the element that was asked for and
    /// stays with it, rather than migrating to the array that was just built around it.
    /// </remarks>
    ITypeDefinition MakeArray(int rank);

    IReadOnlyList<ITypeDefinition> TypeArguments { get; }
}

public static class ITypeDefinitionExtensions
{
    public static string GetShortName(this ITypeDefinition typeDefinition)
    {
        var stringBuilder = new StringBuilder();

        typeDefinition.WriteTypeName(stringBuilder);

        return stringBuilder.ToString();
    }

    /// <summary>
    /// An array whose <em>elements</em> are nullable - <c>string?[]</c>, as opposed to the nullable
    /// array <c>string[]?</c> that <see cref="ITypeDefinition.MakeArray(int)"/> gives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two are different types and both compile, so a caller who meant one and was handed the
    /// other gets no diagnostic anywhere - the defect class V2-HANDOFF.md section 1 is about.
    /// Until now only <c>string[]?</c> could be written at all.
    /// </para>
    /// <para>
    /// It is a separate call rather than a reading of <c>MakeNullable().MakeArray()</c> because
    /// that composition already has a meaning here:
    /// <c>TypeDefinitionTests.ArrayRankTests.NullableGoesAfterTheShape</c> pins it as the nullable
    /// array, which is what version 1 wrote. Which of the two that composition should mean is
    /// recorded in docs/v2-open-questions.md as a question for the human rather than decided by
    /// changing a test.
    /// </para>
    /// <para>
    /// An implementation of <see cref="ITypeDefinition"/> from outside this assembly cannot be
    /// asked for the shape, so it is refused rather than silently given the other type.
    /// </para>
    /// </remarks>
    public static ITypeDefinition MakeArrayOfNullable(this ITypeDefinition typeDefinition, int rank = 1)
    {
        if (typeDefinition == null)
        {
            throw new ArgumentNullException(nameof(typeDefinition));
        }

        // Nullability carries a position now, so this is the composition it always meant: annotate
        // the element, then wrap it. No case per implementation, and it works for anything that
        // models the shape - the Roslyn bridge's types included.
        var result = typeDefinition.MakeNullable().MakeArray(rank);

        // An implementation whose MakeNullable/MakeArray ignore the request would hand back a type
        // of the wrong shape. Refuse that rather than return it, which is the whole point of the
        // method: an array of a nullable element is not a nullable array.
        if (result.ArrayRanks.Count != typeDefinition.ArrayRanks.Count + 1 ||
            !result.NullableAnnotations[result.NullableAnnotations.Count - 1])
        {
            throw new NotSupportedException(
                typeDefinition.GetType().FullName +
                " does not model element nullability, so an array of a nullable element cannot " +
                "be built from it. Refused rather than written as the nullable array, which is " +
                "a different type.");
        }

        return result;
    }

}
