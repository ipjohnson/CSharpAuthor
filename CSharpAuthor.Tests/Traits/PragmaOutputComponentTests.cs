using Xunit;

namespace CSharpAuthor.Tests.Traits;

public class PragmaOutputComponentTests
{
    [Fact]
    public void PragmaTest()
    {
        var classDefinition = new ClassDefinition("Test");

        classDefinition.AddLeadingTrait(new PragmaOutputComponent(false, "1998"));
        classDefinition.AddTrailingTrait(new PragmaOutputComponent(true, "1998"));
        
        var outputContext = new OutputContext();

        classDefinition.WriteOutput(outputContext);

        var output = outputContext.Output();

        var expected = @"#pragma warning disable 1998
public class Test
{
}
#pragma warning restore 1998
";
        AssertEqual.WithoutNewLine(expected, output);
    }
}