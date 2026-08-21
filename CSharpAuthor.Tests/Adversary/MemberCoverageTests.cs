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
    [Fact(Skip = "ADVERSARY GAP: no API - operator declarations. 'public static Money operator +(Money a, Money b)' cannot be written; MethodDefinition writes a name where the operator keyword and symbol go.")]
    public void OperatorDeclarations()
    {
        Assert.True(false, "no API for operator declarations");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - conversion operators. 'public static implicit operator int(Money m)' and the explicit form have no component.")]
    public void ConversionOperators()
    {
        Assert.True(false, "no API for implicit / explicit conversion operators");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - destructors/finalizers. '~Host()' has no component; a ConstructorDefinition named ~Host would still write an access modifier, which a destructor may not have.")]
    public void Destructors()
    {
        Assert.True(false, "no API for destructors");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - const fields. ComponentModifier has Readonly and Static but no Const, so 'public const int X = 1;' cannot be declared - and a const is not the same as a static readonly to a consumer, because only a const can be a case label, an attribute argument or a default parameter value.")]
    public void ConstFields()
    {
        Assert.True(false, "no ComponentModifier.Const");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - the 'required' modifier. There is no ComponentModifier.Required, so a required property cannot be declared.")]
    public void RequiredMembers()
    {
        Assert.True(false, "no ComponentModifier.Required");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - the 'field' keyword (C# 13) in a property accessor has no representation")]
    public void FieldKeyword()
    {
        Assert.True(false, "no API for the field keyword");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - extension blocks and extension members. An extension method is reachable via ParameterDefinition.This; the C# 14 'extension(T x) { }' block, and extension properties and indexers inside it, are not.")]
    public void ExtensionBlocksAndMembers()
    {
        Assert.True(false, "no API for extension blocks, extension properties or extension indexers");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - 'volatile' has no ComponentModifier flag")]
    public void VolatileFields()
    {
        Assert.True(false, "no ComponentModifier.Volatile");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - 'unsafe' has no ComponentModifier flag, so neither an unsafe member nor a pointer type can be declared")]
    public void UnsafeMembers()
    {
        Assert.True(false, "no ComponentModifier.Unsafe, and no pointer type in ITypeDefinition");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - 'extern' has no ComponentModifier flag, so a DllImport declaration cannot be emitted")]
    public void ExternMembers()
    {
        Assert.True(false, "no ComponentModifier.Extern");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - the 'new' member-hiding modifier has no flag, so a generated member that shadows a base one emits CS0108 as a warning on every build")]
    public void NewMemberHiding()
    {
        Assert.True(false, "no ComponentModifier.New");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - the 'file' accessibility modifier (C# 11) has no flag, which is the accessibility a source generator most wants for a helper type it does not intend to publish")]
    public void FileLocalTypes()
    {
        Assert.True(false, "no ComponentModifier.File");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - static abstract and static virtual interface members (C# 11) cannot be declared: InterfaceMethodDefinition writes no modifiers at all")]
    public void StaticAbstractInterfaceMembers()
    {
        Assert.True(false, "no API for static abstract / static virtual interface members");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - ref returns and ref locals. MethodDefinition writes the return type with no modifier position, so 'ref int M()' and 'ref readonly int M()' cannot be declared.")]
    public void RefReturns()
    {
        Assert.True(false, "no API for ref / ref readonly returns");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - an interface cannot declare an event, an indexer, a nested type, or a generic parameter. InterfaceDefinition holds only methods and properties.")]
    public void InterfaceMemberKinds()
    {
        Assert.True(false, "InterfaceDefinition supports methods and properties only");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - an interface cannot be generic. InterfaceDefinition has no AddGenericParameter and no constraint list, so 'interface IRepo<T> where T : class' cannot be declared.")]
    public void GenericInterfaces()
    {
        Assert.True(false, "InterfaceDefinition has no type parameters");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - a delegate cannot be generic. DelegateDefinition inherits MethodDefinition's generic parameters but a caller cannot reach constraints on them in a delegate position.")]
    public void GenericDelegateConstraints()
    {
        Assert.True(false, "no constraints on a delegate's type parameters");
    }

    [Fact(Skip = "ADVERSARY GAP: no API - an enum member cannot be given a negative or hex value in a controlled way; EnumValueDefinition writes Value.ToString(), so the literal form is whatever the CLR chose")]
    public void EnumMemberLiteralForm()
    {
        Assert.True(false, "no control over an enum member's literal form");
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
