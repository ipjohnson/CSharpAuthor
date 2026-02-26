using Xunit;

namespace CSharpAuthor.Tests.PropertyDefinitionTests;

public class InitPropertyTests
{
    [Fact]
    public void InitProperty()
    {
        var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(string)), "Name");
        propertyDefinition.Set!.IsInit = true;

        var context = new OutputContext();
        propertyDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(InitPropertyOutput, context.Output());
    }

    private const string InitPropertyOutput =
        @"public string Name { get; init; }
";

    [Fact]
    public void InitPropertyWithDefaultValue()
    {
        var propertyDefinition = new PropertyDefinition(TypeDefinition.Get(typeof(string)), "Name");
        propertyDefinition.Set!.IsInit = true;
        propertyDefinition.DefaultValue = new CodeOutputComponent("default!") { Indented = false };

        var context = new OutputContext();
        propertyDefinition.WriteOutput(context);

        AssertEqual.WithoutNewLine(InitPropertyWithDefaultOutput, context.Output());
    }

    private const string InitPropertyWithDefaultOutput =
        @"public string Name { get; init; } = default!;
";
}
