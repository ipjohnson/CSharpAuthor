using System;
using CSharpAuthor.Profiles;
using Xunit;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// The profile itself: presets, and the rule that preference resolves against capability.
/// </summary>
public class EmitProfileTests
{
    [Fact]
    public void ConservativeIsCSharp8BlockNamespaceAndNoSugar()
    {
        var profile = EmitProfile.Conservative;

        Assert.Equal(LanguageVersion.CSharp8, profile.Target);
        Assert.False(profile.FileScopedNamespace);
        Assert.False(profile.PreferVar);
        Assert.False(profile.PreferTargetTypedNew);
        Assert.False(profile.PreferCollectionExprs);
        Assert.False(profile.PreferExpressionBodied);
        Assert.False(profile.PreferRawStrings);
    }

    [Fact]
    public void DefaultIsCSharp12()
    {
        var profile = EmitProfile.Default;

        Assert.Equal(LanguageVersion.CSharp12, profile.Target);
        Assert.Equal(' ', profile.IndentChar);
        Assert.Equal(4, profile.IndentWidth);
        Assert.Equal("\n", profile.NewLine);
        Assert.Equal(BraceStyle.Allman, profile.Braces);
        Assert.True(profile.FileScopedNamespace);
        Assert.Equal(TypeOutputMode.ShortName, profile.TypeMode);
        Assert.True(profile.AliasCollisions);
        Assert.Null(profile.ContainingNamespace);
    }

    [Fact]
    public void LatestSupportsEverythingInTheTable()
    {
        foreach (var info in LanguageFeatures.All)
        {
            Assert.True(
                EmitProfile.Latest.Supports(info.Feature),
                info.Feature + " is not supported by the Latest profile.");
        }
    }

    [Fact]
    public void APreferenceIsNeverAnErrorWhenTheTargetIsTooOld()
    {
        // The rule from the handoff: PreferCollectionExprs with a C# 8 target emits new[] { ... },
        // silently and correctly. It is a rendering decision, not a mistake.
        var profile = EmitProfile.Conservative.With(p => p.PreferCollectionExprs = true);

        Assert.True(profile.Prefers(LanguageFeature.CollectionExpressions));
        Assert.False(profile.Supports(LanguageFeature.CollectionExpressions));
        Assert.False(profile.CanEmit(LanguageFeature.CollectionExpressions));

        var session = new EmitSession(profile);

        Assert.False(session.MayEmit(LanguageFeature.CollectionExpressions, "Sizes"));
        Assert.False(session.HasErrors);
    }

    [Fact]
    public void ACapabilityIsNotAPreference()
    {
        // Supported but not wanted is still "do not emit it", and is equally not an error.
        var profile = EmitProfile.Default.With(p => p.PreferCollectionExprs = false);

        Assert.True(profile.Supports(LanguageFeature.CollectionExpressions));
        Assert.False(profile.Prefers(LanguageFeature.CollectionExpressions));
        Assert.False(profile.CanEmit(LanguageFeature.CollectionExpressions));
    }

    [Fact]
    public void AFeatureWithNoPreferenceFlagIsAlwaysWanted()
    {
        // A node that asks for `init` means `init`. There is no style option that turns it off.
        Assert.True(EmitProfile.Conservative.Prefers(LanguageFeature.InitOnlyProperties));
        Assert.True(EmitProfile.Conservative.Prefers(LanguageFeature.RequiredMembers));
        Assert.True(EmitProfile.Conservative.Prefers(LanguageFeature.LabeledJumps));
    }

    [Fact]
    public void APresetCannotBeMutated()
    {
        // The presets are shared. Assigning to one would change every other caller's output, and
        // finding that out from the diff of a generated file is not a good afternoon.
        var exception = Assert.Throws<InvalidOperationException>(() => EmitProfile.Default.IndentWidth = 2);

        Assert.Contains("Clone()", exception.Message);
        Assert.Equal(4, EmitProfile.Default.IndentWidth);
    }

    [Fact]
    public void CloneIsMutableAndIndependent()
    {
        var copy = EmitProfile.Default.Clone();

        copy.IndentWidth = 2;
        copy.Target = LanguageVersion.CSharp8;

        Assert.False(copy.IsFrozen);
        Assert.Equal(2, copy.IndentWidth);
        Assert.Equal(4, EmitProfile.Default.IndentWidth);
        Assert.Equal(LanguageVersion.CSharp12, EmitProfile.Default.Target);
    }

