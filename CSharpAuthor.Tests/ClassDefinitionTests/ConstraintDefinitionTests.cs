using System;
using Xunit;

namespace CSharpAuthor.Tests.ClassDefinitionTests;

/// <summary>
/// A where clause could always be assigned as a rendered string. These cover building one part by
/// part, where the ordering rules C# fixes — one primary constraint first, new() last — are the thing
/// worth not repeating in every caller.
/// </summary>
public class ConstraintDefinitionTests
{
    [Fact]
    public void ClassConstraint()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");
        classDefinition.AddConstraint("T").Class();

        AssertEqual.WithoutNewLine(
            @"public class Box<T> where T : class
{
}
",
            Write(classDefinition));
    }

    /// <summary>
    /// Added out of order and written in order, which is the point: a caller reading a symbol takes
    /// the parts as the symbol reports them.
    /// </summary>
    [Fact]
    public void PartsAreWrittenInTheOrderCSharpRequires()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");

        var constraint = classDefinition.AddConstraint("T");
        constraint.DefaultConstructor();
        constraint.Implements(TypeDefinition.Get("Ns", "IThing"));
        constraint.Class();

        AssertEqual.WithoutNewLine(
            @"public class Box<T> where T : class, IThing, new()
{
}
",
            Write(classDefinition));
    }

    [Fact]
    public void SeveralParametersEachGetTheirOwnClause()
    {
        var classDefinition = new ClassDefinition("Pair");
        classDefinition.AddGenericParameter("TKey");
        classDefinition.AddGenericParameter("TValue");
        classDefinition.AddConstraint("TKey").NotNull();
        classDefinition.AddConstraint("TValue").Class();

        AssertEqual.WithoutNewLine(
            @"public class Pair<TKey, TValue> where TKey : notnull where TValue : class
{
}
",
            Write(classDefinition));
    }

    [Fact]
    public void ConstraintsFollowTheBaseTypes()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");
        classDefinition.AddBaseType(
            new GenericTypeDefinition(
                TypeDefinitionEnum.ClassDefinition,
                "Ns",
                "Container",
                new ITypeDefinition[] { new TypeParameterDefinition("T") }));
        classDefinition.AddConstraint("T").Class();

        AssertEqual.WithoutNewLine(
            @"public class Box<T> : Container<T> where T : class
{
}
",
            Write(classDefinition));
    }

    [Theory]
    [InlineData("struct")]
    [InlineData("unmanaged")]
    [InlineData("notnull")]
    public void PrimaryConstraints(string keyword)
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");

        var constraint = classDefinition.AddConstraint("T");

        switch (keyword)
        {
            case "struct":
                constraint.Struct();
                break;
            case "unmanaged":
                constraint.Unmanaged();
                break;
            default:
                constraint.NotNull();
                break;
        }

        Assert.Contains($"where T : {keyword}", Write(classDefinition));
    }

    [Fact]
    public void NullableClassConstraint()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");
        classDefinition.AddConstraint("T").Class(nullable: true);

        Assert.Contains("where T : class?", Write(classDefinition));
    }

    /// <summary>
    /// The same parameter asked for twice is one clause. Two <c>where T</c> clauses on one type do
    /// not compile, and a caller collecting constraints in a loop is exactly where that would happen.
    /// </summary>
    [Fact]
    public void AskingTwiceForOneParameterExtendsTheSameClause()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");
        classDefinition.AddConstraint("T").Class();
        classDefinition.AddConstraint("T").Implements(TypeDefinition.Get("Ns", "IThing"));

        AssertEqual.WithoutNewLine(
            @"public class Box<T> where T : class, IThing
{
}
",
            Write(classDefinition));

        Assert.Single(classDefinition.Constraints);
    }

    [Fact]
    public void AnEmptyConstraintWritesNothing()
    {
        var classDefinition = new ClassDefinition("Box");
        classDefinition.AddGenericParameter("T");
        classDefinition.AddConstraint("T");

        AssertEqual.WithoutNewLine(
            @"public class Box<T>
{
}
",
            Write(classDefinition));
    }

    /// <summary>
    /// The rendered form still works, and is written before any built one.
    /// </summary>
    [Fact]
    public void WhereStatementAndAddConstraintCoexist()
    {
        var classDefinition = new ClassDefinition("Pair");
        classDefinition.AddGenericParameter("TKey");
        classDefinition.AddGenericParameter("TValue");
        classDefinition.WhereStatement = new CodeOutputComponent(" where TKey : notnull") { Indented = false };
        classDefinition.AddConstraint("TValue").Class();

        AssertEqual.WithoutNewLine(
            @"public class Pair<TKey, TValue> where TKey : notnull where TValue : class
{
}
",
            Write(classDefinition));
    }

    [Fact]
    public void TwoPrimaryConstraintsThrow()
    {
        var constraint = new ConstraintDefinition("T").Class();

        var exception = Assert.Throws<InvalidOperationException>(() => constraint.Struct());

        Assert.Contains("only one", exception.Message);
    }

    /// <summary>
    /// <c>struct</c> already guarantees a default constructor, and <c>where T : struct, new()</c> is
    /// CS0451. Caught here rather than by the consumer's compiler.
    /// </summary>
    [Fact]
    public void StructWithDefaultConstructorThrows()
    {
        var constraint = new ConstraintDefinition("T").Struct();

        var exception = Assert.Throws<InvalidOperationException>(() => constraint.DefaultConstructor());

        Assert.Contains("new()", exception.Message);
    }

    [Fact]
    public void DefaultConstructorThenStructThrows()
    {
        var constraint = new ConstraintDefinition("T").DefaultConstructor();

        Assert.Throws<InvalidOperationException>(() => constraint.Unmanaged());
    }

    [Fact]
    public void RepeatingTheSamePrimaryConstraintIsAllowed()
    {
        var constraint = new ConstraintDefinition("T").Class().Class();

        Assert.False(constraint.IsEmpty);
    }

    [Fact]
    public void AConstraintHasToNameItsParameter()
    {
        Assert.Throws<ArgumentException>(() => new ConstraintDefinition(" "));
    }

    private static string Write(ClassDefinition classDefinition)
    {
        var context = new OutputContext();

        classDefinition.WriteOutput(context);

        return context.Output();
    }
}
