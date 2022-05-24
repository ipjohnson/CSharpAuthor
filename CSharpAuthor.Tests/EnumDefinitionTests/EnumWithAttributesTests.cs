using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpAuthor.Tests.Models;
using Xunit;

namespace CSharpAuthor.Tests.EnumDefinitionTests
{
    public class EnumWithAttributesTests
    {

        [Fact]
        public void CreateAttributedEnum()
        {
            var enumDefinition = new EnumDefinition("Test").AddFlags();

            enumDefinition.AddAttribute(typeof(GeneratedCodeAttribute));

            enumDefinition.AddValue("Value1").AddAttribute(typeof(GetAttribute), "Path = \"/1\"");
            enumDefinition.AddValue("Value2").AddAttribute(typeof(GetAttribute), "Path = \"/2\"");
            enumDefinition.AddValue("Value3").AddAttribute(typeof(GetAttribute), "Path = \"/3\"");

            var outputContext = new OutputContext();

            enumDefinition.WriteOutput(outputContext);

            var enumDefString = outputContext.Output();

            Assert.Equal(AttributedEnum, enumDefString);
        }

        private const string AttributedEnum =
            @"[Flags]
[GeneratedCode]
public enum Test
{
    [Get(Path = ""/1"")]
    Value1,
    [Get(Path = ""/2"")]
    Value2,
    [Get(Path = ""/3"")]
    Value3,
}
";

    }
}
