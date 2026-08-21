using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// <see cref="ITypeDefinition"/> as a value: comparison, equality and hashing.
/// </summary>
/// <remarks>
/// <para>
/// This is not an emission question and nothing here appears in any output, which is why it is easy
/// to miss - and it is load-bearing. §1 calls the type model the product, and §7 asks for
/// <c>EquatableArray&lt;T&gt;</c> so that a generator's model caches correctly across incremental
/// runs. A model caches on the equality of the things inside it. If <see cref="ITypeDefinition"/>
/// does not have a coherent one, <c>EquatableArray&lt;T&gt;</c> is built on sand.
/// </para>
/// <para>
/// <see cref="IComparable{T}"/> requires that if <c>a.CompareTo(b)</c> is zero then
/// <c>b.CompareTo(a)</c> is zero too. It is not, and a sort is only defined for a comparator that
/// keeps that promise.
/// </para>
/// </remarks>
public class TypeModelContractTests
{
    private static ITypeDefinition Plain() => TypeDefinition.Get("Ns", "List");

    private static ITypeDefinition Generic() =>
        new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "List",
            new[] { TypeDefinition.Get(typeof(int)) });

    /// <summary>
    /// <c>TypeDefinition.CompareTo</c> stops at the base comparison, which knows nothing about type
    /// arguments, so a bare <c>List</c> reports itself equal to <c>List&lt;int&gt;</c>. The generic
    /// side does check, and reports them different. The two answers disagree.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: CompareTo is asymmetric - TypeDefinition('Ns','List').CompareTo(List<int>) is 0 while the reverse is -1, so IComparable's contract is broken and any sort over a mixed list is undefined")]
    public void CompareToIsSymmetric()
    {
        var plain = Plain();
        var generic = Generic();

        Assert.Equal(
            Math.Sign(plain.CompareTo(generic)),
            -Math.Sign(generic.CompareTo(plain)));
    }

    /// <summary>
    /// The same disagreement stated the other way: <c>CompareTo</c> says equal, <c>Equals</c> says
    /// not. Whichever is right, a type cannot be both.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: CompareTo returns 0 for a pair that Equals reports as unequal, so the ordering and the equality disagree about the same two values")]
    public void CompareToAgreesWithEquals()
    {
        var plain = Plain();
        var generic = Generic();

        Assert.Equal(plain.CompareTo(generic) == 0, plain.Equals(generic));
    }

    /// <summary>
    /// A sort over the four kinds the model has. It does not throw here, but the result is not
    /// ordered by anything - and with a different element order the framework is entitled to throw
    /// "IComparer.Compare() method returns inconsistent results" instead.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: sorting a mixed list of ITypeDefinition produces an arbitrary order (T, Ns.List<.int>, Ns.List, .int) because the comparator is not a total order")]
    public void SortingProducesAStableOrder()
    {
        var types = new List<ITypeDefinition>
        {
            Generic(), Plain(), new TypeParameterDefinition("T"), TypeDefinition.Get(typeof(int))
        };

        var forwards = types.ToList();
        var backwards = Enumerable.Reverse(types).ToList();

        forwards.Sort();
        backwards.Sort();

        Assert.Equal(
            forwards.Select(t => t.ToString()),
            backwards.Select(t => t.ToString()));
    }

    /// <summary>
    /// <c>TypeDefinition.ToString</c> is <c>"{Namespace}.{Name}"</c> and the hash is the hash of
    /// that, so it takes no account of <c>IsArray</c> or <c>IsNullable</c> - <c>int</c> and
    /// <c>int[]</c> hash identically. Legal, and a guaranteed collision for every array in every
    /// dictionary the model is used as a key in.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: GetHashCode is built from ToString(), which omits IsArray and IsNullable, so int and int[] always collide - unequal values with the same hash, by construction rather than by chance")]
    public void ArrayAndElementTypesHashDifferently()
    {
        var element = TypeDefinition.Get(typeof(int));
        var array = TypeDefinition.Get(typeof(int)).MakeArray();

        Assert.False(element.Equals(array));
        Assert.NotEqual(element.GetHashCode(), array.GetHashCode());
    }

    /// <summary>
    /// <c>ToString</c> on a keyword type writes a leading dot, because the namespace is empty and
    /// the format joins unconditionally. It is only a debugging string - except that it is also
    /// what <c>GetHashCode</c> is computed from.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: TypeDefinition.ToString() joins namespace and name unconditionally, so a keyword type renders as '.int' - and that string is what GetHashCode hashes")]
    public void ToStringOfAKeywordTypeHasNoLeadingDot()
    {
        Assert.Equal("int", TypeDefinition.Get(typeof(int)).ToString());
    }

    [Fact(Skip = "ADVERSARY GAP: no API - EquatableArray<T> does not exist (§7 requires it). Records compare collection members by reference, so a generator model holding an IReadOnlyList never matches its cached self and the incremental pipeline re-runs on every keystroke.")]
    public void EquatableArrayExists()
    {
        var type = typeof(TypeDefinition).Assembly
            .GetTypes()
            .FirstOrDefault(t => t.Name.StartsWith("EquatableArray"));

        Assert.NotNull(type);
    }

    // ---- equality behaviour that is correct, kept as guards ----

    [Fact]
    public void EqualTypesAreEqualAndHashAlike()
    {
        var a = TypeDefinition.Get("Ns", "Thing");
        var b = TypeDefinition.Get("Ns", "Thing");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void DifferentNamespacesAreNotEqual()
    {
        Assert.NotEqual(TypeDefinition.Get("Ns1", "Thing"), TypeDefinition.Get("Ns2", "Thing"));
    }

    [Fact]
    public void GenericTypesWithDifferentArgumentsAreNotEqual()
    {
        var ofInt = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "List",
            new[] { TypeDefinition.Get(typeof(int)) });

        var ofString = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "List",
            new[] { TypeDefinition.Get(typeof(string)) });

        Assert.NotEqual(ofInt, ofString);
    }

    [Fact]
    public void TypeParametersCompareByName()
    {
        Assert.Equal(new TypeParameterDefinition("T"), new TypeParameterDefinition("T"));
        Assert.NotEqual(new TypeParameterDefinition("T"), new TypeParameterDefinition("U"));
    }

    [Fact]
    public void ArrayAndNullableAreNotEqualToTheirElement()
    {
        var element = TypeDefinition.Get(typeof(int));

        Assert.NotEqual(element, element.MakeArray());
        Assert.NotEqual(element, element.MakeNullable());
    }

    /// <summary>
    /// A <see cref="HashSet{T}"/> keeps the two apart despite the shared hash, because it falls back
    /// to <c>Equals</c>. Guard: a hashing fix must not break this.
    /// </summary>
    [Fact]
    public void ArrayAndElementAreDistinctInAHashSet()
    {
        var set = new HashSet<ITypeDefinition>
        {
            TypeDefinition.Get(typeof(int)),
            TypeDefinition.Get(typeof(int)).MakeArray()
        };

        Assert.Equal(2, set.Count);
    }
}
