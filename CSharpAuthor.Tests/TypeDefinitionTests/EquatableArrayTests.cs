using System;
using System.Collections.Generic;
using System.Linq;
using CSharpAuthor.Collections;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

public class EquatableArrayTests
{
    /// <summary>
    /// The defect this type exists to remove: a record compares a collection member by reference, so
    /// two models built from identical content report unequal, and an incremental generator that
    /// caches on that comparison misses its cache on every edit.
    /// </summary>
    private record ReferenceComparedModel(string Name, IReadOnlyList<string> Items);

    private record ValueComparedModel(string Name, EquatableArray<string> Items);

    [Fact]
    public void RecordWithAPlainCollectionMemberComparesByReference()
    {
        var left = new ReferenceComparedModel("m", new[] { "a", "b" });
        var right = new ReferenceComparedModel("m", new[] { "a", "b" });

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void RecordWithAnEquatableArrayMemberComparesByValue()
    {
        var left = new ValueComparedModel("m", new EquatableArray<string>(new[] { "a", "b" }));
        var right = new ValueComparedModel("m", new EquatableArray<string>(new[] { "a", "b" }));

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void EqualElementwise()
    {
        Assert.True(new EquatableArray<int>(new[] { 1, 2, 3 }).Equals(new EquatableArray<int>(new[] { 1, 2, 3 })));
        Assert.True(new EquatableArray<int>(new[] { 1, 2, 3 }) == new EquatableArray<int>(new[] { 1, 2, 3 }));

        Assert.False(new EquatableArray<int>(new[] { 1, 2, 3 }).Equals(new EquatableArray<int>(new[] { 1, 2, 4 })));
        Assert.True(new EquatableArray<int>(new[] { 1, 2, 3 }) != new EquatableArray<int>(new[] { 1, 2 }));
    }

    [Fact]
    public void EqualsIsSymmetricAcrossLengths()
    {
        var shorter = new EquatableArray<string>(new[] { "a" });
        var longer = new EquatableArray<string>(new[] { "a", "b" });

        Assert.False(shorter.Equals(longer));
        Assert.False(longer.Equals(shorter));
    }

    [Fact]
    public void NullElementsCompareAndHash()
    {
        var left = new EquatableArray<string?>(new string?[] { "a", null });
        var right = new EquatableArray<string?>(new string?[] { "a", null });

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, new EquatableArray<string?>(new string?[] { "a", "b" }));
    }

    /// <summary>
    /// A default value and an empty array are the same value - a struct member left unset must not
    /// compare differently from one explicitly given nothing.
    /// </summary>
    [Fact]
    public void DefaultAndEmptyAreTheSameValue()
    {
        var fromDefault = default(EquatableArray<int>);
        var fromEmptyArray = new EquatableArray<int>(Array.Empty<int>());
        var fromNull = new EquatableArray<int>(null);

        Assert.Equal(fromDefault, EquatableArray<int>.Empty);
        Assert.Equal(fromDefault, fromEmptyArray);
        Assert.Equal(fromDefault, fromNull);
        Assert.Equal(fromDefault.GetHashCode(), fromEmptyArray.GetHashCode());
        Assert.Equal(fromDefault.GetHashCode(), fromNull.GetHashCode());

        var count = fromDefault.Count;

        Assert.Equal(0, count);
        Assert.True(fromDefault.IsEmpty);
        Assert.Empty(fromDefault.ToArray());
        Assert.Same(Array.Empty<int>(), fromDefault.ToArray());
    }

    [Fact]
    public void OrderIsPartOfTheValue()
    {
        Assert.NotEqual(
            new EquatableArray<int>(new[] { 1, 2 }),
            new EquatableArray<int>(new[] { 2, 1 }));
    }

    [Fact]
    public void ReadsAsAReadOnlyList()
    {
        IReadOnlyList<string> list = new EquatableArray<string>(new[] { "a", "b", "c" });

        Assert.Equal(3, list.Count);
        Assert.Equal("b", list[1]);
        Assert.Equal(new[] { "a", "b", "c" }, list.ToArray());
    }

    [Fact]
    public void EnumeratesWithoutBoxing()
    {
        var values = new EquatableArray<int>(new[] { 1, 2, 3 });
        var total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        Assert.Equal(6, total);

        var empty = 0;

        foreach (var value in default(EquatableArray<int>))
        {
            empty += value;
        }

        Assert.Equal(0, empty);
    }

    [Fact]
    public void IndexOutOfRangeThrows()
    {
        var values = new EquatableArray<int>(new[] { 1 });

        Assert.Throws<ArgumentOutOfRangeException>(() => values[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => values[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => default(EquatableArray<int>)[0]);
    }

    [Fact]
    public void BuildsFromAnySequence()
    {
        Assert.Equal(
            new EquatableArray<int>(new[] { 1, 2, 3 }),
            EquatableArray<int>.From(Enumerable.Range(1, 3)));

        Assert.Equal(EquatableArray<int>.Empty, EquatableArray<int>.From(null));
        Assert.Equal(EquatableArray<int>.Empty, EquatableArray<int>.From(Enumerable.Empty<int>()));
    }

    [Fact]
    public void ImplicitlyConvertsFromAnArray()
    {
        EquatableArray<int> values = new[] { 1, 2 };

        Assert.Equal(new EquatableArray<int>(new[] { 1, 2 }), values);
    }

    /// <summary>
    /// The type model's own values are held in models a generator caches on, so an array of them has
    /// to compare the same way.
    /// </summary>
    [Fact]
    public void HoldsTypeDefinitions()
    {
        var left = new EquatableArray<ITypeDefinition>(
            new ITypeDefinition[] { TypeDefinition.Get("Ns", "A"), TypeDefinition.Get("Ns", "B") });

        var right = new EquatableArray<ITypeDefinition>(
            new ITypeDefinition[] { TypeDefinition.Get("Ns", "A"), TypeDefinition.Get("Ns", "B") });

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());

        Assert.NotEqual(
            left,
            new EquatableArray<ITypeDefinition>(
                new ITypeDefinition[] { TypeDefinition.Get("Ns", "A"), TypeDefinition.Get("Ns", "C") }));
    }
}
