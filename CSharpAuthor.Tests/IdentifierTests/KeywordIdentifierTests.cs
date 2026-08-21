using Xunit;

namespace CSharpAuthor.Tests.IdentifierTests;

/// <summary>
/// Names that are C# keywords, at every place a name is written.
/// </summary>
/// <remarks>
/// A generator names things after something it read - a column called <c>class</c>, a JSON property
/// called <c>event</c> - and <c>void M(string class)</c> is CS1001. C# has one answer, the
/// <c>@</c> prefix, and it has to be applied at the declaration and at every reference or the two
/// stop agreeing.
/// </remarks>
public class KeywordIdentifierTests
{
    [Theory]
    [InlineData("class")]
    [InlineData("event")]
    [InlineData("int")]
    [InlineData("string")]
    [InlineData("namespace")]
    [InlineData("return")]
    [InlineData("operator")]
    [InlineData("stackalloc")]
    [InlineData("volatile")]
    public void ReservedWordsAreRecognised(string keyword)
    {
        Assert.True(CSharpIdentifier.IsReservedKeyword(keyword));
        Assert.Equal("@" + keyword, CSharpIdentifier.Escape(keyword));
    }

    [Theory]
    // Contextual keywords are legal identifiers as they stand; prefixing them would be noise.
    [InlineData("value")]
    [InlineData("var")]
    [InlineData("record")]
    [InlineData("async")]
    [InlineData("await")]
    [InlineData("where")]
    [InlineData("nint")]
    [InlineData("required")]
    [InlineData("init")]
    [InlineData("Class")]
    [InlineData("_class")]
    [InlineData("classes")]
    public void NonReservedNamesAreLeftAlone(string name)
    {
        Assert.False(CSharpIdentifier.IsReservedKeyword(name));
        Assert.Equal(name, CSharpIdentifier.Escape(name));
    }

    [Fact]
    public void AnAlreadyEscapedNameIsNotEscapedTwice()
    {
        Assert.Equal("@class", CSharpIdentifier.Escape("@class"));
    }

    [Fact]
    public void EachSegmentOfADottedNameIsEscaped()
    {
        Assert.Equal("Foo.@class.Bar", CSharpIdentifier.EscapeQualified("Foo.class.Bar"));
        Assert.Equal("@namespace.@event", CSharpIdentifier.EscapeQualified("namespace.event"));
        Assert.Equal("Foo.Bar", CSharpIdentifier.EscapeQualified("Foo.Bar"));
    }

    [Fact]
    public void AnExpressionThatHappensToHoldADotIsLeftAlone()
    {
        // Not a name, so rewriting any part of it would be a worse failure than leaving it.
        Assert.Equal("Foo().Bar", CSharpIdentifier.EscapeReference("Foo().Bar"));
        Assert.Equal("items[0].class", CSharpIdentifier.EscapeReference("items[0].class"));
    }

    [Fact]
    public void ThisAndBaseStayThemselvesAtAReferenceSite()
    {
        // They arrive as expressions rather than names, and @this is not the same thing.
        Assert.Equal("this", CSharpIdentifier.EscapeReference("this"));
        Assert.Equal("base", CSharpIdentifier.EscapeReference("base"));
        Assert.Equal("null", CSharpIdentifier.EscapeReference("null"));
        Assert.Equal("this.Name", CSharpIdentifier.EscapeReference("this.Name"));

        // But a name really declared as one still needs the prefix.
        Assert.Equal("@this", CSharpIdentifier.Escape("this"));
    }

    [Fact]
    public void ParameterNamedAfterAKeyword()
    {
        var method = new MethodDefinition("Method") { Modifiers = ComponentModifier.Public };

        method.AddParameter(TypeDefinition.Get(typeof(string)), "class");

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        // CS1001 before this.
        AssertEqual.ContainsWithoutNewLine("Method(string @class)", outputContext.Output());
    }

    [Fact]
    public void ParameterReferenceMatchesItsDeclaration()
    {
        var parameter = new ParameterDefinition(TypeDefinition.Get(typeof(string)), "class");

        var outputContext = new OutputContext();

        parameter.WriteOutput(outputContext);

        Assert.Equal("@class", outputContext.Output());
    }

    [Fact]
    public void ParameterForwardedAsAnArgumentMatchesItsDeclaration()
    {
        var parameter = new ParameterDefinition(TypeDefinition.Get(typeof(string)), "class")
        {
            Modifier = ParameterModifier.Ref
        };

        var outputContext = new OutputContext();

        parameter.AsArgument().WriteOutput(outputContext);

        Assert.Equal("ref @class", outputContext.Output());
    }

