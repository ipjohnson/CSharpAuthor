using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CSharpAuthor.Tests;

public class EquatableArrayTests
{
    private record Model(EquatableArray<string> Names);

    private record ReferenceModel(IReadOnlyList<string> Names);

    /// <summary>
    /// The reason this type exists: a record holding an ordinary collection compares it by
    /// reference, so an incremental generator's cache misses on every edit.
    /// </summary>
    [Fact]
    public void RecordHoldingOneComparesByContents()
    {
        var first = new Model(EquatableArray.Create("a", "b"));
        var second = new Model(EquatableArray.Create("a", "b"));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());

        // The behaviour being replaced.
        Assert.NotEqual(
            new ReferenceModel(new List<string> { "a", "b" }),
            new ReferenceModel(new List<string> { "a", "b" }));
    }

    [Fact]
    public void DifferentContentsAreNotEqual()
    {
        Assert.NotEqual(EquatableArray.Create("a", "b"), EquatableArray.Create("a", "c"));
        Assert.NotEqual(EquatableArray.Create("a"), EquatableArray.Create("a", "b"));
    }

    [Fact]
    public void OrderMatters()
    {
        Assert.NotEqual(EquatableArray.Create("a", "b"), EquatableArray.Create("b", "a"));
    }

    [Fact]
    public void DefaultAndEmptyBehave()
    {
        var uninitialised = default(EquatableArray<string>);

        Assert.Empty(uninitialised);
        Assert.Equal(0, uninitialised.Count);
        Assert.Equal(uninitialised, new EquatableArray<string>((string[]?)null));
        Assert.Equal(EquatableArray<string>.Empty, EquatableArray.Create<string>());
    }

    /// <summary>
    /// The type this library is mostly used to carry. It compares by value but implements
    /// <c>IComparable</c> rather than <c>IEquatable</c>, which is why there is no constraint.
    /// </summary>
    [Fact]
    public void HoldsTypeDefinitions()
    {
        var first = EquatableArray.Create<ITypeDefinition>(
            TypeDefinition.Get("Sample", "Widget"),
            TypeDefinition.Get(typeof(int)).MakeArray());

        var second = EquatableArray.Create<ITypeDefinition>(
            TypeDefinition.Get("Sample", "Widget"),
            TypeDefinition.Get(typeof(int)).MakeArray());

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());

        Assert.NotEqual(
            first,
            EquatableArray.Create<ITypeDefinition>(
                TypeDefinition.Get("Sample", "Widget"),
                TypeDefinition.Get(typeof(int)).MakeArray(2)));
    }

    [Fact]
    public void EnumeratesAndIndexes()
    {
        var array = EquatableArray.Create("a", "b", "c");

        Assert.Equal(3, array.Count);
        Assert.Equal("b", array[1]);
        Assert.Equal(new[] { "a", "b", "c" }, array.ToArray());
        Assert.Equal("abc", string.Concat(array.ToList()));
    }

    [Fact]
    public void BuildsFromAnySequence()
    {
        Assert.Equal(
            EquatableArray.Create("a", "b"),
            Enumerable.Range(0, 2).Select(i => i == 0 ? "a" : "b").ToEquatableArray());
    }

    [Fact]
    public void OperatorsMatchEquals()
    {
        Assert.True(EquatableArray.Create("a") == EquatableArray.Create("a"));
        Assert.True(EquatableArray.Create("a") != EquatableArray.Create("b"));
    }
}
