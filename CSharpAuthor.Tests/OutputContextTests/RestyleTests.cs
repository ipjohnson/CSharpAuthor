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
    /// A file needs no directive naming its own namespace: everything in it is already in scope.
    /// </summary>
    [Fact]
    public void TheFilesOwnNamespaceNeedsNoDirective()
    {
        var file = new CSharpFileDefinition("Sample.Models");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddParameter(TypeDefinition.Get("Sample.Models", "Request"), "request");
        method.SetReturnType(TypeDefinition.Get("Sample.Other", "Result"));

        var output = Write(file, new OutputContextOptions());

        Assert.DoesNotContain("using Sample.Models;", output);
        Assert.Contains("using Sample.Other;", output);
        Assert.Contains("Result Handle(Request request)", output);
    }

    /// <summary>
    /// The same for a caller writing into a context directly, where nothing declares a namespace
    /// for the context to notice.
    /// </summary>
    [Fact]
    public void TheContainingNamespaceCanAlsoBeNamedOnTheOptions()
    {
        var context = new OutputContext(new OutputContextOptions { ContainingNamespace = "Sample.Models" });

        context.Write(TypeDefinition.Get("Sample.Models", "Request"));
        context.WriteLine();
        context.Write(TypeDefinition.Get("Sample.Other", "Result"));
        context.GenerateUsingStatements();

        var output = context.Output();

        Assert.DoesNotContain("using Sample.Models;", output);
        Assert.Contains("using Sample.Other;", output);
    }

    /// <summary>
    /// A namespace segment that is a keyword is written the same way on both sides.
    /// </summary>
    /// <remarks>
    /// The declaration was escaped and the directive was not, so a file naming a namespace called
    /// <c>event</c> emitted <c>using Company.event.Models;</c> - CS1001 - above a namespace
    /// declaration that was correct.
    /// </remarks>
    [Fact]
    public void AKeywordNamespaceSegmentIsEscapedInTheDirectiveToo()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddParameter(TypeDefinition.Get("Company.event.Models", "Payload"), "payload");

        var output = Write(file, new OutputContextOptions());

        Assert.Contains("using Company.@event.Models;", output);
        Assert.DoesNotContain("using Company.event.Models;", output);
    }

    [Fact]
    public void AnAliasWithAKeywordSegmentIsEscapedOnBothSides()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddParameter(TypeDefinition.Get("First", "Model"), "a");
        method.AddParameter(TypeDefinition.Get("Company.event", "Model"), "b");

        var output = Write(file, new OutputContextOptions());

        Assert.Contains("using eventModel = Company.@event.Model;", output);
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
