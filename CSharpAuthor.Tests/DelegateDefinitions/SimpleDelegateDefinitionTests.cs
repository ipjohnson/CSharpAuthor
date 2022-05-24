using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.DelegateDefinitions
{
    public class SimpleDelegateDefinitionTests
    {
        [Fact]
        public void SimpleDelegateDefinition()
        {
            var definition = new DelegateDefinition("Test");

            definition.SetReturnType(typeof(string));

            definition.AddParameter(typeof(int), "intValue");
            definition.AddParameter(typeof(string), "stringValue");

            var outputContext = new OutputContext();

            definition.WriteOutput(outputContext);

            var definitionString = outputContext.Output();

            Assert.Equal(SimpleDelegateDefinitionString, definitionString);
        }

        private const string SimpleDelegateDefinitionString = 
            @"public delegate string Test(int intValue, string stringValue);
";
    }
}
