using System;
using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Whole files: nesting, base lists, member layout, and what happens when a caller gets it wrong.
/// </summary>
public class StructureAdversaryTests
{
    /// <summary>
    /// <c>AddBaseType</c> returns early when the type is already in the list, so adding the same
    /// base twice - once bare and once with the arguments its constructor needs - keeps the first
    /// and drops the arguments.
    /// </summary>
    /// <remarks>
    /// The order that produces it is the natural one: a generator reads the base type from a symbol
    /// and adds it, then works out the arguments and adds them. What comes out is
    /// <c>record Dog(string Id) : Pet;</c> - a call to a constructor with no arguments where one
    /// takes a parameter, which is CS7036 at the consumer.
    /// </remarks>
    [Fact(Skip = "ADVERSARY GAP: AddBaseType deduplicates on the type alone, so a second call carrying constructor arguments is discarded - 'record Dog(string Id) : Pet;' rather than ': Pet(Id)', CS7036")]
    public void AddBaseTypeTwiceKeepsTheArguments()
    {
        var record = new ClassDefinition("Dog")
        {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true
        };

        var constructor = record.AddConstructor();

        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");

        record.AddBaseType(TypeDefinition.Get("Probe", "Pet"));
        record.AddBaseType(TypeDefinition.Get("Probe", "Pet"), CodeOutputComponent.Get("Id"));

        RoslynAssert.Compiles(
            "namespace Probe { public record Pet(string Id); }\n" +
            "using Probe;\n" +
            Emit.Component(record));
    }

    /// <summary>
    /// A generic type built with an empty argument list writes <c>Thing&lt;&gt;</c>, which is only
    /// legal inside <c>typeof</c>. In a field or parameter position it is CS1031.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: GenericTypeDefinition with no type arguments writes an empty argument list - Thing<> - which is CS1031 anywhere except inside typeof")]
    public void GenericTypeWithNoArguments()
    {
        var type = new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Probe", "Thing", Array.Empty<ITypeDefinition>());

        RoslynAssert.MemberCompiles(
            "public " + Emit.TypeName(type) + " Field;",
            preamble: "namespace Probe { public class Thing { } }\n");
    }

    /// <summary>
    /// <c>EnumValueDefinition</c> writes <c>Value.ToString()</c>, which is the ambient-culture,
    /// CLR-formatted rendering rather than a C# literal - so a <see cref="bool"/> becomes
    /// <c>False</c> with a capital F. It is the same root cause as the culture defect: a value is
    /// turned into text by whatever <c>ToString</c> happens to do.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: EnumValueDefinition writes Value.ToString() rather than going through CodeOutputComponent, so a bool value emits 'A = False' - CS0103 - where the component would have written 'false'")]
    public void EnumMemberValueUsesCSharpLiteralForm()
    {
        var enumDefinition = new EnumDefinition("E");

        enumDefinition.AddValue("A", false);

        RoslynAssert.Compiles(Emit.Component(enumDefinition));
    }

    /// <summary>
    /// Closing more scopes than were opened builds a negative indent and throws out of
    /// <c>new string(char, count)</c> - an exception naming a count, from a stack that says nothing
    /// about which component was unbalanced.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: CloseScope past zero throws ArgumentOutOfRangeException from new string(char, -4), rather than reporting an unbalanced scope")]
    public void ClosingAnUnopenedScope()
    {
        var context = new OutputContext();

        var exception = Record.Exception(() => context.CloseScope());

        Assert.True(
            exception is null or InvalidOperationException,
            "expected either no error or a diagnosis of the unbalanced scope, got: " + exception);
    }

    /// <summary>
    /// A blank line is written before every member, including the first, so a type's body opens with
    /// an empty line. Harmless, and present in the snapshot of every generated type in both
    /// consumer repositories.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: WriteMemberComponents writes a separating blank line before each member including the first, so every class body opens with an empty line")]
    public void NoBlankLineBeforeTheFirstMember()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddProperty(typeof(int), "P");

