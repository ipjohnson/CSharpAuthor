using System;
using System.Collections.Generic;
using System.Linq;
using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.NamespaceTests;

/// <summary>
/// <see cref="NamespaceDefinition.AddUsingInsideNamespace"/>: where the directive is written, and
/// that the types written under it still bind to the types the tree named.
/// </summary>
/// <remarks>
/// Every binding case is compiled together with <see cref="Support"/>, whose checks only compile if
/// each property has the type the tree gave it. A property captured by the namespace-level using
/// has the other <c>Result</c>, and the check reports CS0029.
/// </remarks>
public class NamespaceUsingTests
{
    private const string Support = @"
namespace Probe.Origin
{
    public class Result { }
}

namespace Probe.Imported
{
    public class Result { }

    public class Local { }

    public class Close { }
}

namespace Sample
{
    public class Local { }
}

namespace Sample.Inner
{
    public class Close { }
}

public static class BindingCheck
{
    public static global::Probe.Origin.Result Value(global::Sample.Holder h) => h.Value;

    public static global::System.Collections.Generic.List<global::Probe.Origin.Result> Values(global::Sample.Holder h) => h.Values;

    public static global::Sample.Local Near(global::Sample.Holder h) => h.Near;
}
";

    private const string NestedSupport = @"
namespace Probe.Imported
{
    public class Local { }

    public class Close { }
}

namespace Sample
{
    public class Local { }
}

namespace Sample.Inner
{
    public class Close { }
}

public static class BindingCheck
{
    public static global::Sample.Local Near(global::Sample.Inner.Holder h) => h.Near;

    public static global::Sample.Inner.Close Close(global::Sample.Inner.Holder h) => h.Close;

    public static global::Sample.Local After(global::Sample.After a) => a.Near;
}
";

    private static readonly ITypeDefinition OriginResult = TypeDefinition.Get("Probe.Origin", "Result");

    private static readonly ITypeDefinition SampleLocal = TypeDefinition.Get("Sample", "Local");

    private static string Render(CSharpFileDefinition file, TypeOutputMode mode) =>
        Emit.File(file, new OutputContextOptions { TypeOutputMode = mode });

    private static CSharpFileDefinition BindingFile(bool fileScoped = false)
    {
        var file = new CSharpFileDefinition("Sample") { FileScopedNamespace = fileScoped };

        file.AddUsingInsideNamespace("Probe.Imported");

        var holder = file.AddClass("Holder");

        holder.AddProperty(OriginResult, "Value");
        holder.AddProperty(new GenericTypeDefinition(typeof(List<>), new[] { OriginResult }), "Values");
        holder.AddProperty(SampleLocal, "Near");

        return file;
    }

