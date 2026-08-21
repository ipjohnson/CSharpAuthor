using System.Linq;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// Accessibility crossed with every other modifier.
/// </summary>
/// <remarks>
/// Accessibility is the one category where the compiler cannot be the judge. <c>private protected</c>
/// and <c>protected</c> behave identically inside a single compilation - the difference only shows
/// when a second assembly derives from the type - so these are string assertions, and they say so.
/// Everywhere the defect produces something the compiler can reject, the test asks the compiler.
/// </remarks>
public class ModifierAdversaryTests
{
    [Fact(Skip = "ADVERSARY GAP (§7 'protected internal'): GetAccessModifier returns on the first flag it matches, so protected|internal emits internal - a derived type in another assembly loses access")]
    public void ProtectedInternalOnAClass()
    {
        var classDefinition = new ClassDefinition("Host")
        {
            Modifiers = ComponentModifier.Protected | ComponentModifier.Internal
        };

        Assert.StartsWith("protected internal class", Emit.Component(classDefinition));
    }

    [Fact(Skip = "ADVERSARY GAP (§7 'private protected'): emits protected, which WIDENS access - another assembly's derived type can now reach the member")]
    public void PrivateProtectedOnAMethod()
    {
        var method = new MethodDefinition("M")
        {
            Modifiers = ComponentModifier.Private | ComponentModifier.Protected
        };

        Assert.StartsWith("private protected void", Emit.Component(method));
    }

    [Fact(Skip = "ADVERSARY GAP: Internal is tested before Public, so a component carrying both emits internal - narrowing, silently")]
    public void PublicAndInternalTogether()
    {
        var classDefinition = new ClassDefinition("Host")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Internal
        };

