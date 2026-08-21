using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// A reference to a type, kept unrendered so it can be written differently depending on the file it
/// lands in.
/// </summary>
/// <remarks>
/// <para>
/// This is the reason a generated file's <c>using</c> list can be derived rather than declared.
/// A type reference is carried all the way to <see cref="OutputContext.Output"/> as a type - never
/// flattened to a string when the tree is built - so at the moment the file is serialized it can be
/// qualified, given an alias, or left short, and its namespace can be counted. A name that became
/// text early has none of that: it is the same characters whatever file it is in, and nothing knows
/// a type was ever mentioned.
/// </para>
/// <para>
/// Get one with <see cref="TypeDefinition.Get(Type)"/> for a type that exists,
/// <see cref="TypeDefinition.Get(string, string, bool, bool)"/> for one this generator is emitting,
/// or <see cref="TypeDefinition.GetNested"/> for one declared inside another. Shapes are built by
/// asking an existing type for a variant of itself - <see cref="MakeArray()"/>,
/// <see cref="MakeNullable"/>, <see cref="ITypeDefinitionExtensions.MakeArrayOfNullable"/> - which
/// return new instances and leave the original alone.
/// </para>
/// <para>
/// <strong><see cref="object.ToString"/> is not the C# name.</strong> Use
/// <see cref="ITypeDefinitionExtensions.GetShortName"/>, or write the type into an
/// <see cref="IOutputContext"/> and let the file spell it. See
/// <see cref="TypeDefinition.ToString"/>.
/// </para>
/// </remarks>
public interface ITypeDefinition : IComparable<ITypeDefinition>
{
    /// <summary>
    /// What kind of type this is - class, interface, enum, type parameter.
    /// </summary>
    /// <remarks>
    /// Carried for callers that need to branch on it; it does not change how the name is written.
    /// </remarks>
    TypeDefinitionEnum TypeDefinitionEnum { get; }

    /// <summary>
    /// Whether the type <em>itself</em> carries a <c>?</c> - the outermost annotation, which is
    /// <see cref="NullableAnnotations"/>[0]. <c>int?</c> is nullable; so is <c>int[]?</c>; but
    /// <c>int?[]</c> is an array, and an array is not nullable for having a nullable element.
    /// </summary>
    bool IsNullable { get; }

    /// <summary>
    /// Whether the type is an array of anything - equivalently, whether
    /// <see cref="ArrayRanks"/> has entries. It says nothing about how many levels or of what rank.
    /// </summary>
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

    /// <summary>
    /// The simple name, without namespace, container, type arguments or array specifiers -
    /// <c>List</c> for <c>List&lt;string&gt;</c>.
    /// </summary>
    /// <remarks>
    /// A predefined type carries its C# keyword here and an empty <see cref="Namespace"/>:
    /// <c>TypeDefinition.Get(typeof(int)).Name</c> is <c>"int"</c>, not <c>"Int32"</c>. A keyword
    /// needs no namespace, so it reads the same in every <see cref="TypeOutputMode"/>.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// The declaring namespace, or the empty string for a predefined type and for a type declared
    /// at the top level of a file.
    /// </summary>
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

    /// <summary>
    /// Every namespace this reference needs a <c>using</c> for - its own, its container's, and its
    /// type arguments'.
    /// </summary>
    /// <remarks>
    /// The file reads this rather than being told, which is what makes the <c>using</c> list
    /// derived: a generic type brings its arguments' namespaces with it, and a nested type brings
    /// its container's, without anything having to remember to ask.
    /// </remarks>
    IEnumerable<string> KnownNamespaces { get; }

    /// <summary>
    /// Appends the type as C# spells it - keyword, qualification, container, type arguments,
    /// nullable annotations and array specifiers, in the right order.
    /// </summary>
    /// <remarks>
    /// This is the real name, and <see cref="ITypeDefinitionExtensions.GetShortName"/> is the
    /// convenience wrapper around it. Writing a type into an <see cref="IOutputContext"/> is what
    /// almost every caller wants instead - the context passes the mode the file settled on, and
    /// counts the namespace.
    /// </remarks>
    void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);

    /// <summary>
    /// This type with its own - outermost - annotation set or cleared. Everything inside keeps its
    /// own: <c>int?[]</c>.MakeNullable() is <c>int?[]?</c>, never <c>int[]?</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A new instance; the original is unchanged. <c>MakeNullable(false)</c> clears the outer
    /// annotation, which is how a nullable type is made non-nullable again.
    /// </para>
    /// <para>
    /// <strong>Order of composition decides which type you get, and both compile.</strong> The
    /// annotation attaches to whatever is outermost <em>at the time of the call</em>:
    /// </para>
    /// <example>
    /// <code>
    /// TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray()  // int?[]  - array of nullable ints
    /// TypeDefinition.Get(typeof(int)).MakeArray().MakeNullable()  // int[]?  - nullable array of ints
    /// </code>
    /// </example>
    /// <para>
    /// Annotate then wrap, and the <c>?</c> is on the element. Wrap then annotate, and it is on the
    /// array. Nothing warns about either. Say which one you mean by name:
    /// <see cref="ITypeDefinitionExtensions.MakeArrayOfNullable"/> is <c>int?[]</c>, and
    /// <c>MakeArray().MakeNullable()</c> is <c>int[]?</c>.
    /// </para>
    /// </remarks>
    ITypeDefinition MakeNullable(bool nullable = true);

    /// <summary>
    /// A one-dimensional array of this type: <c>int</c> becomes <c>int[]</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="MakeArray(int)"/> with a rank of 1. Use that overload for
    /// <c>int[,]</c>; call this twice for <c>int[][]</c>, which is a different type. For an array
    /// whose <em>elements</em> are nullable, use
    /// <see cref="ITypeDefinitionExtensions.MakeArrayOfNullable"/> rather than composing this with
    /// <see cref="MakeNullable"/> and hoping the order is right.
    /// </remarks>
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

    /// <summary>
    /// The type arguments this type is closed over - <c>[string]</c> for <c>List&lt;string&gt;</c>.
    /// Empty for a type that is not generic.
    /// </summary>
    /// <remarks>
    /// A nested generic type carries only the arguments it declares itself; its container's belong
    /// to <see cref="ContainingType"/>, which is closed over them.
    /// </remarks>
    IReadOnlyList<ITypeDefinition> TypeArguments { get; }
}

