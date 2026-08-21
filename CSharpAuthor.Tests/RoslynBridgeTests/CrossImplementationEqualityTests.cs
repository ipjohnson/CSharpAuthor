using System.Collections.Generic;
using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// A type read off a symbol and the same type built by hand are the same value.
/// </summary>
/// <remarks>
/// <para>
/// This is the comparison a generator lives on. It reads a parameter through the bridge, and looks
/// it up against a registration a caller wrote out with <c>TypeDefinition.Get</c>; if the two do not
/// match, nothing resolves and nothing says why. The bridge implements <see cref="ITypeDefinition"/>
/// several times over for shapes the core model cannot hold - a nullable value type, a ranked array,
/// a nested type whose containers carry arguments - so every one of those lookups crosses an
/// implementation boundary.
/// </para>
/// <para>
/// The negatives are the other half and are not filler. Equality that matched everything would pass
/// every test above them and would be worse than equality that matched nothing, because a generator
/// would resolve the wrong registration instead of failing to find one.
/// </para>
/// </remarks>
public class CrossImplementationEqualityTests
{
    private const string BridgeNamespace = "BridgeTestNamespace";

    private const string Fields = @"
        public int? nullableInt;
        public int plainInt;
        public string? nullableString;
        public Color? nullableEnum;
        public KeyValuePair<string, int>? nullableGeneric;
        public List<int> listOfInt;
        public int[] oneDimensional;
        public int[,] twoDimensional;
        public int[][] jagged;
        public int[,][] twoOfOne;
        public Plain.Middle.Deepest deepest;
        public Outer<int>.Inner<string> inner;
        public Outer<string>.Inner<string> innerOfString;
        public (int a, string b) tuple;
        public int* pointer;
        public delegate*<int, void> functionPointer;
";

    private static ITypeDefinition Bridged(string field) =>
        TestCompilation.FieldType(Fields, field).GetTypeDefinition();

    // ---- the same type, from both sides ----

    /// <summary>
    /// <c>int?</c>. The bridge keeps it as a <c>NullableValueTypeDefinition</c> so an emitter can
    /// tell it from <c>string?</c>, and it still has to be the type a caller wrote.
    /// </summary>
    [Fact]
    public void ABridgedNullableValueTypeIsAHandBuiltOne()
    {
        AssertType.Same(Bridged("nullableInt"), TypeDefinition.Get(typeof(int)).MakeNullable());
    }

    /// <summary>
    /// The nullable case the old comparison got wrong in the other direction: it fell back to
    /// <c>ToString()</c>, which on a nullable value type is namespace and name with the arguments
    /// and the <c>?</c> missing, so a nullable generic never matched its hand-built self.
    /// </summary>
    [Fact]
    public void ABridgedNullableGenericIsAHandBuiltOne()
    {
        AssertType.Same(
            Bridged("nullableGeneric"),
            TypeDefinition.Get(typeof(KeyValuePair<string, int>)).MakeNullable());
    }

    [Fact]
    public void ABridgedNullableEnumIsAHandBuiltOne()
    {
        AssertType.Same(
            Bridged("nullableEnum"),
            TypeDefinition.Get(TypeDefinitionEnum.EnumDefinition, BridgeNamespace, "Color").MakeNullable());
    }

    /// <summary>
    /// Every array shape, from a <c>ArrayTypeDefinition</c> that holds a rank and an element type
    /// against a <c>TypeDefinition</c> that holds a list of ranks.
    /// </summary>
    [Theory]
    [InlineData("oneDimensional", typeof(int[]))]
    [InlineData("twoDimensional", typeof(int[,]))]
    [InlineData("jagged", typeof(int[][]))]
    [InlineData("twoOfOne", typeof(int[,][]))]
    public void ABridgedArrayIsAHandBuiltOne(string field, System.Type handBuilt)
    {
        AssertType.Same(Bridged(field), TypeDefinition.Get(handBuilt));
    }

    [Fact]
    public void ABridgedGenericIsAHandBuiltOne()
    {
        AssertType.Same(Bridged("listOfInt"), TypeDefinition.Get(typeof(List<int>)));
    }

    /// <summary>
    /// A nested type reaches the same value from three directions: the bridge, a container chain,
    /// and the dotted name a consumer writes today.
    /// </summary>
    [Fact]
    public void ABridgedNestedTypeIsAHandBuiltOne()
    {
        var byContainer = TypeDefinition.GetNested(
            TypeDefinition.GetNested(TypeDefinition.Get(BridgeNamespace, "Plain"), "Middle"),
            "Deepest");

        AssertType.Same(Bridged("deepest"), byContainer);
        AssertType.Same(Bridged("deepest"), TypeDefinition.Get(BridgeNamespace, "Plain.Middle.Deepest"));
    }

    /// <summary>
    /// A nested type whose container carries arguments is the shape the core model has no single
    /// class for. It is still reachable, as a generic with a generic container, and the two agree.
    /// </summary>
    [Fact]
    public void ABridgedNestedGenericIsAHandBuiltOne()
    {
        AssertType.Same(Bridged("inner"), HandBuiltInner(TypeDefinition.Get(typeof(int))));
    }

