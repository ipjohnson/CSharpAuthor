using CSharpAuthor.Syntax;
using Xunit;
using static CSharpAuthor.Tests.SyntaxNodeTests.NodeEmit;

namespace CSharpAuthor.Tests.SyntaxNodeTests;

/// <summary>
/// <c>{ 1, 2, }</c>. The trailing comma is legal C#, Roslyn keeps it in the tree as a separator
/// token, and a writer that joins elements with an <c>if (i &gt; 0)</c> separator has nowhere to
/// record that it was there.
/// </summary>
/// <remarks>
/// One test per node kind that owns a separated list a caller writes a trailing separator in, plus
/// the two cases that say what the flag does <em>not</em> do: an empty list gets no separator out
/// of it, and a list style that has no separator ignores it rather than inventing one.
/// </remarks>
public class TrailingSeparatorTests
{
    // -- the six node kinds ------------------------------------------------------------------

    /// <summary>An enum whose last member is followed by a comma - the idiom that keeps a diff to
    /// one line when a member is appended.</summary>
    [Fact]
    public void EnumDeclaration()
    {
        var declaration = new EnumDeclaration("Color");

        declaration.Modifiers.Add("public");
        declaration.Members.Add(new EnumMemberDeclaration("Red"));
        declaration.Members.Add(new EnumMemberDeclaration("Green"));
        declaration.Members.TrailingSeparator = true;

        Assert.Equal(
            "public enum Color\n" +
            "{\n" +
            "    Red,\n" +
            "    Green,\n" +
            "}\n",
            Emit(declaration));
    }

    [Fact]
    public void EnumDeclaration_WithoutTheSeparator()
    {
        var declaration = new EnumDeclaration("Color");

        declaration.Members.Add(new EnumMemberDeclaration("Red"));
        declaration.Members.Add(new EnumMemberDeclaration("Green"));

        Assert.Equal(
            "enum Color\n" +
            "{\n" +
            "    Red,\n" +
            "    Green\n" +
            "}\n",
            Emit(declaration));
    }

    /// <summary>An object initializer: <c>new Widget { Width = 1, Height = 2, }</c>.</summary>
    [Fact]
    public void ObjectInitializerExpression()
    {
        var creation = new ObjectCreationExpression(Type("Acme", "Widget"))
        {
            Initializer = new InitializerExpression(),
        };

        creation.Initializer.Expressions.Add(Assign("Width", "1"));
        creation.Initializer.Expressions.Add(Assign("Height", "2"));
        creation.Initializer.Expressions.TrailingSeparator = true;

        Assert.Equal("new Widget { Width = 1, Height = 2, }", Emit(creation));
    }

    /// <summary>A collection initializer: <c>new List&lt;int&gt; { 1, 2, }</c>.</summary>
    [Fact]
    public void CollectionInitializerExpression()
    {
        var creation = new ObjectCreationExpression(Type("System.Collections.Generic", "List"))
        {
            Initializer = new InitializerExpression(),
        };

        creation.Initializer.Expressions.Add(new LiteralExpression("1"));
        creation.Initializer.Expressions.Add(new LiteralExpression("2"));
        creation.Initializer.Expressions.TrailingSeparator = true;

        Assert.Equal("new List { 1, 2, }", Emit(creation));
    }

    /// <summary>An array initializer: <c>new int[] { 1, 2, }</c>.</summary>
    [Fact]
    public void ArrayInitializerExpression()
    {
        var creation = new ArrayCreationExpression(
            TypeRef.Of(TypeDefinition.Get(typeof(int)).MakeArray()))
        {
            Initializer = new InitializerExpression(),
        };

        creation.Initializer.Expressions.Add(new LiteralExpression("1"));
        creation.Initializer.Expressions.Add(new LiteralExpression("2"));
        creation.Initializer.Expressions.TrailingSeparator = true;

        Assert.Equal("new int[] { 1, 2, }", Emit(creation));
    }

    /// <summary>A collection expression: <c>[1, 2,]</c>.</summary>
    [Fact]
    public void CollectionExpression()
    {
        var collection = new CollectionExpression();

        collection.Elements.Add(new ExpressionElement(new LiteralExpression("1")));
        collection.Elements.Add(new ExpressionElement(new LiteralExpression("2")));
        collection.Elements.TrailingSeparator = true;

        Assert.Equal("[1, 2,]", Emit(collection));
    }

    /// <summary>A switch expression: <c>value switch { 1 => "one", _ => "other", }</c>.</summary>
    [Fact]
    public void SwitchExpression()
    {
        var expression = new SwitchExpression(Id("value"));

        expression.Arms.Add(new SwitchExpressionArm(
            new ConstantPattern(new LiteralExpression("1")), new LiteralExpression("\"one\"")));
        expression.Arms.Add(new SwitchExpressionArm(
            new DiscardPattern(), new LiteralExpression("\"other\"")));
        expression.Arms.TrailingSeparator = true;

        Assert.Equal("value switch { 1 => \"one\", _ => \"other\", }", Emit(expression));
    }

    // -- what it does not do ------------------------------------------------------------------

    /// <summary>
    /// An empty list writes nothing, flag or no flag. <c>{ , }</c> is not C# and a list with no
    /// elements has no separator to trail.
    /// </summary>
    [Fact]
    public void AnEmptyListStillWritesNothing()
    {
        var collection = new CollectionExpression();

        collection.Elements.TrailingSeparator = true;

        Assert.Equal("[]", Emit(collection));
    }

    /// <summary>
    /// A style with no separator ignores the flag. A block's statements are joined by line breaks,
    /// and a trailing one of those would be a blank line rather than a token.
    /// </summary>
    [Fact]
    public void AListStyleWithoutASeparatorIgnoresIt()
    {
        var block = new Block();

        block.Statements.Add(Statement(Call(Id("First"))));
        block.Statements.Add(Statement(Call(Id("Second"))));
        block.Statements.TrailingSeparator = true;

        Assert.Equal(
            "{\n" +
            "    First();\n" +
            "    Second();\n" +
            "}\n",
            Emit(block));
    }

    /// <summary>
    /// A single element takes one too - <c>[1,]</c> is legal, and it is what a one-element list in
    /// the source looked like.
    /// </summary>
    [Fact]
    public void OneElementTakesOneToo()
    {
        var collection = new CollectionExpression();

        collection.Elements.Add(new ExpressionElement(new LiteralExpression("1")));
        collection.Elements.TrailingSeparator = true;

        Assert.Equal("[1,]", Emit(collection));
    }

    /// <summary>An argument list is separated too, but C# has no trailing comma there - so the
    /// default has to be off, and nothing sets it for a caller.</summary>
    [Fact]
    public void TheFlagIsOffUntilItIsSet()
    {
        var list = new ArgumentList();

        Assert.False(list.Arguments.TrailingSeparator);
    }

    private static AssignmentExpression Assign(string name, string value) =>
        new(Id(name), "=", new LiteralExpression(value));
}
