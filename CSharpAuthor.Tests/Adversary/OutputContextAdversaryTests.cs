using System;
using System.Linq;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// The output context: line endings, indentation, output mode, and derived using directives.
/// </summary>
public class OutputContextAdversaryTests
{
    /// <summary>
    /// <c>OutputContextOptions.NewLine</c> is honoured by <c>WriteLine()</c> and ignored by
    /// <c>WriteLine(text)</c> and <c>WriteIndentedLine(text)</c>, which call
    /// <c>StringBuilder.AppendLine</c> and so append <see cref="Environment.NewLine"/> instead.
    /// </summary>
    /// <remarks>
    /// Almost every line of a generated file goes through the second path, so on Windows the
    /// configured <c>"\n"</c> default is overridden to CRLF for those lines while the blank lines
    /// between members stay LF - a file with mixed endings, from a library whose option said
    /// otherwise. On Linux and macOS <see cref="Environment.NewLine"/> is <c>"\n"</c>, so the whole
    /// thing is invisible: it reproduces only on the platform nobody generating this ran it on.
    /// </remarks>
    [Fact(Skip = "ADVERSARY GAP: OutputContext.WriteLine(text) and WriteIndentedLine use StringBuilder.AppendLine, which appends Environment.NewLine and ignores Options.NewLine - so a file emitted with NewLine set has mixed line endings, and on Windows the default is silently CRLF")]
    public void NewLineOptionIsHonouredEverywhere()
    {
        var options = new OutputContextOptions { NewLine = "\r\n" };

        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(int), "f");

        var output = Emit.Component(classDefinition, options);