    private static ITypeDefinition HandBuiltInner(ITypeDefinition outerArgument)
    {
        var outer = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, BridgeNamespace, "Outer", new[] { outerArgument });

        return new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            BridgeNamespace,
            "Inner",
            new[] { TypeDefinition.Get(typeof(string)) },
            null,
            false,
            outer);
    }

    // ---- types that look alike and are not the same ----

    [Fact]
    public void ABridgedNullableIsNotItsUnderlyingType()
    {
        AssertType.Different(Bridged("nullableInt"), Bridged("plainInt"));
        AssertType.Different(Bridged("nullableInt"), TypeDefinition.Get(typeof(int)));
    }

    /// <summary>
    /// The distinction the model's one nullability bit loses. Both write a trailing <c>?</c>; only
    /// one of them is a different runtime type from what it wraps.
    /// </summary>
    [Fact]
    public void ABridgedNullableValueTypeIsNotANullableReference()
    {
        AssertType.Different(Bridged("nullableInt"), Bridged("nullableString"));
    }

    [Theory]
    [InlineData("oneDimensional", typeof(int[][]))]
    [InlineData("twoDimensional", typeof(int[][]))]
    [InlineData("jagged", typeof(int[,]))]
    [InlineData("twoOfOne", typeof(int[][,]))]
    [InlineData("oneDimensional", typeof(int))]
    public void ABridgedArrayIsNotADifferentShape(string field, System.Type handBuilt)
    {
        AssertType.Different(Bridged(field), TypeDefinition.Get(handBuilt));
    }

    [Fact]
    public void ABridgedNestedGenericIsNotADifferentClosing()
    {
        AssertType.Different(Bridged("inner"), Bridged("innerOfString"));
        AssertType.Different(Bridged("inner"), HandBuiltInner(TypeDefinition.Get(typeof(string))));
    }

    /// <summary>
    /// The three pairs the property-by-property comparison called equal. Each shares a kind, a name,
    /// a namespace, an array shape and a nullability with the other, and differs only in what the
    /// rendering carries - which is why identity had to move to the rendering.
    /// </summary>
    [Fact]
    public void ATupleIsNotTheValueTupleName()
    {
        AssertType.Different(Bridged("tuple"), TypeDefinition.Get("System", "ValueTuple"));
    }

    [Fact]
    public void APointerIsNotWhatItPointsAt()
    {
        AssertType.Different(Bridged("pointer"), TypeDefinition.Get(typeof(int)));
        AssertType.Different(Bridged("pointer"), Bridged("plainInt"));
    }

    [Fact]
    public void AFunctionPointerIsNotATypeCalledDelegateStar()
    {
        AssertType.Different(Bridged("functionPointer"), TypeDefinition.Get("", "delegate*"));
    }

    // ---- what the equality is for ----

    /// <summary>
    /// A registration written by hand, looked up with what the bridge read - and the other way
    /// round, because a generator does both.
    /// </summary>
    [Fact]
    public void ABridgedTypeFindsAHandBuiltRegistration()
    {
        var registrations = new Dictionary<ITypeDefinition, string>
        {
            { TypeDefinition.Get(typeof(int)).MakeNullable(), "int?" },
            { TypeDefinition.Get(typeof(int[,])), "int[,]" },
            { TypeDefinition.Get(BridgeNamespace, "Plain.Middle.Deepest"), "deepest" },
            { TypeDefinition.Get(typeof(List<int>)), "List<int>" }
        };

        Assert.Equal("int?", registrations[Bridged("nullableInt")]);
        Assert.Equal("int[,]", registrations[Bridged("twoDimensional")]);
        Assert.Equal("deepest", registrations[Bridged("deepest")]);
        Assert.Equal("List<int>", registrations[Bridged("listOfInt")]);

        Assert.False(registrations.ContainsKey(Bridged("plainInt")));
        Assert.False(registrations.ContainsKey(Bridged("jagged")));
        Assert.False(registrations.ContainsKey(Bridged("pointer")));
    }

    [Fact]
    public void AHandBuiltTypeFindsABridgedRegistration()
    {
        var registrations = new Dictionary<ITypeDefinition, string>
        {
            { Bridged("nullableInt"), "int?" },
            { Bridged("jagged"), "int[][]" },
            { Bridged("inner"), "Outer<int>.Inner<string>" }
        };

        Assert.Equal("int?", registrations[TypeDefinition.Get(typeof(int)).MakeNullable()]);
        Assert.Equal("int[][]", registrations[TypeDefinition.Get(typeof(int[][]))]);
        Assert.Equal("Outer<int>.Inner<string>", registrations[HandBuiltInner(TypeDefinition.Get(typeof(int)))]);

        Assert.False(registrations.ContainsKey(TypeDefinition.Get(typeof(int))));
        Assert.False(registrations.ContainsKey(TypeDefinition.Get(typeof(int[,]))));
    }
}
