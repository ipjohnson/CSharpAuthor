using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

/// <summary>
/// Indentation, line endings and brace placement are decided when the file is serialized, not when
/// it is written.
/// </summary>
/// <remarks>
/// A scope is recorded as a marker and an indent as a depth, so the same components, writing the
/// same thing, produce a file in whatever shape the host project keeps its code in. Nothing in the
/// tree changes between one style and the next.
/// </remarks>
public class RestyleTests
{
    [Fact]
    public void TheIndentWidthIsAppliedAtSerialization()
    {
        var output = Write(new OutputContextOptions { IndentCharCount = 2 });

        Assert.Contains("\n  public class Service\n", output);
        Assert.Contains("\n    public void Handle()\n", output);
    }

    [Fact]
    public void TabsIndentJustAsWell()
    {
        var output = Write(new OutputContextOptions { IndentChar = '\t', IndentCharCount = 1 });

        Assert.Contains("\n\tpublic class Service\n", output);
        Assert.Contains("\n\t\tpublic void Handle()\n", output);
    }

    [Fact]
    public void TheBraceJoinsTheLineWhenTheStyleSaysSo()
    {
        var output = Write(new OutputContextOptions { BraceStyle = BraceStyle.KAndR });

        Assert.Contains("public class Service {\n", output);
        Assert.Contains("public void Handle() {\n", output);
        Assert.DoesNotContain("Service\n    {", output);
    }

    [Fact]
    public void TheBraceIsOnItsOwnLineByDefault()
    {
        var output = Write(new OutputContextOptions());

        Assert.Contains("public class Service\n    {\n", output);
    }

    [Fact]
    public void TheLineEndingIsAppliedAtSerialization()
    {
        var output = Write(new OutputContextOptions { NewLine = "\r\n" });

        Assert.Contains("public class Service\r\n", output);

        // Every line break is the one that was asked for, with none left over from anywhere else.
        Assert.DoesNotContain("\n", output.Replace("\r\n", ""));
    }

    /// <summary>
    /// The file's own namespace needs no directive. Off unless it is set, because dropping one a
    /// caller was relying on is worse than leaving a redundant one in.
    /// </summary>
    [Fact]
    public void TheFilesOwnNamespaceIsDroppedWhenItIsNamed()
    {
        var file = new CSharpFileDefinition("Sample.Models");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddParameter(TypeDefinition.Get("Sample.Models", "Request"), "request");
        method.SetReturnType(TypeDefinition.Get("Sample.Other", "Result"));

        var withName = Write(file, new OutputContextOptions { ContainingNamespace = "Sample.Models" });

        Assert.DoesNotContain("using Sample.Models;", withName);
        Assert.Contains("using Sample.Other;", withName);

        var without = Write(file, new OutputContextOptions());

        Assert.Contains("using Sample.Models;", without);
    }

    private static string Write(OutputContextOptions options)
    {
        var file = new CSharpFileDefinition("TestNamespace");

        file.AddClass("Service").AddMethod("Handle");

        return Write(file, options);
    }

    private static string Write(CSharpFileDefinition file, OutputContextOptions options)
    {
        var context = new OutputContext(options);

        file.WriteOutput(context);

        return context.Output();
    }
}
