using System;
using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

/// <summary>
/// <see cref="OutputContextOptions.NewLine"/> used to reach only the parameterless
/// <c>WriteLine()</c> and the using block. Everything else went through
/// <c>StringBuilder.AppendLine</c>, which appends <see cref="Environment.NewLine"/> - so the
/// setting was ignored and generated output varied with the operating system it was generated on.
/// </summary>
public class NewLineOptionTests
{
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void EveryLineEndsWithTheConfiguredNewLine(string newLine)
    {
        var method = new MethodDefinition("Boom");

        method.SetReturnType(TypeDefinition.Get(typeof(int)));
        method.Throw(typeof(Exception), SyntaxHelpers.QuoteString("bad"));
        method.Return(1);

        var context = new OutputContext(new OutputContextOptions { NewLine = newLine });

        method.WriteOutput(context);

        var expected =
            "public int Boom()" + newLine +
            "{" + newLine +
            "    throw new Exception(\"bad\");" + newLine +
            "    return 1;" + newLine +
            "}" + newLine;

        Assert.Equal(expected, context.Output());
    }

    /// <summary>
    /// A file, rather than a single member - the using block, namespace and class body each write
    /// their own line breaks.
    /// </summary>
    [Fact]
    public void FileOutputCarriesNoForeignLineEndings()
    {
        var file = new CSharpFileDefinition("Sample");
        var classDefinition = file.AddClass("Holder");

        classDefinition.AddField(TypeDefinition.Get(typeof(string)), "_name");
        classDefinition.AddMethod("Work").AddCode("Run();");
        classDefinition.AddUsingNamespace("System.Text");

        var context = new OutputContext(new OutputContextOptions { NewLine = "\r\n" });

        file.WriteOutput(context);

        var output = context.Output();

        Assert.DoesNotContain("\n", output.Replace("\r\n", ""));
    }

    [Fact]
    public void ThrowIsTerminatedLikeEveryOtherStatement()
    {
        var method = new MethodDefinition("Boom");

        method.Throw(TypeDefinition.Get(typeof(Exception)));

        var context = new OutputContext(new OutputContextOptions { NewLine = "\r\n" });

        method.WriteOutput(context);

        Assert.Contains("    throw new Exception();\r\n", context.Output());
    }
}
