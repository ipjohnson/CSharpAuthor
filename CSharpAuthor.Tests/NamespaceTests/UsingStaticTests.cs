using System;
using System.Collections.Generic;
using System.Linq;
using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.NamespaceTests;

/// <summary>
/// <see cref="CSharpFileDefinition.AddUsingStatic"/> and <see cref="NamespaceDefinition.AddUsingStatic"/>:
/// where the directive is written, that a static member of the target is reached by its bare name,
/// and that the types written under it still bind to the types the tree named.
/// </summary>
/// <remarks>
/// <c>Probe.Statics.Helpers</c> has a nested <c>Item</c>, which the directive imports along with
/// its static members. The binding cases compile together with <see cref="Support"/>, whose checks
/// only compile if each property has the type the tree gave it.
/// </remarks>
public class UsingStaticTests
{
    private const string Types = @"
namespace Probe.Models
{
    public class Item { }
}

namespace Probe.Statics
{
    public static class Helpers
    {
        public static int Twice(int value) => value * 2;

        public class Item { }
    }
}

namespace Sample
{
    public class Widget { }
}
";

    private const string Support = Types + @"
public static class BindingCheck
{
    public static global::Probe.Models.Item Value(global::Sample.Consumer c) => c.Value;

    public static global::System.Collections.Generic.List<global::Probe.Models.Item> Values(global::Sample.Consumer c) => c.Values;

    public static global::Sample.Widget Widget(global::Sample.Consumer c) => c.Widget;
}
";

    private static readonly ITypeDefinition ModelsItem = TypeDefinition.Get("Probe.Models", "Item");

    private static readonly ITypeDefinition Helpers = TypeDefinition.Get("Probe.Statics", "Helpers");

    private static readonly ITypeDefinition SampleWidget = TypeDefinition.Get("Sample", "Widget");

    private static ITypeDefinition ComparerOf(ITypeDefinition argument) =>
        new GenericTypeDefinition(typeof(Comparer<>), new[] { argument });

    private static string Render(CSharpFileDefinition file, TypeOutputMode mode) =>
        Emit.File(file, new OutputContextOptions { TypeOutputMode = mode });

    [Theory]
    [InlineData(TypeOutputMode.ShortName)]
    [InlineData(TypeOutputMode.FullName)]
    [InlineData(TypeOutputMode.Global)]
    public void FileUsingStaticIsWrittenQualifiedAndReachesStaticMembersByBareName(TypeOutputMode mode)
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingStatic(Helpers);
        file.AddUsingStatic(ComparerOf(ModelsItem));
        file.AddUsingStatic(TypeDefinition.Get(typeof(Math)));

        file.AddClass("Consumer").AddMethod("Run").AddCode(
            "var doubled = Twice(Max(1, 2));\nvar comparer = Default;");

        var output = Render(file, mode);

        const string directives =
            "using static global::Probe.Statics.Helpers;\n" +
            "using static global::System.Collections.Generic.Comparer<global::Probe.Models.Item>;\n" +
            "using static global::System.Math;\n\n";

