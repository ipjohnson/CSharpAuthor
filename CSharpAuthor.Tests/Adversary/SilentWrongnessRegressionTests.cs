using System;
using System.Collections.Generic;
using CSharpAuthor.Expressions;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// The declaration-level defects found in the preview1003 review, each pinned by compiling what
/// the library writes.
/// </summary>
/// <remarks>
/// Every case here emitted confident, well-formatted output. Four of the six produced code that
/// does not compile, and two produced code that compiles and is wrong - which is the worse half,
/// because nothing reports it. What they have in common is that a string-equality assertion would
/// have passed on all of them: the output looked exactly like what the writer meant to write.
/// </remarks>
public class SilentWrongnessRegressionTests
{
    private static string Render(CSharpFileDefinition file) =>
        Emit.File(file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

    private static CSharpFileDefinition File(out ClassDefinition host)
    {
        var file = new CSharpFileDefinition("Probe.Regression");

        host = file.AddClass("Host");
        host.Modifiers = ComponentModifier.Public;

        return file;
    }

    /// <summary>
    /// A get-only auto-property keeps its initializer.
    /// </summary>
    /// <remarks>
    /// The accessor-list branch returned as soon as it had written <c>{ get; }</c>, so
    /// <c>DefaultValue</c> was dropped. The result compiled, which is what made it dangerous: under
    /// <c>Nullable=disable</c> nothing warned, and the property simply held its type's default at
    /// run time instead of the value the generator set.
    /// </remarks>
    [Fact]
    public void GetOnlyAutoPropertyKeepsItsInitializer()
    {
        var file = File(out var host);

        var property = host.AddProperty(typeof(int), "Count");
        property.Modifiers = ComponentModifier.Public;
        property.Set = null;
        property.DefaultValue = new CodeOutputComponent("7") { Indented = false };

        var output = Render(file);

        Assert.Contains("public int Count { get; } = 7;", output);

        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// An expression-bodied accessor built with <c>Return</c>.
    /// </summary>
    /// <remarks>
    /// <c>LambdaSyntax</c> and <c>Return</c> are both documented, and pairing them - the obvious
    /// thing to do - wrote <c>public int Answer =&gt;         return 42;</c> followed by a stray
    /// terminator on its own line. Two wrappers were in the way: <c>Return</c> builds an
    /// <c>AppendStatement</c>, and <c>AddIndentedStatement</c> wraps that in an
    /// <c>IndentedStatementComponent</c>, which is what wrote the block indent and the <c>;</c>.
    /// </remarks>
    [Fact]
    public void ExpressionBodiedAccessorUnwrapsItsReturn()
    {
        var file = File(out var host);

        var property = host.AddProperty(typeof(int), "Answer");
        property.Modifiers = ComponentModifier.Public;
        property.Set = null;
        property.Get.LambdaSyntax = true;
        property.Get.Return(Ex.Int(42));

        var output = Render(file);

        Assert.Contains("public int Answer => 42;", output);
        Assert.DoesNotContain("return", output);

        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// An interface method writes the constraints it was given.
    /// </summary>
    /// <remarks>
    /// <c>InterfaceMethodDefinition</c> overrode <c>WriteEndOfMethodSignature</c> to write its own
    /// terminator and never called the base, so the constraint loop went with it. <c>AddConstraint</c>
    /// accepted the constraint, stored it, and nothing ever wrote it - the generated interface was
    /// simply less constrained than the one asked for, and compiled.
    /// </remarks>
    [Fact]
    public void InterfaceMethodWritesItsConstraints()
    {
        var file = new CSharpFileDefinition("Probe.Regression");

        var contract = file.AddInterface("IContract");
        contract.Modifiers = ComponentModifier.Public;

        var method = contract.AddMethod("Go");
        method.SetReturnType(typeof(void));
        method.AddGenericParameter(TypeDefinition.Get("", "T"));
        method.AddConstraint("T").Class().DefaultConstructor();

        var output = Render(file);

        Assert.Contains("void Go<T>() where T : class, new();", output);

        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// A base-class constraint is written before the interfaces it is listed with.
    /// </summary>
    /// <remarks>
    /// C# takes <c>where T : Stream, IDisposable</c> and rejects the reverse with CS0406. Callers
    /// add constraints in whatever order they read them - a loop over a symbol's
    /// <c>ConstraintTypes</c> has no reason to sort - so the order is fixed at the point where the
    /// kind of each type is already known.
    /// </remarks>
    [Fact]
    public void BaseClassConstraintIsWrittenBeforeInterfaces()
    {
        var file = File(out var host);

        var method = host.AddMethod("Go");
        method.Modifiers = ComponentModifier.Public;
        method.SetReturnType(typeof(void));
        method.AddGenericParameter(TypeDefinition.Get("", "T"));

        // The interface first, which is the order that used to emit CS0406.
        method.AddConstraint("T")
            .Implements(TypeDefinition.Get(typeof(IDisposable)))
            .Implements(TypeDefinition.Get(typeof(System.IO.Stream)));

        var output = Render(file);

        var stream = output.IndexOf("System.IO.Stream", StringComparison.Ordinal);
        var disposable = output.IndexOf("System.IDisposable", StringComparison.Ordinal);

        Assert.True(stream >= 0 && disposable >= 0);
        Assert.True(stream < disposable, "the base class has to precede the interface");

        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// An assembly attribute is written above the namespace.
    /// </summary>
    /// <remarks>
    /// <c>AddComponent</c> forwarded everything to the namespace, so an assembly attribute landed
    /// inside it - CS1730, which says these must precede every element in the file except usings
    /// and extern aliases. There was no other API for one, so the construct was unreachable.
    /// </remarks>
    [Fact]
    public void AssemblyAttributeIsWrittenAboveTheNamespace()
    {
        var file = File(out _);

        file.AddComponent(
            new AttributeDefinition(TypeDefinition.Get("System.Reflection", "AssemblyMetadataAttribute"))
            {
                Target = "assembly"
            });

        var output = Render(file);

        var attribute = output.IndexOf("[assembly:", StringComparison.Ordinal);
        var namespaceKeyword = output.IndexOf("namespace ", StringComparison.Ordinal);

        Assert.True(attribute >= 0, "the attribute has to be written at all");
        Assert.True(attribute < namespaceKeyword, "and above the namespace");
    }

    /// <summary>
    /// An unbound generic in <c>typeof</c>.
    /// </summary>
    /// <remarks>
    /// <c>typeof(List&lt;&gt;)</c> is neither a constructed generic nor a plain type, so it fell to
    /// the branch that writes <c>Type.Name</c> straight through - and <c>Type.Name</c> for an
    /// unbound generic is the CLR's <c>List`1</c>, which is not C#.
    /// </remarks>
    [Theory]
    [MemberData(nameof(UnboundGenerics))]
    public void UnboundGenericWritesEmptyTypeArguments(Type type, string expected)
    {
        var file = File(out var host);

        var method = host.AddMethod("Go");
        method.Modifiers = ComponentModifier.Public;
        method.SetReturnType(typeof(Type));
        method.Return(Ex.TypeOf(TypeDefinition.Get(type)));

        var output = Render(file);

        Assert.Contains(expected, output);
        Assert.DoesNotContain("`", output);

        RoslynAssert.Compiles(output);
    }

    public static TheoryData<Type, string> UnboundGenerics() => new()
    {
        { typeof(List<>), "typeof(global::System.Collections.Generic.List<>)" },
        { typeof(Dictionary<,>), "typeof(global::System.Collections.Generic.Dictionary<,>)" },
    };

    /// <summary>
    /// Documentation text is XML-escaped.
    /// </summary>
    /// <remarks>
    /// A generator mirroring a user's type documents something as <c>List&lt;string&gt;</c> sooner
    /// or later, and written through that is malformed XML - CS1570 in the consumer's build, not
    /// the generator's. The trade is that markup can no longer be embedded in a comment; prose is
    /// what these properties are handed, and prose silently producing broken XML is the worse
    /// failure.
    /// </remarks>
    [Fact]
    public void DocumentationTextIsEscaped()
    {
        var file = File(out var host);

        var method = host.AddMethod("Go");
        method.Modifiers = ComponentModifier.Public;
        method.SetReturnType(typeof(void));
        method.Comment = "Takes a List<string> & returns <nothing>";

        var output = Render(file);

        Assert.Contains("List&lt;string&gt; &amp; returns &lt;nothing&gt;", output);

        Assert.Empty(RoslynAssert.Errors(output, RoslynAssert.MaxLanguageVersion, "CS1570"));
    }
}
