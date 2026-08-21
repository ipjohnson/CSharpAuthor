using System;
using System.Linq;
using Xunit;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// The capability table: feature -&gt; minimum version -&gt; category.
/// </summary>
/// <remarks>
/// One table, consulted by every node, so it is worth checking it says what the handoff says.
/// A wrong row here is not a wrong test - it is every file emitted for that target.
/// </remarks>
public class CapabilityTableTests
{
    [Fact]
    public void EveryFeatureHasARow()
    {
        foreach (LanguageFeature feature in Enum.GetValues(typeof(LanguageFeature)))
        {
            var info = LanguageFeatures.Get(feature);

            Assert.Equal(feature, info.Feature);
            Assert.NotEqual(LanguageVersion.Default, info.MinimumVersion);
            Assert.False(string.IsNullOrWhiteSpace(info.Syntax));
        }
    }

    [Fact]
    public void NoFeatureIsListedTwice()
    {
        var duplicates = LanguageFeatures.All
            .GroupBy(x => x.Feature)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key.ToString())
            .ToList();

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData(LanguageFeature.CollectionExpressions, LanguageVersion.CSharp12)]
    [InlineData(LanguageFeature.TargetTypedNew, LanguageVersion.CSharp9)]
    [InlineData(LanguageFeature.FileScopedNamespaces, LanguageVersion.CSharp10)]
    [InlineData(LanguageFeature.RawStringLiterals, LanguageVersion.CSharp11)]
    [InlineData(LanguageFeature.NameOf, LanguageVersion.CSharp6)]
    [InlineData(LanguageFeature.UsingDeclarations, LanguageVersion.CSharp8)]
    [InlineData(LanguageFeature.LabeledJumps, LanguageVersion.CSharp15)]
    [InlineData(LanguageFeature.PrimaryConstructors, LanguageVersion.CSharp12)]
    [InlineData(LanguageFeature.FieldKeyword, LanguageVersion.CSharp14)]
    [InlineData(LanguageFeature.ParamsCollections, LanguageVersion.CSharp13)]
    [InlineData(LanguageFeature.SwitchExpressions, LanguageVersion.CSharp8)]
    [InlineData(LanguageFeature.InitOnlyProperties, LanguageVersion.CSharp9)]
    [InlineData(LanguageFeature.RequiredMembers, LanguageVersion.CSharp11)]
    [InlineData(LanguageFeature.RefStructs, LanguageVersion.CSharp7_2)]
    [InlineData(LanguageFeature.StaticAbstractInterfaceMembers, LanguageVersion.CSharp11)]
    [InlineData(LanguageFeature.DefaultInterfaceMembers, LanguageVersion.CSharp8)]
    [InlineData(LanguageFeature.FunctionPointers, LanguageVersion.CSharp9)]
    [InlineData(LanguageFeature.InlineArrays, LanguageVersion.CSharp12)]
    [InlineData(LanguageFeature.NativeIntegerKeywords, LanguageVersion.CSharp9)]
    [InlineData(LanguageFeature.Records, LanguageVersion.CSharp9)]
    [InlineData(LanguageFeature.RecordStructs, LanguageVersion.CSharp10)]
    public void MinimumVersionsAreTheOnesTheFeaturesShippedIn(LanguageFeature feature, LanguageVersion expected)
    {
        Assert.Equal(expected, LanguageFeatures.MinimumVersion(feature));
    }

    [Theory]
    [InlineData(LanguageFeature.CollectionExpressions)]
    [InlineData(LanguageFeature.TargetTypedNew)]
    [InlineData(LanguageFeature.FileScopedNamespaces)]
    [InlineData(LanguageFeature.RawStringLiterals)]
    [InlineData(LanguageFeature.NameOf)]
    [InlineData(LanguageFeature.UsingDeclarations)]
    [InlineData(LanguageFeature.LabeledJumps)]
    [InlineData(LanguageFeature.PrimaryConstructors)]
    [InlineData(LanguageFeature.FieldKeyword)]
    [InlineData(LanguageFeature.ParamsCollections)]
    [InlineData(LanguageFeature.NativeIntegerKeywords)]
    public void TheFreeOnesAreFree(LanguageFeature feature)
    {
        var info = LanguageFeatures.Get(feature);

        Assert.Equal(FeatureCategory.Free, info.Category);
        Assert.False(info.IsLossy);
    }

    [Theory]
    [InlineData(LanguageFeature.InitOnlyProperties)]
    [InlineData(LanguageFeature.RequiredMembers)]
    public void ThePolyfillableOnesHaveAPolyfillAndSayWhatIsLost(LanguageFeature feature)
    {
        var info = LanguageFeatures.Get(feature);

        Assert.Equal(FeatureCategory.Polyfillable, info.Category);
        Assert.True(info.IsLossy);
        Assert.NotEmpty(Polyfill.For(feature));
    }

    [Theory]
    [InlineData(LanguageFeature.RefStructs)]
    [InlineData(LanguageFeature.StaticAbstractInterfaceMembers)]
    [InlineData(LanguageFeature.DefaultInterfaceMembers)]
    [InlineData(LanguageFeature.FunctionPointers)]
    [InlineData(LanguageFeature.InlineArrays)]
    [InlineData(LanguageFeature.Records)]
    [InlineData(LanguageFeature.RecordStructs)]
    public void TheImpossibleOnesHaveNoDownlevelAtAll(LanguageFeature feature)
    {
        var info = LanguageFeatures.Get(feature);

        Assert.Equal(FeatureCategory.Impossible, info.Category);
        Assert.Equal(DownlevelSupport.None, info.Downlevel);
        Assert.Empty(Polyfill.For(feature));
    }

    [Fact]
    public void AFreeFeatureNeverCarriesAConsequence()
    {
        // A consequence is what a `// DOWNLEVEL:` comment is for. A free downlevel means the same
        // thing, so a comment about it would be noise in every file that took it.
        foreach (var info in LanguageFeatures.All.Where(x => x.Category == FeatureCategory.Free))
        {
            Assert.False(info.IsLossy, info.Feature + " is free but claims a consequence.");
        }
    }

    [Fact]
    public void NintIsAKeywordChoiceNotARecoveredFact()
    {
        // nint IS IntPtr - the downlevel is the same type spelled differently, so it costs
        // nothing and says nothing. Reflection cannot tell the two apart either: a reference built
        // from typeof(IntPtr) and one built from nint are the same reference, so which spelling
        // was meant is a preference, never something the emitter recovered.
        var info = LanguageFeatures.Get(LanguageFeature.NativeIntegerKeywords);

        Assert.Equal(FeatureCategory.Free, info.Category);
        Assert.False(info.IsLossy);
        Assert.True(EmitProfile.Default.Supports(LanguageFeature.NativeIntegerKeywords));
        Assert.False(EmitProfile.Conservative.Supports(LanguageFeature.NativeIntegerKeywords));
    }

    [Theory]
    [InlineData(LanguageVersion.CSharp9, "C#9")]
    [InlineData(LanguageVersion.CSharp7_3, "C#7.3")]
    [InlineData(LanguageVersion.CSharp12, "C#12")]
    [InlineData(LanguageVersion.CSharp6, "C#6")]
    [InlineData(LanguageVersion.Latest, "latest")]
    [InlineData(LanguageVersion.Preview, "preview")]
    public void VersionsPrintTheWayADownlevelCommentNeedsThem(LanguageVersion version, string expected)
    {
        Assert.Equal(expected, version.ToDisplayName());
    }

    [Fact]
    public void RoslynFourteenCannotValidateAboveCSharp13()
    {
        // Stated as a test so it cannot quietly stop being true: Microsoft.CodeAnalysis.CSharp
        // 4.14.0 knows language versions only up to C# 13. Anything above renders, but no parser
        // in this repository can prove the rendering parses - and `break outer;` is exactly such
        // a case, which is why LabeledJumps is a C# 15 row.
        Assert.True(LanguageVersion.CSharp13.IsValidatableByRoslyn414());
        Assert.False(LanguageVersion.CSharp14.IsValidatableByRoslyn414());
        Assert.False(LanguageVersion.CSharp15.IsValidatableByRoslyn414());
        Assert.False(LanguageVersion.Preview.IsValidatableByRoslyn414());

        Assert.Equal(
            LanguageVersion.CSharp15,
            LanguageFeatures.MinimumVersion(LanguageFeature.LabeledJumps));
    }
}
