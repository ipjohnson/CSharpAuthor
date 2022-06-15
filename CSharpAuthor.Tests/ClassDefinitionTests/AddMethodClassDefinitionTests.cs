using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests
{
    public class AddMethodClassDefinitionTests
    {
        [Fact]
        public void ClassWithPropertyTest()
        {
            var classDefinition = new ClassDefinition("Test");

            var property = classDefinition.AddProperty(typeof(int), "IntValue");

            property.Get.LambdaSyntax = true;
            property.Get.AddCode("_intValue");

            var outputContext = new OutputContext();

            classDefinition.WriteOutput(outputContext);
        }

        private const string ClassWithPropertyExpected =
@"public class Test
{
    public int IntValue => _intValue;
}";
    }
}
