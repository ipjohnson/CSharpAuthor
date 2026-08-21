using System.Collections.Generic;
using System.Text;
using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

/// <summary>
/// Where the <c>?</c> goes. <c>int?[]</c> and <c>int[]?</c> are different types, and a model that
/// carries one positionless bit can only write one of them.
/// </summary>
/// <remarks>
/// This is the silent-wrongness class of defect: nothing threw, nothing failed to compile, and the
/// caller got a type it did not ask for. <c>int?[]?</c> went further and lost one of its two
/// annotations outright, and <c>new string?[] { null }</c> came out as <c>new string[]? { null }</c>,
/// which is not a differently-spelled array creation but an object creation with an initializer.
/// Every case below is asserted as text and, where the compiler can tell the difference, as code
/// the compiler accepts or rejects.
/// </remarks>
public class NullablePositionTests
{
    // -- the table -------------------------------------------------------------------------

    /// <summary>An array of nullable ints: the annotation is on the element.</summary>
    [Fact]
    public void NullableElementArray()
    {
        var type = TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray();

        Assert.Equal("int?[]", Emit.TypeName(type));
        Assert.False(type.IsNullable);
        Assert.True(type.IsArray);
        Assert.Equal(new[] { false, true }, type.NullableAnnotations);
    }

    /// <summary>The same for a reference type, where the annotation is the nullable one.</summary>
    [Fact]
    public void NullableReferenceElementArray()
    {
        var type = TypeDefinition.Get(typeof(string)).MakeNullable().MakeArray();

        Assert.Equal("string?[]", Emit.TypeName(type));
    }

    /// <summary>A nullable array of non-null elements: the annotation is on the array.</summary>
    [Fact]
    public void NullableArrayOfNonNullElement()
    {
        var type = TypeDefinition.Get(typeof(int)).MakeArray().MakeNullable();

        Assert.Equal("int[]?", Emit.TypeName(type));
        Assert.True(type.IsNullable);
        Assert.Equal(new[] { true, false }, type.NullableAnnotations);
    }

    /// <summary>
    /// Both at once. One bit could hold only one of these two annotations, so the other was dropped
    /// without a word.
    /// </summary>
    [Fact]
    public void NullableArrayOfNullableElement()
    {
        var type = TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray().MakeNullable();

        Assert.Equal("int?[]?", Emit.TypeName(type));
        Assert.True(type.IsNullable);
        Assert.Equal(new[] { true, true }, type.NullableAnnotations);
    }

    /// <summary>
    /// <c>new string?[] { null }</c>. Written the other way round the text re-parses as an object
    /// creation with an initializer - a different node kind, from an emitter that reported success.
    /// </summary>
    [Fact]
    public void NewArrayOfNullableElement()
    {
        var element = TypeDefinition.Get(typeof(string)).MakeNullable();

        var statement = new NewArrayStatement(element, CodeOutputComponent.Get("null"));

        Assert.Equal("new string?[] { null }", Emit.Component(statement));
    }

    /// <summary>The same array creation through the grammar layer, which takes the array type whole.</summary>
    [Fact]
    public void NewArrayOfNullableElement_ThroughTheGrammar()
    {
        var creation = new Syntax.ArrayCreationExpression(
            Syntax.TypeRef.Of(TypeDefinition.Get(typeof(string)).MakeNullable().MakeArray()));

        creation.Initializer = new Syntax.InitializerExpression();
        creation.Initializer.Expressions.Add(new Syntax.LiteralExpression("null"));

        Assert.Equal("new string?[] { null }", Emit.Component(creation));
    }

    // -- the compiler's opinion, which no amount of agreeing with the old output can satisfy ---

    /// <summary>
    /// <c>string?[]</c> takes a null element and <c>string[]?</c> does not. The two spellings are
    /// told apart here by the compiler rather than by a string comparison.
    /// </summary>
    [Fact]
    public void NullableElementArray_AcceptsANullElement()
    {
        var type = TypeDefinition.Get(typeof(string)).MakeNullable().MakeArray();

        RoslynAssert.MemberCompiles(
            "public void M(" + Emit.TypeName(type) + " values) { values[0] = null; }",
            warningsAsErrors: "CS8625");
    }

    /// <summary>
    /// And the array that is itself nullable takes a null <em>array</em>, which the element-nullable
    /// one does not.
    /// </summary>
    [Fact]
    public void NullableArray_AcceptsANullArray()
    {
        var type = TypeDefinition.Get(typeof(string)).MakeArray().MakeNullable();

        RoslynAssert.MemberCompiles(
            "public void M(" + Emit.TypeName(type) + " values) { values = null; }",
            warningsAsErrors: "CS8625");
    }

    [Fact]
    public void NullableArrayOfNullableElement_Compiles()
    {
        var type = TypeDefinition.Get(typeof(string)).MakeNullable().MakeArray().MakeNullable();

        RoslynAssert.MemberCompiles(
            "public void M(" + Emit.TypeName(type) + " values) { values = null; }",
            warningsAsErrors: "CS8625");
    }

    // -- every kind of type definition, and every output mode --------------------------------

