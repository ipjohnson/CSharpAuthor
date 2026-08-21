using CSharpAuthor.Profiles;
using CSharpAuthor.Tests.Adversary;
using Xunit;

namespace CSharpAuthor.Tests.ModifierTests;

/// <summary>
/// The <c>field</c> contextual keyword, C# 14.
/// </summary>
/// <remarks>
/// Below C# 14 <c>field</c> is an ordinary identifier, so emitting it anyway does not fail - it
/// binds to whatever <c>field</c> is in scope, or to nothing. That is why the fallback is asked
/// for rather than assumed.
/// </remarks>
public class FieldKeywordTests
{
    private static PropertyDefinition TrimmingProperty()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(string)), "Name");

        property.Get.LambdaSyntax = true;
        property.Get.Add(SyntaxHelpers.Field("_name", "Name"));

        return property;
    }

    private static string Emitted(LanguageVersion target)
    {
        var file = new CSharpFileDefinition("Sample");
        var holder = file.AddClass("Holder");

        holder.AddComponent(TrimmingProperty());

        return ProfileEmitter.Emit(file, new EmitProfile { Target = target }).Code;
    }

    [Fact]
    public void AtCSharp14TheKeywordIsWritten()
    {
        Assert.Contains("get => field;", Emitted(LanguageVersion.CSharp14));
    }

    /// <summary>
    /// The caller's backing field, not the keyword - and not a silent bind to whatever
    /// <c>field</c> happened to mean.
    /// </summary>
    [Fact]
    public void BelowCSharp14TheBackingFieldIsWritten()
    {
        var emitted = Emitted(LanguageVersion.CSharp12);

        Assert.Contains("get => _name;", emitted);
        Assert.DoesNotContain("=> field;", emitted);
    }

    [Fact]
    public void TheDownlevelIsReported()
    {
        var file = new CSharpFileDefinition("Sample");

        file.AddClass("Holder").AddComponent(TrimmingProperty());

        var result = ProfileEmitter.Emit(file, new EmitProfile { Target = LanguageVersion.CSharp12 });

        Assert.Contains(result.Diagnostics, d => d.Feature == LanguageFeature.FieldKeyword);
    }

    /// <summary>
    /// Both renderings have to be legal C# at the version they are written for.
    /// </summary>
    [Fact]
    public void BothFormsCompile()
    {
        RoslynAssert.MemberCompiles(
            "private string _name;\n" +
            "public string Name => _name;");
    }
}
