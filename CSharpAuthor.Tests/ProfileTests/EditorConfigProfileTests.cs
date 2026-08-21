using System;
using System.Collections.Generic;
using System.IO;
using CSharpAuthor.Profiles;
using Xunit;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// Reading the host project's formatting out of its .editorconfig.
/// </summary>
/// <remarks>
/// A generated file that does not look like the files around it is a generated file everyone can
/// see is generated. The keys here are the real ones, with the severity suffixes real
/// .editorconfig files carry.
/// </remarks>
public class EditorConfigProfileTests
{
    [Fact]
    public void ThisRepositorysOwnEditorConfigIsRead()
    {
        var profile = EmitProfile.FromEditorConfigText(FindRepositoryEditorConfig(), "Widget.cs");

        Assert.Equal(' ', profile.IndentChar);
        Assert.Equal(4, profile.IndentWidth);
        Assert.Equal("\r\n", profile.NewLine);
        Assert.Equal(BraceStyle.Allman, profile.Braces);

        // csharp_style_namespace_declarations = file_scoped:error - the severity is not part of
        // the value.
        Assert.True(profile.FileScopedNamespace);

        // All three csharp_style_var_* keys are false here.
        Assert.False(profile.PreferVar);

        // csharp_style_expression_bodied_methods = false, whatever the property one says.
        Assert.False(profile.PreferExpressionBodied);
    }

    [Fact]
    public void TabsAreOneTabPerLevel()
    {
        // indent_size is how wide a tab looks, not how many of them a level is. Taking it
        // literally would indent with four tabs.
        var profile = EmitProfile.FromEditorConfigText(
            "[*.cs]\nindent_style = tab\nindent_size = 4\ntab_width = 4\n");

        Assert.Equal('\t', profile.IndentChar);
        Assert.Equal(1, profile.IndentWidth);
    }

    [Fact]
    public void IndentSizeCanDeferToTabWidth()
    {
        var profile = EmitProfile.FromEditorConfigText(
            "[*.cs]\nindent_style = space\nindent_size = tab\ntab_width = 2\n");

        Assert.Equal(' ', profile.IndentChar);
        Assert.Equal(2, profile.IndentWidth);
    }

    [Theory]
    [InlineData("lf", "\n")]
    [InlineData("crlf", "\r\n")]
    [InlineData("cr", "\r")]
    public void EndOfLineIsTheHostProjectsNotThePlatforms(string value, string expected)
    {
        Assert.Equal(expected, EmitProfile.FromEditorConfigText("[*.cs]\nend_of_line = " + value).NewLine);
    }

    [Theory]
    [InlineData("all", BraceStyle.Allman)]
    [InlineData("none", BraceStyle.KAndR)]
    [InlineData("types,methods", BraceStyle.Allman)]
    [InlineData("accessors,lambdas", BraceStyle.KAndR)]
    public void BracePlacementIsRead(string value, BraceStyle expected)
    {
        Assert.Equal(
            expected,
            EmitProfile.FromEditorConfigText("[*.cs]\ncsharp_new_line_before_open_brace = " + value).Braces);
    }

    [Theory]
    [InlineData("file_scoped:error", true)]
    [InlineData("file_scoped", true)]
    [InlineData("block_scoped:silent", false)]
    public void TheNamespaceStyleIsReadWithoutItsSeverity(string value, bool expected)
    {
        Assert.Equal(
            expected,
            EmitProfile.FromEditorConfigText("[*.cs]\ncsharp_style_namespace_declarations = " + value)
                .FileScopedNamespace);
    }

    [Fact]
    public void VarIsReadFromTheKeyThatMattersMostToGeneratedCode()
    {
        // Three keys, one flag. Generated code declares locals where the type is apparent far more
        // often than anywhere else, so that key decides when it is present.
        Assert.True(
            EmitProfile.FromEditorConfigText(
                    "[*.cs]\ncsharp_style_var_when_type_is_apparent = true:suggestion\ncsharp_style_var_elsewhere = false")
                .PreferVar);

        Assert.False(
            EmitProfile.FromEditorConfigText("[*.cs]\ncsharp_style_var_elsewhere = false").PreferVar);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("when_on_single_line", true)]
    [InlineData("false:silent", false)]
    public void ExpressionBodiesAreRead(string value, bool expected)
    {
        Assert.Equal(
            expected,
            EmitProfile.FromEditorConfigText("[*.cs]\ncsharp_style_expression_bodied_methods = " + value)
                .PreferExpressionBodied);
    }

