using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

public class GenericMethodTests
{
    [Fact]
    public void GenericMethodTest()
    {
        var method = new MethodDefinition("Test");
        
        method.AddGenericParameter(TypeDefinition.Get("","T"));

        var context = new OutputContext();
        
        method.WriteOutput(context);
        AssertEqual.WithoutNewLine(ExpectedOutput, context.Output());
    }

    private string ExpectedOutput = @"public void Test<T>()
{
}
";
}