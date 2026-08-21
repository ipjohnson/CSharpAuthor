using System.Collections.Generic;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// What makes two <see cref="ITypeDefinition"/> values the same type, and - the half that matters
/// just as much - what keeps two that merely look alike apart.
/// </summary>
/// <remarks>
/// Equality used to require that the two values were instances of the same class. That is not a
/// property of a type, and the model has several classes for the same type, so it could only ever
/// answer for a pair that happened to be built the same way. The other half of that check was doing
/// real work, though: the comparison it guarded read the properties one at a time, and those
/// properties are shared by types that are not the same. This file pins both halves.
/// </remarks>
public class TypeIdentityTests
{
    private const string Ns = "Ns";

    private static ITypeDefinition Int => TypeDefinition.Get(typeof(int));

    private static ITypeDefinition Generic(string name, params ITypeDefinition[] arguments) =>
        new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, Ns, name, arguments);

    /// <summary>
    /// Every type this model can build, each one different from all the others. A single case would
    /// pass on a fix that made everything equal; this is the assertion that would not.
    /// </summary>
    private static IEnumerable<ITypeDefinition> DistinctTypes()
    {
        yield return Int;
        yield return Int.MakeNullable();
        yield return Int.MakeArray();
        yield return Int.MakeArray().MakeArray();
        yield return Int.MakeArray(2);
        yield return Int.MakeArray(2).MakeArray(1);
        yield return Int.MakeArray(1).MakeArray(2);
        yield return Int.MakeArray().MakeNullable();

        yield return TypeDefinition.Get(typeof(string));

        yield return TypeDefinition.Get(Ns, "List");
        yield return Generic("List", Int);
        yield return Generic("List", TypeDefinition.Get(typeof(string)));
        yield return Generic("List", Int, TypeDefinition.Get(typeof(string)));
        yield return Generic("List", Generic("List", Int));
        yield return Generic("Dictionary", Int);

        yield return TypeDefinition.Get(Ns, "Outer");
        yield return TypeDefinition.Get(Ns, "Inner");
        yield return TypeDefinition.GetNested(TypeDefinition.Get(Ns, "Outer"), "Inner");
        yield return TypeDefinition.GetNested(TypeDefinition.Get(Ns, "Other"), "Inner");
        yield return TypeDefinition.GetNested(TypeDefinition.Get(Ns, "Outer"), "Deepest");

        yield return new TypeParameterDefinition("T");
        yield return new TypeParameterDefinition("U");
        yield return new TypeParameterDefinition("T", isArray: true);
        yield return new TypeParameterDefinition("T", isNullable: true);

        yield return TypeDefinition.Get(Ns, "MarkerAttribute");
        yield return TypeDefinition.Get(Ns, "Marker");
        yield return new AttributeTypeReference(TypeDefinition.Get(Ns, "MarkerAttribute"));

        // Two types cannot share a fully qualified name, so a disagreement about the kind is a
        // disagreement about which type is meant rather than two descriptions of one. This is what
        // the comparison has always said, and the identity keeps saying it.
        yield return TypeDefinition.Get(TypeDefinitionEnum.InterfaceDefinition, Ns, "Marker");
        yield return TypeDefinition.Get(TypeDefinitionEnum.EnumDefinition, Ns, "Marker");
    }

    [Fact]
    public void EveryDistinctTypeDiffersFromEveryOther()
    {
        var types = new List<ITypeDefinition>(DistinctTypes());

        for (var i = 0; i < types.Count; i++)
        {
            for (var j = i + 1; j < types.Count; j++)
            {
                AssertType.Different(types[i], types[j]);
            }
        }
    }

    /// <summary>A value is the same type as an identically built one, and as itself.</summary>
    [Fact]
    public void EveryTypeIsTheSameAsAnIdenticallyBuiltOne()
    {
        var left = new List<ITypeDefinition>(DistinctTypes());
        var right = new List<ITypeDefinition>(DistinctTypes());

        for (var i = 0; i < left.Count; i++)
        {
            AssertType.Same(left[i], left[i]);
            AssertType.Same(left[i], right[i]);
        }
    }

    /// <summary>
    /// The pairs a rendering that dropped a detail would merge. Each is spelled out so a failure
    /// names the distinction that was lost rather than an index into a list.
    /// </summary>
    [Fact]
    public void ANullableIsNotItsUnderlyingType()
    {
        AssertType.Different(Int, Int.MakeNullable());
    }

    [Fact]
    public void ArrayShapesAreDifferentTypes()
    {
        AssertType.Different(Int.MakeArray(), Int.MakeArray().MakeArray());
        AssertType.Different(Int.MakeArray(2), Int.MakeArray().MakeArray());
        AssertType.Different(Int.MakeArray(2).MakeArray(1), Int.MakeArray(1).MakeArray(2));
        AssertType.Different(Int, Int.MakeArray());
    }

    [Fact]
    public void ANestedTypeIsQualifiedByItsContainer()
    {
        var inOuter = TypeDefinition.GetNested(TypeDefinition.Get(Ns, "Outer"), "Inner");
        var inOther = TypeDefinition.GetNested(TypeDefinition.Get(Ns, "Other"), "Inner");

        AssertType.Different(inOuter, inOther);
        AssertType.Different(inOuter, TypeDefinition.Get(Ns, "Inner"));
        AssertType.Same(inOuter, TypeDefinition.GetNested(TypeDefinition.Get(Ns, "Outer"), "Inner"));
    }

    [Fact]
    public void ClosedGenericsDifferByTheirArguments()
    {
        AssertType.Different(Generic("List", Int), Generic("List", TypeDefinition.Get(typeof(string))));
    }

    /// <summary>
    /// The pair the old comparison called equal one way round: it never looked at the arguments,
    /// because only a <c>GenericTypeDefinition</c> knew it had any.
    /// </summary>
    [Fact]
    public void AnOpenNameIsNotAClosedGeneric()
    {
        AssertType.Different(TypeDefinition.Get(Ns, "List"), Generic("List", Int));
    }

    /// <summary>
    /// A type parameter writes itself as its name in every mode, and so does a type built with that
    /// name and no namespace. They name the same thing in the declaration they appear in, so the
    /// model says so - which is what lets a <c>T</c> read off a symbol match a <c>T</c> a caller
    /// built.
    /// </summary>
    [Fact]
    public void ATypeParameterIsTheNameItWrites()
    {
        AssertType.Same(new TypeParameterDefinition("T"), TypeDefinition.Get("", "T"));
        AssertType.Different(new TypeParameterDefinition("T"), TypeDefinition.Get(Ns, "T"));
    }

    // ---- AttributeTypeReference: deliberately not the type it stands for ----

    /// <summary>
    /// An attribute reference writes <c>Marker</c> where the type it wraps writes
    /// <c>MarkerAttribute</c>. They are two different values because the name plan - a dictionary
    /// keyed on type definitions - has to be able to alias them separately, and because a real type
    /// called <c>Marker</c> can exist alongside <c>MarkerAttribute</c> in the same file.
    /// </summary>
    [Fact]
    public void AnAttributeReferenceIsNotTheTypeItStandsFor()
    {
        var attributeType = TypeDefinition.Get(Ns, "MarkerAttribute");

        AssertType.Different(new AttributeTypeReference(attributeType), attributeType);
    }

    /// <summary>
    /// The case the rendering alone cannot separate: both of these write <c>Ns.Marker</c>, and they
    /// are not the same type.
    /// </summary>
    [Fact]
    public void AnAttributeReferenceIsNotATypeOfTheNameItWrites()
    {
        var reference = new AttributeTypeReference(TypeDefinition.Get(Ns, "MarkerAttribute"));

        AssertType.Different(reference, TypeDefinition.Get(Ns, "Marker"));
    }

    /// <summary>Two references to one attribute type are the same value, which is the point of it.</summary>
    [Fact]
    public void TwoReferencesToTheSameAttributeAreTheSameValue()
    {
        AssertType.Same(
            new AttributeTypeReference(TypeDefinition.Get(Ns, "MarkerAttribute")),
            new AttributeTypeReference(TypeDefinition.Get(Ns, "MarkerAttribute")));

        AssertType.Different(
            new AttributeTypeReference(TypeDefinition.Get(Ns, "MarkerAttribute")),
            new AttributeTypeReference(TypeDefinition.Get("Other", "MarkerAttribute")));
    }

    // ---- what equality is for ----

    /// <summary>
    /// The reason any of this matters: a type built one way finds an entry keyed by a type built
    /// another way. Nothing in a generator calls <c>Equals</c> directly - it looks something up.
    /// </summary>
    [Fact]
    public void ATypeFindsAnEntryKeyedByAnEquivalentOne()
    {
        var registrations = new Dictionary<ITypeDefinition, string>
        {
            { Int.MakeNullable(), "int?" },
            { Int.MakeArray(2), "int[,]" },
            { TypeDefinition.GetNested(TypeDefinition.Get(Ns, "Outer"), "Inner"), "Ns.Outer.Inner" },
            { new TypeParameterDefinition("T"), "T" }
        };

        Assert.Equal("int?", registrations[TypeDefinition.Get(typeof(int)).MakeNullable()]);
        Assert.Equal("int[,]", registrations[TypeDefinition.Get(typeof(int[,]))]);
        Assert.Equal("Ns.Outer.Inner", registrations[TypeDefinition.Get(Ns, "Outer.Inner")]);
        Assert.Equal("T", registrations[TypeDefinition.Get("", "T")]);

        Assert.False(registrations.ContainsKey(Int));
        Assert.False(registrations.ContainsKey(Int.MakeArray()));
    }

    [Fact]
    public void ASetKeepsOneEntryPerType()
    {
        var set = new HashSet<ITypeDefinition>(DistinctTypes());

        var count = set.Count;

        foreach (var duplicate in DistinctTypes())
        {
            set.Add(duplicate);
        }

        Assert.Equal(count, set.Count);
    }
}
