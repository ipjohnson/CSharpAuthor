using System;
using System.IO;
using System.Linq;
using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// The README's quickstart, run against the library and checked against the output the README
/// claims for it.
/// </summary>
/// <remarks>
/// <para>
/// A README is the one piece of a library nothing else verifies, and it is the piece every new user
/// runs first. This one had two samples that did not work: an install line that resolved to the
/// previous major, and a pinned version that did not exist. Both were found by trying them, not by
/// reading them.
/// </para>
/// <para>
/// The check is deliberately against the README file on disk rather than against a string held
/// here. A copy in this file would drift from the README the same way the README drifted from the
/// library, and the drift is the thing being tested.
/// </para>
/// </remarks>
public class ReadmeSampleTests
{
    /// <summary>
    /// The quickstart, transcribed from the README's first code block.
    /// </summary>
    /// <remarks>
    /// Transcribed rather than extracted, because the sample declares its own variables and reads
    /// as a program - the only way to know it compiles is for it to be compiled, which is what this
    /// file does by containing it.
    /// </remarks>
    private static string RunQuickstart()
    {
        var file = new CSharpFileDefinition("Sample.Generated");

        var widget = file.AddClass("Widget");
        widget.Modifiers = ComponentModifier.Public | ComponentModifier.Partial;

        var name = widget.AddProperty(typeof(string), "Name");
        name.Modifiers = ComponentModifier.Public;

        var describe = widget.AddMethod("Describe");
        describe.Modifiers = ComponentModifier.Public;
        describe.SetReturnType(typeof(string));
        describe.Return(Ex.Interpolate("widget ", Ex.Id("Name")));

        var rank = widget.AddMethod("Rank");
        rank.Modifiers = ComponentModifier.Public;
        rank.SetReturnType(typeof(int));
        rank.Return(Ex.Switch(Ex.Id("Name"),
            Ex.Arm(Pat.Null, Ex.Int(0)),
            Ex.Arm(Pat.Declaration(TypeDefinition.Get(typeof(string)), "s"),
                   Ex.Id("s").Dot("Length").Is(Pat.GreaterThan(Ex.Int(8))),
                   Ex.Int(2)),
            Ex.Arm(Pat.Discard, Ex.Int(1))));

        var context = new OutputContext();

        file.WriteOutput(context);

        return context.Output();
    }

    /// <summary>
    /// What the README says the quickstart emits is what it emits.
    /// </summary>
    [Fact]
    public void QuickstartEmitsWhatTheReadmeShows()
    {
        var claimed = ReadmeBlockContaining("namespace Sample.Generated");

        AssertEqual.WithoutNewLine(claimed, RunQuickstart());
    }

    /// <summary>
    /// And what it emits compiles.
    /// </summary>
    /// <remarks>
    /// The half a snapshot cannot answer. A README sample that emits exactly what the README says
    /// and does not compile is still a broken sample.
    /// </remarks>
    [Fact]
    public void QuickstartOutputCompiles()
    {
        RoslynAssert.Compiles(RunQuickstart());
    }

    /// <summary>
    /// The README's install snippet names a version that exists.
    /// </summary>
    /// <remarks>
    /// It said <c>Version="2.0.0"</c> while the published package was <c>2.0.0-preview1003</c>, so
    /// copying it gave NU1102. This asserts only that the version is a prerelease of 2.0.0 or a
    /// release of it - not which one - so cutting a new preview does not fail the build, but
    /// dropping back to a version line that cannot resolve does.
    /// </remarks>
    [Fact]
    public void ReadmePinsAVersionThatCouldResolve()
    {
        var readme = ReadReadme();

        var line = readme
            .Split('\n')
            .FirstOrDefault(l => l.Contains("PackageReference Include=\"CSharpAuthor\""));

        Assert.NotNull(line);

        var start = line!.IndexOf("Version=\"", StringComparison.Ordinal) + "Version=\"".Length;
        var version = line.Substring(start, line.IndexOf('"', start) - start);

        Assert.StartsWith("2.0.0", version);

        // A bare "2.0.0" is the trap: it does not exist while 2.0 is in preview.
        Assert.True(
            version == "2.0.0" == PackageIsReleased(version),
            "the README pins 2.0.0, which only resolves once a non-prerelease 2.0.0 is published");
    }

    /// <summary>
    /// The install command carries <c>--prerelease</c> for as long as the pinned version is one.
    /// </summary>
    /// <remarks>
    /// Without it <c>dotnet add package CSharpAuthor</c> resolves to the previous major, and
    /// silently: the quickstart compiles there and emits the same output, so nothing tells the
    /// reader they are on the wrong library.
    /// </remarks>
    [Fact]
    public void InstallCommandMatchesThePinnedVersion()
    {
        var readme = ReadReadme();

        var pinned = readme.Contains("Version=\"2.0.0-");

        Assert.Equal(pinned, readme.Contains("dotnet add package CSharpAuthor --prerelease"));
    }

    private static bool PackageIsReleased(string version) => !version.Contains('-');

    private static string ReadmeBlockContaining(string marker)
    {
        var readme = ReadReadme().Replace("\r\n", "\n");

        var blocks = readme.Split(new[] { "```" }, StringSplitOptions.None);

        foreach (var block in blocks)
        {
            if (!block.Contains(marker))
            {
                continue;
            }

            // The fence's language tag is the first line of the block.
            var newline = block.IndexOf('\n');

            return block.Substring(newline + 1);
        }

        throw new InvalidOperationException($"No README code block contains '{marker}'.");
    }

    private static string ReadReadme()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "README.md");

            if (System.IO.File.Exists(candidate))
            {
                return System.IO.File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("README.md was not found above " + AppContext.BaseDirectory);
    }
}
