using System;
using Xunit;
using static CSharpAuthor.SyntaxHelpers;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Attributes: targets, generic attributes, and the suffix rule.
/// </summary>
public class AttributeAdversaryTests
{
    private const string Attributes = @"
using Probe;
namespace Probe
{
    public class MyAttribute : System.Attribute
    {
        public MyAttribute() { }
        public MyAttribute(System.Type t, int[] values) { }
        public MyAttribute(double d) { }
    }

    public class ValidateAttribute<T> : System.Attribute { }

    public class NotNullAttribute : System.Attribute { }
}
";

    /// <summary>
    /// A generic attribute, which C# 11 allows. <c>AttributeDefinition</c> writes
    /// <c>ITypeDefinition.Name</c> rather than the rendered type, so the type argument never reaches
    /// the output - and the name that is left is the name of a different attribute.
    /// </summary>
    [Fact]
    public void GenericAttribute()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddAttribute(new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Probe", "ValidateAttribute",
            new[] { TypeDefinition.Get(typeof(int)) }));

        RoslynAssert.Compiles(Attributes + Emit.Component(classDefinition));
    }

    /// <summary>
    /// The <c>Attribute</c> suffix is stripped unconditionally, so a type whose whole name is
    /// <c>Attribute</c> is stripped to nothing.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: the suffix strip is unconditional, so an attribute type named exactly 'Attribute' emits [] - CS1001")]
    public void AttributeTypeNamedAttribute()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddAttribute(TypeDefinition.Get("System", "Attribute"));

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    /// <summary>
    /// An assembly-level attribute has to precede every declaration in the file. There is nowhere to
    /// put one: <c>CSharpFileDefinition</c> holds a single namespace and writes everything inside it.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: [assembly:] lands inside the namespace, because CSharpFileDefinition has no position outside it - CS1730, attributes must precede all other elements")]
    public void AssemblyLevelAttribute()
    {
        var file = new CSharpFileDefinition("Probe");

        file.AddComponent(new AttributeDefinition(TypeDefinition.Get("Probe", "MyAttribute"))
        {
            Target = "assembly"
        });

        file.AddClass("Host");

        RoslynAssert.Compiles(Attributes + Emit.File(file));
    }

    /// <summary>
    /// <c>[return:]</c> works, because the target is an opaque string written straight through.
    /// Unskipped as a guard.
    /// </summary>
    [Fact]
    public void ReturnTargetedAttribute()
    {
        var method = new MethodDefinition("M");

        method.SetReturnType(typeof(string));
        method.AddCode("return null;");
        method.AddAttribute(TypeDefinition.Get("Probe", "NotNullAttribute")).Target = "return";

        RoslynAssert.MemberCompiles(Emit.Component(method), preamble: Attributes);
    }

    [Fact]
    public void FieldTargetedAttributeOnAProperty()
    {
        var classDefinition = new ClassDefinition("Host");

        var property = classDefinition.AddProperty(typeof(int), "P");

        property.AddAttribute(TypeDefinition.Get("Probe", "MyAttribute")).Target = "field";

        RoslynAssert.Compiles(Attributes + Emit.Component(classDefinition));
    }

    [Fact]
    public void TypeofAndArrayArguments()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddAttribute(
            TypeDefinition.Get("Probe", "MyAttribute"),
            TypeOf(TypeDefinition.Get(typeof(string))),
            new[] { 1, 2, 3 });

        RoslynAssert.Compiles(Attributes + Emit.Component(classDefinition));
    }

    /// <summary>
    /// A parameter attribute is written inline rather than on its own line, which is what a
    /// parameter position needs.
    /// </summary>
    [Fact]
    public void ParameterAttributeIsWrittenInline()
    {
        var method = new MethodDefinition("M");

        method.AddParameter(typeof(int), "x").AddAttribute(
            TypeDefinition.Get("Probe", "MyAttribute"));

        RoslynAssert.MemberCompiles(Emit.Component(method), preamble: Attributes);
    }

    /// <summary>
    /// The suffix rule the right way round: a type named <c>MyAttribute</c> is written <c>[My]</c>,
    /// which is how C# resolves attribute names. Unskipped, so a fix for the empty-name case cannot
    /// stop stripping altogether.
    /// </summary>
    [Fact]
    public void AttributeSuffixIsStripped()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddAttribute(TypeDefinition.Get("Probe", "MyAttribute"));

        Assert.Contains("[My]", Emit.Component(classDefinition));
    }

    /// <summary>
    /// An attribute's namespace has to be imported even though the name written is not the type's
    /// name. It is, because the import goes through <c>AddImportNamespace(ITypeDefinition)</c>
    /// before the name is rewritten.
    /// </summary>
    [Fact]
    public void AttributeNamespaceIsImported()
    {
        var file = new CSharpFileDefinition("Consumer");

        file.AddClass("Host").AddAttribute(TypeDefinition.Get("Probe", "MyAttribute"));

        Assert.Contains("using Probe;", Emit.File(file));
    }

    /// <summary>
    /// A generic attribute's type arguments carry namespaces of their own, and those have to be
    /// imported too. They are - the name is what is lost, not the imports.
    /// </summary>
    [Fact]
    public void GenericAttributeTypeArgumentNamespaceIsImported()
    {
        var file = new CSharpFileDefinition("Consumer");

        file.AddClass("Host").AddAttribute(new GenericTypeDefinition(
            TypeDefinitionEnum.ClassDefinition, "Probe", "ValidateAttribute",
            new[] { TypeDefinition.Get("Other.Place", "Rule") }));

        Assert.Contains("using Other.Place;", Emit.File(file));
    }

    /// <summary>
    /// An attribute in <see cref="TypeOutputMode.Global"/> is written as a bare short name, because
    /// the name is rebuilt from <c>ITypeDefinition.Name</c> and never passes through the mode.
    /// </summary>
    [Fact]
    public void AttributeInGlobalMode()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddAttribute(TypeDefinition.Get("Probe", "MyAttribute"));

        var output = Emit.Component(
            classDefinition,
            new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Assert.Contains("[global::Probe.My]", output);
    }
}
