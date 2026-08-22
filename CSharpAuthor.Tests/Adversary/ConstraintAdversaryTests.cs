using System;
using Xunit;

namespace CSharpAuthor.Tests.Adversary;

/// <summary>
/// <c>where</c> clauses.
/// </summary>
/// <remarks>
/// This is the best-covered corner of the library and most of this file is guards. The one finding
/// is a gap in the language rather than a defect - and one correction to the brief: §5 asks for
/// <c>where T : struct, IComparable&lt;T&gt;, new()</c>, which is not legal C#. <c>struct</c> already
/// guarantees a parameterless constructor, so combining it with <c>new()</c> is CS0451. The library
/// refuses the combination, which is right, and <see cref="StructAndNewIsRejected"/> pins that
/// behaviour down so a later change cannot quietly start emitting it.
/// </remarks>
public class ConstraintAdversaryTests
{
    private static ITypeDefinition ComparableOfT() =>
        new GenericTypeDefinition(
            TypeDefinitionEnum.InterfaceDefinition, "System", "IComparable",
            new ITypeDefinition[] { new TypeParameterDefinition("T") });

    /// <summary>
    /// §5's example, which the compiler rejects: CS0451. The library throws rather than emit it.
    /// </summary>
    [Fact]
    public void StructAndNewIsRejected()
    {
        var classDefinition = new ClassDefinition("Box");

        classDefinition.AddGenericParameter("T");

        var constraint = classDefinition.AddConstraint("T").Struct().Implements(ComparableOfT());

        Assert.Throws<InvalidOperationException>(() => constraint.DefaultConstructor());

        RoslynAssert.Compiles("using System;\n" + Emit.Component(classDefinition));
    }

    [Fact]
    public void ClassInterfaceAndNewCompile()
    {
        var classDefinition = new ClassDefinition("Box");

        classDefinition.AddGenericParameter("T");
        classDefinition.AddConstraint("T").Class().Implements(ComparableOfT()).DefaultConstructor();

        RoslynAssert.Compiles("using System;\n" + Emit.Component(classDefinition));
    }

    [Theory]
    [InlineData("notnull")]
    [InlineData("unmanaged")]
    [InlineData("struct")]
    [InlineData("class")]
    public void PrimaryConstraintsCompile(string keyword)
    {
        var classDefinition = new ClassDefinition("Box");

        classDefinition.AddGenericParameter("T");

        var constraint = classDefinition.AddConstraint("T");

        switch (keyword)
        {
            case "notnull": constraint.NotNull(); break;
            case "unmanaged": constraint.Unmanaged(); break;
            case "struct": constraint.Struct(); break;
            case "class": constraint.Class(); break;
        }

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void NullableClassConstraintCompiles()
    {
        var classDefinition = new ClassDefinition("Box");

        classDefinition.AddGenericParameter("T");
        classDefinition.AddConstraint("T").Class(nullable: true);

        RoslynAssert.Compiles("#nullable enable\n" + Emit.Component(classDefinition));
    }

    [Fact]
    public void TwoTypeParametersEachConstrained()
    {
        var classDefinition = new ClassDefinition("Box");

        classDefinition.AddGenericParameter("T");
        classDefinition.AddGenericParameter("U");
        classDefinition.AddConstraint("T").Class();
        classDefinition.AddConstraint("U").Struct();

        RoslynAssert.Compiles(Emit.Component(classDefinition));
    }

    [Fact]
    public void ConstraintsFollowTheBaseList()
    {
        var classDefinition = new ClassDefinition("Box");

        classDefinition.AddGenericParameter("T");
        classDefinition.AddBaseType(TypeDefinition.Get("Probe", "Base"));
        classDefinition.AddConstraint("T").Class();

        RoslynAssert.Compiles(
            "using Probe;\nnamespace Probe { public class Base { } }\n" +
            Emit.Component(classDefinition));
    }

    [Fact]
    public void MethodConstraintsCompile()
    {
        var method = new MethodDefinition("M");

        method.AddGenericParameter(new TypeParameterDefinition("T"));
        method.AddConstraint("T").Class().DefaultConstructor();

        RoslynAssert.MemberCompiles(Emit.Component(method));
    }

    /// <summary>
    /// A constrained type's namespace has to be imported, and is - the constraint writes through
    /// <c>IOutputContext.Write(ITypeDefinition)</c> rather than rebuilding the name, which is what
    /// <c>Is</c> and <c>AttributeDefinition</c> do not do.
    /// </summary>
    [Fact]
    public void ConstraintTypeNamespaceIsImported()
    {
        var file = new CSharpFileDefinition("Consumer");

        var classDefinition = file.AddClass("Box");

        classDefinition.AddGenericParameter("T");
        classDefinition.AddConstraint("T").Implements(TypeDefinition.Get("Far.Away", "IThing"));

        Assert.Contains("using Far.Away;", Emit.File(file));
    }

    [Fact]
    public void ConstraintTypeHonoursGlobalMode()
    {
        var classDefinition = new ClassDefinition("Box");

        classDefinition.AddGenericParameter("T");
        classDefinition.AddConstraint("T").Implements(TypeDefinition.Get("Far.Away", "IThing"));

        var output = Emit.Component(
            classDefinition, new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global });

        Assert.Contains("global::Far.Away.IThing", output);
    }

}
