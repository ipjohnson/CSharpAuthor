using CSharpAuthor.Profiles;
using Xunit;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// Everything a profile and an <see cref="OutputContextOptions"/> both describe has to survive the
/// trip between them.
/// </summary>
/// <remarks>
/// <see cref="EmitProfile.Braces"/>, <see cref="EmitProfile.AliasCollisions"/> and
/// <see cref="EmitProfile.ContainingNamespace"/> were declared on the profile and never carried, in
/// either direction. A profile asking for K&amp;R braces was read and dropped, and
/// <c>FromEditorConfig</c> made it worse by parsing <c>csharp_new_line_before_open_brace</c> into a
/// field nothing would ever look at. Nothing failed; the setting simply did not happen.
///
/// It was found by someone writing documentation, not by a test, which is why the last test here
/// asserts the property rather than the three fields: a field added to both types and forgotten in
/// the conversion should fail, without anyone remembering to come back and extend this file.
/// </remarks>
public class ProfileOptionsCarryTests
{
    private static EmitProfile Populated()
    {
        var profile = EmitProfile.Default.Clone();

        profile.IndentChar = '\t';
        profile.IndentWidth = 3;
        profile.NewLine = "\r\n";
        profile.TypeMode = TypeOutputMode.Global;
        profile.Braces = BraceStyle.KAndR;
        profile.AliasCollisions = false;
        profile.ContainingNamespace = "Some.Namespace";

        return profile;
    }

    [Fact]
    public void TheProfileCarriesItsFormattingToTheWriter()
    {
        var options = Populated().ToOutputContextOptions();

        Assert.Equal('\t', options.IndentChar);
        Assert.Equal(3, options.IndentCharCount);
        Assert.Equal("\r\n", options.NewLine);
        Assert.Equal(TypeOutputMode.Global, options.TypeOutputMode);
        Assert.Equal(BraceStyle.KAndR, options.BraceStyle);
        Assert.False(options.AliasCollisions);
        Assert.Equal("Some.Namespace", options.ContainingNamespace);
    }

    [Fact]
    public void AndCarriesThemBack()
    {
        var profile = EmitProfile.FromOutputContextOptions(Populated().ToOutputContextOptions());

        Assert.Equal('\t', profile.IndentChar);
        Assert.Equal(3, profile.IndentWidth);
        Assert.Equal("\r\n", profile.NewLine);
        Assert.Equal(TypeOutputMode.Global, profile.TypeMode);
        Assert.Equal(BraceStyle.KAndR, profile.Braces);
        Assert.False(profile.AliasCollisions);
        Assert.Equal("Some.Namespace", profile.ContainingNamespace);
    }

    /// <summary>
    /// The brace style actually reaching the emitted file, which is the thing the caller asked for.
    /// </summary>
    [Fact]
    public void AProfileAskingForJoinedBracesGetsThem()
    {
        var profile = EmitProfile.Default.Clone();

        profile.Braces = BraceStyle.KAndR;

        var file = new CSharpFileDefinition("Probe.Ns");

        file.AddClass("Thing").AddMethod("Run");

        var context = new OutputContext(profile.ToOutputContextOptions());

        file.WriteOutput(context);

        Assert.Contains("public class Thing {", context.Output());
    }

    /// <summary>
    /// The guard against the next dropped field: every profile property whose name matches one on
    /// <see cref="OutputContextOptions"/> must differ from the default after the trip, or it is not
    /// being carried.
    /// </summary>
    [Fact]
    public void NoSharedSettingIsSilentlyDropped()
    {
        var carried = Populated().ToOutputContextOptions();
        var untouched = new OutputContextOptions();

        var shared = new (string Name, object? Carried, object? Untouched)[]
        {
            ("IndentChar", carried.IndentChar, untouched.IndentChar),
            ("IndentCharCount", carried.IndentCharCount, untouched.IndentCharCount),
            ("NewLine", carried.NewLine, untouched.NewLine),
            ("TypeOutputMode", carried.TypeOutputMode, untouched.TypeOutputMode),
            ("BraceStyle", carried.BraceStyle, untouched.BraceStyle),
            ("AliasCollisions", carried.AliasCollisions, untouched.AliasCollisions),
            ("ContainingNamespace", carried.ContainingNamespace, untouched.ContainingNamespace)
        };

        foreach (var (name, value, fallback) in shared)
        {
            Assert.True(
                !Equals(value, fallback),
                name + " came back at its default, so ToOutputContextOptions() is not carrying it.");
        }
    }
}
