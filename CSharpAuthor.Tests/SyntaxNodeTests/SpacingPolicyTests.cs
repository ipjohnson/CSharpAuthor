using CSharpAuthor.Syntax;
using Xunit;
using static CSharpAuthor.Tests.SyntaxNodeTests.NodeEmit;

namespace CSharpAuthor.Tests.SyntaxNodeTests;

/// <summary>
/// The spacing half of the grammar. The generated nodes encode token order; every one of
/// these tests pins down a whitespace decision that the grammar does not make.
/// </summary>
public class SpacingPolicyTests
{
    // R1 - two word-like tokens separate.

    [Fact]
    public void KeywordsSeparateFromEachOther()
    {
        var declaration = new FieldDeclaration(
            new VariableDeclaration(Type("System", "Int32")) { Variables = { new VariableDeclarator("count") } });

        declaration.Modifiers.Add("public");
        declaration.Modifiers.Add("static");
        declaration.Modifiers.Add("readonly");

        Assert.Equal("public static readonly Int32 count;", Emit(declaration));
    }

    // R2 - punctuation binds tight.

    [Fact]
    public void MemberAccessTakesNoSpace()
    {
        var expression = new MemberAccessExpression(Id("builder"), ".", "Length");

        Assert.Equal("builder.Length", Emit(expression));
    }

    [Fact]
    public void SemicolonBindsToTheStatement()
    {
        Assert.Equal("Foo();", Emit(Statement(Call(Id("Foo")))));
    }

    [Fact]
    public void CommaSeparatesArgumentsWithOneSpace()
    {
        var call = Call(Id("Foo"), Id("a"), Id("b"), Id("c"));

        Assert.Equal("Foo(a, b, c)", Emit(call));
    }

    // R3 - the call-versus-control parenthesis. This is the rule that produced
    // `Dogor(Cator_)` in the prototype, and the one that decides `typeof(int)`
    // against `if (x)`.

    [Fact]
    public void CallParenthesisBindsToItsTarget()
    {
        Assert.Equal("Dogor(Cator)", Emit(Call(Id("Dogor"), Id("Cator"))));
    }

    [Fact]
    public void ControlKeywordTakesASpaceBeforeItsParenthesis()
    {
        var statement = new IfStatement(Id("condition"), new Block());

        Assert.Equal("if (condition)\n{\n}\n", Emit(statement));
    }

    [Fact]
    public void FunctionLikeKeywordBindsToItsParenthesis()
    {
        var expression = new TypeOfExpression(TypeRef.Of(new PredefinedType("int")));

        Assert.Equal("typeof(int)", Emit(expression));
    }

    [Fact]
    public void ChainedCallsStayTight()
    {
        var expression = Call(new MemberAccessExpression(Call(Id("Get")), ".", "ToString"));

        Assert.Equal("Get().ToString()", Emit(expression));
    }

    // R4 - brackets.

    [Fact]
    public void ArrayRankBindsToTheElementType()
    {
        var type = new ArrayType(TypeRef.Of(new PredefinedType("int"))) { RankSpecifiers = { Rank(1) } };

        Assert.Equal("int[]", Emit(type));
    }

    [Fact]
    public void JaggedArrayKeepsBothRanks()
    {
        var type = new ArrayType(TypeRef.Of(new PredefinedType("int"))) { RankSpecifiers = { Rank(1), Rank(1) } };

        Assert.Equal("int[][]", Emit(type));
    }

    [Fact]
    public void MultiDimensionalArrayKeepsItsCommas()
    {
        var type = new ArrayType(TypeRef.Of(new PredefinedType("int"))) { RankSpecifiers = { Rank(2) } };

        Assert.Equal("int[,]", Emit(type));
    }

    [Fact]
    public void IndexerBindsToItsTarget()
    {
        var list = new BracketedArgumentList();
        list.Arguments.Add(new Argument(Literal.Int(0)));

        var expression = new ElementAccessExpression(Id("items"), list);

        Assert.Equal("items[0]", Emit(expression));
    }

    // R5 - angle brackets are always tight, because comparison operators never
    // arrive as literal `<` token fields.

    [Fact]
    public void GenericArgumentsAreTight()
    {
        var arguments = new TypeArgumentList();
        arguments.Arguments.Add(new PredefinedType("string"));
        arguments.Arguments.Add(new PredefinedType("int"));

        var name = new GenericName("Dictionary", arguments);

        Assert.Equal("Dictionary<string, int>", Emit(name));
    }

    [Fact]
    public void GenericTypeSeparatesFromTheNameItDeclares()
    {
        var arguments = new TypeArgumentList();
        arguments.Arguments.Add(new PredefinedType("int"));

        var declaration = new VariableDeclaration(TypeRef.Of(new GenericName("List", arguments)))
        {
            Variables = { new VariableDeclarator("values") },
        };

        Assert.Equal("List<int> values", Emit(declaration));
    }

