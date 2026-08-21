using Xunit;

namespace CSharpAuthor.Tests.ModifierTests;

/// <summary>
/// The five accessibility levels, on every kind of declaration.
/// </summary>
/// <remarks>
/// Two of C#'s five levels are spelled with two keywords, and <see cref="ComponentModifier"/> is a
/// flags enum, so each is a pair of flags. The reader took the first flag that matched:
/// <c>protected internal</c> came out as <c>internal</c>, and <c>private protected</c> - the most
/// restrictive level C# has - came out as <c>protected</c>, which is reachable from a derived type
/// in <em>any</em> assembly. A member declared as tightly as the language allows was published
/// outside its own assembly, and it compiled, so nothing said so.
/// </remarks>
public class AccessibilityTests
{
    private static string WriteMethod(ComponentModifier modifiers)
    {
        var method = new MethodDefinition("Method") { Modifiers = modifiers };

        var outputContext = new OutputContext();

        method.SetReturnType(TypeDefinition.Get(typeof(void)));
        method.WriteOutput(outputContext);

        return outputContext.Output();
    }

    [Fact]
    public void ProtectedInternalKeepsBothKeywords()
    {
        AssertEqual.ContainsWithoutNewLine(
            "protected internal void Method()",
            WriteMethod(ComponentModifier.Protected | ComponentModifier.Internal));
    }

    [Fact]
    public void PrivateProtectedKeepsBothKeywordsAndDoesNotWidenAccess()
    {
        var output = WriteMethod(ComponentModifier.Private | ComponentModifier.Protected);

        AssertEqual.ContainsWithoutNewLine("private protected void Method()", output);

        // The regression that matters: `protected` alone is a wider accessibility than was asked
        // for, and it is what this used to emit. Asserted on the leading token rather than by
        // substring, because "protected void" is a substring of "private protected void".
        Assert.StartsWith("private protected ", output.TrimStart());
    }

    [Fact]
    public void NamedCombinationsMatchTheirFlagPairs()
    {
        Assert.Equal(
            ComponentModifier.ProtectedInternal,
            ComponentModifier.Protected | ComponentModifier.Internal);

        Assert.Equal(
            ComponentModifier.PrivateProtected,
            ComponentModifier.Private | ComponentModifier.Protected);
    }

    [Theory]
    [InlineData(ComponentModifier.Public, "public")]
    [InlineData(ComponentModifier.Private, "private")]
    [InlineData(ComponentModifier.Protected, "protected")]
    [InlineData(ComponentModifier.Internal, "internal")]
    [InlineData(ComponentModifier.ProtectedInternal, "protected internal")]
    [InlineData(ComponentModifier.PrivateProtected, "private protected")]
    public void EveryLevelOnAMethod(ComponentModifier modifiers, string expected)
    {
        AssertEqual.ContainsWithoutNewLine(expected + " void Method()", WriteMethod(modifiers));
    }

    [Theory]
    [InlineData(ComponentModifier.ProtectedInternal, "protected internal")]
    [InlineData(ComponentModifier.PrivateProtected, "private protected")]
    public void EveryLevelOnAField(ComponentModifier modifiers, string expected)
    {
        var field = new FieldDefinition(TypeDefinition.Get(typeof(int)), "_count")
        {
            Modifiers = modifiers
        };

        var outputContext = new OutputContext();

        field.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(expected + " ", outputContext.Output());
        AssertEqual.ContainsWithoutNewLine(" _count;", outputContext.Output());
    }

    [Theory]
    [InlineData(ComponentModifier.ProtectedInternal, "protected internal")]
    [InlineData(ComponentModifier.PrivateProtected, "private protected")]
    public void EveryLevelOnAProperty(ComponentModifier modifiers, string expected)
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Count")
        {
            Modifiers = modifiers
        };

        var outputContext = new OutputContext();

        property.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(expected + " ", outputContext.Output());
    }

    [Theory]
    [InlineData(ComponentModifier.ProtectedInternal, "protected internal class Widget")]
    [InlineData(ComponentModifier.PrivateProtected, "private protected class Widget")]
    [InlineData(ComponentModifier.Internal, "internal class Widget")]
    public void EveryLevelOnAClass(ComponentModifier modifiers, string expected)
    {
        var classDefinition = new ClassDefinition("Widget") { Modifiers = modifiers };

        var outputContext = new OutputContext();

        classDefinition.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(expected, outputContext.Output());
    }

    [Fact]
    public void EveryLevelOnAnEnum()
    {
        var enumDefinition = new EnumDefinition("Kind")
        {
            Modifiers = ComponentModifier.ProtectedInternal
        };

        var outputContext = new OutputContext();

        enumDefinition.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("protected internal enum Kind", outputContext.Output());
    }

    [Fact]
    public void PrivateProtectedSetterOnAnAutoProperty()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Count")
        {
            Modifiers = ComponentModifier.Public
        };

        property.Set!.Modifiers = ComponentModifier.PrivateProtected;

        var outputContext = new OutputContext();

        property.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("{ get; private protected set; }", outputContext.Output());
    }

    [Fact]
    public void ProtectedInternalSetterOnAnAutoProperty()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Count")
        {
            Modifiers = ComponentModifier.Public
        };

        property.Set!.Modifiers = ComponentModifier.ProtectedInternal;

        var outputContext = new OutputContext();

        property.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine(
            "{ get; protected internal set; }", outputContext.Output());
    }

    [Fact]
    public void AnAccessorAsAccessibleAsItsPropertyWritesNoKeyword()
    {
        // CS0273: an accessor may only be more restrictive than the property it belongs to.
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(int)), "Count")
        {
            Modifiers = ComponentModifier.Public
        };

        property.Set!.Modifiers = ComponentModifier.Public;

        var outputContext = new OutputContext();

        property.WriteOutput(outputContext);

        AssertEqual.ContainsWithoutNewLine("{ get; set; }", outputContext.Output());
    }

    [Fact]
    public void NoAccessibilityStillWinsOverEverything()
    {
        var method = new MethodDefinition("Method")
        {
            Modifiers = ComponentModifier.NoAccessibility | ComponentModifier.PrivateProtected
        };

        method.SetReturnType(TypeDefinition.Get(typeof(void)));

        var outputContext = new OutputContext();

        method.WriteOutput(outputContext);

        Assert.DoesNotContain("private", outputContext.Output());
        Assert.DoesNotContain("protected", outputContext.Output());
    }
}
