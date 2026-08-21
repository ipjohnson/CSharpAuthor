using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CSharpAuthor.Tests.FacadeCoverage;

/// <summary>
/// <c>AddUsingNamespace</c> works on every component that offers it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BaseOutputComponent"/> gives all 45 of its subclasses a public
/// <c>AddUsingNamespace</c>, but the namespaces it collects are read in exactly one place:
/// <see cref="BaseOutputComponent.WriteOutput"/>. A component that is written inline - as part of
/// a line rather than as a line of its own - reaches output through a different entry point, and
/// every such entry point silently dropped them.
/// </para>
/// <para>
/// That is the shape of the bug rather than an instance of it, so this file ratchets the shape.
/// Inherited API is a blind spot: it looks tested because the base class is tested, and a subclass
/// that quietly opts out of the base implementation is invisible to a test written against the base.
/// </para>
/// </remarks>
public class UsingNamespaceRoutingTests
{
    /// <summary>
    /// Every alternate write entry point, and whether it has been checked to route namespaces.
    /// </summary>
    /// <remarks>
    /// A new inline entry point fails this test until it is added here, which is the point: adding
    /// the entry is the moment someone has to decide whether it routes.
    /// </remarks>
    private static readonly Dictionary<string, bool> WriteEntryPoints = new()
    {
        // The base implementation - the one that always routed.
        ["BaseOutputComponent.WriteOutput"] = true,

        // Inline entry points. Both of these dropped namespaces until they were fixed.
        ["ParameterDefinition.WriteWithSignature"] = true,
        ["AttributeDefinition.WriteInline"] = true,
    };

    [Fact]
    public void EveryWriteEntryPointOnAComponentIsAccountedFor()
    {
        var found = typeof(BaseOutputComponent).Assembly
            .GetTypes()
            .Where(t => typeof(BaseOutputComponent).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.Name.StartsWith("Write", StringComparison.Ordinal)
                        && m.ReturnType == typeof(void)
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(IOutputContext))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Distinct()
            .ToList();

        var unaccounted = found.Where(n => !WriteEntryPoints.ContainsKey(n)).ToList();

        Assert.True(
            unaccounted.Count == 0,
            "These write a component to an output context but have not been checked for whether "
            + "they route UsingNamespaces. Verify each, then add it to WriteEntryPoints: "
            + string.Join(", ", unaccounted));

        // And nothing on the list has been deleted out from under it.
        var missing = WriteEntryPoints.Keys.Where(k => !found.Contains(k)).ToList();

        Assert.True(
            missing.Count == 0,
            "Listed but no longer present - remove from WriteEntryPoints: " + string.Join(", ", missing));
    }

    [Fact]
    public void AParameterAttributesNamespaceReachesTheFile()
    {
        var file = new CSharpFileDefinition("Probe");
        var method = file.AddClass("Thing").AddMethod("Run");

        var parameter = method.AddParameter(TypeDefinition.Get("Ns", "Handler"), "handler");

        parameter.AddAttribute(TypeDefinition.Get("Ns.Attrs", "NotNull"))
            .AddUsingNamespace("Some.Analyzer.Annotations");

        var context = new OutputContext(
            new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        file.WriteOutput(context);

        Assert.Contains("using Some.Analyzer.Annotations;", context.Output());
    }

    /// <summary>
    /// The namespaces are routed in a qualifying mode too, where they are the only way to reach an
    /// extension method, and are not confused for the type's own namespace.
    /// </summary>
    [Theory]
    [InlineData(TypeOutputMode.ShortName)]
    [InlineData(TypeOutputMode.Global)]
    [InlineData(TypeOutputMode.FullName)]
    public void InEveryOutputMode(TypeOutputMode mode)
    {
        var file = new CSharpFileDefinition("Probe");
        var method = file.AddClass("Thing").AddMethod("Run");

        method.AddParameter(TypeDefinition.Get("Ns", "Handler"), "handler")
            .AddUsingNamespace("Explicitly.Requested");

        var context = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });

        file.WriteOutput(context);

        Assert.Contains("using Explicitly.Requested;", context.Output());
    }
}