    [Fact]
    public void UsingIsWrittenInsideTheBracesBeforeTheFirstMember()
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingInsideNamespace("System.Text");
        file.AddClass("Holder").AddProperty(typeof(int), "Count");

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.StartsWith(
            "namespace Sample\n{\n    using System.Text;\n\n    public class Holder\n", output);
        RoslynAssert.Compiles(output);
    }

    [Fact]
    public void UsingIsWrittenAfterTheFileScopedNamespaceLine()
    {
        var file = new CSharpFileDefinition("Sample") { FileScopedNamespace = true };
        file.AddUsingInsideNamespace("System.Text");
        file.AddClass("Holder").AddProperty(typeof(int), "Count");

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.StartsWith("namespace Sample;\n\nusing System.Text;\n\npublic class Holder\n", output);
        RoslynAssert.Compiles(output);
    }

    [Fact]
    public void UsingsAreWrittenOnceInTheOrderTheyWereAdded()
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingInsideNamespace("System.Text");
        file.AddUsingInsideNamespace("System.Collections");
        file.AddUsingInsideNamespace("System.Text");
        file.AddClass("Holder");

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.StartsWith(
            "namespace Sample\n{\n    using System.Text;\n    using System.Collections;\n\n    public class Holder\n",
            output);
    }

    [Fact]
    public void KeywordSegmentIsEscaped()
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingInsideNamespace("Probe.event.Models");
        file.AddClass("Holder");

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.Contains("    using Probe.@event.Models;\n", output);
        RoslynAssert.CompilesWith(output, "namespace Probe.@event.Models { public class Marker { } }\n");
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, false)]
    [InlineData(TypeOutputMode.ShortName, true)]
    [InlineData(TypeOutputMode.FullName, false)]
    [InlineData(TypeOutputMode.FullName, true)]
    [InlineData(TypeOutputMode.Global, false)]
    [InlineData(TypeOutputMode.Global, true)]
    public void TrackedTypesBindToTheTypesTheTreeNamed(TypeOutputMode mode, bool fileScoped)
    {
        var output = Render(BindingFile(fileScoped), mode);

        switch (mode)
        {
            case TypeOutputMode.ShortName:
                Assert.Contains("public global::Probe.Origin.Result Value", output);
                Assert.Contains(
                    "public global::System.Collections.Generic.List<global::Probe.Origin.Result> Values", output);
                // Sample's own members are found before the declaration's usings are consulted.
                Assert.Contains("public Local Near", output);
                Assert.DoesNotContain("using Probe.Origin;", output);
                break;

            case TypeOutputMode.FullName:
                Assert.Contains("public Probe.Origin.Result Value", output);
                Assert.Contains("public Sample.Local Near", output);
                break;

            default:
                Assert.Contains("public global::Probe.Origin.Result Value", output);
                Assert.Contains("public global::Sample.Local Near", output);
                break;
        }

        Assert.Contains("using Probe.Imported;", output);
        RoslynAssert.CompilesWith(output, Support);
    }

    /// <summary>
    /// The control for the test above: the short name it avoided does bind to the wrong type, so
    /// the support code can tell the two apart.
    /// </summary>
    [Fact]
    public void ShortNameUnderTheUsingWouldBindToTheImportedType()
    {
        var output = Render(BindingFile(), TypeOutputMode.ShortName);

        var captured = "using Probe.Origin;\nusing System.Collections.Generic;\n\n" +
                       output.Replace("global::System.Collections.Generic.List<", "List<")
                           .Replace("global::Probe.Origin.Result", "Result");

        var errors = RoslynAssert.Errors(captured + Support);

        Assert.Equal(2, errors.Count(e => e.Id == "CS0029"));
    }

    [Fact]
    public void NestedDeclarationQualifiesWhatItsUsingCouldCaptureAndScopeEndsWithIt()
    {
        var file = new CSharpFileDefinition("Sample");

        var inner = new NamespaceDefinition("Inner");
        inner.AddUsingInsideNamespace("Probe.Imported");

        var holder = inner.AddClass("Holder");
        holder.AddProperty(SampleLocal, "Near");
        holder.AddProperty(TypeDefinition.Get("Sample.Inner", "Close"), "Close");

        file.AddComponent(inner);
        file.AddClass("After").AddProperty(SampleLocal, "Near");

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.Contains("        using Probe.Imported;\n", output);
        Assert.Contains("public global::Sample.Local Near", output);
        Assert.Contains("public Close Close", output);
        Assert.Contains("public Local Near", output);
        RoslynAssert.CompilesWith(output, NestedSupport);
    }

    /// <summary>
    /// The decision is made at serialization, so a context written in a qualifying mode and then
    /// switched to short names still qualifies what the using could capture.
    /// </summary>
    [Fact]
    public void ModeChangedAfterWritingStillQualifies()
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        BindingFile().WriteOutput(context);

        context.Options.TypeOutputMode = TypeOutputMode.ShortName;

        var output = context.Output();

        Assert.Contains("public global::Probe.Origin.Result Value", output);
        Assert.Contains("public Local Near", output);
        RoslynAssert.CompilesWith(output, Support);
    }

    [Fact]
    public void UnnamedNamespaceRefusesAUsingItHasNowhereToPut()
    {
        Assert.Throws<InvalidOperationException>(
            () => new NamespaceDefinition().AddUsingInsideNamespace("System.Text"));
        Assert.Throws<InvalidOperationException>(
            () => new CSharpFileDefinition().AddUsingInsideNamespace("System.Text"));
        Assert.Throws<ArgumentException>(
            () => new NamespaceDefinition("Sample").AddUsingInsideNamespace(""));
    }
}