    // R6 - `?` is a nullable marker in a type and a ternary elsewhere.

    [Fact]
    public void NullableMarkerBindsToItsType()
    {
        var declaration = new VariableDeclaration(TypeRef.Of(new NullableType(TypeRef.Of(new PredefinedType("int")))))
        {
            Variables = { new VariableDeclarator("maybe") },
        };

        Assert.Equal("int? maybe", Emit(declaration));
    }

    [Fact]
    public void TernaryQuestionMarkIsSpaced()
    {
        var expression = new ConditionalExpression(Id("flag"), Id("yes"), Id("no"));

        Assert.Equal("flag ? yes : no", Emit(expression));
    }

    // R7 - colons.

    [Fact]
    public void BaseListColonIsSpaced()
    {
        var declaration = new ClassDeclaration("Widget")
        {
            BaseList = new BaseList { Types = { new SimpleBaseType(Type("", "IWidget")) } },
        };

        Assert.Equal("class Widget : IWidget\n{\n}\n", Emit(declaration));
    }

    [Fact]
    public void NamedArgumentColonBindsToTheName()
    {
        var call = new InvocationExpression(
            Id("Foo"),
            new ArgumentList
            {
                Arguments = { new Argument(Literal.Int(1)) { NameColon = new NameColon(TypeRef.Of(Id("count"))) } },
            });

        Assert.Equal("Foo(count: 1)", Emit(call));
    }

    // R8 - the two semicolons in a `for` header are separators, not terminators.
    // Structural: a semicolon that is not the node's last token.

    [Fact]
    public void ForHeaderSemicolonsDoNotBreakTheLine()
    {
        var statement = new ForStatement(Statement(Call(Id("Step"))))
        {
            Declaration = new VariableDeclaration(TypeRef.Of(new PredefinedType("int")))
            {
                Variables = { new VariableDeclarator("i") { Initializer = new EqualsValueClause(Literal.Int(0)) } },
            },
            Condition = new BinaryExpression(Id("i"), "<", Id("count")),
            Incrementors = { new PostfixUnaryExpression(Id("i"), "++") },
        };

        Assert.Equal("for (int i = 0; i < count; i++)\n    Step();", Emit(statement));
    }

    // R9 - Allman block braces versus inline initialiser braces.

    [Fact]
    public void BlockBracesOwnTheirLines()
    {
        var block = new Block { Statements = { Statement(Call(Id("First"))), Statement(Call(Id("Second"))) } };

        Assert.Equal("{\n    First();\n    Second();\n}\n", Emit(block));
    }

    [Fact]
    public void InitializerBracesStayOnTheLine()
    {
        var initializer = new InitializerExpression
        {
            Expressions = { Literal.Int(1), Literal.Int(2), Literal.Int(3) },
        };

        var creation = new ArrayCreationExpression(
            TypeRef.Of(new ArrayType(TypeRef.Of(new PredefinedType("int"))) { RankSpecifiers = { Rank(1) } }))
        {
            Initializer = initializer,
        };

        Assert.Equal("new int[] { 1, 2, 3 }", Emit(creation));
    }

    // R10 - an embedded statement takes its own line at one extra indent; a block does not.

    [Fact]
    public void EmbeddedStatementIsIndentedOnItsOwnLine()
    {
        var statement = new IfStatement(Id("flag"), new ReturnStatement());

        Assert.Equal("if (flag)\n    return;", Emit(statement));
    }

    [Fact]
    public void ElseIfStaysOnTheElseLine()
    {
        var inner = new IfStatement(Id("second"), new ReturnStatement());
        var outer = new IfStatement(Id("first"), new ReturnStatement())
        {
            Else = new ElseClause(inner),
        };

        Assert.Equal("if (first)\n    return;\nelse if (second)\n    return;", Emit(outer));
    }

    // R15 - a keyword used as an identifier is escaped rather than emitted wrong.

    [Fact]
    public void KeywordIdentifierIsEscaped()
    {
        var parameter = new Parameter { Type = TypeRef.Of(new PredefinedType("string")), Identifier = "class" };

        Assert.Equal("string @class", Emit(parameter));
    }

    [Fact]
    public void ContextualKeywordIdentifierIsNotEscaped()
    {
        var parameter = new Parameter { Type = TypeRef.Of(new PredefinedType("string")), Identifier = "value" };

        Assert.Equal("string value", Emit(parameter));
    }

    private static ArrayRankSpecifier Rank(int dimensions)
    {
        var rank = new ArrayRankSpecifier();

        for (var i = 0; i < dimensions; i++)
        {
            rank.Sizes.Add(new OmittedArraySizeExpression());
        }

        return rank;
    }
}
