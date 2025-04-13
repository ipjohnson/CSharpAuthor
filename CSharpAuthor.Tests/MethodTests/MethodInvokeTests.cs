using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

public class MethodInvokeTests
{
    [Fact]
    public void MethodInvokeTest()
    {
        var method = new MethodDefinition("Test");

        var helloVar = method.Assign(
            SyntaxHelpers.QuoteString("Hello")).ToVar("helloVar");
        
        method.AddIndentedStatement(helloVar.Invoke("ToString", "1", "2", "3"));
        
        var output = new OutputContext();
        
        method.WriteOutput(output);
        
        var stringOutput = output.Output();
        
        AssertEqual.WithoutNewLine(assertValue, stringOutput);
    }

    private string assertValue = @"public void Test()
{
    var helloVar = ""Hello"";
    helloVar.ToString(
        1, 
        2, 
        3
    );
}
";
}