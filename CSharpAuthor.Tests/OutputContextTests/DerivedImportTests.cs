using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

/// <summary>
/// A type reaches the file only by being written, and the file's <c>using</c> list is read off the
/// types it wrote.
/// </summary>
/// <remarks>
/// Nothing declares a namespace any more, so nothing can forget to: a missing directive stops being
/// a mistake that is possible to make. These are the positions that used to declare one by hand.
/// </remarks>
public class DerivedImportTests
{
    [Fact]
    public void AParameterTypeBringsItsNamespace()
    {
        Assert.Contains("using Sample.Models;", WriteMethod(
            method => method.AddParameter(TypeDefinition.Get("Sample.Models", "Request"), "request")));
    }

    [Fact]
    public void AReturnTypeBringsItsNamespace()
    {
        Assert.Contains("using Sample.Models;", WriteMethod(
            method => method.SetReturnType(TypeDefinition.Get("Sample.Models", "Result"))));
    }

    [Fact]
    public void AnExplicitInterfaceImplementationBringsItsNamespace()
    {
        Assert.Contains("using Sample.Contracts;", WriteMethod(
            method => method.InterfaceImplementation = TypeDefinition.Get("Sample.Contracts", "IHandler")));
    }

    [Fact]
    public void AFieldTypeBringsItsNamespace()
    {
        var file = new CSharpFileDefinition("TestNamespace");

        file.AddClass("Service").AddField(TypeDefinition.Get("Sample.Models", "Result"), "_result");

        Assert.Contains("using Sample.Models;", Write(file));
    }

    [Fact]
    public void AnEventHandlerTypeBringsItsNamespace()
    {
        var file = new CSharpFileDefinition("TestNamespace");

        file.AddClass("Service").AddEvent(TypeDefinition.Get("Sample.Events", "ChangedHandler"), "Changed");

        Assert.Contains("using Sample.Events;", Write(file));
    }

    [Fact]
    public void AnAttributeTypeBringsItsNamespace()
    {
        var file = new CSharpFileDefinition("TestNamespace");

        file.AddClass("Service").AddAttribute(TypeDefinition.Get("Sample.Annotations", "MarkerAttribute"));

        var output = Write(file);

        Assert.Contains("using Sample.Annotations;", output);
        Assert.Contains("[Marker]", output);
    }

    [Fact]
    public void AGenericArgumentBringsItsNamespaceAsWell()
    {
        Assert.Contains("using Sample.Models;", WriteMethod(
            method => method.SetReturnType(
                TypeDefinition.IEnumerable(TypeDefinition.Get("Sample.Models", "Result")))));
    }

    /// <summary>
    /// A type held but never written brings nothing. The list follows the file, not the model.
    /// </summary>
    [Fact]
    public void ATypeThatIsNeverWrittenBringsNothing()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        classDefinition.AddMethod("Handle").SetReturnType(TypeDefinition.Get("Sample.Models", "Result"));

        // Held by a definition that is not part of the file.
        var orphan = new FieldDefinition(TypeDefinition.Get("Sample.Unused", "Never"), "_never");

        Assert.NotNull(orphan);

        var output = Write(file);

        Assert.Contains("using Sample.Models;", output);
        Assert.DoesNotContain("using Sample.Unused;", output);
    }

    /// <summary>A keyword type has no namespace and brings none.</summary>
    [Fact]
    public void AKeywordTypeBringsNothing()
    {
        var output = WriteMethod(method => method.AddParameter(typeof(int), "count"));

        Assert.DoesNotContain("using ;", output);
        Assert.Contains("Handle(int count)", output);
    }

    private static string WriteMethod(System.Action<MethodDefinition> build)
    {
        var file = new CSharpFileDefinition("TestNamespace");

        build(file.AddClass("Service").AddMethod("Handle"));

        return Write(file);
    }

    private static string Write(CSharpFileDefinition file)
    {
        var context = new OutputContext();

        file.WriteOutput(context);

        return context.Output();
    }
}
