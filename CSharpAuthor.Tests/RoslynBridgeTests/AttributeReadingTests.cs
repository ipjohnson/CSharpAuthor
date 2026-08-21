using System.Linq;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// Reading attributes off symbols as values rather than as source text.
/// </summary>
public class AttributeReadingTests
{
    private const string Source = @"
#nullable enable
using System;
using System.Collections.Generic;

namespace AttributeTests {
    public enum Color { Red, Green, Blue }

    [Flags]
    public enum Access { None = 0, Read = 1, Write = 2, Execute = 4, All = 7 }

    public class Marker { }

    public class SimpleAttribute : Attribute { }

    public class ValuesAttribute : Attribute {
        public ValuesAttribute(string name, int count) { }
        public string? Description { get; set; }
        public Color Shade { get; set; }
        public Type? Target { get; set; }
        public bool Enabled { get; set; }
    }

    public class TypedAttribute : Attribute {
        public TypedAttribute(Type type) { }
    }

    public class ManyAttribute : Attribute {
        public ManyAttribute(params string[] names) { }
        public int[] Numbers { get; set; } = Array.Empty<int>();
    }

    public class AccessAttribute : Attribute {
        public AccessAttribute(Access access) { }
    }

    public class GenericAttribute<T> : Attribute { }

    public class NullableAttribute : Attribute {
        public NullableAttribute(string? name) { }
    }

    [Simple]
    public class HasSimple { }

    [Values(""first"", 3, Description = ""a description"", Shade = Color.Green, Enabled = true)]
    public class HasValues { }

    [Typed(typeof(List<Marker>))]
    public class HasTyped { }

    [Typed(typeof(List<>))]
    public class HasOpenTyped { }

    [Many(""one"", ""two"", Numbers = new[] { 1, 2, 3 })]
    public class HasMany { }

    [Access(Access.Read | Access.Write)]
    public class HasFlags { }

    [Access(Access.All)]
    public class HasCombinedFlag { }

    [Access((Access)64)]
    public class HasUnnamedFlag { }

    [Generic<Marker>]
    public class HasGeneric { }

    [Nullable(null)]
    public class HasNull { }

    public partial class Split { }

    [Simple]
    public partial class Split { }
}
";

    private static AttributeInstance Attribute(string typeName, int index = 0)
    {
        var symbol = TestCompilation.NamedType(Source, "AttributeTests." + typeName);

        return symbol.GetAttributeInstances()[index];
    }

    [Fact]
    public void AttributeTypeIsTheAttributeClass()
    {
        var attribute = Attribute("HasSimple");

        Assert.Equal("SimpleAttribute", attribute.AttributeType.Name);
        Assert.Equal("AttributeTests", attribute.AttributeType.Namespace);
    }

    /// <summary>
    /// Positional arguments carry the name of the parameter they bound to, so a reader does not have
    /// to count.
    /// </summary>
    [Fact]
    public void ConstructorArgumentsCarryTheirParameterNames()
    {
        var attribute = Attribute("HasValues");

        Assert.Equal(2, attribute.ConstructorArguments.Count);
        Assert.Equal("name", attribute.ConstructorArguments[0].Name);
        Assert.Equal("first", attribute.ConstructorArguments[0].Value);
        Assert.Equal(AttributeArgumentKind.String, attribute.ConstructorArguments[0].Kind);

        Assert.Equal("count", attribute.ConstructorArguments[1].Name);
        Assert.Equal(3, attribute.ConstructorArguments[1].Value);
        Assert.Equal(AttributeArgumentKind.Primitive, attribute.ConstructorArguments[1].Kind);
    }

    [Fact]
    public void NamedArgumentsAreFoundByName()
    {
        var attribute = Attribute("HasValues");

        Assert.Equal("a description", attribute.FindNamedArgument("Description")!.Value);
        Assert.Equal(true, attribute.FindNamedArgument("Enabled")!.Value);
        Assert.Null(attribute.FindNamedArgument("Target"));
    }

