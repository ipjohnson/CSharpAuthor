using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Everything that is not a declaration: directives and documentation.
/// </summary>
public class TriviaAdversaryTests
{
    [Fact]
    public void PragmaWrapCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.WrapInPragma("CS0618");

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    /// <summary>
    /// <c>EnableNullable</c> opens with <c>#nullable enable</c> and closes with
    /// <c>#nullable disable</c>. Closing is not the same as restoring: in a file that was already
    /// in a nullable context - which is every file in a project with
    /// <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c> - the close turns the analysis off for
    /// everything after it rather than putting it back the way it was. <c>#nullable restore</c> is
    /// the directive that means "back to the project setting".
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: EnableNullable closes with '#nullable disable' rather than '#nullable restore', so it silently disables nullable analysis for the rest of the file instead of restoring the project's setting DEFERRED, not unknown: the fix is agreed and held only by the nine DependencyModules snapshots it would diff - see 'A note on release sequencing'.")]
    public void EnableNullableRestoresRatherThanDisables()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.EnableNullable();

        Assert.Contains("#nullable restore", Emit.Component(classDefinition));
    }

    [Fact]
    public void EnableNullableCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.EnableNullable();

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    /// <summary>
    /// A documentation comment is written as text, so <c>&amp;</c> and <c>&lt;</c> in it are written
    /// as themselves and the comment is not well-formed XML. The compiler reports CS1570 - a warning,
    /// which becomes a build failure in any project that turns documentation warnings into errors,
    /// and which is exactly what a <c>&lt;code&gt;</c> sample containing a generic type produces.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: comment text is not XML-escaped, so a summary containing & or < emits malformed XML - CS1570, and the documentation file silently loses the element")]
    public void CommentContainingMarkupCharacters()
    {
        var classDefinition = new ClassDefinition("Host")
        {
            Comment = "Use List<int> when a & b"
        };

        var warnings = RoslynAssert.Errors(
            Emit.Component(classDefinition),
            RoslynAssert.MaxLanguageVersion,
            "CS1570");

        Assert.Empty(warnings);
    }

    /// <summary>
    /// A multi-line comment writes a marker on every line, which is the fix already in place for the
    /// case that used to emit continuation lines with no <c>///</c>. Guard.
    /// </summary>
    [Fact]
    public void MultiLineCommentCompiles()
    {
        var method = new MethodDefinition("M")
        {
            Comment = "line one\nline two"
        };

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    /// <summary>
    /// A <c>&lt;code&gt;</c> block whose content is already escaped comes through intact, so the
    /// escaping fix has something correct to preserve.
    /// </summary>
    [Fact]
    public void CommentContainingEscapedMarkupIsWellFormed()
    {
        var classDefinition = new ClassDefinition("Host")
        {
            Comment = "<code>List&lt;int&gt; a &amp; b</code>"
        };

        var warnings = RoslynAssert.Errors(
            Emit.Component(classDefinition),
            RoslynAssert.MaxLanguageVersion,
            "CS1570");

        Assert.Empty(warnings);
    }

    /// <summary>
    /// <c>#region</c> shipped in 2.0.0-preview1002. An unbalanced region is CS1038, reported at the
    /// end of the file rather than where it opened, so the compile question is the one worth asking.
    /// </summary>
    [Fact]
    public void RegionDirective()
    {
        var host = new ClassDefinition("Host");
        var region = new RegionComponent("Generated members");

        region.Add(new MethodDefinition("Work"));
        host.AddComponent(region);

        var emitted = Emit.Component(host).Replace("\r\n", "\n");

        Assert.Contains("#region Generated members\n", emitted);
        Assert.Contains("#endregion\n", emitted);
        RoslynAssert.Compiles(emitted);
    }

    /// <summary>
    /// <c>#if</c> / <c>#elif</c> / <c>#else</c> / <c>#endif</c>, also shipped in preview1002. Both
    /// symbol states are compiled, because a branch that only parses under one of them is the
    /// defect this guards.
    /// </summary>
    [Fact]
    public void ConditionalCompilationDirective()
    {
        var host = new ClassDefinition("Host");
        var directive = new ConditionalDirectiveComponent("NET8_0_OR_GREATER");

        directive.If.Add(new MethodDefinition("Modern"));
        directive.ElseIf("NETSTANDARD2_0").Add(new MethodDefinition("Legacy"));
        directive.Else().Add(new MethodDefinition("Fallback"));
        host.AddComponent(directive);

        var emitted = Emit.Component(host).Replace("\r\n", "\n");

        Assert.Contains("#if NET8_0_OR_GREATER\n", emitted);
        Assert.Contains("#elif NETSTANDARD2_0\n", emitted);
        Assert.Contains("#endif\n", emitted);
        RoslynAssert.Compiles(emitted);
    }

    /// <summary>All three <c>#line</c> forms, shipped in preview1002.</summary>
    [Fact]
    public void LineDirective()
    {
        Assert.Equal("#line 42\n", Emit.Component(LineDirectiveComponent.At(42)).Replace("\r\n", "\n"));
        Assert.Equal("#line default\n", Emit.Component(LineDirectiveComponent.Default()).Replace("\r\n", "\n"));
        Assert.Equal("#line hidden\n", Emit.Component(LineDirectiveComponent.Hidden()).Replace("\r\n", "\n"));

        var host = new ClassDefinition("Host");
        host.AddComponent(LineDirectiveComponent.At(7, "Source.cs"));
        host.AddMethod("Work");

        RoslynAssert.Compiles(Emit.Component(host));
    }

    /// <summary>
    /// <c>#pragma warning disable</c> with no code at all disables everything, which is legal and is
    /// what an empty argument list produces. Recorded rather than asserted as a defect - it compiles,
    /// and the caller asked for it.
    /// </summary>
    [Fact]
    public void PragmaWithNoCodesCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.WrapInPragma();

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    /// <summary>
    /// The <c>&lt;auto-generated/&gt;</c> marker has to be the first line of the file. Analyzers,
    /// StyleCop and the IDE all look at line one to decide whether to skip a file, and a generator
    /// that cannot put it there gets every one of its rules reported against generated code.
    /// </summary>
    /// <remarks>
    /// <c>GenerateUsingStatements</c> inserts the using directives at index 0 of the buffer, after
    /// everything else has been written - so anything a caller attaches as a leading trait ends up
    /// below them.
    /// </remarks>
    [Fact]
    public void AutoGeneratedHeaderIsTheFirstLine()
    {
        var file = new CSharpFileDefinition("Probe");

        file.AddLeadingTrait(new CodeOutputComponent("// <auto-generated/>"));
        file.AddClass("Host").AddField(TypeDefinition.Get("Far.Away", "Thing"), "t");

        Assert.StartsWith("// <auto-generated/>", Emit.File(file));
    }

    /// <summary>
    /// <c>CSharpFileDefinition</c> inherits <c>Comment</c> like every other component and is one of
    /// the two that never writes it, so setting one compiles, reads as documented, and emits
    /// nothing.
    /// </summary>
    [Fact]
    public void FileLevelCommentIsWritten()
    {
        var file = new CSharpFileDefinition("Probe") { Comment = "generated by the widget tool" };

        file.AddClass("Host");

        Assert.Contains("generated by the widget tool", Emit.File(file));
    }

    [Fact]
    public void NamespaceCommentIsWritten()
    {
        var namespaceDefinition = new NamespaceDefinition("Probe") { Comment = "the widget namespace" };

        namespaceDefinition.AddClass("Host");

        Assert.Contains("the widget namespace", Emit.Component(namespaceDefinition));
    }

    /// <summary>
    /// The markup-escaping defect at its other two sites: a parameter's documentation and a return
    /// documentation are written the same way the summary is.
    /// </summary>
    [Fact]
    public void ParameterAndReturnCommentsAreEscaped()
    {
        var method = new MethodDefinition("M") { Comment = "does a thing" };

        method.SetReturnType(typeof(int));
        method.AddParameter(typeof(int), "x").Comment = "the <x> & value";
        method.ReturnComment = "a > b";
        method.AddCode("return x;");

        var warnings = RoslynAssert.Errors(
            "public class Host\n{\n" + Emit.Component(method) + "}\n",
            RoslynAssert.MaxLanguageVersion,
            "CS1570");

        Assert.Empty(warnings);
    }
}