        Assert.DoesNotContain("{\n\n", Emit.Component(classDefinition).Replace("\r\n", "\n"));
    }

    // ---- structure that works, kept as guards ----

    /// <summary>
    /// Three levels of nesting inside a namespace. Indentation is the thing a tree is supposed to
    /// get right for free, and it does.
    /// </summary>
    [Fact]
    public void DeeplyNestedTypesIndentAndCompile()
    {
        var file = new CSharpFileDefinition("A.B");

        var inner = file.AddClass("Outer").AddClass("Mid").AddClass("Inner");

        inner.AddField(typeof(int), "x");
        inner.AddMethod("M").AddCode("var y = 1;");

        var output = Emit.File(file);

        Assert.Contains("                private int x;", output);

        RoslynAssert.Compiles(output);
    }

    [Fact]
    public void BrokenArgumentListsCompile()
    {
        var method = new MethodDefinition("M");

        method.Assign(New(TypeDefinition.Get("Probe", "Thing"),
            CodeOutputComponent.Get("1"),
            CodeOutputComponent.Get("2"),
            CodeOutputComponent.Get("3"))).ToVar("t");

        RoslynAssert.MemberCompiles(
            Emit.Component(method),
            preamble: "using Probe;\nnamespace Probe { public class Thing { public Thing(int a, int b, int c) { } } }\n");
    }

    [Fact]
    public void UnbrokenArgumentListsCompile()
    {
        var method = new MethodDefinition("M");

        method.Assign(New(TypeDefinition.Get("Probe", "Thing"),
            CodeOutputComponent.Get("1"),
            CodeOutputComponent.Get("2"))).ToVar("t");

        var output = Emit.Component(method, new OutputContextOptions { BreakInvokeLines = false });

        Assert.Contains("new Thing(1, 2)", output);

        RoslynAssert.MemberCompiles(
            output,
            preamble: "using Probe;\nnamespace Probe { public class Thing { public Thing(int a, int b) { } } }\n");
    }

    [Fact]
    public void ObjectInitializerCompiles()
    {
        var statement = New(TypeDefinition.Get("Probe", "Thing"));

        statement.AddInitValue(CodeOutputComponent.Get("A = 1"));
        statement.AddInitValue(CodeOutputComponent.Get("B = 2"));

        RoslynAssert.ExpressionCompiles(
            Emit.Component(statement),
            preamble: "using Probe;\nnamespace Probe { public class Thing { public int A; public int B; } }\n");
    }

    [Fact]
    public void BaseTypeWithConstructorArgumentsCompiles()
    {
        var record = new ClassDefinition("Dog")
        {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true
        };

        var constructor = record.AddConstructor();

        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");

        record.AddBaseType(TypeDefinition.Get("Probe", "Pet"), CodeOutputComponent.Get("Id"));

        RoslynAssert.Compiles(
            "using Probe;\nnamespace Probe { public record Pet(string Id); }\n" +
            Emit.Component(record));
    }

    /// <summary>
    /// A realistic generated file, end to end: file-scoped namespace, derived usings, a partial
    /// class with an interface, a readonly field, a constructor that assigns it, an init-only
    /// property and a method. This is the shape a generator actually emits, and it is the guard
    /// that catches a fix which is correct in isolation and wrong in composition.
    /// </summary>
    [Fact]
    public void ARealisticGeneratedFileCompiles()
    {
        var file = new CSharpFileDefinition("Probe.Generated") { FileScopedNamespace = true };

        var classDefinition = file.AddClass("Service");

        classDefinition.Modifiers = ComponentModifier.Public | ComponentModifier.Partial;
        classDefinition.AddBaseType(TypeDefinition.Get("System", "IDisposable"));

        classDefinition.AddField(TypeDefinition.Get("System.Text", "StringBuilder"), "_builder")
            .Modifiers = ComponentModifier.Private | ComponentModifier.Readonly;

        var constructor = classDefinition.AddConstructor();

        constructor.AddParameter(TypeDefinition.Get("System.Text", "StringBuilder"), "builder");
        constructor.Assign(CodeOutputComponent.Get("builder")).To("_builder");

        classDefinition.AddProperty(TypeDefinition.Get(typeof(string)), "Name").Set!.IsInit = true;

        var method = classDefinition.AddMethod("Dispose");

        method.SetReturnType(typeof(void));
        method.AddCode("_builder.Clear();");

        RoslynAssert.Compiles(Emit.File(file));
    }

    /// <summary>
    /// The same file in <see cref="TypeOutputMode.Global"/>. It compiles - the stray usings are
    /// redundant rather than wrong - so this guards the qualification path while
    /// <see cref="OutputContextAdversaryTests.GlobalModeEmitsNoUsings"/> records what is wrong
    /// about it.
    /// </summary>
    [Fact]
    public void ARealisticGeneratedFileCompilesInGlobalMode()
    {
        var file = new CSharpFileDefinition("Probe.Generated") { FileScopedNamespace = true };

        var classDefinition = file.AddClass("Service");

        classDefinition.AddBaseType(TypeDefinition.Get("System", "IDisposable"));
        classDefinition.AddField(TypeDefinition.Get("System.Text", "StringBuilder"), "_builder");

        var method = classDefinition.AddMethod("Dispose");

        method.SetReturnType(typeof(void));

        RoslynAssert.Compiles(
            Emit.File(file, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global }));
    }
}
