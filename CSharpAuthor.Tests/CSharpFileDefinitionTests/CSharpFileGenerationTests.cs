using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.CSharpFileDefinitionTests
{
    public class CSharpFileGenerationTests
    {
        [Fact]
        public void SimpleCSharpFile()
        {
            var file = new CSharpFileDefinition("TestNamespace");

            var classDefinition = file.AddClass("TestClass");

            var method = classDefinition.AddMethod("SomeMethod");

            classDefinition.AddUsingNamespace("TestingNamespace");
            
            var outputContext = new OutputContext();

            file.WriteOutput(outputContext);

            var outputString = outputContext.Output();
            
            AssertEqual.WithoutNewLine(_expectedCSharpFile, outputString);
        }

        private const string _expectedCSharpFile =
@"using TestingNamespace;

namespace TestNamespace
{
    public class TestClass
    {

        public void SomeMethod()
        {
        }
    }
}
";
    }
}
