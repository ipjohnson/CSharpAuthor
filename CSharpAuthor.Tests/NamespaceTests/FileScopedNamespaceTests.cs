using Xunit;

namespace CSharpAuthor.Tests.NamespaceTests;

public class FileScopedNamespaceTests
{
    [Fact]
    public void FileScopedNamespace()
    {
        var ns = new NamespaceDefinition("TestNamespace");
        ns.FileScopedNamespace = true;

        var classDefinition = ns.AddClass("TestClass");
        classDefinition.AddMethod("DoWork");

        var context = new OutputContext();
        ns.WriteOutput(context);

        AssertEqual.WithoutNewLine(FileScopedOutput, context.Output());
    }

    private const string FileScopedOutput =
        @"namespace TestNamespace;

public class TestClass
{

    public void DoWork()
    {
    }
}
";

    [Fact]
    public void FileScopedNamespaceViaFile()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        file.FileScopedNamespace = true;

        var classDefinition = file.AddClass("TestClass");

        var context = new OutputContext();
        file.WriteOutput(context);

        AssertEqual.WithoutNewLine(FileScopedFileOutput, context.Output());
    }

    private const string FileScopedFileOutput =
        @"namespace TestNamespace;

public class TestClass
{
}
";
}
