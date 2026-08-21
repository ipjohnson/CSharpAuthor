using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

/// <summary>
/// An expression node writes no terminator, because it does not know it is being used as a
/// statement. Adding one straight to a block emitted an unterminated line and the generated file
/// did not compile.
/// </summary>
public class StatementPositionTests
{
    private static string Write(IOutputComponent component)
    {
        var context = new OutputContext();

        component.WriteOutput(context);

        return context.Output();
    }

    [Fact]
    public void AddStatementIndentsAndTerminates()
    {
        var method = new MethodDefinition("Test");

        method.AddStatement(new InvokeDefinition("writer", "WriteStartObject"));

        AssertEqual.WithoutNewLine(
            "public void Test()\n{\n    writer.WriteStartObject();\n}\n", Write(method));
    }

    /// <summary>
    /// The wrapper writes the indent, so a component that would indent itself must not - otherwise
    /// the line is indented twice.
    /// </summary>
    [Fact]
    public void AddStatementDoesNotDoubleTheIndent()
    {
        var method = new MethodDefinition("Test");

        var forEach = method.ForEach("item", CodeOutputComponent.Get("items"));

        forEach.AddStatement(new InvokeDefinition("writer", "Write", CodeOutputComponent.Get("item")));

        AssertEqual.ContainsWithoutNewLine("        writer.Write(item);\n", Write(method));
    }

    [Fact]
    public void AddStatementReturnsTheComponentTyped()
    {
        var method = new MethodDefinition("Test");

        InvokeDefinition invoke = method.AddStatement(new InvokeDefinition("writer", "Flush"));

        Assert.NotNull(invoke);
    }

    /// <summary>
    /// Composes with the Invoke extension, so a call built as an expression can be placed as a
    /// statement without being rebuilt.
    /// </summary>
    [Fact]
    public void ComposesWithTheInvokeExtension()
    {
        var method = new MethodDefinition("Test");

        var writer = method.AddParameter(TypeDefinition.Get("System.Text.Json", "Utf8JsonWriter"), "writer");

        method.AddStatement(writer.Invoke("WriteEndObject"));

        AssertEqual.ContainsWithoutNewLine("    writer.WriteEndObject();\n", Write(method));
    }
}
