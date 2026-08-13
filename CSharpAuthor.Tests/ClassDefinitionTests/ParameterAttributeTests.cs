using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

/// <summary>
/// Attributes on parameters, and the target that decides what they land on.
/// </summary>
/// <remarks>
/// A parameter could always carry attributes - <c>AddAttribute</c> is on the base - but nothing
/// wrote them. The target matters for a positional record, where the parameter and the property it
/// declares are two things in one position: an attribute without <c>property:</c> stays on the
/// parameter, where a source generator reading properties never sees it.
/// </remarks>
public class ParameterAttributeTests
{
    [Fact]
    public void AParameterAttributeIsWrittenInline()
    {
        var classDefinition = new ClassDefinition("Service");
        var method = classDefinition.AddMethod("Handle");

        method.AddParameter(typeof(string), "value")
            .AddAttribute(TypeDefinition.Get("Sample", "NotNullAttribute"));

        Assert.Contains("public void Handle([NotNull] string value)", Write(classDefinition));
    }

    [Fact]
    public void TheTargetIsWrittenWhenSet()
    {
        var classDefinition = new ClassDefinition("Pet") { TypeKeyword = ClassKeyword.Record };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;

        var parameter = constructor.AddParameter(typeof(string), "Name");
        parameter.AddAttribute(TypeDefinition.Get("Sample", "RequiredAttribute")).Target = "property";

        Assert.Contains("public record Pet([property: Required] string Name)", Write(classDefinition));
    }

    [Fact]
    public void SeveralAttributesEachGetTheirOwnBrackets()
    {
        var classDefinition = new ClassDefinition("Pet") { TypeKeyword = ClassKeyword.Record };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;

        var parameter = constructor.AddParameter(typeof(string), "Name");
        parameter.AddAttribute(TypeDefinition.Get("Sample", "RequiredAttribute")).Target = "property";
        parameter.AddAttribute(TypeDefinition.Get("Sample", "StringLengthAttribute"),
            new CodeOutputComponent("1") { Indented = false },
            new CodeOutputComponent("100") { Indented = false }).Target = "property";

        Assert.Contains(
            "public record Pet([property: Required] [property: StringLength(1, 100)] string Name)",
            Write(classDefinition));
    }

    [Fact]
    public void ArgumentsAreWrittenOnAParameterAttribute()
    {
        var classDefinition = new ClassDefinition("Service");
        var method = classDefinition.AddMethod("Handle");

        method.AddParameter(typeof(string), "value")
            .AddAttribute(TypeDefinition.Get("Sample", "RangeAttribute"),
                new CodeOutputComponent("1") { Indented = false },
                new CodeOutputComponent("10") { Indented = false });

        Assert.Contains("Handle([Range(1, 10)] string value)", Write(classDefinition));
    }

    /// <summary>
    /// A target on an attribute written in its own right, rather than inline on a parameter.
    /// </summary>
    [Fact]
    public void TheTargetIsWrittenOnAStandaloneAttributeToo()
    {
        var classDefinition = new ClassDefinition("Thing");

        classDefinition.AddAttribute(TypeDefinition.Get("Sample", "SerializableAttribute")).Target = "type";

        Assert.Contains("[type: Serializable]", Write(classDefinition));
    }

    [Fact]
    public void AnAttributeWithNoTargetIsUnchanged()
    {
        var classDefinition = new ClassDefinition("Thing");

        classDefinition.AddAttribute(TypeDefinition.Get("Sample", "SerializableAttribute"));

        var output = Write(classDefinition);

        Assert.Contains("[Serializable]", output);
        Assert.DoesNotContain(":", output);
    }

    private static string Write(ClassDefinition classDefinition)
    {
        var context = new OutputContext();

        classDefinition.WriteOutput(context);

        return context.Output();
    }
}
