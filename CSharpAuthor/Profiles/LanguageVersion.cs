namespace CSharpAuthor;

/// <summary>
/// A C# language version, used as the <em>capability</em> half of an <see cref="EmitProfile"/>.
/// </summary>
/// <remarks>
/// The numeric values match <c>Microsoft.CodeAnalysis.CSharp.LanguageVersion</c> so the two can be
/// converted by a cast in the Roslyn-gated source folder, and so ordering comparisons
/// (<c>target &gt;= CSharp9</c>) mean what they read as. The type is declared here rather than
/// taken from Roslyn because the core library targets netstandard2.0 and must compile with no
/// Roslyn reference at all.
/// <para>
/// Known limit: the emitter can describe versions the validator cannot check.
/// <c>Microsoft.CodeAnalysis.CSharp</c> 4.14.0 knows language versions only up to
/// <see cref="CSharp13"/> - its <c>LanguageVersion.Preview</c> cannot parse <c>break outer;</c>
/// even though the .NET 11 SDK compiler can. <see cref="CSharp14"/> and <see cref="CSharp15"/>
/// therefore select renderings that no bundled parser in this repository can verify. Do not read a
/// green test run as a conformance claim for them.
/// </para>
/// </remarks>
public enum LanguageVersion
{
    /// <summary>
    /// Unspecified. Resolved to <see cref="EmitProfile.DefaultTarget"/> by
    /// <see cref="EmitProfile.EffectiveTarget"/>; never treated as "nothing is supported".
    /// </summary>
    Default = 0,

    CSharp1 = 1,

    CSharp2 = 2,

    CSharp3 = 3,

    CSharp4 = 4,

    CSharp5 = 5,

    CSharp6 = 6,

    CSharp7 = 7,

    CSharp7_1 = 701,

    CSharp7_2 = 702,

    CSharp7_3 = 703,

    CSharp8 = 800,

    CSharp9 = 900,

    CSharp10 = 1000,

    CSharp11 = 1100,

    CSharp12 = 1200,

    CSharp13 = 1300,

    /// <summary>C# 14. Beyond what Roslyn 4.14.0 can parse.</summary>
    CSharp14 = 1400,

    /// <summary>C# 15. Beyond what Roslyn 4.14.0 can parse.</summary>
    CSharp15 = 1500,

    /// <summary>Everything the compiler has, including unreleased features.</summary>
    Preview = int.MaxValue - 1,

    /// <summary>Every released feature this library knows about.</summary>
    Latest = int.MaxValue
}

/// <summary>
/// Display helpers for <see cref="LanguageVersion"/>.
/// </summary>
public static class LanguageVersionExtensions
{
    /// <summary>
    /// The form used in a <c>// DOWNLEVEL:</c> comment - <c>C#9</c>, <c>C#7.3</c>, <c>preview</c>.
    /// </summary>
    public static string ToDisplayName(this LanguageVersion version)
    {
        switch (version)
        {
            case LanguageVersion.Default:
                return "default";
            case LanguageVersion.Preview:
                return "preview";
            case LanguageVersion.Latest:
                return "latest";
        }

        var value = (int)version;

        if (value < 100)
        {
            return "C#" + value;
        }

        var major = value / 100;
        var minor = value % 100;

        return minor == 0 ? "C#" + major : "C#" + major + "." + minor;
    }

    /// <summary>
    /// Whether a bundled Roslyn parser can be asked to validate output rendered for this version.
    /// </summary>
    /// <remarks>
    /// <c>Microsoft.CodeAnalysis.CSharp</c> 4.14.0 tops out at C# 13. Anything above it renders,
    /// but nothing in this repository can prove the rendering parses.
    /// </remarks>
    public static bool IsValidatableByRoslyn414(this LanguageVersion version) =>
        version != LanguageVersion.Default && version <= LanguageVersion.CSharp13;
}