    [Fact]
    public void MethodNamedAfterAKeyword()
    {
        var method = new MethodDefinition("lock") { Modifiers = ComponentModifier.Public };

        method.SetReturnType(TypeDefinition.Get(typeof(void)));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("void @lock()", outputContext.Output());
    }

    [Fact]
    public void FieldNamedAfterAKeyword()
    {
        var field = new FieldDefinition(TypeDefinition.Get(typeof(int)), "int");

        var outputContext = new OutputContext();

        field.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(" @int;", outputContext.Output());
    }

    [Fact]
    public void PropertyNamedAfterAKeyword()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "default")
        {
            Modifiers = ComponentModifier.Public
        };

        var outputContext = new OutputContext();

        property.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("@default { get; set; }", outputContext.Output());
    }

    [Fact]
    public void ClassNamedAfterAKeyword()
    {
        var classDefinition = new ClassDefinition("object")
        {
            Modifiers = ComponentModifier.Public
        };

        var outputContext = new OutputContext();

        classDefinition.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("public class @object", outputContext.Output());
    }

    [Fact]
    public void ConstructorMatchesTheEscapedClassName()
    {
        var classDefinition = new ClassDefinition("object")
        {
            Modifiers = ComponentModifier.Public
        };

        classDefinition.AddConstructor().Modifiers = ComponentModifier.Public;

        var outputContext = new OutputContext();

        classDefinition.WriteOutput(outputContext);

        var output = outputContext.Output();

        AssertEqual.ContainsWithoutNewLine("public class @object", output);
        AssertEqual.ContainsWithoutNewLine("public @object()", output);
    }

    [Fact]
    public void InterfaceNamedAfterAKeyword()
    {
        var interfaceDefinition = new InterfaceDefinition("base")
        {
            Modifiers = ComponentModifier.Public
        };

        var outputContext = new OutputContext();

        interfaceDefinition.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("interface @base", outputContext.Output());
    }

    [Fact]
    public void EnumAndItsValuesNamedAfterKeywords()
    {
        var enumDefinition = new EnumDefinition("switch")
        {
            Modifiers = ComponentModifier.Public
        };

        enumDefinition.AddValue("default");
        enumDefinition.AddValue("Normal");

        var outputContext = new OutputContext();

        enumDefinition.WriteOutput(outputContext);

        var output = outputContext.Output();

        AssertEqual.ContainsWithoutNewLine("public enum @switch", output);
        AssertEqual.ContainsWithoutNewLine("@default,", output);
        AssertEqual.ContainsWithoutNewLine("Normal,", output);
    }

    [Fact]
    public void NamespaceSegmentNamedAfterAKeyword()
    {
        var namespaceDefinition = new NamespaceDefinition("Company.event.Models")
        {
            FileScopedNamespace = true
        };

        var outputContext = new OutputContext();

        namespaceDefinition.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "namespace Company.@event.Models;", outputContext.Output());
    }

    [Fact]
    public void BlockNamespaceSegmentNamedAfterAKeyword()
    {
        var namespaceDefinition = new NamespaceDefinition("Company.event.Models");

        var outputContext = new OutputContext();

        namespaceDefinition.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "namespace Company.@event.Models", outputContext.Output());
    }

    [Fact]
    public void EventNamedAfterAKeyword()
    {
        var eventDefinition = new EventDefinition(
            TypeDefinition.Get(typeof(System.EventHandler)), "checked")
        {
            Modifiers = ComponentModifier.Public
        };

        var outputContext = new OutputContext();

        eventDefinition.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("@checked", outputContext.Output());
    }

    [Fact]
    public void IndexerKeepsItsThisKeyword()
    {
        // The one property whose name is a keyword on purpose: `this[int index]`, not `@this[...]`.
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(string)), "this")
        {
            Modifiers = ComponentModifier.Public,
            IndexType = TypeDefinition.Get(typeof(int))
        };

        var outputContext = new OutputContext();

        property.WriteOutput(outputContext);

        var output = outputContext.Output();

        AssertEqual.ContainsWithoutNewLine("this[int index]", output);
        Assert.DoesNotContain("@this", output);
    }

    [Fact]
    public void InstanceReferenceEscapesEachSegment()
    {
        var instance = new InstanceDefinition("record").Property("class").Property("Name");

        var outputContext = new OutputContext();

        instance.WriteOutput(outputContext);

        // `record` is contextual and stays; `class` is reserved and does not.
        Assert.Equal("record.@class.Name", outputContext.Output());
    }
}