/// <summary>
/// The type-shape operations that are the same for every <see cref="ITypeDefinition"/>, so they are
/// written once rather than in each implementation.
/// </summary>
public static class ITypeDefinitionExtensions
{
    /// <summary>
    /// The type as C# spells it, using <see cref="TypeOutputMode.ShortName"/> - <c>List&lt;string&gt;</c>,
    /// <c>int?[]</c>, <c>void</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>This, not <see cref="object.ToString"/>, is the name.</strong> <c>ToString()</c>
    /// keeps its 1.x shape - <c>System.Void</c> for <c>void</c>, <c>.int</c> for <c>int</c>, and
    /// the same answer for <c>int</c> and <c>int[]</c> - because consumers assert on it. It is an
    /// identity string, not C#.
    /// </para>
    /// <example>
    /// <code>
    /// var array = TypeDefinition.Get(typeof(int[]));
    /// array.GetShortName();  // "int[]"
    /// array.ToString();      // ".int"     - the array is gone
    ///
    /// var nested = TypeDefinition.GetNested(TypeDefinition.Get("Sample", "Outer"), "Inner");
    /// nested.GetShortName(); // "Outer.Inner"
    /// nested.ToString();     // "Sample.Inner"  - the container is gone
    /// </code>
    /// </example>
    /// <para>
    /// This is for a caller that needs the name as a value - building a file name, or a string it
    /// is going to write with <see cref="BaseBlockDefinition.AddCode(string, object[])"/>'s
    /// <c>[argN]</c>. Anything going into generated code should be written as a type instead, so
    /// the file can qualify it and count its namespace; that is
    /// <see cref="ITypeDefinition.WriteTypeName"/>, which this wraps, and which
    /// <see cref="IOutputContext.Write(ITypeDefinition)"/> calls with the mode the file settled on.
    /// </para>
    /// </remarks>
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
    /// <example>
    /// Which one you get depends on the order, and nothing warns either way:
    /// <code>
    /// intType.MakeArrayOfNullable()               // int?[]   array of nullable ints
    /// intType.MakeNullable().MakeArray()          // int?[]   the same - annotate, then wrap
    /// intType.MakeArray().MakeNullable()          // int[]?   nullable array - wrap, then annotate
    ///
    /// intType.MakeArrayOfNullable(2)              // int?[,]
    /// intType.MakeArrayOfNullable().MakeArray()   // int?[][]
    /// intType.MakeArrayOfNullable().MakeNullable()// int?[]?  both annotated
    /// </code>
    /// </example>
    /// <para>
    /// It is a named call rather than a composition because the composition is silent about which
    /// of the two it produces, and the failure is a type that compiles as something else. Reading
    /// <c>MakeNullable().MakeArray()</c> takes knowing that an annotation attaches to whatever is
    /// outermost when it is asked for and stays there when an array is built around it - which is
    /// what <c>TypeDefinitionTests.ArrayRankTests.NullableGoesAfterTheShape</c> pins, and is
    /// version 1's behaviour reversed. This says it in the name.
    /// </para>
    /// <para>
    /// An implementation of <see cref="ITypeDefinition"/> from outside this assembly cannot be
    /// asked for the shape, so it is refused rather than silently given the other type.
    /// </para>
    /// </remarks>
    /// <param name="typeDefinition">The element type. Its own annotation, if it has one, is what
    /// this sets.</param>
    /// <param name="rank">The rank of the array built around it. 1 is <c>T?[]</c>, 2 is
    /// <c>T?[,]</c>.</param>
    /// <exception cref="NotSupportedException"><paramref name="typeDefinition"/> is an
    /// implementation that does not model element nullability, so the requested shape cannot be
    /// built. Refused rather than returned as the nullable array, which is a different
    /// type.</exception>
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
