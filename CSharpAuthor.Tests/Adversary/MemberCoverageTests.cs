using System;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Member declarations with no entry point, and the modifiers <see cref="ComponentModifier"/> has
/// no flag for.
/// </summary>
/// <remarks>
/// Each of these is a coverage finding rather than a defect: nothing emits the wrong thing, because
/// nothing emits anything. They are here because a coverage percentage with the gaps named is what
/// §9 asks for, and because a test is checked by the build where a list in a document is not.
/// </remarks>
public class MemberCoverageTests
{
    /// <summary>
    /// <c>ComponentModifier.Const</c> shipped in preview1002. The assertion is the thing a
    /// <c>static readonly</c> cannot do: stand as a <c>case</c> label.
    /// </summary>
    [Fact]
    public void ConstFields()
    {
        var field = new FieldDefinition(TypeDefinition.Get(typeof(int)), "X")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Const,
            InitializeValue = CodeOutputComponent.Get(1)
        };

        var emitted = Emit.Component(field);

        Assert.Equal("public const int X = 1;\n", emitted.Replace("\r\n", "\n"));
        RoslynAssert.MemberCompiles(
            emitted + "\npublic void M(int v) { switch (v) { case X: break; } }");
    }

    /// <summary>
    /// <c>required</c> is reachable - as <see cref="PropertyDefinition.IsRequired"/>, not as a
    /// <see cref="ComponentModifier"/> flag, which is why the placeholder this replaces looked for
    /// it in the wrong place and concluded it was missing.
    /// </summary>
    [Fact]
    public void RequiredMembers()
    {
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(string)), "Name")
        {
            Modifiers = ComponentModifier.Public,
            IsRequired = true
        };

        var emitted = Emit.Component(property);

        Assert.Contains("required", emitted);
        RoslynAssert.MemberCompiles(emitted, containerHeader: "public class AdversaryHost");
    }

    /// <summary>
    /// The <c>field</c> keyword has a component as of preview1002.
    /// </summary>
    /// <remarks>
    /// It is C# <em>14</em>, not 13 as the placeholder this replaces claimed, and Roslyn 4.14 tops
    /// out at 13 - so this asserts the emitted text and the downlevel fallback rather than
    /// compiling it. Below C# 14 the component writes the backing field name the caller supplied.
    /// </remarks>
    [Fact]
    public void FieldKeyword()
    {
        // With no profile in force the session is permissive, so the keyword itself is written.
        Assert.Equal("field", Emit.Component(SyntaxHelpers.Field("_name", "Name")));

        // Gated on the target: below C# 14 the caller's backing field is written instead, which is
        // the half that has to be right - `field` below 14 is an ordinary identifier and would
        // bind to something else rather than fail.
        var file = new CSharpFileDefinition("Sample");
        var property = new PropertyDefinition(TypeDefinition.Get(typeof(string)), "Name");

        property.Get.LambdaSyntax = true;
        property.Get.Add(SyntaxHelpers.Field("_name", "Name"));
        file.AddClass("Holder").AddComponent(property);

        var downlevel = CSharpAuthor.Profiles.ProfileEmitter.Emit(
            file,
            new CSharpAuthor.Profiles.EmitProfile
            {
                Target = CSharpAuthor.Profiles.LanguageVersion.CSharp12
            }).Code;

        Assert.Contains("get => _name;", downlevel);
        Assert.DoesNotContain("=> field;", downlevel);
    }

    /// <summary>
    /// <c>ComponentModifier.New</c> shipped in preview1002. CS0108 as an error is the judge: if the
    /// modifier did not take, the hiding warning fires and fails the test.
    /// </summary>
    [Fact]
    public void NewMemberHiding()
    {
        var method = new MethodDefinition("ToString")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.New
        };

        method.SetReturnType(TypeDefinition.Get(typeof(string)));
        method.Return(SyntaxHelpers.QuoteString("x"));

        var emitted = Emit.Component(method);

        Assert.StartsWith("public new string ToString()", emitted);
        RoslynAssert.MemberCompiles(emitted, warningsAsErrors: "CS0108");
    }

    /// <summary>
    /// <c>ComponentModifier.File</c> shipped in preview1002 - the accessibility a generator most
    /// wants for a helper type, so two generators can each emit one without colliding.
    /// </summary>
    [Fact]
    public void FileLocalTypes()
    {
        var helper = new ClassDefinition("Helper")
        {
            Modifiers = ComponentModifier.File
        };

        helper.AddMethod("Work");

        var emitted = Emit.Component(helper);

        Assert.StartsWith("file class Helper", emitted);
        RoslynAssert.Compiles(emitted);
    }

    /// <summary>
    /// <c>static abstract</c> interface members are reachable through
    /// <see cref="InterfaceMethodDefinition.IsStaticAbstract"/>. The placeholder this replaces said
    /// they could not be declared at all.
    /// </summary>
    [Fact]
    public void StaticAbstractInterfaceMembers()
    {
        var contract = new InterfaceDefinition("IFactory")
        {
            Modifiers = ComponentModifier.Public
        };

        var create = contract.AddMethod("Create");

        create.IsStaticAbstract = true;
        create.SetReturnType(TypeDefinition.Get(typeof(string)));

        var emitted = Emit.Component(contract);

        Assert.Contains("static abstract", emitted);
        RoslynAssert.Compiles(emitted);
    }

    // ---- member kinds that do work, kept as guards ----

    [Fact]
    public void EventWithAccessorsCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        var eventDefinition = classDefinition.AddEvent(typeof(EventHandler), "Changed");

        eventDefinition.Add.AddCode("var a = 1;");
        eventDefinition.Remove.AddCode("var b = 1;");

        RoslynAssert.Compiles("using System;\n" + Emit.Component(classDefinition));
    }

    [Fact]
    public void FieldLikeEventCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddEvent(typeof(EventHandler), "Changed");

        RoslynAssert.Compiles("using System;\n" + Emit.Component(classDefinition));
    }

    [Fact]
    public void DelegateCompiles()
    {
        var delegateDefinition = new DelegateDefinition("Handler");

        delegateDefinition.SetReturnType(typeof(void));
        delegateDefinition.AddParameter(typeof(int), "x");

        RoslynAssert.Compiles(Emit.Component(delegateDefinition));
    }

    [Fact]
    public void InitOnlyPropertyCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddProperty(typeof(int), "P").Set!.IsInit = true;

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void ExpressionBodiedPropertyCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        var property = classDefinition.AddProperty(typeof(int), "P");

        property.Set = null;
        property.Get.LambdaSyntax = true;
        property.Get.AddCode("1");

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void PositionalRecordCompiles()
    {
        var record = new ClassDefinition("Pet")
        {
            TypeKeyword = ClassKeyword.Record,
            TerminateWithSemicolon = true
        };

        var constructor = record.AddConstructor();

        constructor.IsPrimary = true;
        constructor.AddParameter(typeof(string), "Id");

        RoslynAssert.Compiles(Emit.Component(record));
    }

    [Fact]
    public void ParameterModifiersCompile()
    {
        var method = new MethodDefinition("M");

        method.AddParameter(typeof(int), "a").Modifier = ParameterModifier.Out;
        method.AddParameter(typeof(int), "b").Modifier = ParameterModifier.Ref;
        method.AddParameter(typeof(int), "c").Modifier = ParameterModifier.In;
        method.AddParameter(typeof(int), "d").Modifier = ParameterModifier.RefReadOnly;
        method.AddCode("a = 0;");

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void ParamsAndDefaultValuesCompile()
    {
        var method = new MethodDefinition("M");

        method.AddParameter(typeof(int), "a").DefaultValue = CodeOutputComponent.Get(5);
        method.AddParameter(TypeDefinition.Get(typeof(string)).MakeArray(), "rest").IsParams = true;

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void ExtensionMethodCompiles()
    {
        var classDefinition = new ClassDefinition("Extensions")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Static
        };

        var method = classDefinition.AddMethod("Twice");

        method.Modifiers = ComponentModifier.Public | ComponentModifier.Static;
        method.SetReturnType(typeof(string));
        method.AddParameter(typeof(string), "s").This = true;
        method.AddCode("return s + s;");

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void InterfaceWithDefaultImplementationCompiles()
    {
        var interfaceDefinition = new InterfaceDefinition("IThing");

        interfaceDefinition.AddMethod("M").AddCode("return;");
        interfaceDefinition.AddProperty(typeof(int), "P");

        RoslynAssert.Compiles(Emit.Component(interfaceDefinition));
    }

    [Fact]
    public void EnumWithBaseTypeAndFlagsCompiles()
    {
        var enumDefinition = new EnumDefinition("E");

        enumDefinition.AddFlags();
        enumDefinition.BaseType = TypeDefinition.Get(typeof(long));
        enumDefinition.AddValue("None", 0);
        enumDefinition.AddValue("A", 1);

        RoslynAssert.Compiles("using System;\n" + Emit.Component(enumDefinition));
    }
}
