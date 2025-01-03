using Xunit;

namespace CSharpAuthor.Tests.SyntaxTests;

public class IsTests
{
    [Fact]
    public void BasicIsTest()
    {
        var isStatement = 
            SyntaxHelpers.Is(CodeOutputComponent.Get("obj"), TypeDefinition.Get(typeof(object)));

        var output = new OutputContext();
        isStatement.WriteOutput(output);
        
        var stringOutput = output.Output();
        
        Assert.Equal("obj is object", stringOutput);
    }
}