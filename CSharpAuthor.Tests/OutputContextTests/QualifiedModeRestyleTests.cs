using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

/// <summary>
/// A mode that qualifies every type it writes goes straight into the output rather than into a
/// record - and indentation, line endings, brace placement and the output mode itself are still
/// decided when the file is serialized, not when it was written.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RestyleTests"/> makes that promise and sets its options before writing, which a
/// write-time styling would satisfy without keeping it. Everything here sets them <em>after</em>
/// the whole file has been written, which is the case that tells the two apart: a stream that
/// finds one of them moved is turned back into the record it would have been, and the ordinary
/// serializer answers.
/// </para>
/// <para>
/// The comparisons are against the same file written with those options from the start, so the
/// assertion is not "it looks right" but "it is the same string".
/// </para>
/// </remarks>
public class QualifiedModeRestyleTests
{
    [Fact]
    public void TheIndentWidthIsStillAppliedAtSerialization()
    {
        var context = Written(TypeOutputMode.Global);

        context.Options.IndentCharCount = 2;

        Assert.Equal(Expected(TypeOutputMode.Global, o => o.IndentCharCount = 2), context.Output());
    }

    [Fact]
    public void TheIndentCharacterIsStillAppliedAtSerialization()
    {
        var context = Written(TypeOutputMode.Global);

        context.Options.IndentChar = '\t';
        context.Options.IndentCharCount = 1;

        var output = context.Output();

        Assert.Equal(
            Expected(TypeOutputMode.Global, o => { o.IndentChar = '\t'; o.IndentCharCount = 1; }),
            output);
        Assert.Contains("\n\tpublic class Service\n", output);
    }

    [Fact]
    public void TheLineEndingIsStillAppliedAtSerialization()
    {
        var context = Written(TypeOutputMode.Global);

        context.Options.NewLine = "\r\n";

        var output = context.Output();

        Assert.Equal(Expected(TypeOutputMode.Global, o => o.NewLine = "\r\n"), output);

        // Every line break is the one that was asked for, with none left over from the write.
        Assert.DoesNotContain("\n", output.Replace("\r\n", ""));
    }

    [Fact]
    public void TheBracePlacementIsStillAppliedAtSerialization()
    {
        var context = Written(TypeOutputMode.Global);

        context.Options.BraceStyle = BraceStyle.KAndR;

        var output = context.Output();

        Assert.Equal(Expected(TypeOutputMode.Global, o => o.BraceStyle = BraceStyle.KAndR), output);
        Assert.Contains("public class Service {\n", output);
    }

    /// <summary>
    /// The one that decides everything else: the output mode is read when the file is serialized,
    /// so a file written qualifying its names can still come out by short name - with the using
    /// directives that then have to exist.
    /// </summary>
    [Fact]
    public void TheOutputModeItselfIsStillDecidedAtSerialization()
    {
        var context = Written(TypeOutputMode.Global);

        context.Options.TypeOutputMode = TypeOutputMode.ShortName;

        var output = context.Output();

        Assert.Equal(Expected(TypeOutputMode.ShortName, _ => { }), output);
        Assert.Contains("using Sample.Other;", output);
        Assert.Contains("Result Handle(Request request)", output);
        Assert.DoesNotContain("global::", output);
    }

    /// <summary>
    /// And the collision pass with it: two types contesting a name are only found once the whole
    /// file is known, which is as true of a file written in a qualifying mode as of any other.
    /// </summary>
    [Fact]
    public void ACollisionIsStillResolvedWhenTheModeChangesAtSerialization()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddParameter(TypeDefinition.Get("First", "Model"), "a");
        method.AddParameter(TypeDefinition.Get("Second", "Model"), "b");

        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        file.WriteOutput(context);

        context.Options.TypeOutputMode = TypeOutputMode.ShortName;

        var output = context.Output();

