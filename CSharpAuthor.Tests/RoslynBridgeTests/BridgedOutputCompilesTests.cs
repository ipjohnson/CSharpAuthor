using System.Collections.Generic;
using System.Linq;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpAuthor.Tests.RoslynBridgeTests;

/// <summary>
/// Takes every converted type back through the writers and makes the compiler read the result.
/// </summary>
/// <remarks>
/// A conversion that produces the right string in a unit test can still produce a file that does not
/// compile - a missing <c>using</c>, a name that needed escaping, a qualification that is only valid
/// in one output mode. This emits a class with a field of each converted type, in both short-name and
/// fully-qualified mode, and compiles the output against the source the types came from. Nothing
/// here asserts a spelling; the compiler does the asserting.
/// </remarks>
public class BridgedOutputCompilesTests
{
    /// <summary>
    /// Pointers and function pointers are absent because a field of one needs the <c>unsafe</c>
    /// modifier, and the modifier set has no <c>unsafe</c> to give it. They are covered against
    /// Roslyn's own spelling in the display oracle instead.
    /// </summary>
    private const string Fields = @"
        public int[] a1;
        public int[,] a2;
        public int[][] a3;
        public int[,][] a4;
        public int[][,] a5;
        public List<int> a6;
        public Dictionary<string, int> a7;
        public List<Dictionary<string, int?>> a8;
        public int? a9;
        public string? a10;
        public Val? a11;
        public Color? a12;
        public Outer<int>.PlainInner a13;
        public Outer<int>.Inner<string> a14;
        public Outer<int>.Inner<string>.Deepest a15;
        public Plain.Middle.Deepest a16;
        public (int a, string b) a17;
        public (int, string) a18;
        public (int a, (string x, bool y) b) a19;
        public dynamic a20;
        public float a21;
        public char a22;
        public sbyte a23;
        public nint a24;
        public object a25;
        public @event a26;
        public @event.@void a27;
        public string?[] a28;
        public string[]? a29;
        public List<Dictionary<string, int?>>?[][] a30;
        public GlobalThing a31;
        public GlobalThing.Inner a32;
        public IThing a33;
        public List<T> a34;
        public Color a35;
";

    [Theory]
    [InlineData(TypeOutputMode.ShortName)]
    [InlineData(TypeOutputMode.Global)]
    public void EveryConvertedTypeCompiles(TypeOutputMode mode)
    {
        var source = TestCompilation.Wrap(Fields);

        var types = TestCompilation.FieldTypes(Fields);

        var file = new CSharpFileDefinition("BridgeGenerated");

        var classDefinition = file.AddClass("BridgedTypes");

        classDefinition.AddGenericParameter("T");

        foreach (var field in types.OrderBy(pair => pair.Key, System.StringComparer.Ordinal))
        {
            classDefinition.AddField(field.Value.GetTypeDefinition(), "field_" + field.Key);
        }

        var outputContext = new OutputContext(new OutputContextOptions { TypeOutputMode = mode });

        file.WriteOutput(outputContext);

        var generated = outputContext.Output();

        var compilation = TestCompilation.Compile(source)
            .AddSyntaxTrees(TestCompilation.ParseTree(generated));

        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(
            errors.Count == 0,
            string.Join("\n", errors.Select(error => error.ToString())) + "\n---- generated ----\n" + generated);
    }

    /// <summary>
    /// Short-name mode has to derive the imports from what was written. The types the fields name
    /// live in two namespaces and the global one; all of it comes from the type definitions, and
    /// none of it from anyone calling <c>AddImportNamespace</c>.
    /// </summary>
    [Fact]
    public void ShortNameModeDerivesItsUsings()
    {
        var types = TestCompilation.FieldTypes(Fields);

        var file = new CSharpFileDefinition("BridgeGenerated");

        var classDefinition = file.AddClass("BridgedTypes");

        classDefinition.AddGenericParameter("T");

        foreach (var field in types)
        {
            classDefinition.AddField(field.Value.GetTypeDefinition(), "field_" + field.Key);
        }

        var outputContext = new OutputContext();

        file.WriteOutput(outputContext);

        var generated = outputContext.Output();

        Assert.Contains("using BridgeTestNamespace;", generated);
        Assert.Contains("using System.Collections.Generic;", generated);
    }

    /// <summary>
    /// The same tree, written twice, in two modes. Nothing was committed to text at conversion time,
    /// which is the property the type model exists to keep.
    /// </summary>
    [Fact]
    public void TheSameTypeWritesBothWays()
    {
        var typeDefinition = TestCompilation
            .FieldType(Fields, "a14")
            .GetTypeDefinition();

        var written = new List<string>
        {
            TestCompilation.Write(typeDefinition),
            TestCompilation.Write(typeDefinition, TypeOutputMode.Global),
            TestCompilation.Write(typeDefinition, TypeOutputMode.FullName)
        };

        Assert.Equal("Outer<int>.Inner<string>", written[0]);
        Assert.Equal("global::BridgeTestNamespace.Outer<int>.Inner<string>", written[1]);
        Assert.Equal("BridgeTestNamespace.Outer<int>.Inner<string>", written[2]);
    }
}