        // Every LF in the output has to be preceded by a CR.
        for (var i = 0; i < output.Length; i++)
        {
            if (output[i] == '\n')
            {
                Assert.True(i > 0 && output[i - 1] == '\r',
                    "a line ending at index " + i + " is LF, not the configured CRLF");
            }
        }
    }

    /// <summary>
    /// The §1 bug, isolated. In <see cref="TypeOutputMode.Global"/> the context does not import
    /// anything - <c>Write(ITypeDefinition)</c> checks the mode first - but
    /// <c>FieldDefinition</c> and <c>ParameterDefinition</c> call <c>AddImportNamespace</c>
    /// themselves, before and outside that check, so the usings appear anyway.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP (§7 'Global mode'): FieldDefinition and ParameterDefinition call AddImportNamespace directly, bypassing the mode check in Write(ITypeDefinition), so a fully global::-qualified file still carries using directives it does not need")]
    public void GlobalModeEmitsNoUsings()
    {
        var file = new CSharpFileDefinition("Probe");

        var classDefinition = file.AddClass("Host");

        classDefinition.AddField(TypeDefinition.Get("Some.Where", "Thing"), "t");
        classDefinition.AddMethod("M").AddParameter(TypeDefinition.Get("Other.Place", "Widget"), "w");

        var output = Emit.File(
            file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Assert.DoesNotContain("using ", output);
    }

    /// <summary>
    /// Two types with the same short name from different namespaces. Both usings are emitted and
    /// both names are written short, so every use of the name is ambiguous - CS0104.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP (§7 'Same-name collision'): two types named Thing from two namespaces are both imported and both written short - CS0104, 'Thing' is an ambiguous reference")]
    public void SameShortNameFromTwoNamespaces()
    {
        var file = new CSharpFileDefinition("Probe");

        var classDefinition = file.AddClass("Host");

        classDefinition.AddField(TypeDefinition.Get("Ns1", "Thing"), "a");
        classDefinition.AddField(TypeDefinition.Get("Ns2", "Thing"), "b");

        RoslynAssert.Compiles(
            "namespace Ns1 { public class Thing { } }\n" +
            "namespace Ns2 { public class Thing { } }\n" +
            Emit.File(file));
    }

    /// <summary>
    /// §7 records that <c>AddCode</c> renders its type arguments eagerly. What that costs is
    /// visible here: the type is rendered at the moment <c>AddCode</c> is called, using the short
    /// name, long before the context knows it is writing a <see cref="TypeOutputMode.Global"/> file
    /// - so it stays short while every other type in the file is qualified.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP (§7 'AddCode'): AddCode calls GetShortName() when the statement is built, so the type is text before the output mode is known - it stays unqualified in a Global-mode file, and its using is emitted to make it resolve")]
    public void AddCodeDefersItsTypes()
    {
        var file = new CSharpFileDefinition("Probe");

        file.AddClass("Host").AddMethod("M")
            .AddCode("var x = new {arg1}();", TypeDefinition.Get("Ns", "Thing"));

        var output = Emit.File(
            file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Assert.Contains("new global::Ns.Thing()", output);
    }

    /// <summary>
    /// A file whose own namespace is imported. Legal, and a line of noise in every snapshot of
    /// every generator that refers to a type beside the one it is writing.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: a type in the file's own namespace is still imported, so the file emits 'using Probe;' above 'namespace Probe'")]
    public void FilesOwnNamespaceIsNotImported()
    {
        var file = new CSharpFileDefinition("Probe");

        file.AddClass("Host").AddField(TypeDefinition.Get("Probe", "Thing"), "t");

        Assert.DoesNotContain("using Probe;", Emit.File(file));
    }

    /// <summary>
    /// Usings are sorted as plain strings, so <c>System</c> sorts among the rest. Every C# style
    /// guide, and the default IDE behaviour, puts the <c>System</c> namespaces first.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: GenerateUsingStatements sorts ordinally, so System namespaces are interleaved with the rest - 'using Aaa; using System.Text; using Zzz;' - which every consuming repository's formatter will want to rewrite")]
    public void SystemUsingsSortFirst()
    {
        var file = new CSharpFileDefinition("Probe");

        var classDefinition = file.AddClass("Host");

        classDefinition.AddField(TypeDefinition.Get("Aaa", "X"), "x");
        classDefinition.AddField(typeof(System.Text.StringBuilder), "sb");
        classDefinition.AddField(TypeDefinition.Get("Zzz", "Y"), "y");

        var usings = Emit.File(file)
            .Split('\n')
            .Where(line => line.StartsWith("using "))
            .ToList();

        Assert.Equal("using System.Text;", usings[0]);
    }

    // ---- context behaviour that works, kept as guards ----

    [Fact]
    public void IndentCharAndWidthAreHonoured()
    {
        var options = new OutputContextOptions { IndentChar = '\t', IndentCharCount = 1 };

        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(int), "f");

        var output = Emit.Component(classDefinition, options);

        Assert.Contains("\tprivate int f;", output);

        RoslynAssert.Compiles(output);
    }

    [Fact]
    public void ShortNameModeImportsAndCompiles()
    {
        var file = new CSharpFileDefinition("Probe");

        file.AddClass("Host").AddField(typeof(System.Text.StringBuilder), "sb");

        var output = Emit.File(file);

        Assert.Contains("using System.Text;", output);

        RoslynAssert.Compiles(output);
    }

    [Fact]
    public void GlobalModeQualifiesAndCompiles()
    {
        var file = new CSharpFileDefinition("Probe");

        file.AddClass("Host").AddField(typeof(System.Text.StringBuilder), "sb");

        var output = Emit.File(
            file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Assert.Contains("global::System.Text.StringBuilder", output);

        RoslynAssert.Compiles(output);
    }

    [Fact]
    public void FullNameModeQualifiesAndCompiles()
    {
        var file = new CSharpFileDefinition("Probe");

        file.AddClass("Host").AddField(typeof(System.Text.StringBuilder), "sb");

        var output = Emit.File(
            file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.FullName });

        Assert.Contains("System.Text.StringBuilder", output);

        RoslynAssert.Compiles(output);
    }

    [Fact]
    public void FileScopedNamespaceCompiles()
    {
        var file = new CSharpFileDefinition("Probe") { FileScopedNamespace = true };

        file.AddClass("Host").AddField(typeof(System.Text.StringBuilder), "sb");

        RoslynAssert.Compiles(Emit.File(file));
    }

    [Fact]
    public void NestedNamespacesCompile()
    {
        var file = new CSharpFileDefinition("A.B.C");

        file.AddClass("Host");

        RoslynAssert.Compiles(Emit.File(file));
    }

    [Fact]
    public void GenerateDocumentationOffOmitsComments()
    {
        var classDefinition = new ClassDefinition("Host") { Comment = "a summary" };

        var output = Emit.Component(
            classDefinition, new OutputContextOptions { GenerateDocumentation = false });

        Assert.DoesNotContain("///", output);
    }

    /// <summary>
    /// A primitive carries no namespace, so nothing is imported for it. Guard, because an import
    /// fix that starts writing empty using directives would be caught here and nowhere else.
    /// </summary>
    [Fact]
    public void PrimitivesImportNothing()
    {
        var file = new CSharpFileDefinition("Probe");

        var classDefinition = file.AddClass("Host");

        classDefinition.AddField(typeof(int), "i");
        classDefinition.AddField(typeof(string), "s");

        var output = Emit.File(file);

        Assert.DoesNotContain("using ", output);

        RoslynAssert.Compiles(output);
    }
}