    [Fact]
    public void OnAGenericType()
    {
        var list = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
            new ITypeDefinition[] { TypeDefinition.Get(typeof(int)) });

        Assert.Equal("List<int>?[]", Emit.TypeName(list.MakeNullable().MakeArray()));
        Assert.Equal("List<int>[]?", Emit.TypeName(list.MakeArray().MakeNullable()));
        Assert.Equal("List<int>?[]?", Emit.TypeName(list.MakeNullable().MakeArray().MakeNullable()));
    }

    [Fact]
    public void OnATypeParameter()
    {
        var parameter = new TypeParameterDefinition("T");

        Assert.Equal("T?[]", Emit.TypeName(parameter.MakeNullable().MakeArray()));
        Assert.Equal("T[]?", Emit.TypeName(parameter.MakeArray().MakeNullable()));
        Assert.Equal("T?[]?", Emit.TypeName(parameter.MakeNullable().MakeArray().MakeNullable()));
    }

    [Fact]
    public void OnANestedType()
    {
        var outer = TypeDefinition.Get(TypeDefinitionEnum.ClassDefinition, "Ns", "Outer");
        var inner = TypeDefinition.GetNested(outer, "Inner");

        Assert.Equal("Outer.Inner?[]", Emit.TypeName(inner.MakeNullable().MakeArray()));
        Assert.Equal("Ns.Outer.Inner?[]", Emit.TypeName(inner.MakeNullable().MakeArray(), TypeOutputMode.FullName));
    }

    /// <summary>
    /// The annotation is part of the name, so it survives qualification: a mode changes what comes
    /// before the name, never where the <c>?</c> sits after it.
    /// </summary>
    [Theory]
    [InlineData(TypeOutputMode.ShortName, "Name?[]")]
    [InlineData(TypeOutputMode.FullName, "Ns.Name?[]")]
    [InlineData(TypeOutputMode.Global, "global::Ns.Name?[]")]
    public void EveryOutputMode(TypeOutputMode mode, string expected)
    {
        var type = TypeDefinition.Get("Ns", "Name").MakeNullable().MakeArray();

        Assert.Equal(expected, Emit.TypeName(type, mode));
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, "List<int>?[]?")]
    [InlineData(TypeOutputMode.FullName, "System.Collections.Generic.List<int>?[]?")]
    [InlineData(TypeOutputMode.Global, "global::System.Collections.Generic.List<int>?[]?")]
    public void EveryOutputMode_Generic(TypeOutputMode mode, string expected)
    {
        var list = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
            new ITypeDefinition[] { TypeDefinition.Get(typeof(int)) });

        Assert.Equal(expected, Emit.TypeName(list.MakeNullable().MakeArray().MakeNullable(), mode));
    }

    // -- shape and position together ----------------------------------------------------------

    /// <summary>
    /// An annotation closes off the specifiers to its left before the next array wraps them, so
    /// <c>string[]?[]</c> is an array of nullable arrays and not a rank the writer reordered.
    /// </summary>
    [Fact]
    public void AnnotationBreaksARunOfRanks()
    {
        var type = TypeDefinition.Get(typeof(string)).MakeArray().MakeNullable().MakeArray();

        Assert.Equal("string[]?[]", Emit.TypeName(type));
        Assert.Equal(new[] { 1, 1 }, type.ArrayRanks);
        Assert.Equal(new[] { false, true, false }, type.NullableAnnotations);
    }

    [Fact]
    public void MultiDimensionalRanksKeepTheirOrder()
    {
        var type = TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray().MakeArray(2);

        Assert.Equal("int?[,][]", Emit.TypeName(type));
        Assert.Equal(new[] { 2, 1 }, type.ArrayRanks);
    }

    [Fact]
    public void ThereIsOneAnnotationPerLevelPlusTheElement()
    {
        var type = TypeDefinition.Get(typeof(int)).MakeArray().MakeArray(3);

        Assert.Equal(type.ArrayRanks.Count + 1, type.NullableAnnotations.Count);
        Assert.Equal(new[] { false, false, false }, type.NullableAnnotations);
    }

    /// <summary>A type that is not an array carries the one flag, and it is <see cref="ITypeDefinition.IsNullable"/>.</summary>
    [Fact]
    public void ANonArrayCarriesOneFlag()
    {
        Assert.Equal(new[] { false }, TypeDefinition.Get(typeof(int)).NullableAnnotations);
        Assert.Equal(new[] { true }, TypeDefinition.Get(typeof(int)).MakeNullable().NullableAnnotations);
    }

    // -- equality --------------------------------------------------------------------------

    /// <summary>
    /// The two types the old model could not tell apart are not equal and do not hash alike.
    /// </summary>
    [Fact]
    public void PositionSeparatesTwoTypes()
    {
        var elementNullable = TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray();
        var arrayNullable = TypeDefinition.Get(typeof(int)).MakeArray().MakeNullable();

        Assert.NotEqual(elementNullable, arrayNullable);
        Assert.NotEqual(elementNullable.GetHashCode(), arrayNullable.GetHashCode());
        Assert.NotEqual(0, elementNullable.CompareTo(arrayNullable));
    }

    [Fact]
    public void PositionSeparatesTwoTypeParameters()
    {
        var elementNullable = new TypeParameterDefinition("T").MakeNullable().MakeArray();
        var arrayNullable = new TypeParameterDefinition("T").MakeArray().MakeNullable();

        Assert.NotEqual(elementNullable, arrayNullable);
        Assert.NotEqual(elementNullable.GetHashCode(), arrayNullable.GetHashCode());
    }

    /// <summary>The same shape built the same way is still one value.</summary>
    [Fact]
    public void TheSameShapeIsStillEqual()
    {
        var left = TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray();
        var right = TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray();

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    // -- the annotation-carrying constructor -------------------------------------------------

    [Fact]
    public void TheConstructorTakesTheAnnotationsDirectly()
    {
        var type = new TypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "Name",
            new[] { 1 }, new[] { false, true }, null);

        Assert.Equal("Name?[]", Emit.TypeName(type));
    }

    /// <summary>
    /// A list of the wrong length is refused rather than padded. Guessing which level the caller
    /// meant is how a wrong type gets built quietly, which is the whole defect.
    /// </summary>
    [Fact]
    public void AnAnnotationListOfTheWrongLengthIsRefused()
    {
        Assert.Throws<System.ArgumentException>(() => new TypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Ns", "Name",
            new[] { 1 }, new[] { true }, null));
    }

    /// <summary>
    /// <c>MakeNullable</c> annotates the type it is called on and leaves everything inside alone,
    /// which is what makes the two annotations of <c>int?[]?</c> reachable one at a time.
    /// </summary>
    [Fact]
    public void MakeNullableLeavesTheInsideAlone()
    {
        var elementNullable = TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray();

        Assert.Equal("int?[]?", Emit.TypeName(elementNullable.MakeNullable()));
        Assert.Equal("int?[]", Emit.TypeName(elementNullable.MakeNullable(false)));
    }

    /// <summary>Round-tripping the annotations through the constructor reproduces the type.</summary>
    [Fact]
    public void TheAnnotationsDescribeTheTypeTheyCameFrom()
    {
        var type = TypeDefinition.Get(typeof(string)).MakeArray().MakeNullable().MakeArray();

        var rebuilt = TypeDefinition.Get(
            type.TypeDefinitionEnum, type.Namespace, type.Name,
            type.ArrayRanks, type.NullableAnnotations, type.ContainingType);

        Assert.Equal(Emit.TypeName(type), Emit.TypeName(rebuilt));
        Assert.Equal(type, rebuilt);
    }

    /// <summary>
    /// A whole nested shape, written into a file, compiles - which is the only assertion the
    /// emitter cannot satisfy by agreeing with itself.
    /// </summary>
    [Fact]
    public void TheWholeShapeCompiles()
    {
        var dictionary = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "Dictionary",
            new[] { TypeDefinition.Get(typeof(string)), TypeDefinition.Get(typeof(int)).MakeNullable() });

        var list = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
            new ITypeDefinition[] { dictionary });

        var type = list.MakeNullable().MakeArray().MakeArray();

        Assert.Equal("List<Dictionary<string,int?>>?[][]", Emit.TypeName(type));

        RoslynAssert.MemberCompiles("public " + Emit.TypeName(type) + " Field;");
    }

    /// <summary>
    /// <see cref="ITypeDefinition.WriteTypeName"/> and <see cref="object.ToString"/> agree about a
    /// generic's shape - <c>ToString</c> is a hash key as well as a display string, so a type it
    /// cannot tell apart is one that hashes into the wrong bucket.
    /// </summary>
    [Fact]
    public void AGenericToStringCarriesThePosition()
    {
        var list = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "System.Collections.Generic", "List",
            new ITypeDefinition[] { TypeDefinition.Get(typeof(int)) });

        Assert.EndsWith("?[]", list.MakeNullable().MakeArray().ToString());
        Assert.EndsWith("[]?", list.MakeArray().MakeNullable().ToString());
    }

    /// <summary>
    /// The <c>WriteTypeName</c> overload a consumer reaches through the extension method sees the
    /// same thing.
    /// </summary>
    [Fact]
    public void GetShortNameAgrees()
    {
        var builder = new StringBuilder();

        var type = TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray().MakeNullable();

        type.WriteTypeName(builder);

        Assert.Equal(builder.ToString(), type.GetShortName());
        Assert.Equal("int?[]?", type.GetShortName());
    }

    /// <summary>
    /// A generator holding types in a dictionary is the reason equality has to be cheap and stable;
    /// this checks the two shapes land in different buckets rather than overwriting each other.
    /// </summary>
    [Fact]
    public void TheTwoShapesAreDifferentDictionaryKeys()
    {
        var map = new Dictionary<ITypeDefinition, string>
        {
            { TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray(), "element" },
            { TypeDefinition.Get(typeof(int)).MakeArray().MakeNullable(), "array" },
        };

        Assert.Equal("element", map[TypeDefinition.Get(typeof(int)).MakeNullable().MakeArray()]);
        Assert.Equal("array", map[TypeDefinition.Get(typeof(int)).MakeArray().MakeNullable()]);
    }
}
