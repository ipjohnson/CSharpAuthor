using Xunit;

namespace CSharpAuthor.Tests.ProfileTests;

/// <summary>
/// The impossible features: the ones with no downlevel at all.
/// </summary>
/// <remarks>
/// This is the difference between V2 and V1's defect class. Every entry in the V1 audit -
/// <c>private protected</c> widening to <c>protected</c>, <c>partial</c> dropped, an
/// <c>abstract</c> method emitting a body - was output that looked fine. When there is nothing
/// correct to write, the writer says so: either an exception, or <c>#error</c> in the file. There
/// is no third option where it carries on quietly.
/// </remarks>
public class CapabilityViolationTests
{
    [Fact]
    public void AnInterfaceMemberWithABodyBelowCSharp8Stops()
    {
        var exception = Assert.Throws<EmitCapabilityException>(
            () => ProfileEmitter.Emit(DefaultInterfaceMember(), Target(LanguageVersion.CSharp7_3)));

        Assert.Equal(EmitDiagnostic.CapabilityViolationId, exception.Diagnostic.Id);
        Assert.Equal(EmitSeverity.Error, exception.Diagnostic.Severity);
        Assert.Equal(LanguageFeature.DefaultInterfaceMembers, exception.Diagnostic.Feature);
        Assert.Equal("Describe", exception.Diagnostic.Context);

        // The message says what, what it needs, and what it got.
        Assert.Contains("'default interface member' on Describe", exception.Message);
        Assert.Contains("requires C#8", exception.Message);
        Assert.Contains("target is C#7.3", exception.Message);
    }

    [Fact]
    public void AnInterfaceMemberWithABodyIsFineFromCSharp8()
    {
        var result = ProfileEmitter.Emit(DefaultInterfaceMember(), Target(LanguageVersion.CSharp8));

        Assert.False(result.HasErrors);
        AssertEqual.ContainsWithoutNewLine("string Describe()", result.Code);
    }

    [Fact]
    public void AStaticAbstractMemberBelowCSharp11Stops()
    {
        var exception = Assert.Throws<EmitCapabilityException>(
            () => ProfileEmitter.Emit(StaticAbstractMember(), EmitProfile.Conservative));

        Assert.Equal(LanguageFeature.StaticAbstractInterfaceMembers, exception.Diagnostic.Feature);
    }

    [Fact]
    public void AStaticAbstractMemberIsWrittenFromCSharp11()
    {
        var result = ProfileEmitter.Emit(StaticAbstractMember(), Target(LanguageVersion.CSharp11));

        Assert.False(result.HasErrors);
        AssertEqual.ContainsWithoutNewLine("static abstract Widget Create();", result.Code);
    }

    [Fact]
    public void ARefStructBelowCSharp72Stops()
    {
        var exception = Assert.Throws<EmitCapabilityException>(
            () => ProfileEmitter.Emit(RefStruct(), Target(LanguageVersion.CSharp7_1)));

        Assert.Equal(LanguageFeature.RefStructs, exception.Diagnostic.Feature);
        Assert.Equal("Span", exception.Diagnostic.Context);
    }

    [Fact]
    public void ARefStructIsWrittenFromCSharp72()
    {
        var result = ProfileEmitter.Emit(RefStruct(), Target(LanguageVersion.CSharp7_2));

        Assert.False(result.HasErrors);
        AssertEqual.ContainsWithoutNewLine("public ref struct Span", result.Code);
    }

    [Fact]
    public void RefIsNotSilentlyDroppedFromAStruct()
    {
        // Dropping `ref` gives a type that compiles and can be boxed, captured and put on the
        // heap: every restriction the caller asked for, silently removed. That is precisely the
        // shape of the V1 defects, so it has to be impossible to reach.
        var result = ProfileEmitter.Emit(RefStruct(), Reporting(LanguageVersion.CSharp7_1));

        Assert.True(result.HasErrors);
        Assert.Contains("#error CSA1001", result.Code);
        Assert.Contains("no downlevel form", result.Code);
    }

    [Fact]
    public void AGeneratorCanCollectTheDiagnosticInsteadOfThrowing()
    {
        // A source generator cannot usefully throw, so it takes the other branch: the reason ends
        // up in the file, and the consumer's build fails with it rather than compiling something
        // that means something else.
        var result = ProfileEmitter.Emit(DefaultInterfaceMember(), Reporting(LanguageVersion.CSharp7_3));

        Assert.True(result.HasErrors);
        Assert.Contains("#error CSA1001", result.Code);
        Assert.Contains("'default interface member' on Describe", result.Code);
        Assert.StartsWith("#error", result.Code.TrimStart());
    }

    [Fact]
    public void ARecordBelowCSharp9Stops()
    {
        // Writing `class` instead would compile and would not be a record: no value equality, no
        // `with`, no deconstructor. Nothing here generates those, so there is no downlevel.
        var exception = Assert.Throws<EmitCapabilityException>(
            () => ProfileEmitter.Emit(Record(ClassKeyword.Record), EmitProfile.Conservative));

        Assert.Equal(LanguageFeature.Records, exception.Diagnostic.Feature);
        Assert.Equal(LanguageVersion.CSharp9, exception.Diagnostic.RequiredVersion);
    }

