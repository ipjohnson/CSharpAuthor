using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpAuthor.Tests.Models;
using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests
{
    public class ClassDefinitionGenerationTests
    {
        [Fact]
        public void SimpleClassGeneration()
        {
            var classDefinition = new ClassDefinition("TestClass");

            classDefinition.AddAttribute(typeof(GeneratedCodeAttribute));

            classDefinition.AddField(typeof(string), "testField");

            classDefinition.AddMethod("TestMethod").AddAttribute(typeof(GetAttribute), "Path = \"/Test\"");

            var outputContext = new OutputContext();

            classDefinition.WriteOutput(outputContext);

            var outputString = outputContext.Output();

            AssertEqual.WithoutNewLine(expectedSimpleClass, outputString);
        }

        private static readonly string expectedSimpleClass =
@"[GeneratedCode]
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
}
