using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

public class WriteShortNameTests
{
    [Fact]
    public void WriteGenericShortName()
    {
        var typeDefinition = TypeDefinition.Get(typeof(Task<string>));

        var stringBuilder = new StringBuilder();

        typeDefinition.WriteShortName(stringBuilder);

        Assert.Equal("Task<string>", stringBuilder.ToString());
    }
}