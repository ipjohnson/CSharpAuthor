using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.EnumDefinitionTests
{
    public class BaseEnumDefinitionTests
    {

        [Fact]
        public void CreateSimpleIntEnum()
        {
            var enumDefinition = new EnumDefinition("Test");

            enumDefinition.BaseType = TypeDefinition.Get(typeof(short));

            enumDefinition.AddValue("Value1");
            enumDefinition.AddValue("Value2");
            enumDefinition.AddValue("Value3");

            var outputContext = new OutputContext();

            enumDefinition.WriteOutput(outputContext);

            var enumDefString = outputContext.Output();

            AssertEqual.WithoutNewLine(SimpleShortEnum, enumDefString);
        }

        private const string SimpleShortEnum =
            @"public enum Test : short
{
    Value1,
    Value2,
    Value3,
}
";

    }
}
