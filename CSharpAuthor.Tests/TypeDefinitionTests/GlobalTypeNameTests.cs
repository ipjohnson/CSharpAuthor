using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests;

public class GlobalTypeNameTests
{
    [Fact]
    public void WriteGenericGlobalTypeName()
    {
        var typeDefinition = TypeDefinition.Get(typeof(Task<string>));

        var stringBuilder = new StringBuilder();

        typeDefinition.WriteTypeName(stringBuilder, TypeOutputMode.Global);

        Assert.Equal("global::System.Threading.Tasks.Task<string>", stringBuilder.ToString());
    }
}