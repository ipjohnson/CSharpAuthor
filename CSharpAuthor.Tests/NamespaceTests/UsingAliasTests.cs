using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.NamespaceTests;

/// <summary>
/// <see cref="CSharpFileDefinition.AddUsingAlias"/> and <see cref="NamespaceDefinition.AddUsingAlias"/>:
/// where the directive is written, and that the types written under it still bind to the types the
/// tree named.
/// </summary>
/// <remarks>
/// Every binding case is compiled together with <see cref="Support"/>, whose checks only compile if
/// each property has the type the tree gave it. A property captured by the alias has the alias's
/// target instead, and the check reports CS0029.
/// </remarks>
public class UsingAliasTests
{
    private const string Types = @"
namespace Probe.Origin
{
    public class Result { }
}

namespace Probe.Imported
{
    public class Result { }
}

namespace Shadow.Models
{
    public class Item { }
}
";

    private const string Support = Types + @"
public static class BindingCheck
{
    public static global::Probe.Origin.Result Value(global::Sample.Holder h) => h.Value;

    public static global::System.Collections.Generic.List<global::Probe.Origin.Result> Values(global::Sample.Holder h) => h.Values;
}
";

    private static readonly ITypeDefinition OriginResult = TypeDefinition.Get("Probe.Origin", "Result");

    private static readonly ITypeDefinition ImportedResult = TypeDefinition.Get("Probe.Imported", "Result");

    private static readonly ITypeDefinition ShadowItem = TypeDefinition.Get("Shadow.Models", "Item");

    private static ITypeDefinition ListOf(ITypeDefinition argument) =>
        new GenericTypeDefinition(typeof(List<>), new[] { argument });

    private static string Render(CSharpFileDefinition file, TypeOutputMode mode) =>
        Emit.File(file, new OutputContextOptions { TypeOutputMode = mode });

