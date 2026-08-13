using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

/// <summary>
/// Primary constructors, and the <c>;</c> terminator that usually goes with them.
/// </summary>
public class PrimaryConstructorTests
{
    [Fact]
    public void APrimaryConstructorIsWrittenInTheTypeHeader()
    {
        var classDefinition = new ClassDefinition("Pet") { TypeKeyword = ClassKeyword.Record };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");

        AssertEqual.WithoutNewLine(HeaderOutput, Write(classDefinition));
    }

    private const string HeaderOutput = @"public record Pet(string Id)
{
}
";

    [Fact]
    public void TerminateWithSemicolonReplacesTheBody()
    {
        var classDefinition = new ClassDefinition("Pet") {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true,
            Modifiers = ComponentModifier.Public | ComponentModifier.Partial
        };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");

        AssertEqual.WithoutNewLine("public partial record Pet(string Id);\n", Write(classDefinition));
    }

    /// <summary>
    /// The shape the whole feature exists for: required parameters first, optional ones defaulted.
    /// </summary>
    [Fact]
    public void ParametersCarryTheirDefaults()
    {
        var classDefinition = new ClassDefinition("Pet") {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true,
            Modifiers = ComponentModifier.Public | ComponentModifier.Partial
        };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");
        constructor.AddParameter(typeof(string), "Name").DefaultValue = new CodeOutputComponent("default") { Indented = false };

        AssertEqual.WithoutNewLine(
            "public partial record Pet(string Id, string Name = default);\n", Write(classDefinition));
    }

    /// <summary>
    /// A primary constructor taking nothing is not the same declaration as no primary constructor,
    /// so the empty parameter list is still written.
    /// </summary>
    [Fact]
    public void AnEmptyPrimaryConstructorStillWritesItsParentheses()
    {
        var classDefinition = new ClassDefinition("Marker") {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true
        };

        classDefinition.AddConstructor().IsPrimary = true;

        AssertEqual.WithoutNewLine("public record Marker();\n", Write(classDefinition));
    }

    [Fact]
    public void AClassWithNoPrimaryConstructorIsUnchanged()
    {
        var classDefinition = new ClassDefinition("Plain");

        AssertEqual.WithoutNewLine("public class Plain\n{\n}\n", Write(classDefinition));
    }

    /// <summary>
    /// The primary constructor is the header; an ordinary one alongside it is still a member.
    /// </summary>
    [Fact]
    public void OrdinaryConstructorsAreStillWrittenAsMembers()
    {
        var classDefinition = new ClassDefinition("Pet") { TypeKeyword = ClassKeyword.Record };

        var primary = classDefinition.AddConstructor();
        primary.IsPrimary = true;
        primary.AddParameter(typeof(string), "Id");

        classDefinition.AddConstructor();

        var output = Write(classDefinition);

        Assert.Contains("public record Pet(string Id)", output);
        Assert.Contains("public Pet()", output);
    }

    [Fact]
    public void PrimaryConstructorParametersPrecedeBaseTypes()
    {
        var classDefinition = new ClassDefinition("Pet") { TypeKeyword = ClassKeyword.Record };
        classDefinition.AddBaseType(TypeDefinition.Get("Sample", "IAnimal"));

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");

        Assert.Contains("public record Pet(string Id) : IAnimal", Write(classDefinition));
    }

    /// <summary>
    /// Terminating with a semicolon is the caller's choice rather than something inferred from an
    /// empty member list, so a type that has members and asks for it gets what it asked for.
    /// </summary>
    [Fact]
    public void TerminateWithSemicolonAppliesToAnyType()
    {
        var classDefinition = new ClassDefinition("Marker") { TerminateWithSemicolon = true };

        AssertEqual.WithoutNewLine("public class Marker;\n", Write(classDefinition));
    }

    private static string Write(ClassDefinition classDefinition)
    {
        var context = new OutputContext();

        classDefinition.WriteOutput(context);

        return context.Output();
    }
}
