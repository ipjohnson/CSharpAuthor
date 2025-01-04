using System.CodeDom.Compiler;
using CSharpAuthor.Tests.Models;
using static CSharpAuthor.SyntaxHelpers;
using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

public class ClassDefinitionGenerationTests
{
    [Fact]
    public void SimpleClassGeneration()
    {
        var classDefinition = new ClassDefinition("TestClass");

        classDefinition.AddAttribute(typeof(GeneratedCodeAttribute), QuoteString("Test"),QuoteString("1.0.0"));

        classDefinition.AddField(typeof(string), "testField");

        classDefinition.AddMethod("TestMethod").AddAttribute(typeof(GetAttribute), "Path = \"/Test\"");

        var outputContext = new OutputContext();

        classDefinition.WriteOutput(outputContext);

        var outputString = outputContext.Output();

        AssertEqual.WithoutNewLine(expectedSimpleClass, outputString);
    }

    private static readonly string expectedSimpleClass =
        @"[GeneratedCode(""Test"", ""1.0.0"")]
public class TestClass
{
    private string testField;

    [Get(Path = ""/Test"")]
    public void TestMethod()
    {
    }
}
";
}