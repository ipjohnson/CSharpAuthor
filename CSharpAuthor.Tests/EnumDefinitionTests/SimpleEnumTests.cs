using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.EnumDefinitionTests;

public class SimpleEnumTests
{
    [Fact]
    public void CreateSimpleIntEnum()
    {
        var enumDefinition = new EnumDefinition("Test");

        enumDefinition.AddValue("Value1");
        enumDefinition.AddValue("Value2");
        enumDefinition.AddValue("Value3");

        var outputContext = new OutputContext();

        enumDefinition.WriteOutput(outputContext);

        var enumDefString = outputContext.Output();

        AssertEqual.WithoutNewLine(SimpleIntEnum, enumDefString);
    }

    private const string SimpleIntEnum =
        @"public enum Test
{
    Value1,
    Value2,
    Value3,
}
";
        
}