    [Fact]
    public void ARecordIsWrittenFromCSharp9()
    {
        AssertEqual.ContainsWithoutNewLine(
            "public record Pet",
            ProfileEmitter.Emit(Record(ClassKeyword.Record), Target(LanguageVersion.CSharp9)).Code);
    }

    [Fact]
    public void ARecordStructNeedsCSharp10()
    {
        Assert.Throws<EmitCapabilityException>(
            () => ProfileEmitter.Emit(Record(ClassKeyword.RecordStruct), Target(LanguageVersion.CSharp9)));

        AssertEqual.ContainsWithoutNewLine(
            "public record struct Pet",
            ProfileEmitter.Emit(Record(ClassKeyword.RecordStruct), Target(LanguageVersion.CSharp10)).Code);
    }

    [Fact]
    public void APrimaryConstructorOnAClassNeedsCSharp12()
    {
        // Free in the table - it could be written out as fields and a constructor - but nothing
        // here writes that, and dropping the parameters would leave a type with no way to
        // construct it. A writer with no alternative demands rather than asks.
        var exception = Assert.Throws<EmitCapabilityException>(
            () => ProfileEmitter.Emit(ClassWithPrimaryConstructor(), Target(LanguageVersion.CSharp11)));

        Assert.Equal(LanguageFeature.PrimaryConstructors, exception.Diagnostic.Feature);

        AssertEqual.ContainsWithoutNewLine(
            "public class Widget(string id)",
            ProfileEmitter.Emit(ClassWithPrimaryConstructor(), Target(LanguageVersion.CSharp12)).Code);
    }

    [Theory]
    [InlineData(LanguageFeature.FunctionPointers, LanguageVersion.CSharp8)]
    [InlineData(LanguageFeature.InlineArrays, LanguageVersion.CSharp11)]
    [InlineData(LanguageFeature.RefStructs, LanguageVersion.CSharp7_1)]
    [InlineData(LanguageFeature.StaticAbstractInterfaceMembers, LanguageVersion.CSharp10)]
    [InlineData(LanguageFeature.DefaultInterfaceMembers, LanguageVersion.CSharp7_3)]
    public void EveryImpossibleFeatureGoesThroughTheSameGate(LanguageFeature feature, LanguageVersion target)
    {
        var session = new EmitSession(Target(target));

        Assert.Throws<EmitCapabilityException>(() => session.Require(feature, "Member"));
    }

    [Fact]
    public void AskingWhetherAnImpossibleFeatureIsAllowedIsTheWrongQuestion()
    {
        // There is no other form to fall back to, so "may I?" cannot be answered with a quiet no.
        var session = new EmitSession(EmitProfile.Conservative);

        Assert.Throws<EmitCapabilityException>(
            () => session.MayEmit(LanguageFeature.InlineArrays, "Buffer"));
    }

    [Fact]
    public void ADiagnosticIsRecordedOncePerDistinctCause()
    {
        var session = new EmitSession(Reporting(LanguageVersion.CSharp10));

        session.Require(LanguageFeature.InlineArrays, "Buffer");
        session.Require(LanguageFeature.InlineArrays, "Buffer");
        session.Require(LanguageFeature.InlineArrays, "Other");

        Assert.Equal(2, session.Diagnostics.Count);
        Assert.True(session.HasErrors);
    }

    private static EmitProfile Target(LanguageVersion version) =>
        EmitProfile.Default.With(p => p.Target = version);

    private static EmitProfile Reporting(LanguageVersion version) =>
        EmitProfile.Default.With(p =>
        {
            p.Target = version;
            p.OnCapabilityViolation = CapabilityViolationBehavior.EmitErrorDirective;
        });

    private static InterfaceMethodDefinition DefaultInterfaceMember()
    {
        var method = new InterfaceMethodDefinition("Describe");

        method.SetReturnType(typeof(string));
        method.Return("\"widget\"");

        return method;
    }

    private static InterfaceMethodDefinition StaticAbstractMember()
    {
        var method = new InterfaceMethodDefinition("Create") { IsStaticAbstract = true };

        method.SetReturnType(TypeDefinition.Get("Acme", "Widget"));

        return method;
    }

    private static ClassDefinition Record(ClassKeyword keyword) =>
        new ClassDefinition("Pet")
        {
            TypeKeyword = keyword,
            Modifiers = ComponentModifier.Public
        };

    private static ClassDefinition ClassWithPrimaryConstructor()
    {
        var definition = new ClassDefinition("Widget") { Modifiers = ComponentModifier.Public };

        var constructor = definition.AddConstructor();

        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "id");

        return definition;
    }

    private static ClassDefinition RefStruct() =>
        new ClassDefinition("Span")
        {
            TypeKeyword = ClassKeyword.Struct,
            IsRefStruct = true,
            Modifiers = ComponentModifier.Public
        };
}
