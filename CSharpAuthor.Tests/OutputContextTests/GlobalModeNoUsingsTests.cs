using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

/// <summary>
/// A file that qualifies every type it writes needs no <c>using</c> derived from those types.
/// </summary>
/// <remarks>
/// It used to emit them anyway, because the writers declared the namespaces they needed on the side
/// rather than being read off what they wrote, and that declaration ran whatever the mode was. The
/// two halves held each other up: a name that carried no namespace of its own resolved <em>because
/// of</em> a directive that should not have been there. Removing either alone breaks the file, so
/// both go at once - the namespaces are derived from the types written, and anything written as a
/// bare string is given a type to be derived from.
/// </remarks>
public class GlobalModeNoUsingsTests
{
    [Fact]
    public void NoUsingIsDerivedFromAQualifiedType()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("Microsoft.Extensions.DependencyInjection", "IServiceCollection"), "services");
        method.SetReturnType(TypeDefinition.Get("Sample.Models", "Result"));

        var output = Write(file, TypeOutputMode.Global);

        Assert.DoesNotContain("using ", output);
        Assert.Contains("global::Microsoft.Extensions.DependencyInjection.IServiceCollection services", output);
        Assert.Contains("global::Sample.Models.Result Handle", output);
    }

    [Fact]
    public void AFieldTypeDerivesNoUsingEither()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        classDefinition.AddField(TypeDefinition.Get("Sample.Models", "Result"), "_result");

        var output = Write(file, TypeOutputMode.Global);

        Assert.DoesNotContain("using ", output);
        Assert.Contains("global::Sample.Models.Result _result", output);
    }

    /// <summary>
    /// An attribute is qualified with everything else, rather than written bare and left to a
    /// directive to resolve.
    /// </summary>
    [Fact]
    public void AnAttributeIsQualifiedAndDerivesNoUsing()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        classDefinition.AddAttribute(
            TypeDefinition.Get("System.Diagnostics.CodeAnalysis", "ExcludeFromCodeCoverageAttribute"));

        var output = Write(file, TypeOutputMode.Global);

        Assert.DoesNotContain("using ", output);
        Assert.Contains("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]", output);
    }

    [Fact]
    public void AnInlineParameterAttributeIsQualifiedToo()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(typeof(string), "value")
            .AddAttribute(TypeDefinition.Get("Sample", "NotNullAttribute"));

        var output = Write(file, TypeOutputMode.Global);

        Assert.DoesNotContain("using ", output);
        Assert.Contains("[global::Sample.NotNull] string value", output);
    }

    /// <summary>
    /// A namespace asked for by name is kept, because qualification cannot stand in for it: an
    /// extension method is found through a using and no other way.
    /// </summary>
    [Fact]
    public void ANamespaceAskedForByNameSurvives()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        classDefinition.AddUsingNamespace("Microsoft.Extensions.DependencyInjection.Extensions");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("Microsoft.Extensions.DependencyInjection", "IServiceCollection"), "services");

        var output = Write(file, TypeOutputMode.Global);

        Assert.Contains("using Microsoft.Extensions.DependencyInjection.Extensions;", output);
        Assert.DoesNotContain("using Microsoft.Extensions.DependencyInjection;", output);
    }

    [Fact]
    public void NotEvenTheExplicitOnesWhenTheOptionSaysSo()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        classDefinition.AddUsingNamespace("Microsoft.Extensions.DependencyInjection.Extensions");

        var context = new OutputContext(new OutputContextOptions
        {
            TypeOutputMode = TypeOutputMode.Global,
            EmitExplicitUsings = false,
        });

        file.WriteOutput(context);

        Assert.DoesNotContain("using ", context.Output());
    }

    /// <summary>
    /// The same rule in <see cref="TypeOutputMode.FullName"/>, which also names its own namespaces.
    /// </summary>
    [Fact]
    public void FullNameModeDerivesNoUsingEither()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("Sample.Models", "Result"), "result");

        var output = Write(file, TypeOutputMode.FullName);

        Assert.DoesNotContain("using ", output);
        Assert.Contains("Sample.Models.Result result", output);
    }

    /// <summary>
    /// A writer written against version 1 that still declares its namespaces by hand no longer puts
    /// a directive in a file that qualifies everything.
    /// </summary>
    [Fact]
    public void AHandDeclaredTypeNamespaceIsIgnoredInAQualifyingMode()
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        context.AddImportNamespace(TypeDefinition.Get("Sample.Models", "Result"));
        context.WriteIndentedLine("// body");
        context.GenerateUsingStatements();

        Assert.DoesNotContain("using ", context.Output());
    }

    private static string Write(CSharpFileDefinition file, TypeOutputMode mode)
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });

        file.WriteOutput(context);

        return context.Output();
    }
}