    /// <summary>
    /// A file whose Holder writes <c>Probe.Origin.Result</c> under a caller alias of
    /// <paramref name="aliasName"/>, declared at file level or inside <c>namespace Sample</c>.
    /// </summary>
    private static CSharpFileDefinition BindingFile(
        string aliasName, ITypeDefinition aliasTarget, bool atFileLevel, bool fileScoped = false)
    {
        var file = new CSharpFileDefinition(atFileLevel ? "Sample" : "") { FileScopedNamespace = fileScoped };

        ClassDefinition holder;

        if (atFileLevel)
        {
            file.AddUsingAlias(aliasName, aliasTarget);
            holder = file.AddClass("Holder");
        }
        else
        {
            var sample = new NamespaceDefinition("Sample") { FileScopedNamespace = fileScoped };
            sample.AddUsingAlias(aliasName, aliasTarget);
            file.AddComponent(sample);
            holder = sample.AddClass("Holder");
        }

        holder.AddProperty(OriginResult, "Value");
        holder.AddProperty(ListOf(OriginResult), "Values");

        return file;
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName)]
    [InlineData(TypeOutputMode.FullName)]
    [InlineData(TypeOutputMode.Global)]
    public void FileAliasIsWrittenWithTheOtherUsingsAndQualifiedInEveryMode(TypeOutputMode mode)
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingAlias("Results", ListOf(OriginResult));

        var holder = file.AddClass("Holder");
        holder.AddProperty(typeof(System.Text.StringBuilder), "Builder");
        holder.AddMethod("Fill").AddCode(
            "Results values = new global::System.Collections.Generic.List<global::Probe.Origin.Result>();");

        var output = Render(file, mode);

        const string directive =
            "using Results = global::System.Collections.Generic.List<global::Probe.Origin.Result>;\n";

        Assert.Contains(directive, output);
        Assert.True(
            output.IndexOf(directive, StringComparison.Ordinal) <
            output.IndexOf("namespace Sample", StringComparison.Ordinal));

        if (mode == TypeOutputMode.ShortName)
        {
            Assert.StartsWith("using System.Text;\n" + directive + "\nnamespace Sample", output);
        }
        else
        {
            Assert.StartsWith(directive + "\nnamespace Sample", output);
        }

        RoslynAssert.CompilesWith(output, Types);
    }

    [Fact]
    public void FileAliasIsWrittenWhenExplicitUsingsAreOff()
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingAlias("Item", ShadowItem);
        file.AddClass("Holder");

        var output = Emit.File(
            file,
            new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global, EmitExplicitUsings = false });

        Assert.StartsWith("using Item = global::Shadow.Models.Item;\n\nnamespace Sample", output);
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, false)]
    [InlineData(TypeOutputMode.ShortName, true)]
    [InlineData(TypeOutputMode.FullName, false)]
    [InlineData(TypeOutputMode.FullName, true)]
    [InlineData(TypeOutputMode.Global, false)]
    [InlineData(TypeOutputMode.Global, true)]
    public void NamespaceAliasIsWrittenAfterTheNamespaceUsings(TypeOutputMode mode, bool fileScoped)
    {
        var file = new CSharpFileDefinition();
        var sample = new NamespaceDefinition("Sample") { FileScopedNamespace = fileScoped };
        file.AddComponent(sample);

        sample.AddUsingInsideNamespace("System.Text");
        sample.AddUsingAlias("Results", ListOf(OriginResult));
        sample.AddClass("Holder").AddMethod("Fill").AddCode(
            "Results values = new global::System.Collections.Generic.List<global::Probe.Origin.Result>();");

        var output = Render(file, mode);

        const string directives =
            "using System.Text;\n" +
            "using Results = global::System.Collections.Generic.List<global::Probe.Origin.Result>;\n\n";

        if (fileScoped)
        {
            Assert.StartsWith("namespace Sample;\n\n" + directives + "public class Holder\n", output);
        }
        else
        {
            Assert.StartsWith(
                "namespace Sample\n{\n" + directives.Replace("using ", "    using ") + "    public class Holder\n",
                output);
        }

        RoslynAssert.CompilesWith(output, Types);
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, true)]
    [InlineData(TypeOutputMode.ShortName, false)]
    [InlineData(TypeOutputMode.FullName, true)]
    [InlineData(TypeOutputMode.FullName, false)]
    [InlineData(TypeOutputMode.Global, true)]
    [InlineData(TypeOutputMode.Global, false)]
    public void TrackedTypeWithTheAliasesShortNameBindsToTheIntendedType(TypeOutputMode mode, bool atFileLevel)
    {
        var file = BindingFile("Result", ImportedResult, atFileLevel);

        var output = Render(file, mode);

        if (mode == TypeOutputMode.FullName)
        {
            Assert.Contains("public Probe.Origin.Result Value", output);
        }
        else
        {
            Assert.Contains("public global::Probe.Origin.Result Value", output);
            Assert.Contains(
                "public global::System.Collections.Generic.List<global::Probe.Origin.Result> Values", output);
        }

        Assert.DoesNotContain("using Probe.Origin;", output);
        Assert.Contains("using Result = global::Probe.Imported.Result;", output);
        RoslynAssert.CompilesWith(output, Support);
    }

    /// <summary>
    /// The control for the test above: the short name it avoided does bind to the alias, so the
    /// support code can tell the two apart.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShortNameUnderTheAliasWouldBindToTheAliasTarget(bool atFileLevel)
    {
        var output = Render(BindingFile("Result", ImportedResult, atFileLevel), TypeOutputMode.ShortName);

        var captured = "using Probe.Origin;\nusing System.Collections.Generic;\n" +
                       output.Replace("global::System.Collections.Generic.List<", "List<")
                           .Replace("global::Probe.Origin.Result", "Result");

        var errors = RoslynAssert.Errors(captured + Support);

        Assert.Equal(2, errors.Count(e => e.Id == "CS0029"));
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, true)]
    [InlineData(TypeOutputMode.ShortName, false)]
    [InlineData(TypeOutputMode.FullName, true)]
    [InlineData(TypeOutputMode.FullName, false)]
    [InlineData(TypeOutputMode.Global, true)]
    [InlineData(TypeOutputMode.Global, false)]
    public void TrackedTypeWhoseFirstNamespaceSegmentIsTheAliasBindsToTheIntendedType(
        TypeOutputMode mode, bool atFileLevel)
    {
        var output = Render(BindingFile("Probe", ShadowItem, atFileLevel), mode);

        Assert.Contains("public global::Probe.Origin.Result Value", output);
        Assert.Contains(
            "public global::System.Collections.Generic.List<global::Probe.Origin.Result> Values", output);
        Assert.Contains("using Probe = global::Shadow.Models.Item;", output);
        RoslynAssert.CompilesWith(output, Support);
    }

    /// <summary>
    /// The control for the FullName case above: <c>Probe.Origin.Result</c> written plainly does not
    /// reach the type the tree named.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FullNameUnderTheAliasWouldNotReachTheIntendedType(bool atFileLevel)
    {
        var output = Render(BindingFile("Probe", ShadowItem, atFileLevel), TypeOutputMode.FullName);

        var captured = output.Replace("global::Probe.Origin.Result", "Probe.Origin.Result");

        Assert.NotEmpty(RoslynAssert.Errors(captured + Support));
    }

    /// <summary>
    /// <c>OtherAttribute</c> only goes on methods, so a class attribute that binds to an alias of it
    /// fails with CS0592 even where the name is not ambiguous.
    /// </summary>
    private const string AttributeTypes = @"