    [Fact]
    public void OnlyTheSectionsThatApplyToTheFileAreRead()
    {
        const string text =
            "root = true\n" +
            "[*]\nindent_size = 2\n" +
            "[*.vb]\nindent_size = 8\n" +
            "[*.cs]\nindent_size = 4\n";

        Assert.Equal(4, EmitProfile.FromEditorConfigText(text, "Widget.cs").IndentWidth);
        Assert.Equal(8, EmitProfile.FromEditorConfigText(text, "Widget.vb").IndentWidth);
        Assert.Equal(2, EmitProfile.FromEditorConfigText(text, "Widget.fs").IndentWidth);
    }

    [Fact]
    public void ABraceListSectionMatches()
    {
        const string text = "[*.{cs,vb}]\nindent_size = 3\n";

        Assert.Equal(3, EmitProfile.FromEditorConfigText(text, "Widget.cs").IndentWidth);
        Assert.Equal(3, EmitProfile.FromEditorConfigText(text, "Widget.vb").IndentWidth);
        Assert.Equal(4, EmitProfile.FromEditorConfigText(text, "Widget.fs").IndentWidth);
    }

    [Fact]
    public void CommentsAndBlankLinesAreIgnored()
    {
        var profile = EmitProfile.FromEditorConfigText(
            "# a comment\n; another\n\n[*.cs]\n# indent_size = 99\nindent_size = 3\n");

        Assert.Equal(3, profile.IndentWidth);
    }

    [Fact]
    public void AKeyThatIsNotThereLeavesTheDefault()
    {
        var profile = EmitProfile.FromEditorConfigText("[*.cs]\n");

        Assert.Equal(EmitProfile.Default.IndentChar, profile.IndentChar);
        Assert.Equal(EmitProfile.Default.IndentWidth, profile.IndentWidth);
        Assert.Equal(EmitProfile.Default.NewLine, profile.NewLine);
        Assert.Equal(EmitProfile.Default.Braces, profile.Braces);
        Assert.Equal(EmitProfile.Default.PreferVar, profile.PreferVar);
    }

    [Fact]
    public void TheLanguageVersionDoesNotComeFromEditorConfig()
    {
        // There is no .editorconfig key for it - it is a project setting, read from the parse
        // options by the Roslyn-gated bridge.
        Assert.Equal(EmitProfile.Default.Target, EmitProfile.FromEditorConfigText("[*.cs]\nindent_size = 2").Target);
    }

    [Fact]
    public void OptionsCanComeFromADictionary()
    {
        // The shape Roslyn's AnalyzerConfigOptions has, without needing Roslyn to test it.
        var options = new Dictionary<string, string>
        {
            { "indent_style", "space" },
            { "indent_size", "2" },
            { "end_of_line", "crlf" },
            { "csharp_style_namespace_declarations", "block_scoped:warning" }
        };

        var profile = EmitProfile.FromEditorConfig(options);

        Assert.Equal(2, profile.IndentWidth);
        Assert.Equal("\r\n", profile.NewLine);
        Assert.False(profile.FileScopedNamespace);
    }

    [Fact]
    public void TheProfileItReturnsIsMutable()
    {
        var profile = EmitProfile.FromEditorConfigText("[*.cs]\nindent_size = 2");

        Assert.False(profile.IsFrozen);

        profile.Target = LanguageVersion.CSharp8;

        Assert.Equal(LanguageVersion.CSharp8, profile.Target);
    }

    private static string FindRepositoryEditorConfig()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, ".editorconfig");

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "No .editorconfig above " + AppContext.BaseDirectory +
            ". This test uses the repository's own as its fixture.");
    }
}
