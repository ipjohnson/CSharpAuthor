using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

/// <summary>
/// Base types that take constructor arguments.
/// </summary>
/// <remarks>
/// A base list used to be bare type names, so a derived record could name its base but not pass
/// anything to it - which meant <c>record Dog(string Id) : Pet(Id)</c> could not be written at all
/// and a generator emitting a hierarchy had to give up positional records for init-only
/// properties.
/// </remarks>
public class BaseTypeArgumentTests
{
    [Fact]
    public void ARecordPassesItsParametersToItsBase()
    {
        var classDefinition = new ClassDefinition("Dog") {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true,
            Modifiers = ComponentModifier.Public
        };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");
        constructor.AddParameter(typeof(string), "Breed");

        classDefinition.AddBaseType(
            TypeDefinition.Get("Pets", "Pet"), Argument("Id"));

        AssertEqual.WithoutNewLine(
            "public record Dog(string Id, string Breed) : Pet(Id);\n", Write(classDefinition));
    }

    [Fact]
    public void SeveralArgumentsAreCommaSeparated()
    {
        var classDefinition = new ClassDefinition("Dog") {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true,
            Modifiers = ComponentModifier.Public
        };

        var constructor = classDefinition.AddConstructor();
        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");
        constructor.AddParameter(typeof(string), "Name");

        classDefinition.AddBaseType(
            TypeDefinition.Get("Pets", "Pet"), Argument("Id"), Argument("Name"));

        AssertEqual.WithoutNewLine(
            "public record Dog(string Id, string Name) : Pet(Id, Name);\n", Write(classDefinition));
    }

    /// <summary>
    /// A base type with no arguments writes exactly as it did before, which is what keeps every
    /// interface in a base list unaffected.
    /// </summary>
    [Fact]
    public void ABaseTypeWithoutArgumentsIsUnchanged()
    {
        var classDefinition = new ClassDefinition("Dog") {
            Modifiers = ComponentModifier.Public
        };

        classDefinition.AddBaseType(TypeDefinition.Get("Pets", "Pet"));

        AssertEqual.WithoutNewLine("public class Dog : Pet\n{\n}\n", Write(classDefinition));
    }

    /// <summary>
    /// The base class carries the arguments; interfaces alongside it do not.
    /// </summary>
    [Fact]
    public void OnlyTheEntryCarryingArgumentsGetsThem()
    {
        var classDefinition = new ClassDefinition("Dog") {
            Modifiers = ComponentModifier.Public
        };

        classDefinition.AddBaseType(TypeDefinition.Get("Pets", "Pet"), Argument("id"));
        classDefinition.AddBaseType(TypeDefinition.Get("Pets", "IAnimal"));

        AssertEqual.WithoutNewLine(
            "public class Dog : Pet(id), IAnimal\n{\n}\n", Write(classDefinition));
    }

    /// <summary>
    /// Adding the same base twice is still one entry, as it was before arguments existed.
    /// </summary>
    [Fact]
    public void ABaseTypeIsNotAddedTwice()
    {
        var classDefinition = new ClassDefinition("Dog") {
            Modifiers = ComponentModifier.Public
        };

        classDefinition.AddBaseType(TypeDefinition.Get("Pets", "Pet"));
        classDefinition.AddBaseType(TypeDefinition.Get("Pets", "Pet"), Argument("id"));

        AssertEqual.WithoutNewLine("public class Dog : Pet\n{\n}\n", Write(classDefinition));
    }

    private static IOutputComponent Argument(string text) =>
        new CodeOutputComponent(text) { Indented = false };

    private static string Write(ClassDefinition classDefinition)
    {
        var outputContext = new OutputContext();

        classDefinition.WriteOutput(outputContext);

        return outputContext.Output();
    }
}