    /// <summary>
    /// A <c>typeof</c> argument stays a type, so the file it is emitted into qualifies it under
    /// whatever output mode it ends up with rather than depending on the consumer's usings.
    /// </summary>
    [Fact]
    public void TypeArgumentsStayTypes()
    {
        var attribute = Attribute("HasTyped");

        var argument = attribute.ConstructorArguments[0];

        Assert.Equal(AttributeArgumentKind.Type, argument.Kind);

        var typeDefinition = Assert.IsAssignableFrom<ITypeDefinition>(argument.Value);

        Assert.Equal("List<Marker>", TestCompilation.Write(typeDefinition));
        Assert.Equal(
            "global::System.Collections.Generic.List<global::AttributeTests.Marker>",
            TestCompilation.Write(typeDefinition, TypeOutputMode.Global));
    }

    /// <summary>
    /// <c>typeof(List&lt;&gt;)</c> binds to the unbound symbol, whose arguments are the
    /// declaration's own type parameters - re-emitting them writes <c>typeof(List&lt;T&gt;)</c>,
    /// where <c>T</c> is not in scope.
    /// </summary>
    [Fact]
    public void OpenGenericTypeArgumentStaysOpen()
    {
        var argument = Attribute("HasOpenTyped").ConstructorArguments[0];

        var typeDefinition = Assert.IsAssignableFrom<ITypeDefinition>(argument.Value);

        Assert.Equal("List<>", TestCompilation.Write(typeDefinition));
    }

    /// <summary>
    /// An enum's constant value is its underlying integer. Folding to it emits a number that does not
    /// assign back to the property.
    /// </summary>
    [Fact]
    public void EnumArgumentsKeepTheirMemberName()
    {
        var argument = Attribute("HasValues").FindNamedArgument("Shade")!;

        Assert.Equal(AttributeArgumentKind.Enum, argument.Kind);
        Assert.Equal("Green", Assert.Single(argument.EnumMemberNames));
        Assert.Equal("Color.Green", Render(argument));
        Assert.Equal("global::AttributeTests.Color.Green", Render(argument, TypeOutputMode.Global));
    }

    [Fact]
    public void FlagCombinationsKeepEveryMemberName()
    {
        var argument = Attribute("HasFlags").ConstructorArguments[0];

        Assert.Equal(new[] { "Read", "Write" }, argument.EnumMemberNames.OrderBy(name => name).ToArray());
        Assert.Equal("Access.Read | Access.Write", Render(argument));
    }

    /// <summary>A member that names the whole combination is preferred over its parts.</summary>
    [Fact]
    public void ACombinedMemberIsWrittenAsItself()
    {
        var argument = Attribute("HasCombinedFlag").ConstructorArguments[0];

        Assert.Equal("All", Assert.Single(argument.EnumMemberNames));
        Assert.Equal("Access.All", Render(argument));
    }

    /// <summary>
    /// A value naming no member is still a legal enum value, and a bare number does not assign to
    /// one.
    /// </summary>
    [Fact]
    public void AValueNamingNoMemberIsCast()
    {
        var argument = Attribute("HasUnnamedFlag").ConstructorArguments[0];

        Assert.Empty(argument.EnumMemberNames);
        Assert.Equal("(Access)64", Render(argument));
    }

    [Fact]
    public void ParamsArgumentsArriveAsAnArray()
    {
        var attribute = Attribute("HasMany");

        var argument = Assert.Single(attribute.ConstructorArguments);

        Assert.Equal(AttributeArgumentKind.Array, argument.Kind);
        Assert.Equal(2, argument.Elements.Count);
        Assert.Equal("one", argument.Elements[0].Value);
        Assert.Equal("new string[] { \"one\", \"two\" }", Render(argument));
    }

