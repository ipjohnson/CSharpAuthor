using Xunit;

namespace CSharpAuthor.Tests.MethodTests;

/// <summary>
/// A parameter's requested namespaces reach the file, and a parameter used as a call receiver is
/// escaped the same way its declaration is.
/// </summary>
/// <remarks>
/// Both were silent. <c>AddUsingNamespace</c> is public API inherited from
/// <see cref="BaseOutputComponent"/>, and on a parameter it compiled and did nothing: a parameter
/// is written inline by <see cref="ParameterDefinition.WriteWithSignature"/>, which never goes
/// through <see cref="BaseOutputComponent.WriteOutput"/> - the only place the namespaces were read.
///
/// It only bites in a qualifying mode, and there it bites hard: <c>global::</c> cannot name an
/// extension method, so an explicit directive is the only way to reach one. That is what
/// <see cref="OutputContextOptions.EmitExplicitUsings"/> exists for.
/// </remarks>
public class ParameterUsingAndReceiverTests
{
    [Fact]
    public void AParametersNamespaceReachesTheFileInAQualifyingMode()
    {
        var file = new CSharpFileDefinition("Probe");
        var method = file.AddClass("Thing").AddMethod("Run");

        var parameter = method.AddParameter(
            TypeDefinition.Get("Ns.Services", "IServiceCollection"), "services");

        parameter.AddUsingNamespace("Microsoft.Extensions.DependencyInjection");

        method.AddIndentedStatement(parameter.Invoke("AddSingleton"));

        var context = new OutputContext(
            new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        file.WriteOutput(context);

        var output = context.Output();

        // Without this the call does not compile: the extension method has nowhere to resolve from.
        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", output);
        Assert.Contains("services.AddSingleton()", output);
    }

    [Fact]
    public void AndInShortNameModeToo()
    {
        var file = new CSharpFileDefinition("Probe");
        var method = file.AddClass("Thing").AddMethod("Run");

        var parameter = method.AddParameter(TypeDefinition.Get("Ns", "Thing"), "thing");

        parameter.AddUsingNamespace("Some.Extensions");

        var context = new OutputContext();

        file.WriteOutput(context);

        Assert.Contains("using Some.Extensions;", context.Output());
    }

    /// <summary>
    /// The same identifier, escaped the same way in both places it appears.
    /// </summary>
    [Fact]
    public void AKeywordNamedParameterIsEscapedAsAReceiverToo()
    {
        var file = new CSharpFileDefinition("Probe");
        var method = file.AddClass("Thing").AddMethod("Run");

        var parameter = method.AddParameter(TypeDefinition.Get("Ns", "Handler"), "event");

        method.AddIndentedStatement(parameter.Invoke("Go"));

        var context = new OutputContext();

        file.WriteOutput(context);

        var output = context.Output();

        Assert.Contains("Handler @event", output);
        Assert.Contains("@event.Go()", output);

        // `event.Go()` is CS1041. It used to be emitted directly beneath the escaped declaration.
        Assert.DoesNotContain(" event.Go()", output);
    }

    [Fact]
    public void AndAsAGenericReceiver()
    {
        var file = new CSharpFileDefinition("Probe");
        var method = file.AddClass("Thing").AddMethod("Run");

        var parameter = method.AddParameter(TypeDefinition.Get("Ns", "Handler"), "class");

        method.AddIndentedStatement(
            parameter.InvokeGeneric("Go", new[] { TypeDefinition.Get(typeof(int)) }));

        Assert.Contains("@class.Go<int>()", Render(file));
    }

    /// <summary>An ordinary name is untouched, and `this` is never escaped.</summary>
    [Fact]
    public void AnOrdinaryReceiverIsUnchanged()
    {
        var file = new CSharpFileDefinition("Probe");
        var method = file.AddClass("Thing").AddMethod("Run");

        var parameter = method.AddParameter(TypeDefinition.Get("Ns", "Handler"), "handler");

        method.AddIndentedStatement(parameter.Invoke("Go"));

        var output = Render(file);

        Assert.Contains("handler.Go()", output);
        Assert.DoesNotContain("@handler", output);
    }

    private static string Render(CSharpFileDefinition file)
    {
        var context = new OutputContext();

        file.WriteOutput(context);

        return context.Output();
    }
}
