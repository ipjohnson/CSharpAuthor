using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests
{
    public class NestedClassDefinitionGenerationTests
    {
        [Fact]
        public void BasicNestedClassTest()
        {
            var classDefinition = new ClassDefinition("TestNamespace", "TestClass");

            classDefinition.AddField(typeof(string), "testField");

            classDefinition.AddMethod("TestMethod");

            var nestedClass = classDefinition.AddClass("NestedClass");

            nestedClass.AddField(typeof(string), "_field1");

            nestedClass.AddMethod("NestedTestMethod");
                
            var outputContext = new OutputContext();

            classDefinition.WriteOutput(outputContext);

            var outputString = outputContext.Output();

            AssertEqual.WithoutNewLine(expectedSimpleClass, outputString);
        }

        private static readonly string expectedSimpleClass =
@"namespace TestNamespace
{
    public class TestClass
    {
        private string testField;

        public void TestMethod()
        {
        }

        public class NestedClass
        {
            private string _field1;

            public void NestedTestMethod()
            {
            }
        }
    }
}
";
    }
}