namespace Probe.Attrs
{
    public class MarkAttribute : global::System.Attribute { }
}

namespace Probe.Other
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
    public class OtherAttribute : global::System.Attribute { }
}
";

    private static readonly ITypeDefinition MarkAttribute = TypeDefinition.Get("Probe.Attrs", "MarkAttribute");

    private static readonly ITypeDefinition OtherAttribute = TypeDefinition.Get("Probe.Other", "OtherAttribute");

    private static CSharpFileDefinition AttributeFile(string aliasName, bool atFileLevel)
    {
        var file = new CSharpFileDefinition(atFileLevel ? "Sample" : "");

        ClassDefinition holder;

        if (atFileLevel)
        {
            file.AddUsingAlias(aliasName, OtherAttribute);
            holder = file.AddClass("Holder");
        }
        else
        {
            var sample = new NamespaceDefinition("Sample");
            sample.AddUsingAlias(aliasName, OtherAttribute);
            file.AddComponent(sample);
            holder = sample.AddClass("Holder");
        }

        holder.AddAttribute(MarkAttribute);

        return file;
    }

    [Theory]
    [InlineData(TypeOutputMode.ShortName, "Mark", true)]
    [InlineData(TypeOutputMode.ShortName, "Mark", false)]
    [InlineData(TypeOutputMode.ShortName, "MarkAttribute", true)]
    [InlineData(TypeOutputMode.ShortName, "MarkAttribute", false)]
    [InlineData(TypeOutputMode.FullName, "Mark", true)]
    [InlineData(TypeOutputMode.FullName, "Mark", false)]
    [InlineData(TypeOutputMode.Global, "Mark", true)]
    [InlineData(TypeOutputMode.Global, "Mark", false)]
    public void AttributeWithTheAliasesNameBindsToTheIntendedType(
        TypeOutputMode mode, string aliasName, bool atFileLevel)
    {
        var output = Render(AttributeFile(aliasName, atFileLevel), mode);

        Assert.Contains(
            mode == TypeOutputMode.FullName ? "[Probe.Attrs.Mark]" : "[global::Probe.Attrs.Mark]", output);
        Assert.DoesNotContain("using Probe.Attrs;", output);
        Assert.Contains("using " + aliasName + " = global::Probe.Other.OtherAttribute;", output);
        RoslynAssert.CompilesWith(output, AttributeTypes);
    }

    /// <summary>
    /// The control for the test above: the bracket name it avoided does not compile under the alias.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BracketNameUnderTheAliasDoesNotBindToTheIntendedType(bool atFileLevel)
    {
        var output = Render(AttributeFile("Mark", atFileLevel), TypeOutputMode.ShortName);

        var captured = "using Probe.Attrs;\n" + output.Replace("[global::Probe.Attrs.Mark]", "[Mark]");

        var errors = RoslynAssert.Errors(captured + AttributeTypes);

        Assert.Contains(errors, e => e.Id is "CS1614" or "CS0592");
    }

    /// <summary>
    /// The check is made at serialization, so a file written in Global mode and switched to
    /// FullName still qualifies what the alias could capture.
    /// </summary>
    [Fact]
    public void ModeChangedAfterWritingStillQualifies()
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        BindingFile("Probe", ShadowItem, atFileLevel: false).WriteOutput(context);

        context.Options.TypeOutputMode = TypeOutputMode.FullName;

        var output = context.Output();

        Assert.Contains("public global::Probe.Origin.Result Value", output);
        Assert.Contains("using Probe = global::Shadow.Models.Item;", output);
        RoslynAssert.CompilesWith(output, Support);
    }

    [Fact]
    public void NamespaceAliasEndsWithItsDeclaration()
    {
        var file = new CSharpFileDefinition();

        var inner = new NamespaceDefinition("Sample.Inner");
        inner.AddUsingAlias("Result", ImportedResult);
        inner.AddClass("Holder").AddProperty(OriginResult, "Value");

        file.AddComponent(inner);

        var after = new NamespaceDefinition("Sample");
        var holder = after.AddClass("Holder");
        holder.AddProperty(OriginResult, "Value");
        holder.AddProperty(ListOf(OriginResult), "Values");
        file.AddComponent(after);

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.Contains("public global::Probe.Origin.Result Value", output);
        Assert.Contains("public Result Value", output);
        Assert.StartsWith("using Probe.Origin;\n", output);
        RoslynAssert.CompilesWith(output, Support);
    }

    /// <summary>
    /// A type with no namespace cannot be told apart from the alias itself, so it is written as
    /// given - which is how a copied statement names the alias.
    /// </summary>
    [Theory]
    [InlineData(TypeOutputMode.ShortName)]
    [InlineData(TypeOutputMode.FullName)]
    public void TypeWithNoNamespaceNamesTheAlias(TypeOutputMode mode)
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingAlias("Imported", ImportedResult);
        file.AddClass("Holder").AddProperty(TypeDefinition.Get("", "Imported"), "ViaAlias");

        var output = Render(file, mode);

        Assert.Contains("public Imported ViaAlias", output);
        RoslynAssert.CompilesWith(
            output,
            Types + "public static class AliasCheck { public static global::Probe.Imported.Result Get(global::Sample.Holder h) => h.ViaAlias; }\n");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GeneratedCollisionAliasNeverTakesACallerAliasName(bool atFileLevel)
    {
        static CSharpFileDefinition CollidingFile(string? callerAlias, bool atFileLevel)
        {
            var file = new CSharpFileDefinition(atFileLevel ? "Sample" : "");
            var sample = atFileLevel ? null : new NamespaceDefinition("Sample");

            if (callerAlias != null)
            {
                if (atFileLevel)
                {
                    file.AddUsingAlias(callerAlias, ShadowItem);
                }
                else
                {
                    sample!.AddUsingAlias(callerAlias, ShadowItem);
                }
            }

            var holder = atFileLevel ? file.AddClass("Holder") : sample!.AddClass("Holder");
            holder.AddProperty(TypeDefinition.Get("Alpha.Models", "Widget"), "First");
            holder.AddProperty(TypeDefinition.Get("Beta.Models", "Widget"), "Second");

            if (sample != null)
            {
                file.AddComponent(sample);
            }

            return file;
        }

        const string widgets = @"
namespace Alpha.Models { public class Widget { } }
namespace Beta.Models { public class Widget { } }
namespace Shadow.Models { public class Item { } }
";

        var generated = GeneratedAliases(Render(CollidingFile(null, atFileLevel), TypeOutputMode.ShortName));

        Assert.NotEmpty(generated);

        foreach (var taken in generated)
        {
            var output = Render(CollidingFile(taken, atFileLevel), TypeOutputMode.ShortName);

            Assert.Contains("using " + taken + " = global::Shadow.Models.Item;", output);
            Assert.DoesNotContain(taken, GeneratedAliases(output));
            RoslynAssert.CompilesWith(output, widgets);
        }
    }

    private static List<string> GeneratedAliases(string output) =>
        Regex.Matches(output, @"^\s*using (\w+) = (?!global::)", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();

    [Fact]
    public void KeywordAliasNameIsWrittenWithAnAt()
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingAlias("class", ImportedResult);
        file.AddClass("Holder").AddMethod("Fill").AddCode("global::Probe.Imported.Result value = new @class();");

        var inner = new NamespaceDefinition("Inner");
        inner.AddUsingAlias("@event", OriginResult);
        file.AddComponent(inner);

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.Contains("using @class = global::Probe.Imported.Result;\n", output);
        Assert.Contains("    using @event = global::Probe.Origin.Result;\n", output);
        RoslynAssert.CompilesWith(output, Types);
    }

    [Fact]
    public void SameNameAndTargetIsWrittenOnceAndADifferentTargetThrows()
    {
        var file = new CSharpFileDefinition("Sample");
        file.AddUsingAlias("Result", ImportedResult);
        file.AddUsingAlias("Result", TypeDefinition.Get("Probe.Imported", "Result"));

        Assert.Throws<InvalidOperationException>(() => file.AddUsingAlias("Result", OriginResult));

        var sample = new NamespaceDefinition("Sample");
        sample.AddUsingAlias("Result", ImportedResult);
        sample.AddUsingAlias("Result", ImportedResult);

        Assert.Throws<InvalidOperationException>(() => sample.AddUsingAlias("Result", OriginResult));

        var output = Render(file, TypeOutputMode.ShortName);

        Assert.Single(Regex.Matches(output, "using Result = "));
    }

    [Fact]
    public void BadArgumentsAreRefused()
    {
        var file = new CSharpFileDefinition("Sample");

        Assert.Throws<ArgumentException>(() => file.AddUsingAlias("", ImportedResult));
        Assert.Throws<ArgumentException>(() => file.AddUsingAlias("A.B", ImportedResult));
        Assert.Throws<ArgumentException>(() => file.AddUsingAlias("1st", ImportedResult));
        Assert.Throws<ArgumentNullException>(() => file.AddUsingAlias("Result", null!));
        Assert.Throws<InvalidOperationException>(
            () => new NamespaceDefinition().AddUsingAlias("Result", ImportedResult));
    }
}
