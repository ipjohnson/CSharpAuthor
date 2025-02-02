using Xunit;

namespace CSharpAuthor.Tests.Traits;

public class EnableNullableTests
{
    [Fact]
    public void EnableNullableTest()
    {
        var method = new MethodDefinition("Test");
        
        method.EnableNullable();

        var context = new OutputContext();
        
        method.WriteOutput(context);
        
        AssertEqual.WithoutNewLine(Expected, context.Output());
    }
    
    private const string Expected = @"#nullable enable
public void Test()
{
}
#nullable disable
";
}