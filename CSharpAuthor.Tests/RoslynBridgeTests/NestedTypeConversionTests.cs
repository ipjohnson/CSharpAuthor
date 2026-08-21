using CSharpAuthor.Roslyn;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// A nested type is named by its containers, and its containers may be closed over something.
/// </summary>
public class NestedTypeConversionTests
{
    private const string Nested = @"
        public Plain.Middle middle;
        public Plain.Middle.Deepest deepest;
        public Outer<int>.PlainInner plainInsideGeneric;
        public Outer<int>.Inner<string> genericInsideGeneric;
        public Outer<int>.Inner<string>.Deepest deepestInsideGenerics;
        public Outer<Outer<int>.PlainInner>.Inner<Color> nestedArgument;
        public Outer<T>.Inner<T> closedOverTypeParameters;
        public GlobalThing.Inner globalNested;
        public @event.@void keywordNested;
";

    [Theory]
    [InlineData("middle", "Plain.Middle")]
    [InlineData("deepest", "Plain.Middle.Deepest")]
    [InlineData("plainInsideGeneric", "Outer<int>.PlainInner")]
    [InlineData("genericInsideGeneric", "Outer<int>.Inner<string>")]
    [InlineData("deepestInsideGenerics", "Outer<int>.Inner<string>.Deepest")]
    [InlineData("nestedArgument", "Outer<Outer<int>.PlainInner>.Inner<Color>")]
    [InlineData("closedOverTypeParameters", "Outer<T>.Inner<T>")]
    [InlineData("globalNested", "GlobalThing.Inner")]
    [InlineData("keywordNested", "@event.@void")]
    public void ContainersAreKept(string field, string expected)
    {
        var typeDefinition = TestCompilation.FieldType(Nested, field).GetTypeDefinition();

        Assert.Equal(expected, TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// A non-generic type nested in a closed generic reports <c>IsGenericType</c>, so reading that
    /// flag and building a generic from it emits <c>Outer.PlainInner&lt;&gt;</c> - which does not
    /// compile. Both consumers do exactly that today.
    /// </summary>
    [Fact]
    public void NonGenericNestedInAGenericIsNotWrittenAsGeneric()
    {
        var typeDefinition = TestCompilation.FieldType(Nested, "plainInsideGeneric").GetTypeDefinition();

        var written = TestCompilation.Write(typeDefinition);

        Assert.Equal("Outer<int>.PlainInner", written);
        Assert.DoesNotContain("<>", written);
    }

    /// <summary>
    /// The same symbol, converted the way the consumers convert it today, to show what the flag
    /// answers and what building on it produces.
    /// </summary>
    /// <remarks>
    /// Not a test of the bridge - a test of the premise. If Roslyn ever stopped reporting
    /// <c>IsGenericType</c> for a non-generic type nested in a generic one, this would fail and the
    /// special case it justifies could go.
    /// </remarks>
    [Fact]
    public void TheFlagBothConsumersReadSaysGenericHere()
    {
        var symbol = Assert.IsAssignableFrom<Microsoft.CodeAnalysis.INamedTypeSymbol>(
            TestCompilation.FieldType(Nested, "plainInsideGeneric"));

        Assert.True(symbol.IsGenericType);
        Assert.Equal(0, symbol.Arity);
        Assert.Empty(symbol.TypeArguments);

        // What both consumers build from exactly that: a generic definition closed over the empty
        // argument list.
        var asTheConsumersBuildIt = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition,
            "BridgeTestNamespace",
            "Outer.PlainInner",
            System.Array.Empty<ITypeDefinition>());

        Assert.Equal("Outer.PlainInner<>", TestCompilation.Write(asTheConsumersBuildIt));
    }

    [Fact]
    public void NestedTypeQualifiesFromItsNamespace()
    {
        var typeDefinition = TestCompilation.FieldType(Nested, "genericInsideGeneric").GetTypeDefinition();

        Assert.Equal(
            "global::BridgeTestNamespace.Outer<int>.Inner<string>",
            TestCompilation.Write(typeDefinition, TypeOutputMode.Global));

        Assert.Equal("BridgeTestNamespace", typeDefinition.Namespace);
    }

    /// <summary>
    /// The dotted path is what a caller reading <c>Name</c> off a nested type has always been given,
    /// and it is what a name-based lookup - an attribute's metadata name, say - still needs.
    /// </summary>
    [Fact]
    public void NameIsTheDottedPathWithoutArguments()
    {
        var types = TestCompilation.FieldTypes(Nested);

        Assert.Equal("Plain.Middle.Deepest", types["deepest"].GetTypeDefinition().Name);
        Assert.Equal("Outer.Inner", types["genericInsideGeneric"].GetTypeDefinition().Name);
        Assert.Equal("Outer.PlainInner", types["plainInsideGeneric"].GetTypeDefinition().Name);
    }

    /// <summary>The type's own arguments, not its containers'.</summary>
    [Fact]
    public void TypeArgumentsAreTheTypesOwn()
    {
        var typeDefinition = TestCompilation.FieldType(Nested, "genericInsideGeneric").GetTypeDefinition();

        var typeArgument = Assert.Single(typeDefinition.TypeArguments);

        Assert.Equal("string", TestCompilation.Write(typeArgument));
    }

    /// <summary>
    /// A nested type with no closed container is still the plain shape the type model has always
    /// produced for it, so nothing that compares against a hand-built one breaks.
    /// </summary>
    [Fact]
    public void PlainNestingKeepsTheOriginalShape()
    {
        var typeDefinition = TestCompilation.FieldType(Nested, "deepest").GetTypeDefinition();

        Assert.IsType<TypeDefinition>(typeDefinition);
        Assert.True(typeDefinition.Equals(TypeDefinition.Get("BridgeTestNamespace", "Plain.Middle.Deepest")));
    }

    /// <summary>
    /// Every argument of every container contributes its namespace, or the file that names the type
    /// is missing a using for something it mentions.
    /// </summary>
    [Fact]
    public void ContainerArgumentsContributeNamespaces()
    {
        var source = @"
        public Outer<System.Threading.Tasks.Task>.Inner<System.Text.StringBuilder> deep;
";

        var typeDefinition = TestCompilation.FieldType(source, "deep").GetTypeDefinition();

        Assert.Contains("System.Threading.Tasks", typeDefinition.KnownNamespaces);
        Assert.Contains("System.Text", typeDefinition.KnownNamespaces);
        Assert.Contains("BridgeTestNamespace", typeDefinition.KnownNamespaces);
    }

    /// <summary>A type in the global namespace has no namespace and needs no import.</summary>
    [Fact]
    public void GlobalNamespaceTypeHasNoNamespace()
    {
        var typeDefinition = TestCompilation.FieldType(Nested, "globalNested").GetTypeDefinition();

        Assert.Equal("", typeDefinition.Namespace);
        Assert.Equal("GlobalThing.Inner", TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
        Assert.Equal("GlobalThing.Inner", TestCompilation.Write(typeDefinition, TypeOutputMode.FullName));
    }
}