        Assert.StartsWith(directives + "namespace Sample", output);
        RoslynAssert.CompilesWith(output, Types);
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, false)]
    [InlineData(TypeOutputMode.ShortName, true)]
    [InlineData(TypeOutputMode.FullName, false)]
    [InlineData(TypeOutputMode.FullName, true)]
    [InlineData(TypeOutputMode.Global, false)]
    [InlineData(TypeOutputMode.Global, true)]
    public void NamespaceUsingStaticIsWrittenBetweenTheUsingsAndTheAliases(TypeOutputMode mode, bool fileScoped)
    {
        var file = new CSharpFileDefinition();
        var sample = new NamespaceDefinition("Sample") { FileScopedNamespace = fileScoped };
        file.AddComponent(sample);

        sample.AddUsingAlias("Models", ModelsItem);
        sample.AddUsingStatic(ComparerOf(ModelsItem));
        sample.AddUsingInsideNamespace("System.Text");
        sample.AddUsingStatic(Helpers);
        sample.AddClass("Consumer").AddMethod("Run").AddCode(
            "var doubled = Twice(2);\nvar comparer = Default;\nModels item = new Models();");

        var output = Render(file, mode);

        const string directives =
            "using System.Text;\n" +
            "using static global::System.Collections.Generic.Comparer<global::Probe.Models.Item>;\n" +
            "using static global::Probe.Statics.Helpers;\n" +
            "using Models = global::Probe.Models.Item;\n\n";

        if (fileScoped)
        {
            Assert.StartsWith("namespace Sample;\n\n" + directives + "public class Consumer\n", output);
        }
        else
        {
            Assert.StartsWith(
                "namespace Sample\n{\n" + directives.Replace("using ", "    using ") + "    public class Consumer\n",
                output);
        }

        RoslynAssert.CompilesWith(output, Types);
    }

    /// <summary>
    /// A file whose Consumer writes <c>Probe.Models.Item</c> and <c>Sample.Widget</c> under
    /// <c>using static Probe.Statics.Helpers</c>, declared at file level or inside
    /// <c>namespace Sample</c>.
    /// </summary>
    private static CSharpFileDefinition BindingFile(bool atFileLevel, bool fileScoped = false)
    {
        var file = new CSharpFileDefinition(atFileLevel ? "Sample" : "") { FileScopedNamespace = fileScoped };

        ClassDefinition consumer;

        if (atFileLevel)
        {
            file.AddUsingStatic(Helpers);
            consumer = file.AddClass("Consumer");
        }
        else
        {
            var sample = new NamespaceDefinition("Sample") { FileScopedNamespace = fileScoped };
            sample.AddUsingStatic(Helpers);
            file.AddComponent(sample);
            consumer = sample.AddClass("Consumer");
        }

        consumer.AddProperty(ModelsItem, "Value");
        consumer.AddProperty(new GenericTypeDefinition(typeof(List<>), new[] { ModelsItem }), "Values");
        consumer.AddProperty(SampleWidget, "Widget");
        consumer.AddMethod("Run").AddCode("var doubled = Twice(2);");

        return file;
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, true, false)]
    [InlineData(TypeOutputMode.ShortName, true, true)]
    [InlineData(TypeOutputMode.ShortName, false, false)]
    [InlineData(TypeOutputMode.ShortName, false, true)]
    [InlineData(TypeOutputMode.FullName, true, false)]
    [InlineData(TypeOutputMode.FullName, false, false)]
    [InlineData(TypeOutputMode.Global, true, false)]
    [InlineData(TypeOutputMode.Global, false, false)]
    public void TrackedTypeNamedLikeANestedTypeOfTheTargetBindsToTheIntendedType(
        TypeOutputMode mode, bool atFileLevel, bool fileScoped)
    {
        var output = Render(BindingFile(atFileLevel, fileScoped), mode);

        switch (mode)
        {
            case TypeOutputMode.ShortName:
                Assert.Contains("public global::Probe.Models.Item Value", output);
                Assert.Contains(
                    "public global::System.Collections.Generic.List<global::Probe.Models.Item> Values", output);
                Assert.Contains("public Widget Widget", output);
                Assert.DoesNotContain("using Probe.Models;", output);
                break;
            case TypeOutputMode.FullName:
                Assert.Contains("public Probe.Models.Item Value", output);
                break;
            default:
                Assert.Contains("public global::Probe.Models.Item Value", output);
                break;
        }

        Assert.Contains("using static global::Probe.Statics.Helpers;", output);
        RoslynAssert.CompilesWith(output, Support);
    }

    /// <summary>
    /// The control for the test above: written by its short name, <c>Item</c> is ambiguous with the
    /// nested type at file level, and binds to the nested type inside the namespace.
    /// </summary>
    [Theory]
    [InlineData(true, "CS0104")]
    [InlineData(false, "CS0029")]
    public void ShortNameUnderTheUsingStaticWouldNotBindToTheIntendedType(bool atFileLevel, string error)
    {
        var output = Render(BindingFile(atFileLevel), TypeOutputMode.ShortName);

        var captured = "using Probe.Models;\nusing System.Collections.Generic;\n" +
                       output.Replace("global::System.Collections.Generic.List<", "List<")
                           .Replace("global::Probe.Models.Item", "Item");

        var errors = RoslynAssert.Errors(captured + Support);

        Assert.Contains(errors, e => e.Id == error);
    }

    [Fact]
    public void FileUsingStaticIsWrittenWhenExplicitUsingsAreOff()
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingStatic(Helpers);
        file.AddClass("Consumer");

        var output = Emit.File(
            file,
            new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global, EmitExplicitUsings = false });

        Assert.StartsWith("using static global::Probe.Statics.Helpers;\n\nnamespace Sample", output);
    }

    [Fact]
    public void TheSameTargetTwiceIsWrittenOnce()
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingStatic(Helpers);
        file.AddUsingStatic(TypeDefinition.Get("Probe.Statics", "Helpers"));

        var sample = new NamespaceDefinition("Inner");
        sample.AddUsingStatic(Helpers);
        sample.AddUsingStatic(Helpers);
        file.AddComponent(sample);

        var output = Render(file, TypeOutputMode.Global);

        Assert.Equal(2, output.Split(new[] { "using static " }, StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void NullTargetAndUnnamedNamespaceThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new CSharpFileDefinition("Sample").AddUsingStatic(null!));
        Assert.Throws<ArgumentNullException>(() => new NamespaceDefinition("Sample").AddUsingStatic(null!));
        Assert.Throws<InvalidOperationException>(() => new NamespaceDefinition().AddUsingStatic(Helpers));
    }
}
