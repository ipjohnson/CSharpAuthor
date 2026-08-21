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
    [Fact(Skip = "ADVERSARY GAP: EnableNullable closes with '#nullable disable' rather than '#nullable restore', so it silently disables nullable analysis for the rest of the file instead of restoring the project's setting")]
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

    [Fact(Skip = "ADVERSARY GAP: no API - there is no #region emitter. A generated file of any size is unreadable without one, and it is the directive consumers ask for first.")]
    public void RegionDirective()
    {
        Assert.True(false, "no API for #region / #endregion");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no #if / #else / #endif emitter, so a file cannot carry a conditionally compiled member. PragmaOutputComponent is the only directive component and it only writes #pragma warning.")]
    public void ConditionalCompilationDirective()
    {
        Assert.True(false, "no API for #if / #else / #elif / #endif");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no #line emitter, so generated code cannot map diagnostics back to the file it was generated from")]
    public void LineDirective()
    {
        Assert.True(false, "no API for #line");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - there is no ordinary // or /* */ comment emitter; Comment on a component is always written as a /// documentation comment")]
    public void OrdinaryComment()
    {
        Assert.True(false, "no API for a non-documentation comment");
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
}