    [Fact]
    public void WithCopiesEveryProperty()
    {
        var original = EmitProfile.Default.With(p =>
        {
            p.IndentChar = '\t';
            p.IndentWidth = 1;
            p.NewLine = "\r\n";
            p.Braces = BraceStyle.KAndR;
            p.FileScopedNamespace = false;
            p.TypeMode = TypeOutputMode.Global;
            p.AliasCollisions = false;
            p.ContainingNamespace = "Acme.Generated";
            p.Target = LanguageVersion.CSharp9;
            p.PreferVar = false;
            p.PreferTargetTypedNew = false;
            p.PreferCollectionExprs = false;
            p.PreferExpressionBodied = true;
            p.PreferRawStrings = true;
            p.Polyfills = PolyfillMode.Always;
            p.DownlevelComments = DownlevelCommentPlacement.FileHeader;
            p.OnCapabilityViolation = CapabilityViolationBehavior.EmitErrorDirective;
            p.BreakInvokeLines = false;
            p.GenerateDocumentation = false;
        });

        var copy = original.Clone();

        Assert.Equal('\t', copy.IndentChar);
        Assert.Equal(1, copy.IndentWidth);
        Assert.Equal("\r\n", copy.NewLine);
        Assert.Equal(BraceStyle.KAndR, copy.Braces);
        Assert.False(copy.FileScopedNamespace);
        Assert.Equal(TypeOutputMode.Global, copy.TypeMode);
        Assert.False(copy.AliasCollisions);
        Assert.Equal("Acme.Generated", copy.ContainingNamespace);
        Assert.Equal(LanguageVersion.CSharp9, copy.Target);
        Assert.False(copy.PreferVar);
        Assert.False(copy.PreferTargetTypedNew);
        Assert.False(copy.PreferCollectionExprs);
        Assert.True(copy.PreferExpressionBodied);
        Assert.True(copy.PreferRawStrings);
        Assert.Equal(PolyfillMode.Always, copy.Polyfills);
        Assert.Equal(DownlevelCommentPlacement.FileHeader, copy.DownlevelComments);
        Assert.Equal(CapabilityViolationBehavior.EmitErrorDirective, copy.OnCapabilityViolation);
        Assert.False(copy.BreakInvokeLines);
        Assert.False(copy.GenerateDocumentation);
    }

    [Fact]
    public void AnUnspecifiedTargetIsNotAnEmptyOne()
    {
        // LanguageVersion.Default is 0. Compared with >= it would mean "nothing is supported",
        // which is the kind of silent wrongness this library exists to remove.
        var profile = EmitProfile.Default.With(p => p.Target = LanguageVersion.Default);

        Assert.Equal(EmitProfile.DefaultTarget, profile.EffectiveTarget);
        Assert.True(profile.Supports(LanguageFeature.NameOf));
        Assert.True(profile.Supports(LanguageFeature.CollectionExpressions));
    }

    [Fact]
    public void TheFormattingHalfRoundTripsThroughV1OutputOptions()
    {
        var profile = EmitProfile.Default.With(p =>
        {
            p.IndentChar = '\t';
            p.IndentWidth = 1;
            p.NewLine = "\r\n";
            p.TypeMode = TypeOutputMode.FullName;
            p.BreakInvokeLines = false;
            p.GenerateDocumentation = false;
        });

        var options = profile.ToOutputContextOptions();

        Assert.Equal('\t', options.IndentChar);
        Assert.Equal(1, options.IndentCharCount);
        Assert.Equal("\r\n", options.NewLine);
        Assert.Equal(TypeOutputMode.FullName, options.TypeOutputMode);
        Assert.False(options.BreakInvokeLines);
        Assert.False(options.GenerateDocumentation);

        var round = EmitProfile.FromOutputContextOptions(options);

        Assert.Equal('\t', round.IndentChar);
        Assert.Equal(1, round.IndentWidth);
        Assert.Equal("\r\n", round.NewLine);
        Assert.Equal(TypeOutputMode.FullName, round.TypeMode);
        Assert.False(round.BreakInvokeLines);
        Assert.False(round.GenerateDocumentation);
    }

    [Fact]
    public void AContextWithNoProfileBehavesLikeV1()
    {
        // Existing call sites pass no profile. They must keep emitting what they emitted before,
        // which means the fallback cannot be a profile that downlevels anything.
        var session = EmitSession.For(new OutputContext());

        Assert.Same(EmitProfile.V1Compatible, session.Profile);
        Assert.True(session.MayEmit(LanguageFeature.InitOnlyProperties, "Name"));
        Assert.True(session.MayEmit(LanguageFeature.CollectionExpressions, "Sizes"));
        Assert.Empty(session.RequiredPolyfills);
        Assert.Empty(session.DownlevelNotes);
    }

    [Fact]
    public void AProfiledContextTakesItsFormattingFromTheProfile()
    {
        var context = new ProfiledOutputContext(EmitProfile.Default.With(p =>
        {
            p.IndentChar = '\t';
            p.IndentWidth = 1;
        }));

        context.IncrementIndent();

        Assert.Equal("\t", context.IndentString);
        Assert.Same(context.Session.Profile, context.EmitProfile());
    }
}