        Assert.Contains("using First;", output);
        Assert.Contains("using SecondModel = Second.Model;", output);
        Assert.Contains("Handle(Model a, SecondModel b)", output);
    }

    /// <summary>
    /// The style can also move part way through, which is what a caller writing into a context
    /// directly can do. The file still comes out in one style: the one in force at serialization.
    /// </summary>
    [Fact]
    public void AStyleChangedPartWayThroughStillStylesTheWholeFile()
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        context.WriteIndentedLine("class Service");
        context.OpenScope();
        context.WriteIndent();
        context.Write(TypeDefinition.Get("Sample.Models", "Result"));
        context.WriteLine(" Value;");

        context.Options.IndentCharCount = 2;

        context.WriteIndentedLine("// tail");
        context.CloseScope();

        Assert.Equal(
            "class Service\n{\n  global::Sample.Models.Result Value;\n  // tail\n}\n",
            context.Output());
    }

    /// <summary>
    /// A leading trait stays on line one, above the directives a qualifying file can still carry.
    /// </summary>
    [Fact]
    public void TheHeaderStaysAboveTheDirectives()
    {
        var file = new CSharpFileDefinition("TestNamespace");

        file.AddLeadingTrait(new CodeOutputComponent("// <auto-generated/>"));

        var classDefinition = file.AddClass("Service");

        classDefinition.AddUsingNamespace("Extension.Methods");
        classDefinition.AddMethod("Handle").AddParameter(TypeDefinition.Get("Sample.Models", "Result"), "r");

        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        file.WriteOutput(context);

        var output = context.Output();

        Assert.StartsWith("// <auto-generated/>", output);
        Assert.Contains("using Extension.Methods;", output);
        Assert.True(
            output.IndexOf("// <auto-generated/>", System.StringComparison.Ordinal) <
            output.IndexOf("using Extension.Methods;", System.StringComparison.Ordinal));
    }

    /// <summary>Asking twice answers twice, and answers the same thing.</summary>
    [Fact]
    public void TheOutputCanBeAskedForMoreThanOnce()
    {
        var context = Written(TypeOutputMode.Global);

        var first = context.Output();
        var second = context.Output();

        Assert.Equal(first, second);
    }

    /// <summary>
    /// And with directives to insert, which is the case that writes into the buffer it hands back.
    /// </summary>
    [Fact]
    public void TheOutputCanBeAskedForMoreThanOnceWithDirectives()
    {
        var file = new CSharpFileDefinition("TestNamespace");

        file.AddClass("Service").AddUsingNamespace("Extension.Methods");

        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        file.WriteOutput(context);

        var first = context.Output();
        var second = context.Output();

        Assert.Contains("using Extension.Methods;", first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// <see cref="OutputContext.LastCharacter"/> reads the same in either mode - it is how a
    /// component decides whether it owes the line a break.
    /// </summary>
    [Fact]
    public void TheLastCharacterReadsTheSameInAQualifyingMode()
    {
        var global = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });
        var shortName = new OutputContext(new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global, BraceStyle = BraceStyle.KAndR });

        Assert.Null(global.LastCharacter);
        Assert.Null(shortName.LastCharacter);

        foreach (var context in new[] { global, shortName })
        {
            context.Write("a");
            context.WriteSpace();
            context.WriteLine();
            context.OpenScope();
            context.Write(TypeDefinition.Get("Sample.Models", "Result"));
        }

        Assert.Equal(global.LastCharacter, shortName.LastCharacter);
        Assert.Equal('t', global.LastCharacter);
    }

    /// <summary>
    /// A file that qualifies its names and joins its braces is written exactly as it was before -
    /// the streaming path declines that style rather than reproducing its trimming.
    /// </summary>
    [Fact]
    public void JoinedBracesAreUnchangedInAQualifyingMode()
    {
        var output = Expected(TypeOutputMode.Global, o => o.BraceStyle = BraceStyle.KAndR);

        Assert.Contains("public class Service {\n", output);
        Assert.Contains("global::Sample.Other.Result Handle(global::Sample.Models.Request request)", output);
    }

    [Fact]
    public void FullNameModeRestylesJustTheSameWay()
    {
        var context = Written(TypeOutputMode.FullName);

        context.Options.IndentCharCount = 2;
        context.Options.NewLine = "\r\n";

        Assert.Equal(
            Expected(TypeOutputMode.FullName, o => { o.IndentCharCount = 2; o.NewLine = "\r\n"; }),
            context.Output());
    }

    private static CSharpFileDefinition Payload()
    {
        var file = new CSharpFileDefinition("TestNamespace");

        file.AddLeadingTrait(new CodeOutputComponent("// <auto-generated/>"));

        var classDefinition = file.AddClass("Service");

        classDefinition.AddField(TypeDefinition.Get("Sample.Models", "Request"), "_request");

        var method = classDefinition.AddMethod("Handle");

        method.AddParameter(TypeDefinition.Get("Sample.Models", "Request"), "request");
        method.SetReturnType(TypeDefinition.Get("Sample.Other", "Result"));
        method.AddCode("return new {arg1}();", TypeDefinition.Get("Sample.Other", "Result"));

        return file;
    }

    private static OutputContext Written(TypeOutputMode mode)
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });

        Payload().WriteOutput(context);

        return context;
    }

    /// <summary>The same file written with those options from the start.</summary>
    private static string Expected(TypeOutputMode mode, System.Action<OutputContextOptions> style)
    {
        var options = new OutputContextOptions { TypeOutputMode = mode };

        style(options);

        var context = new OutputContext(options);

        Payload().WriteOutput(context);

        return context.Output();
    }
}
