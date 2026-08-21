using System.Linq;
using CSharpAuthor.Profiles;
using Xunit;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// The polyfillable features: <c>init</c> and <c>required</c>. Below the version that has them
/// the code still compiles and means something slightly different, which is the one case that
/// earns a comment in the output.
/// </summary>
public class PolyfillableFeatureTests
{
    [Fact]
    public void InitStaysInitWhereTheTargetHasIt()
    {
        AssertEqual.WithoutNewLine(
            "public string Name { get; init; }\n",
            ProfileEmitter.Emit(InitProperty(), EmitProfile.Default).Code);
    }

    [Fact]
    public void InitBecomesASettablePropertyAndSaysSo()
    {
        var result = ProfileEmitter.Emit(InitProperty(), EmitProfile.Conservative);

        // The exact comment from the handoff.
        AssertEqual.WithoutNewLine(
            "// DOWNLEVEL: Name: 'init' unavailable below C#9 — emitted as a settable property, immutability lost\n" +
            "public string Name { get; set; }\n",
            result.Code);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(EmitDiagnostic.LossyDownlevelId, diagnostic.Id);
        Assert.Equal(EmitSeverity.Warning, diagnostic.Severity);
        Assert.Equal(LanguageFeature.InitOnlyProperties, diagnostic.Feature);
        Assert.Equal(LanguageVersion.CSharp9, diagnostic.RequiredVersion);
        Assert.Equal(LanguageVersion.CSharp8, diagnostic.Target);
        Assert.Equal("Name", diagnostic.Context);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ALossyDownlevelIsNeverSilentButCanBeMovedOrTurnedOff()
    {
        var header = ProfileEmitter.Emit(
            InitProperty(),
            EmitProfile.Conservative.With(p => p.DownlevelComments = DownlevelCommentPlacement.FileHeader));

        AssertEqual.WithoutNewLine(
            "// DOWNLEVEL: Name: 'init' unavailable below C#9 — emitted as a settable property, immutability lost\n" +
            "\npublic string Name { get; set; }\n",
            header.Code);

        var silent = ProfileEmitter.Emit(
            InitProperty(),
            EmitProfile.Conservative.With(p => p.DownlevelComments = DownlevelCommentPlacement.None));

        AssertEqual.WithoutNewLine("public string Name { get; set; }\n", silent.Code);

        // Turning the comment off does not turn the record of it off.
        Assert.Single(silent.Diagnostics);
        Assert.Single(silent.DownlevelNotes);
    }

    [Fact]
    public void InitOnTheVersionThatIntroducedItBringsItsSupportType()
    {
        var result = ProfileEmitter.Emit(
            InitProperty(), EmitProfile.Default.With(p => p.Target = LanguageVersion.CSharp9));

        Assert.Equal(new[] { PolyfillType.IsExternalInit }, result.Polyfills.ToArray());

        AssertEqual.ContainsWithoutNewLine("public string Name { get; init; }", result.Code);
        AssertEqual.ContainsWithoutNewLine(
            "namespace System.Runtime.CompilerServices\n{\n    internal static class IsExternalInit\n    {\n    }\n}",
            result.Code);
    }

    [Fact]
    public void ThePolyfillIsAPolicyNotAnAssumption()
    {
        // Whether the support type is already there is a target-framework question and a profile
        // knows only the language version, so Auto is a proxy and both ends of it are reachable.
        Assert.Empty(ProfileEmitter.Emit(InitProperty(), EmitProfile.Default).Polyfills);

        Assert.NotEmpty(
            ProfileEmitter.Emit(InitProperty(), EmitProfile.Default.With(p => p.Polyfills = PolyfillMode.Always))
                .Polyfills);

        Assert.Empty(
            ProfileEmitter.Emit(
                    InitProperty(),
                    EmitProfile.Default.With(p =>
                    {
                        p.Target = LanguageVersion.CSharp9;
                        p.Polyfills = PolyfillMode.None;
                    }))
                .Polyfills);
    }

    [Fact]
    public void RequiredIsWrittenWhereTheTargetHasIt()
    {
        AssertEqual.WithoutNewLine(
            "public required string Name { get; set; }\n",
            ProfileEmitter.Emit(RequiredProperty(), EmitProfile.Default).Code);
    }

    [Fact]
    public void RequiredIsDroppedAndSaysWhatWasLost()
    {
        var result = ProfileEmitter.Emit(RequiredProperty(), EmitProfile.Conservative);

        AssertEqual.WithoutNewLine(
            "// DOWNLEVEL: Name: 'required' unavailable below C#11 — emitted as an ordinary member, " +
            "initialisation is no longer enforced\n" +
            "public string Name { get; set; }\n",
            result.Code);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void RequiredBringsBothOfTheTypesTheCompilerAsksFor()
    {
        // RequiredMemberAttribute alone is CS0656: the compiler applies CompilerFeatureRequired
        // itself and refuses `required` without it.
        var result = ProfileEmitter.Emit(
            RequiredProperty(), EmitProfile.Default.With(p => p.Target = LanguageVersion.CSharp11));

        Assert.Equal(
            new[] { PolyfillType.RequiredMemberAttribute, PolyfillType.CompilerFeatureRequiredAttribute },
            result.Polyfills.ToArray());
    }

    [Fact]
    public void ASupportTypeForcesABlockNamespace()
    {
        // C# will not accept a file-scoped namespace beside any other namespace declaration, and a
        // support type has to declare System.Runtime.CompilerServices. Compiling wins over style.
        var file = new CSharpFileDefinition("Acme.Generated");
        var definition = file.AddClass("Widget");

        definition.Modifiers = ComponentModifier.Public;

        var property = definition.AddProperty(typeof(string), "Name");

        property.Set!.IsInit = true;

        var result = ProfileEmitter.Emit(file, EmitProfile.Default.With(p => p.Polyfills = PolyfillMode.Always));

        AssertEqual.ContainsWithoutNewLine("namespace Acme.Generated\n{", result.Code);
        Assert.DoesNotContain("namespace Acme.Generated;", result.Code);
        AssertEqual.ContainsWithoutNewLine("internal static class IsExternalInit", result.Code);

        Assert.Contains(
            result.Diagnostics,
            d => d.Feature == LanguageFeature.FileScopedNamespaces && d.Message.Contains("support type"));
    }

    [Fact]
    public void AFileWithNoSupportTypeKeepsItsFileScopedNamespace()
    {
        var file = new CSharpFileDefinition("Acme.Generated");

        file.AddClass("Widget").Modifiers = ComponentModifier.Public;

        AssertEqual.ContainsWithoutNewLine(
            "namespace Acme.Generated;", ProfileEmitter.Emit(file, EmitProfile.Default).Code);

        AssertEqual.ContainsWithoutNewLine(
            "namespace Acme.Generated\n{", ProfileEmitter.Emit(file, EmitProfile.Conservative).Code);
    }

    [Fact]
    public void TheTreeIsLeftExactlyAsItWasFound()
    {
        // The profile decides how the namespace is written; it does not edit the tree to do it.
        var file = new CSharpFileDefinition("Acme.Generated") { FileScopedNamespace = false };

        file.AddClass("Widget");

        ProfileEmitter.Emit(file, EmitProfile.Default);

        Assert.False(file.FileScopedNamespace);
    }

    private static PropertyDefinition InitProperty()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(string)), "Name");

        property.Set!.IsInit = true;

        return property;
    }

    private static PropertyDefinition RequiredProperty() =>
        new PropertyDefinition(TypeDefinition.Get(typeof(string)), "Name") { IsRequired = true };
}