    [Fact]
    public void ArrayNamedArgumentsKeepTheirElementType()
    {
        var argument = Attribute("HasMany").FindNamedArgument("Numbers")!;

        Assert.Equal(AttributeArgumentKind.Array, argument.Kind);
        Assert.Equal("int", TestCompilation.Write(argument.ArrayElementType!));
        Assert.Equal("new int[] { 1, 2, 3 }", Render(argument));
    }

    [Fact]
    public void NullArgumentsAreNull()
    {
        var argument = Attribute("HasNull").ConstructorArguments[0];

        Assert.Equal(AttributeArgumentKind.Null, argument.Kind);
        Assert.Null(argument.Value);
        Assert.Equal("null", Render(argument));
    }

    /// <summary>
    /// A generic attribute keeps its arguments, or it is emitted as its bare name and does not
    /// compile.
    /// </summary>
    [Fact]
    public void GenericAttributesKeepTheirArguments()
    {
        var attribute = Attribute("HasGeneric");

        Assert.Equal("GenericAttribute<Marker>", TestCompilation.Write(attribute.AttributeType));
    }

    /// <summary>
    /// The attribute is on the other part of the partial. Reading the syntax in front of the
    /// generator would not find it; reading the symbol does.
    /// </summary>
    [Fact]
    public void AttributesOnOtherPartsOfAPartialAreFound()
    {
        var symbol = TestCompilation.NamedType(Source, "AttributeTests.Split");

        Assert.True(symbol.HasAttribute(TypeDefinition.Get("AttributeTests", "SimpleAttribute")));
    }

    /// <summary>Matching works with or without the suffix, because both spellings are used.</summary>
    [Fact]
    public void AttributesMatchWithAndWithoutTheSuffix()
    {
        var symbol = TestCompilation.NamedType(Source, "AttributeTests.HasSimple");

        Assert.True(symbol.HasAttribute(TypeDefinition.Get("AttributeTests", "SimpleAttribute")));
        Assert.True(symbol.HasAttribute(TypeDefinition.Get("AttributeTests", "Simple")));
        Assert.False(symbol.HasAttribute(TypeDefinition.Get("AttributeTests", "Other")));
        Assert.False(symbol.HasAttribute(TypeDefinition.Get("SomewhereElse", "Simple")));
    }

    [Fact]
    public void FindAttributeReturnsTheInstance()
    {
        var symbol = TestCompilation.NamedType(Source, "AttributeTests.HasValues");

        var attribute = symbol.FindAttribute(TypeDefinition.Get("AttributeTests", "Values"));

        Assert.NotNull(attribute);
        Assert.Equal("first", attribute!.ConstructorArguments[0].Value);
    }

    /// <summary>
    /// The whole attribute, re-emitted onto generated code with its arguments in the order they were
    /// written.
    /// </summary>
    [Fact]
    public void AttributeRoundTripsToADeclaration()
    {
        var attribute = Attribute("HasValues");

        var outputContext = new OutputContext();

        attribute.ToAttributeDefinition().WriteOutput(outputContext);

        AssertEqual.WithoutNewLine(
            "[Values(\"first\", 3, Description = \"a description\", Shade = Color.Green, Enabled = true)]\n",
            outputContext.Output());
    }

    /// <summary>
    /// Every type the attribute mentions reaches the file's imports through the components, so the
    /// re-emitted attribute does not depend on what happened to be in scope where it was read.
    /// </summary>
    [Fact]
    public void ReEmittedArgumentsContributeTheirNamespaces()
    {
        var attribute = Attribute("HasTyped");

        var outputContext = new OutputContext();

        attribute.ToAttributeDefinition().WriteOutput(outputContext);

        outputContext.GenerateUsingStatements();

        Assert.Contains("using System.Collections.Generic;", outputContext.Output());
        Assert.Contains("using AttributeTests;", outputContext.Output());
    }

    private static string Render(AttributeArgument argument, TypeOutputMode mode = TypeOutputMode.ShortName)
    {
        var outputContext = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });

        argument.GetOutputComponent().WriteOutput(outputContext);

        return outputContext.Output();
    }
}
