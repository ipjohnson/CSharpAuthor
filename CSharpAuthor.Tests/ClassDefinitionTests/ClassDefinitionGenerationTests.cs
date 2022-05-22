using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests
{
    public class ClassDefinitionGenerationTests
    {
        [Fact]
        public void SimpleClassGeneration()
        {
            var classDefinition = new ClassDefinition("TestClass");

            classDefinition.AddField(typeof(string), "testField");

            classDefinition.AddMethod("TestMethod");

            var outputContext = new OutputContext();

            classDefinition.WriteOutput(outputContext);

            var outputString = outputContext.Output();

            AssertEqual.WithoutNewLine(expectedSimpleClass, outputString);
        }

        private static readonly string expectedSimpleClass =
@"public class TestClass
{
    private string testField;

    public void TestMethod()
    {
    }
}
";
    }
}
