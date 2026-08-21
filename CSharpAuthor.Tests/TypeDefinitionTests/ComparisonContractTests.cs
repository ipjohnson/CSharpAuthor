using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// <see cref="IComparable{T}"/>'s contract, across every implementation of
/// <see cref="ITypeDefinition"/> in the library.
/// </summary>
/// <remarks>
/// Each implementation used to answer for itself and they disagreed, so <c>a.CompareTo(b)</c> and
/// <c>b.CompareTo(a)</c> could both be negative and a pair could be "equal" to the comparator and
/// unequal to <c>Equals</c>. That is not a sorting inconvenience: <c>List.Sort</c> is entitled to
/// throw "IComparer.Compare() method returns inconsistent results", and which pairs it compares
/// depends on the order the elements arrived in - so the throw is a function of the data.
/// </remarks>
public class ComparisonContractTests
{
    private static IEnumerable<ITypeDefinition> Everything()
    {
        yield return TypeDefinition.Get(typeof(int));
        yield return TypeDefinition.Get(typeof(string));
        yield return TypeDefinition.Get("Ns", "List");
        yield return TypeDefinition.Get("Ns", "List").MakeArray();
        yield return TypeDefinition.Get("Ns", "List").MakeNullable();
        yield return TypeDefinition.Get("Ns", "List").MakeArrayOfNullable();
        yield return new TypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "Inner", null, false,
            TypeDefinition.Get("Ns", "Outer"));
        yield return new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "List",
            new[] { TypeDefinition.Get(typeof(int)) });
        yield return new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "List",
            new[] { TypeDefinition.Get(typeof(string)) });
        yield return new TypeParameterDefinition("T");
        yield return new TypeParameterDefinition("U");
        yield return new AttributeTypeReference(TypeDefinition.Get("Ns", "MyAttribute"));
        yield return new AttributeTypeReference(TypeDefinition.Get("Ns", "OtherAttribute"));
    }

    [Fact]
    public void ComparisonIsAntisymmetric()
    {
        foreach (var left in Everything())
        {
            foreach (var right in Everything())
            {
                Assert.Equal(
                    Math.Sign(left.CompareTo(right)),
                    -Math.Sign(right.CompareTo(left)));
            }
        }
    }

    [Fact]
    public void ComparisonAgreesWithEquality()
    {
        foreach (var left in Everything())
        {
            foreach (var right in Everything())
            {
                Assert.Equal(left.CompareTo(right) == 0, left.Equals(right));
            }
        }
    }

    [Fact]
    public void ComparisonIsTransitive()
    {
        var all = Everything().ToList();

        foreach (var a in all)
        {
            foreach (var b in all)
            {
                foreach (var c in all)
                {
                    if (a.CompareTo(b) <= 0 && b.CompareTo(c) <= 0)
                    {
                        Assert.True(a.CompareTo(c) <= 0);
                    }
                }
            }
        }
    }

    /// <summary>
    /// The same set sorted from two different starting orders lands in the same place. Without a
    /// total order it does not, and the framework may refuse to sort at all.
    /// </summary>
    [Fact]
    public void SortingIsStableWhicheverWayTheListArrives()
    {
        var forwards = Everything().ToList();
        var backwards = Everything().Reverse().ToList();

        forwards.Sort();
        backwards.Sort();

        Assert.Equal(
            forwards.Select(t => t.GetShortName()),
            backwards.Select(t => t.GetShortName()));
    }

    /// <summary>
    /// A null on the right is always smaller, which is what <see cref="IComparable{T}"/> requires
    /// and what a sort with a hole in it relies on.
    /// </summary>
    [Fact]
    public void NullSortsFirst()
    {
        foreach (var type in Everything())
        {
            Assert.True(type.CompareTo(null!) > 0);
        }
    }
}
