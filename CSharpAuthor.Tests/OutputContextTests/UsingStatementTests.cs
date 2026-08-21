using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

public class UsingStatementTests
{
    /// <summary>
    /// <see cref="CSharpFileDefinition"/> already generates them, and nothing at the call site
    /// says so - so calling it again used to emit the whole block twice.
    /// </summary>
    [Fact]
    public void GeneratingTwiceDoesNotDuplicateTheBlock()
    {
        var file = new CSharpFileDefinition("Sample");

        file.AddClass("Holder").AddMethod("Work")
            .SetReturnType(TypeDefinition.Get("System.Text", "StringBuilder"));

        var context = new OutputContext();

        file.WriteOutput(context);
        context.GenerateUsingStatements();
        context.GenerateUsingStatements();

        var output = context.Output();

        Assert.Equal(1, output.Split(new[] { "using System.Text;" }, System.StringSplitOptions.None).Length - 1);
    }
}
