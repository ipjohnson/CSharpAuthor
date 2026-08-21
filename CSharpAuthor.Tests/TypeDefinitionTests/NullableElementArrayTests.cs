using System;
using System.Collections.Generic;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// <c>string?[]</c> - an array whose elements are nullable - as opposed to <c>string[]?</c>, the
/// nullable array.
/// </summary>
/// <remarks>
/// <para>
/// They are different types and both compile, so a caller who meant one and was handed the other
/// gets no diagnostic at generation and none at consumption either. Before
/// <c>MakeArrayOfNullable</c> only the second could be written at all: the <c>?</c> and the
/// <c>[]</c> came out in a fixed order whatever the caller asked for.
/// </para>
/// <para>
/// What <c>MakeNullable().MakeArray()</c> means is deliberately not changed here.
/// <see cref="ArrayRankTests.NullableGoesAfterTheShape"/> pins it as the nullable array, and that
/// test is not this agent's to edit - the question is recorded in docs/v2-open-questions.md.
/// </para>
/// </remarks>
public class NullableElementArrayTests
{
    [Fact]
    public void TheQuestionMarkGoesBeforeTheBrackets()
    {
        Assert.Equal(
            "string?[]",
            TypeDefinition.Get(typeof(string)).MakeArrayOfNullable().GetShortName());
    }

    [Fact]
    public void TheNullableArrayIsStillReachable()
    {
        Assert.Equal(
            "string[]?",
            TypeDefinition.Get(typeof(string)).MakeArray().MakeNullable().GetShortName());
    }

    [Fact]
    public void ARankIsCarriedThrough()
    {
        Assert.Equal(
            "string?[,]",
            TypeDefinition.Get(typeof(string)).MakeArrayOfNullable(2).GetShortName());
    }

    [Fact]
    public void ItStacksWithOrdinaryArrays()
    {
        Assert.Equal(
            "string?[][]",
            TypeDefinition.Get(typeof(string)).MakeArrayOfNullable().MakeArray().GetShortName());
    }

    [Fact]
    public void AGenericTypeKeepsItsArguments()
    {
        var list = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "System.Collections.Generic",
            "List",
            new[] { TypeDefinition.Get(typeof(int)) });

        Assert.Equal("List<int>?[]", list.MakeArrayOfNullable().GetShortName());
    }

    [Fact]
    public void ATypeParameterTakesTheSameShape()
    {
        Assert.Equal(
            "T?[]", new TypeParameterDefinition("T").MakeArrayOfNullable().GetShortName());
    }

    [Fact]
    public void ANestedTypeKeepsItsContainer()
    {
        var outer = TypeDefinition.Get("Ns", "Outer");

        var inner = new TypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "Inner", null, false, outer);

        Assert.Equal("Outer.Inner?[]", inner.MakeArrayOfNullable().GetShortName());
    }

    /// <summary>
    /// The two shapes are different values: they are not equal, they do not hash the same, and they
    /// have an order. A model that caches on one of them must not find the other.
    /// </summary>
    [Fact]
    public void TheTwoShapesAreDifferentValues()
    {
        var elementNullable = TypeDefinition.Get(typeof(string)).MakeArrayOfNullable();
        var nullableArray = TypeDefinition.Get(typeof(string)).MakeArray().MakeNullable();

        Assert.NotEqual(elementNullable, nullableArray);
        Assert.NotEqual(elementNullable.GetHashCode(), nullableArray.GetHashCode());
        Assert.Equal(
            Math.Sign(elementNullable.CompareTo(nullableArray)),
            -Math.Sign(nullableArray.CompareTo(elementNullable)));
    }

    /// <summary>
    /// An implementation that cannot model the shape is refused rather than handed the other type.
    /// </summary>
    [Fact]
    public void AnUnknownImplementationIsRefused()
    {
        Assert.Throws<NotSupportedException>(() => new ForeignType().MakeArrayOfNullable());
    }

    private sealed class ForeignType : ITypeDefinition
    {
        public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;

        public bool IsNullable => false;

        public bool IsArray => false;

        public IReadOnlyList<int> ArrayRanks => Array.Empty<int>();

        // A single element-level annotation, and MakeNullable below ignores it - which is
        // exactly the implementation this test proves is refused.
        public IReadOnlyList<bool> NullableAnnotations => new[] { false };

        public string Name => "Foreign";

        public string Namespace => "Ns";

        public ITypeDefinition? ContainingType => null;

        public IEnumerable<string> KnownNamespaces => new[] { "Ns" };

        public IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();

        public void WriteTypeName(
            System.Text.StringBuilder builder,
            TypeOutputMode typeOutputMode = TypeOutputMode.ShortName) => builder.Append(Name);

        public ITypeDefinition MakeNullable(bool nullable = true) => this;

        public ITypeDefinition MakeArray() => this;

        public ITypeDefinition MakeArray(int rank) => this;

        public int CompareTo(ITypeDefinition other) => 0;
    }
}
