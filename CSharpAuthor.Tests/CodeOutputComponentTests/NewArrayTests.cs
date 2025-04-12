using Xunit;

namespace CSharpAuthor.Tests.CodeOutputComponentTests;

public class NewArrayTests
{
    [Fact]
    public void GetIntArray()
    {
        var output = CodeOutputComponent.Get(new [] { 1, 2, 3 });
        
        var context = new OutputContext();
        
        output.WriteOutput(context);
        
        Assert.Equal("new int[] { 1, 2, 3 }", context.Output());
    }
    
    [Fact]
    public void GetStringArray()
    {
        var output = CodeOutputComponent.Get(new [] { "Hello", "World" });
        
        var context = new OutputContext();
        
        output.WriteOutput(context);
        
        Assert.Equal("new string[] { \"Hello\", \"World\" }", context.Output());
    }
}