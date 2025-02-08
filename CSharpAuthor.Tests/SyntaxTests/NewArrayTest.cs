using Xunit;

namespace CSharpAuthor.Tests.SyntaxTests;

public class NewArrayTest
{
    [Fact]
    public void NewArrayInitStatement()
    {
        var array = SyntaxHelpers.NewArray(typeof(int), 1, 2, 3);
        
        var context = new OutputContext();
        
        array.WriteOutput(context);
        var output = context.Output();
        
        Assert.Equal("new int[] { 1, 2, 3 }", output);
    }
}