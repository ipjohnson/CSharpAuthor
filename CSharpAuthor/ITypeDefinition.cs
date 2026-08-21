using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

public interface ITypeDefinition : IComparable<ITypeDefinition>
{
    TypeDefinitionEnum TypeDefinitionEnum { get; }

    bool IsNullable { get; }

    bool IsArray { get; }

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

    ITypeDefinition MakeNullable(bool nullable = true);

    /// <summary>
    /// A one-dimensional array of this type.
    /// </summary>
    ITypeDefinition MakeArray();

    /// <summary>
    /// An array of this type with the given rank. The new array goes on the outside, so
    /// <c>Get(typeof(int)).MakeArray().MakeArray()</c> is <c>int[][]</c>.
    /// </summary>
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

        switch (typeDefinition)
        {
            case GenericTypeDefinition generic:
                return new GenericTypeDefinition(
                    generic.TypeDefinitionEnum,
                    generic.Namespace,
                    generic.Name,
                    generic.TypeArguments,
                    BaseTypeDefinition.WithOuterRank(generic.ArrayRanks, rank),
                    generic.IsNullable && generic.IsArray,
                    true,
                    generic.ContainingType);

            case TypeDefinition type:
                return new TypeDefinition(
                    type.TypeDefinitionEnum,
                    type.Namespace,
                    type.Name,
                    BaseTypeDefinition.WithOuterRank(type.ArrayRanks, rank),
                    type.IsNullable && type.IsArray,
                    true,
                    type.ContainingType);

            case TypeParameterDefinition typeParameter:
                return new TypeParameterDefinition(
                    typeParameter.Name,
                    typeParameter.IsNullable && typeParameter.IsArray,
                    true,
                    BaseTypeDefinition.WithOuterRank(typeParameter.ArrayRanks, rank));

            default:
                throw new NotSupportedException(
                    typeDefinition.GetType().FullName +
                    " does not model element nullability, so an array of a nullable element cannot " +
                    "be built from it. Refused rather than written as the nullable array, which is " +
                    "a different type.");
        }
    }
}
