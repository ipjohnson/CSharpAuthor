using Xunit;

namespace CSharpAuthor.Tests.CSharpFileDefinitionTests;

public class AddRecordTests
{
    [Fact]
    public void AddRecordToFile()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        file.FileScopedNamespace = true;

        var record = file.AddRecord("Person");
        record.Modifiers |= ComponentModifier.Sealed;

        var nameProp = record.AddProperty(TypeDefinition.Get(typeof(string)), "Name");
        nameProp.Set!.IsInit = true;
        nameProp.DefaultValue = new CodeOutputComponent("default!") { Indented = false };

        var ageProp = record.AddProperty(TypeDefinition.Get(typeof(int)), "Age");
        ageProp.Set!.IsInit = true;

        var context = new OutputContext();
        file.WriteOutput(context);

        AssertEqual.WithoutNewLine(AddRecordOutput, context.Output());
    }

    private const string AddRecordOutput =
        @"namespace TestNamespace;

public sealed record Person
{
    public string Name { get; init; } = default!;

    public int Age { get; init; }
}
";

    [Fact]
    public void AddRecordToNamespace()
    {
        var ns = new NamespaceDefinition("TestNamespace");
        ns.FileScopedNamespace = true;

        var record = ns.AddRecord("Item");
        record.AddProperty(TypeDefinition.Get(typeof(string)), "Value");

        var context = new OutputContext();
        ns.WriteOutput(context);

        AssertEqual.WithoutNewLine(AddRecordToNsOutput, context.Output());
    }

    private const string AddRecordToNsOutput =
        @"namespace TestNamespace;

public record Item
{
    public string Value { get; set; }
}
";
}