        Assert.StartsWith("public class", Emit.Component(classDefinition));
    }

    /// <summary>
    /// §7's abstract-method case, put to the compiler: an abstract method with a body is CS0500.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP (§7 'abstract method'): the modifier is written and a body is written after it - CS0500")]
    public void AbstractMethodHasNoBody()
    {
        var method = new MethodDefinition("M")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Abstract
        };

        RoslynAssert.MemberCompiles(
            Emit.Component(method), containerHeader: "public abstract class AdversaryHost");
    }

    /// <summary>
    /// The same defect on a property, which §7 does not list - and here it is worse, because the
    /// output compiles. <c>abstract</c> is dropped and an ordinary auto-property is written, so the
    /// member exists, is not virtual, and the first derived type that overrides it fails with
    /// CS0506 - a long way from the generator that caused it.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: abstract on a property is silently dropped and an auto-property body written instead; the type compiles and any override of it is CS0506")]
    public void AbstractPropertyHasNoBody()
    {
        var classDefinition = new ClassDefinition("Base")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Abstract
        };

        classDefinition.AddProperty(typeof(int), "P").Modifiers =
            ComponentModifier.Public | ComponentModifier.Abstract;

        RoslynAssert.Compiles(
            Emit.Component(classDefinition) +
            "\npublic class Derived : Base { public override int P { get => 1; set { } } }");
    }

    /// <summary>
    /// <c>partial</c> is dropped, so the two halves of a partial method become two declarations of
    /// the same member - CS0111.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP (§7 'partial on methods'): the modifier is never written, so a declaration and its implementation collide - CS0111")]
    public void PartialMethodKeepsItsModifier()
    {
        var declaration = new MethodDefinition("M")
        {
            Modifiers = ComponentModifier.NoAccessibility | ComponentModifier.Partial
        };

        RoslynAssert.Compiles(
            "public partial class Host\n{\n" + Emit.Component(declaration) + "}\n" +
            "public partial class Host\n{\n    partial void M() { }\n}");
    }

    /// <summary>
    /// <c>readonly</c> on a struct, proved by the compiler rather than by a string: a readonly
    /// struct may not have a mutable instance field, so if the modifier reached the output the
    /// compiler says CS8340. It does not, so the struct compiles - which is the defect.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP (§7 'readonly on structs'): ClassDefinition never writes Readonly, so a readonly struct is emitted as an ordinary one and its immutability is gone")]
    public void ReadonlyStructIsActuallyReadonly()
    {
        var structDefinition = new ClassDefinition("Point")
        {
            TypeKeyword = ClassKeyword.Struct,
            Modifiers = ComponentModifier.Public | ComponentModifier.Readonly
        };

        structDefinition.AddField(typeof(int), "X").Modifiers = ComponentModifier.Public;

        var errors = RoslynAssert.Errors(Emit.Component(structDefinition));

        Assert.Contains(errors, e => e.Id == "CS8340");
    }

    [Fact(Skip = "ADVERSARY GAP (§7 'sealed + override'): the modifier chain is an if/else, so sealed is skipped once override matches - emits override, leaving the member open to further overriding")]
    public void SealedOverrideKeepsBoth()
    {
        var method = new MethodDefinition("M")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Sealed | ComponentModifier.Override
        };

        Assert.StartsWith("public sealed override void", Emit.Component(method));
    }

    [Fact(Skip = "ADVERSARY GAP (§7 'abstract + sealed/static'): ClassDefinition's if/else picks Sealed and drops Abstract")]
    public void AbstractAndSealedOnAClass()
    {
        var classDefinition = new ClassDefinition("Host")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Sealed | ComponentModifier.Abstract
        };

        var output = Emit.Component(classDefinition);

        Assert.Contains("sealed", output);
        Assert.Contains("abstract", output);
    }

    /// <summary>
    /// <c>readonly</c> on a member of a struct - <c>public readonly int Sum() =&gt; ...</c> - is a
    /// different modifier from <c>readonly</c> on the struct, and neither is written.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: MethodDefinition never writes Readonly, so a readonly struct member cannot be declared at all")]
    public void ReadonlyMethodOnAStruct()
    {
        var method = new MethodDefinition("Sum")
        {
            Modifiers = ComponentModifier.Public | ComponentModifier.Readonly
        };

        Assert.Contains("readonly", Emit.Component(method));
    }

    /// <summary>
    /// The empty access modifier is still followed by its space, so every declaration written
    /// without accessibility carries a stray one. It compiles, and it is in the diff of every
    /// snapshot such a member appears in.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: NoAccessibility writes an empty string and then a space, so the member is indented one column too far - '     int f;' with five spaces")]
    public void NoAccessibilityDoesNotLeaveAStraySpace()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddField(typeof(int), "f").Modifiers = ComponentModifier.NoAccessibility;

        Assert.Contains("\n    int f;", Emit.Component(classDefinition));
    }

    [Fact(Skip = "ADVERSARY GAP: the same stray space on a property written without accessibility")]
    public void NoAccessibilityOnAPropertyDoesNotLeaveAStraySpace()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddProperty(typeof(int), "P").Modifiers = ComponentModifier.NoAccessibility;

        Assert.Contains("\n    int P", Emit.Component(classDefinition));
    }

    /// <summary>
    /// A field written <c>readonly static</c> rather than <c>static readonly</c>. Legal in either
    /// order, so this is a formatting question - but it is a formatting question that appears in
    /// consumer snapshots, which is where formatting questions turn into review time.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: FieldDefinition writes readonly before static, giving 'public readonly static int' - legal, and the reverse of what every style guide and the compiler's own messages use")]
    public void StaticReadonlyFieldModifierOrder()
    {
        var classDefinition = new ClassDefinition("Host");

        var field = classDefinition.AddField(typeof(int), "f");

        field.Modifiers = ComponentModifier.Public | ComponentModifier.Static | ComponentModifier.Readonly;
        field.InitializeValue = CodeOutputComponent.Get(1);

        Assert.Contains("public static readonly int f", Emit.Component(classDefinition));
    }

    // ---- combinations that already work; guards against a modifier fix breaking them ----

    [Theory]
    [InlineData(ComponentModifier.Public)]
    [InlineData(ComponentModifier.Internal)]
    [InlineData(ComponentModifier.Private)]
    [InlineData(ComponentModifier.Protected)]
    [InlineData(ComponentModifier.Public | ComponentModifier.Static)]
    [InlineData(ComponentModifier.Public | ComponentModifier.Virtual)]
    [InlineData(ComponentModifier.Public | ComponentModifier.Override)]
    [InlineData(ComponentModifier.Public | ComponentModifier.Async)]
    [InlineData(ComponentModifier.Internal | ComponentModifier.Static)]
    public void MethodModifierCombinationsCompile(ComponentModifier modifiers)
    {
        var method = new MethodDefinition("M") { Modifiers = modifiers };

        if ((modifiers & ComponentModifier.Async) == ComponentModifier.Async)
        {
            method.SetReturnType(typeof(System.Threading.Tasks.Task));
        }

        var header = (modifiers & (ComponentModifier.Virtual | ComponentModifier.Override)) != 0
            ? "public abstract class AdversaryHost"
            : "public class AdversaryHost";

        var member = (modifiers & ComponentModifier.Override) == ComponentModifier.Override
            ? Emit.Component(method).Replace("override ", "virtual ")
            : Emit.Component(method);

        RoslynAssert.MemberCompiles(member, containerHeader: header);
    }

    [Theory]
    [InlineData(ClassKeyword.Class, ComponentModifier.Public | ComponentModifier.Abstract)]
    [InlineData(ClassKeyword.Class, ComponentModifier.Public | ComponentModifier.Sealed)]
    [InlineData(ClassKeyword.Class, ComponentModifier.Public | ComponentModifier.Static)]
    [InlineData(ClassKeyword.Class, ComponentModifier.Public | ComponentModifier.Partial)]
    [InlineData(ClassKeyword.Class, ComponentModifier.Internal | ComponentModifier.Abstract | ComponentModifier.Partial)]
    [InlineData(ClassKeyword.Struct, ComponentModifier.Public)]
    [InlineData(ClassKeyword.Record, ComponentModifier.Public | ComponentModifier.Sealed)]
    [InlineData(ClassKeyword.RecordStruct, ComponentModifier.Public)]
    public void TypeModifierCombinationsCompile(ClassKeyword keyword, ComponentModifier modifiers)
    {
        var classDefinition = new ClassDefinition("Host")
        {
            TypeKeyword = keyword,
            Modifiers = modifiers
        };

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    /// <summary>
    /// An indexer is declared by naming the property <c>this</c>, which the existing suite relies
    /// on in three tests. Anything that starts escaping keyword identifiers will turn it into
    /// <c>@this</c> and break every indexer in the library - so the guard is here, unskipped, where
    /// whoever writes that fix will trip over it.
    /// </summary>
    [Fact]
    public void IndexerNamedThisMustNotBeEscaped()
    {
        var classDefinition = new ClassDefinition("Row");

        var property = classDefinition.AddProperty(typeof(string), "this");

        property.IndexType = TypeDefinition.Get(typeof(int));
        property.Get.AddIndentedStatement("return null");
        property.Set = null;

        var output = Emit.Component(classDefinition);

        Assert.DoesNotContain("@this", output);

        RoslynAssert.Compiles(output);
    }

    /// <summary>
    /// The other side of that trap: an indexer is only well-formed because the property was named
    /// <c>this</c>. Naming it anything else and setting an index type emits
    /// <c>public string Item[int index]</c>, which is not a declaration C# has.
    /// </summary>
    [Fact(Skip = "ADVERSARY GAP: setting IndexType on a property not named 'this' emits 'public string Item[int index]' - CS1519 - rather than rejecting the combination or writing this[]")]
    public void IndexerOnAPropertyNotNamedThis()
    {
        var classDefinition = new ClassDefinition("Row");

        var property = classDefinition.AddProperty(typeof(string), "Item");

        property.IndexType = TypeDefinition.Get(typeof(int));
        property.Get.AddIndentedStatement("return null");
        property.Set = null;

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void StaticConstructorCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddConstructor().Modifiers = ComponentModifier.Static;

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void ExplicitInterfaceImplementationCompiles()
    {
        var classDefinition = new ClassDefinition("Host");

        classDefinition.AddBaseType(TypeDefinition.Get("Probe", "IThing"));
        classDefinition.AddMethod("M").InterfaceImplementation =
            TypeDefinition.Get("Probe", "IThing");

        RoslynAssert.Compiles(
            "using Probe;\n" +
            "namespace Probe { public interface IThing { void M(); } }\n" +
            Emit.Component(classDefinition));
    }
}
