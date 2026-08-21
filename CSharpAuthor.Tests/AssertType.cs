using System;
using System.Text;
using Xunit;

namespace CSharpAuthor.Tests;

/// <summary>
/// The whole identity contract in two assertions, so a test says which of the two it means and gets
/// every part of it checked.
/// </summary>
/// <remarks>
/// "Equal" is four separate promises - <c>Equals</c> both ways round, <c>CompareTo</c> zero both
/// ways round, and matching hash codes - and a fix that keeps one of them while dropping another is
/// exactly the failure mode being guarded against. Checking them one at a time in each test is how
/// three of the four end up unchecked.
/// </remarks>
internal static class AssertType
{
    /// <summary>The two values denote the same type, whichever class each of them is.</summary>
    public static void Same(ITypeDefinition left, ITypeDefinition right)
    {
        Assert.True(left.Equals(right), $"{Describe(left)} should equal {Describe(right)}");
        Assert.True(right.Equals(left), $"{Describe(right)} should equal {Describe(left)}");

        Assert.True(left.CompareTo(right) == 0, $"{Describe(left)}.CompareTo({Describe(right)}) should be 0");
        Assert.True(right.CompareTo(left) == 0, $"{Describe(right)}.CompareTo({Describe(left)}) should be 0");

        Assert.True(
            left.GetHashCode() == right.GetHashCode(),
            $"{Describe(left)} and {Describe(right)} are equal and must hash alike");
    }

    /// <summary>
    /// The two values denote different types. Hash codes are not asserted: two different types are
    /// allowed to collide, and only equal ones are obliged to agree.
    /// </summary>
    public static void Different(ITypeDefinition left, ITypeDefinition right)
    {
        Assert.False(left.Equals(right), $"{Describe(left)} should not equal {Describe(right)}");
        Assert.False(right.Equals(left), $"{Describe(right)} should not equal {Describe(left)}");

        Assert.True(left.CompareTo(right) != 0, $"{Describe(left)}.CompareTo({Describe(right)}) should not be 0");
        Assert.True(right.CompareTo(left) != 0, $"{Describe(right)}.CompareTo({Describe(left)}) should not be 0");

        // A comparison that disagrees with itself about which of two values is larger is not an
        // ordering, and a sort over it is undefined rather than merely surprising.
        Assert.Equal(Math.Sign(left.CompareTo(right)), -Math.Sign(right.CompareTo(left)));
    }

    private static string Describe(ITypeDefinition type)
    {
        var builder = new StringBuilder();

        type.WriteTypeName(builder, TypeOutputMode.FullName);

        return $"{type.GetType().Name}(\"{builder}\")";
    }
}
