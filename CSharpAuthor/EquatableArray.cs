using System;
using System.Collections;
using System.Collections.Generic;

namespace CSharpAuthor;

/// <summary>
/// A read-only sequence that compares by contents, for use in incremental source generator models.
/// </summary>
/// <remarks>
/// <para>
/// A record compares its collection members by <em>reference</em>: two
/// <c>record Model(ImmutableArray&lt;Item&gt; Items)</c> holding equal items are not equal. An
/// incremental generator caches on model equality, so a model carrying an ordinary collection
/// misses its cache on every keystroke and re-runs the whole pipeline - silently, since the output
/// is still correct, only slow.
/// </para>
/// <para>
/// Swapping the collection type for this one is the whole fix; nothing else about the model
/// changes. Every generator written against this library needs it, which is why it ships here
/// rather than being written again each time.
/// </para>
/// <para>
/// Unconstrained, so that it holds the type this library is mostly used to carry:
/// <see cref="ITypeDefinition"/> compares by value but implements
/// <see cref="IComparable{T}"/> rather than <see cref="IEquatable{T}"/>, and a
/// <c>where T : IEquatable&lt;T&gt;</c> constraint would have shut it out.
/// Elements compare through <see cref="EqualityComparer{T}.Default"/>, which uses whatever
/// equality the element actually defines.
/// </para>
/// </remarks>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly T[]? _items;

    public EquatableArray(params T[]? items)
    {
        _items = items;
    }

    public EquatableArray(IEnumerable<T>? items)
    {
        _items = items switch
        {
            null => null,
            T[] array => array,
            _ => new List<T>(items).ToArray()
        };
    }

    public static EquatableArray<T> Empty => new(Array.Empty<T>());

    public int Count => _items?.Length ?? 0;

    public T this[int index] => (_items ?? throw new IndexOutOfRangeException())[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (ReferenceEquals(_items, other._items))
        {
            return true;
        }

        if (_items == null || other._items == null || _items.Length != other._items.Length)
        {
            return false;
        }

        for (var i = 0; i < _items.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(_items[i], other._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (_items == null)
        {
            return 0;
        }

        unchecked
        {
            var hash = 17;

            foreach (var item in _items)
            {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public T[] ToArray()
    {
        if (_items == null)
        {
            return Array.Empty<T>();
        }

        var copy = new T[_items.Length];

        Array.Copy(_items, copy, _items.Length);

        return copy;
    }
}

public static class EquatableArray
{
    public static EquatableArray<T> Create<T>(params T[] items) => new(items);

    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T>? items) => new(items);
}
