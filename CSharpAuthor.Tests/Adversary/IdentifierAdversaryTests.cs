using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Names that are also keywords. A generator reading a schema, a database column, or a foreign
/// language's model has no say in what it is handed, and <c>class</c>, <c>event</c> and <c>lock</c>
/// are all ordinary words somewhere.
/// </summary>
/// <remarks>
/// C# has the <c>@</c> prefix for exactly this, so every one of these has a mechanical fix. §7
/// records the parameter case; every other declaration site has the same hole and none of them is
/// on the list.
/// </remarks>
public class IdentifierAdversaryTests
{
    [Fact]
    public void ParameterNamedClass()
    {
        var method = new MethodDefinition("M");

        method.AddParameter(typeof(string), "class");

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    [Fact]
    public void ClassNamedEvent()
    {
        RoslynAssert.Compiles(Emit.Component(new ClassDefinition("event")));
    }

    [Fact]
    public void FieldNamedLock()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(int), "lock");

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void PropertyNamedString()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddProperty(typeof(int), "string");

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void MethodNamedIf()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddMethod("if");

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void EnumValueNamedDefault()
    {
        var enumDefinition = new EnumDefinition("E");

        enumDefinition.AddValue("default");

        RoslynAssert.Compiles(Emit.Component(enumDefinition));
    }

    /// <summary>
    /// A namespace is a dotted list of identifiers, so any one of them can be a keyword. Nothing
    /// splits on the dot to check.
    /// </summary>
    [Fact]
    public void NamespaceSegmentNamedNamespace()
    {
        var file = new CSharpFileDefinition("My.namespace.Thing");

        file.AddClass("A");

        RoslynAssert.Compiles(Emit.File(file));
    }

    [Fact(Skip = "ADVERSARY GAP: a type reference's name is never escaped - TypeDefinition.Get(\"Ns\", \"event\") writes event")]
    public void TypeReferenceNamedEvent()
    {
        var type = TypeDefinition.Get("Ns", "event");

        Assert.Equal("@event", Emit.TypeName(type));
    }

    [Fact]
    public void InterfaceNamedInterface()
    {
        RoslynAssert.Compiles(Emit.Component(new InterfaceDefinition("interface")));
    }

    [Fact(Skip = "ADVERSARY GAP: a type parameter name is never escaped - emits class Box<int>, CS1001")]
    public void TypeParameterNamedInt()
    {
        var classDefinition = new ClassDefinition("Box");

        classDefinition.AddGenericParameter("int");

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    /// <summary>
    /// A caller that has already escaped the name must not be escaped again: <c>@@class</c> is not a
    /// thing. Unskipped, because whatever fixes the cases above has to keep this working.
    /// </summary>
    [Fact]
    public void AlreadyEscapedIdentifierIsLeftAlone()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(int), "@class");

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    /// <summary>
    /// A contextual keyword is a legal identifier and must not be escaped - <c>@value</c> compiles
    /// but is not what anyone writes, and in a property setter <c>value</c> is the name the language
    /// itself uses.
    /// </summary>
    [Theory]
    [InlineData("value")]
    [InlineData("var")]
    [InlineData("record")]
    [InlineData("nameof")]
    [InlineData("async")]
    [InlineData("dynamic")]
    [InlineData("required")]
    [InlineData("file")]
    public void ContextualKeywordsAreNotEscaped(string name)
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(int), name);

        var output = Emit.Component(classDefinition);

        Assert.DoesNotContain("@" + name, output);

        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// The ordinary case, so an escaping fix cannot start escaping everything.
    /// </summary>
    [Fact]
    public void OrdinaryIdentifiersAreUntouched()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(int), "count");
        classDefinition.AddProperty(typeof(string), "Name");
        classDefinition.AddMethod("Run");

        var output = Emit.Component(classDefinition);

        Assert.DoesNotContain("@", output);

        RoslynAssert.Compiles(output);
    }
}
