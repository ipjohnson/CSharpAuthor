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
/// <para>
/// A record compares its collection members by reference, so <c>record Model(ImmutableArray&lt;X&gt; Items)</c>
/// reports two models with identical content as unequal. An incremental source generator caches on
/// exactly that comparison, so a reference-compared member silently defeats the cache on every edit
/// and every pipeline stage re-runs. Holding the member as an <see cref="EquatableArray{T}"/> instead
/// makes the record compare element by element, and the cache holds.
/// </para>
/// <para>
/// <strong>This is a correctness-neutral, performance-critical distinction, which is why it is easy
/// to miss.</strong> A generator whose cache never hits produces exactly the right output; it just
/// re-runs the whole pipeline on every keystroke, and shows up as the IDE getting slow rather than
/// as anything failing. Nothing reports it. The rule is simple: <em>every collection member of
/// every record that flows through an incremental pipeline is an
/// <see cref="EquatableArray{T}"/></em> - not <c>T[]</c>, not <c>List&lt;T&gt;</c>, not
/// <c>ImmutableArray&lt;T&gt;</c>, all three of which a record compares by reference.
/// </para>
/// <example>
/// <code>
/// record Model(EquatableArray&lt;string&gt; Items);
/// record BadModel(string[] Items);
///
/// new Model(new[] { "x", "y" })    == new Model(new[] { "x", "y" })    // true
/// new BadModel(new[] { "x", "y" }) == new BadModel(new[] { "x", "y" }) // false
/// </code>
/// The hash codes follow: two <c>Model</c> hash the same and two <c>BadModel</c> do not, which is
/// the half a generator's cache actually asks about first. Nothing about <c>BadModel</c> is
/// incorrect - it is a cache that never hits.
/// </example>
/// <para>
/// The default value and the empty array are the same value: both hold no elements, both compare
/// equal, both hash the same, and neither allocates. That is deliberate - a record field that was
/// never assigned and one assigned an empty collection are the same model, and should not defeat
/// the cache by disagreeing.
/// </para>
/// <para>
/// It is declared in <c>CSharpAuthor.Collections</c> rather than <c>CSharpAuthor</c> because this
/// library is source-compiled into its consumers, and a consumer that already source-includes an
/// <c>EquatableArray&lt;T&gt;</c> of its own would be one <c>using CSharpAuthor;</c> away from
/// CS0104.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly T[]? _items;

    /// <summary>
    /// An array with no elements. Equal to <c>default</c>.
    /// </summary>
    public static readonly EquatableArray<T> Empty = default;

    /// <summary>
    /// Wraps an existing array, without copying it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null and empty both become <see cref="Empty"/>, so <c>IsEmpty</c> is the one question to ask
    /// and there is no null case to handle separately.
    /// </para>
    /// <para>
    /// The array is taken as given rather than copied, so the immutability is a promise the caller
    /// keeps: mutating the array afterwards changes what this compares as, and a generator cache
    /// keyed on it will hold a stale answer. Hand it an array nothing else has a reference to.
    /// Use <see cref="From"/> for anything that is not already a <c>T[]</c>.
    /// </para>
    /// </remarks>
    public EquatableArray(T[]? items)
    {
        _items = items is { Length: > 0 } ? items : null;
    }

    /// <summary>
    /// Wraps <paramref name="items"/>, materialising it first if it is not already an array. Null and
    /// empty both produce <see cref="Empty"/>.
    /// </summary>
    /// <remarks>
    /// The one to reach for when building a model out of a LINQ query, which is what an incremental
    /// generator's transform stage almost always is - it takes the enumerable directly rather than
    /// making the caller write <c>.ToArray()</c> and then wrap the result. An argument that is
    /// already a <c>T[]</c> is wrapped without copying, so the caution on
    /// <see cref="EquatableArray{T}(T[])"/> applies to that case as well: do not hand it an array
    /// something else still holds and may mutate.
    /// </remarks>
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

    /// <summary>The number of elements. Zero for <see cref="Empty"/> and for <c>default</c>.</summary>
    public int Count => _items?.Length ?? 0;

    /// <summary>
    /// Whether there are no elements. True for <c>default</c> as well, because the two are the same
    /// value - so this is the only emptiness check needed, and there is no null case beside it.
    /// </summary>
    public bool IsEmpty => _items == null;

    /// <summary>The element at <paramref name="index"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the array.
    /// Every index is out of range on an empty one, including on <c>default</c>, which throws
    /// rather than dereferencing null.</exception>
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
    /// <remarks>
    /// The internal array, not a copy, so it is a way out of the immutability rather than a way to
    /// take a snapshot. Mutating what comes back changes what this value compares as. For reading,
    /// index it or <c>foreach</c> it instead - <see cref="GetEnumerator"/> allocates nothing.
    /// </remarks>
    public T[] ToArray()
    {
        return _items ?? Array.Empty<T>();
    }

    /// <summary>
    /// Element-by-element equality, using <see cref="EqualityComparer{T}.Default"/> for each.
    /// </summary>
    /// <remarks>
    /// This is the whole point of the type. It means the element type's own equality decides the
    /// answer, so a record holding an <see cref="EquatableArray{T}"/> of records compares all the
    /// way down - and one holding an array of a class that did not override <c>Equals</c> still
    /// compares by reference, one level in. An incremental generator's model types have to have
    /// value equality at every level for its cache to hold.
    /// </remarks>
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

    /// <inheritdoc cref="Equals(EquatableArray{T})" />
    /// <remarks>
    /// Anything that is not an <see cref="EquatableArray{T}"/> of the same element type is unequal,
    /// including a <c>T[]</c> with the same contents: boxing is not the implicit conversion, so it
    /// arrives here as an array and no comparison is attempted. Note that this only applies to a
    /// value already typed as <see cref="object"/> - <c>equatable.Equals(someArray)</c> binds to
    /// <see cref="Equals(EquatableArray{T})"/> through the implicit conversion instead, and is true
    /// when the contents match.
    /// </remarks>
    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    /// <summary>
    /// A hash of the elements, so two arrays that compare equal hash equally.
    /// </summary>
    /// <remarks>
    /// The half a generator cache asks about first: a hash that ignored the contents would send
    /// every model to the same bucket, and one derived from the array's identity would miss every
    /// time. <see cref="Empty"/> and <c>default</c> both hash to zero.
    /// </remarks>
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

    /// <summary>Element-by-element equality. Same as <see cref="Equals(EquatableArray{T})"/>.</summary>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>The negation of <see cref="op_Equality"/>.</summary>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Wraps an array implicitly, so a record's constructor call reads as if it took the array.
    /// </summary>
    /// <remarks>
    /// Implicit because the conversion loses nothing and the direction that matters is this one:
    /// there is no implicit conversion back to <c>T[]</c>, which would hand out the internal array
    /// and let a caller mutate a value that is supposed to be immutable. Use <see cref="ToArray"/>
    /// where an array is genuinely needed.
    /// </remarks>
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

    /// <inheritdoc />
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return (_items ?? Array.Empty<T>()).GetEnumerator();
    }

    /// <summary>
    /// The allocation-free enumerator <c>foreach</c> picks up. A struct, so iterating an
    /// <see cref="EquatableArray{T}"/> costs nothing on the heap.
    /// </summary>
    /// <remarks>
    /// Reached through the pattern rather than through <see cref="IEnumerable{T}"/>: assigning the
    /// array to an interface first boxes it and gives up the saving. Enumerating an empty array,
    /// <c>default</c> included, yields nothing rather than throwing.
    /// </remarks>
    public struct Enumerator
    {
        private readonly T[]? _items;
        private int _index;

        internal Enumerator(T[]? items)
        {
            _items = items;
            _index = -1;
        }

        /// <summary>Advances to the next element, or returns false at the end.</summary>
        public bool MoveNext()
        {
            var items = _items;

            if (items == null)
            {
                return false;
            }

            return ++_index < items.Length;
        }

        /// <summary>
        /// The element at the current position. Only valid after <see cref="MoveNext"/> has
        /// returned true.
        /// </summary>
        public T Current => _items![_index];
    }
}
