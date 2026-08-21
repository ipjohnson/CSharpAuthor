using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Deliberately not the bare CSharpAuthor namespace: this library is source-compiled into its
// consumers, and Hardened.Framework's generators already source-include an EquatableArray<T> of
// their own. Sharing a name in a shared namespace would be one `using CSharpAuthor;` away from
// CS0104 in a repo that includes both.
namespace CSharpAuthor.Collections;

/// <summary>
/// An immutable array with value equality.
/// </summary>
/// <remarks>
/// A record compares its collection members by reference, so <c>record Model(ImmutableArray&lt;X&gt; Items)</c>
/// reports two models with identical content as unequal. An incremental source generator caches on
/// exactly that comparison, so a reference-compared member silently defeats the cache on every edit
/// and every pipeline stage re-runs. Holding the member as an <see cref="EquatableArray{T}"/> instead
/// makes the record compare element by element, and the cache holds.
/// <para>
/// The default value and the empty array are the same value: both hold no elements, both compare
/// equal, both hash the same, and neither allocates.
/// </para>
/// </remarks>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly T[]? _items;

    /// <summary>
    /// An array with no elements. Equal to <c>default</c>.
    /// </summary>
    public static readonly EquatableArray<T> Empty = default;

    public EquatableArray(T[]? items)
    {
        _items = items is { Length: > 0 } ? items : null;
    }

    /// <summary>
    /// Wraps <paramref name="items"/>, materialising it first if it is not already an array. Null and
    /// empty both produce <see cref="Empty"/>.
    /// </summary>
    public static EquatableArray<T> From(IEnumerable<T>? items)
    {
        if (items == null)
        {
            return Empty;
        }

        if (items is T[] array)
        {
            return new EquatableArray<T>(array);
        }

        return new EquatableArray<T>(items.ToArray());
    }

    public int Count => _items?.Length ?? 0;

    public bool IsEmpty => _items == null;

    public T this[int index]
    {
        get
        {
            var items = _items;

            if (items == null || index < 0 || index >= items.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return items[index];
        }
    }

    /// <summary>
    /// The elements as an array. Never null; empty is <see cref="Array.Empty{T}"/>, which does not allocate.
    /// </summary>
    public T[] ToArray()
    {
        return _items ?? Array.Empty<T>();
    }

    public bool Equals(EquatableArray<T> other)
    {
        var left = _items;
        var right = other._items;

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;

        for (var i = 0; i < left.Length; i++)
        {
            if (!comparer.Equals(left[i], right[i]))
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
        var items = _items;

        if (items == null)
        {
            return 0;
        }

        unchecked
        {
            var comparer = EqualityComparer<T>.Default;
            var hash = 17;

            foreach (var item in items)
            {
                hash = (hash * 31) + (item is null ? 0 : comparer.GetHashCode(item));
            }

            return hash;
        }
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }

    public static implicit operator EquatableArray<T>(T[]? items)
    {
        return new EquatableArray<T>(items);
    }

    /// <summary>
    /// A struct enumerator, so <c>foreach</c> over an <see cref="EquatableArray{T}"/> allocates nothing.
    /// </summary>
    public Enumerator GetEnumerator()
    {
        return new Enumerator(_items);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return (_items ?? Array.Empty<T>()).GetEnumerator();
    }

    public struct Enumerator
    {
        private readonly T[]? _items;
        private int _index;

        internal Enumerator(T[]? items)
        {
            _items = items;
            _index = -1;
        }

        public bool MoveNext()
        {
            var items = _items;

            if (items == null)
            {
                return false;
            }

            return ++_index < items.Length;
        }

        public T Current => _items![_index];
    }
}
