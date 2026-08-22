using Xunit;

namespace CSharpAuthor.Tests.OutputContextTests;

/// <summary>
/// A type handed to <c>AddCode</c>, or carried by a statement that is otherwise a string, stays a
/// type until the file is serialized.
/// </summary>
/// <remarks>
/// <c>AddCode</c> used to substitute the short name straight into the statement. That decided how
/// the type would be written before the file knew what mode it would be written in, so a file that
/// qualifies everything still got a bare name - and then needed a <c>using</c>, declared on the
/// side, to make that name resolve. Holding the type instead means one decision, made once, at the
/// end, for every type in the file.
/// </remarks>
public class DeferredCodeTests
{
    [Fact]
    public void ASubstitutedTypeIsWrittenByShortNameAndBringsItsNamespace()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddCode("var value = new {arg1}();", TypeDefinition.Get("Sample.Models", "Result"));

        var output = Write(file, TypeOutputMode.ShortName);

        Assert.Contains("using Sample.Models;", output);
        Assert.Contains("var value = new Result();", output);
    }

    /// <summary>
    /// The same statement in a file that qualifies its types. Version 1 wrote <c>new Result()</c>
    /// here and added a using to make it resolve.
    /// </summary>
    [Fact]
    public void ASubstitutedTypeIsQualifiedWhenTheModeQualifies()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddCode("var value = new {arg1}();", TypeDefinition.Get("Sample.Models", "Result"));

        var output = Write(file, TypeOutputMode.Global);

        Assert.DoesNotContain("using ", output);
        Assert.Contains("var value = new global::Sample.Models.Result();", output);
    }

    [Fact]
    public void ASubstitutedTypeIsWrittenEverywhereItAppears()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddCode("{arg1} value = new {arg1}();", TypeDefinition.Get("Sample.Models", "Result"));

        var output = Write(file, TypeOutputMode.Global);

        Assert.Contains(
            "global::Sample.Models.Result value = new global::Sample.Models.Result();", output);
    }

    [Fact]
    public void ARuntimeTypeIsSubstitutedTheSameWay()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddCode("var value = new {arg1}();", typeof(System.Text.StringBuilder));

        var output = Write(file, TypeOutputMode.Global);

        Assert.DoesNotContain("using ", output);
        Assert.Contains("new global::System.Text.StringBuilder();", output);
    }

    /// <summary>A raw substitution is text by definition and is still substituted where it stands.</summary>
    [Fact]
    public void ARawSubstitutionIsStillText()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddCode("var value = [arg1];", "42");

        Assert.Contains("var value = 42;", Write(file, TypeOutputMode.ShortName));
    }

    /// <summary>
    /// A non-type substitution is code, not a literal, which is what makes <c>{argN}</c> and
    /// <c>[argN]</c> agree for a plain string. They still differ for a type and for an
    /// <c>enum</c>, which is the difference the two spellings exist for.
    /// </summary>
    [Fact]
    public void ANonTypeSubstitutionIsCodeNotALiteral()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddCode("var value = {arg1};", "text");

        Assert.Contains("var value = text;", Write(file, TypeOutputMode.ShortName));
    }

    /// <summary>A caller that means a literal asks for one.</summary>
    [Fact]
    public void AQuotedSubstitutionIsALiteral()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.AddCode("var value = {arg1};", SyntaxHelpers.QuoteString("text"));

        Assert.Contains("var value = \"text\";", Write(file, TypeOutputMode.ShortName));
    }

    /// <summary>
    /// The case the whole thing turns on: a member reached off a type, written as a string.
    /// </summary>
    /// <remarks>
    /// <c>CodeOutputComponent.Get("ServiceLifetime.Transient")</c> tracks no namespace, so the name
    /// resolved only because something else had brought <c>Microsoft.Extensions.DependencyInjection</c>
    /// in - and in a file where every other name is qualified, nothing should have. Handing over the
    /// type gives the string somewhere to come from.
    /// </remarks>
    [Fact]
    public void AMemberOffATypeCarriesTheTypeItComesFrom()
    {
        var serviceLifetime =
            TypeDefinition.Get("Microsoft.Extensions.DependencyInjection", "ServiceLifetime");

        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.Add(CodeOutputComponent.Get(serviceLifetime, "Transient"));

        var globalOutput = Write(file, TypeOutputMode.Global);

        Assert.DoesNotContain("using ", globalOutput);
        Assert.Contains(
            "global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient", globalOutput);
    }

    [Fact]
    public void AMemberOffATypeBringsItsNamespaceByShortName()
    {
        var serviceLifetime =
            TypeDefinition.Get("Microsoft.Extensions.DependencyInjection", "ServiceLifetime");

        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.Add(CodeOutputComponent.Get(serviceLifetime, "Transient"));

        var output = Write(file, TypeOutputMode.ShortName);

        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", output);
        Assert.Contains("ServiceLifetime.Transient", output);
    }

    /// <summary>A type handed to <c>Get</c> on its own is a type, not its own name.</summary>
    [Fact]
    public void ATypeHandedToGetIsDeferred()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var method = file.AddClass("Service").AddMethod("Handle");

        method.Add(CodeOutputComponent.Get(TypeDefinition.Get("Sample.Models", "Result")));

        Assert.Contains("global::Sample.Models.Result", Write(file, TypeOutputMode.Global));
    }

    /// <summary>
    /// A type written through <c>AddCode</c> is in the name plan like any other, so it is aliased
    /// when its name is contested.
    /// </summary>
    [Fact]
    public void ASubstitutedTypeTakesPartInTheNamePlan()
    {
        var file = new CSharpFileDefinition("TestNamespace");
        var classDefinition = file.AddClass("Service");

        var method = classDefinition.AddMethod("Handle");
        method.AddParameter(TypeDefinition.Get("First", "Model"), "a");
        method.AddCode("var value = new {arg1}();", TypeDefinition.Get("Second", "Model"));

        var output = Write(file, TypeOutputMode.ShortName);

        Assert.Contains("using SecondModel = Second.Model;", output);
        Assert.Contains("var value = new SecondModel();", output);
    }

    private static string Write(CSharpFileDefinition file, TypeOutputMode mode)
    {
        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });

        file.WriteOutput(context);

        return context.Output();
    }
}
