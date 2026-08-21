using System.Globalization;
using System.Threading;
using CSharpAuthor.Syntax;
using Xunit;
using static CSharpAuthor.Tests.SyntaxNodeTests.NodeEmit;
using Attr = CSharpAuthor.Syntax.Attribute;

namespace CSharpAuthor.Tests.SyntaxNodeTests;

/// <summary>
/// The corners of the grammar V1 could not reach at all - patterns, lambdas, switch
/// expressions, interpolated strings, ranges, collection expressions, operators, indexers,
/// directives. Every one of these found a real spacing defect the first time it was run.
/// </summary>
public class ModernSyntaxTests
{
    // Lambdas - all three shapes.

    [Fact]
    public void SimpleLambdaWithAnExpressionBody()
    {
        var lambda = new SimpleLambdaExpression(new Parameter { Identifier = "x" })
        {
            ExpressionBody = new BinaryExpression(Id("x"), "*", Literal.Int(2)),
        };

        Assert.Equal("x => x * 2", Emit(lambda));
    }

    [Fact]
    public void ParenthesizedLambdaWithTwoParameters()
    {
        var lambda = new ParenthesizedLambdaExpression(new ParameterList
        {
            Parameters = { new Parameter { Identifier = "a" }, new Parameter { Identifier = "b" } },
        })
        {
            ExpressionBody = new BinaryExpression(Id("a"), "+", Id("b")),
        };

        Assert.Equal("(a, b) => a + b", Emit(lambda));
    }

    [Fact]
    public void LambdaWithABlockBody()
    {
        var lambda = new ParenthesizedLambdaExpression(new ParameterList())
        {
            Block = new Block { Statements = { new ReturnStatement { Expression = Literal.Int(1) } } },
        };

        Assert.Equal("() =>\n{\n    return 1;\n}\n", Emit(lambda));
    }

    // Patterns - V1 emitted none of these.

    [Fact]
    public void DeclarationPattern()
    {
        var expression = new IsPatternExpression(
            Id("o"),
            new DeclarationPattern(Type("", "Widget"), new SingleVariableDesignation("w")));

        Assert.Equal("o is Widget w", Emit(expression));
    }

    [Fact]
    public void NegatedConstantPattern()
    {
        var expression = new IsPatternExpression(Id("o"), new UnaryPattern(new ConstantPattern(Literal.Null())));

        Assert.Equal("o is not null", Emit(expression));
    }

    [Fact]
    public void RelationalPatternOperatorSeparatesFromItsOperand()
    {
        var expression = new IsPatternExpression(
            Id("n"),
            new BinaryPattern(
                new RelationalPattern(">=", Literal.Int(0)),
                "and",
                new RelationalPattern("<", Literal.Int(10))));

        Assert.Equal("n is >= 0 and < 10", Emit(expression));
    }

    [Fact]
    public void RecursivePatternWithAPropertySubpattern()
    {
        var expression = new IsPatternExpression(Id("p"), new RecursivePattern
        {
            Type = Type("", "Point"),
            PropertyPatternClause = new PropertyPatternClause
            {
                Subpatterns =
                {
                    new Subpattern(new ConstantPattern(Literal.Int(0)))
                    {
                        ExpressionColon = new NameColon(TypeRef.Of(Id("X"))),
                    },
                },
            },
        });

        Assert.Equal("p is Point { X: 0 }", Emit(expression));
    }

    [Fact]
    public void ListPatternWithADiscardAndASlice()
    {
        var expression = new IsPatternExpression(Id("a"), new ListPattern
        {
            Patterns = { new ConstantPattern(Literal.Int(1)), new DiscardPattern(), new SlicePattern() },
        });

        Assert.Equal("a is [1, _, ..]", Emit(expression));
    }

    [Fact]
    public void SwitchExpression()
    {
        var expression = new SwitchExpression(Id("value"))
        {
            Arms =
            {
                new SwitchExpressionArm(new ConstantPattern(Literal.Int(1)), Literal.String("one")),
                new SwitchExpressionArm(new DiscardPattern(), Literal.String("many")),
            },
        };

        Assert.Equal("value switch { 1 => \"one\", _ => \"many\" }", Emit(expression));
    }

    // Interpolated strings - everything inside one abuts its neighbour.

    [Fact]
    public void InterpolatedStringHasNoStrayWhitespace()
    {
        var expression = new InterpolatedStringExpression("$\"", "\"")
        {
            Contents =
            {
                new InterpolatedStringText("count is "),
                new Interpolation(Id("count")) { FormatClause = new InterpolationFormatClause("N2") },
            },
        };

        Assert.Equal("$\"count is {count:N2}\"", Emit(expression));
    }

    // Ranges, spreads and null-conditional access.

    [Fact]
    public void RangeBindsToBothOperands()
    {
        var expression = new RangeExpression { LeftOperand = Literal.Int(1), RightOperand = Literal.Int(5) };

        Assert.Equal("1..5", Emit(expression));
    }

    [Fact]
    public void SpreadKeepsTheCommaButNotTheOperandSpace()
    {
        var expression = new CollectionExpression
        {
            Elements = { new ExpressionElement(Literal.Int(1)), new SpreadElement(Id("rest")) },
        };

        Assert.Equal("[1, ..rest]", Emit(expression));
    }

    [Fact]
    public void NullConditionalMemberAccess()
    {
        var expression = new ConditionalAccessExpression(Id("a"), new MemberBindingExpression(TypeRef.Of(Id("B"))));

        Assert.Equal("a?.B", Emit(expression));
    }

    [Fact]
    public void NullConditionalElementAccess()
    {
        var expression = new ConditionalAccessExpression(
            Id("a"),
            new ElementBindingExpression(new BracketedArgumentList { Arguments = { new Argument(Literal.Int(0)) } }));

        Assert.Equal("a?[0]", Emit(expression));
    }

    // Members V1 had no support for at all.

    [Fact]
    public void OperatorDeclarationNamesItselfAndBindsItsParameterList()
    {
        var declaration = new OperatorDeclaration(Type("", "Widget"), "+", new ParameterList
        {
            Parameters =
            {
                new Parameter { Type = Type("", "Widget"), Identifier = "a" },
                new Parameter { Type = Type("", "Widget"), Identifier = "b" },
            },
        })
        {
            SemicolonToken = true,
        };

        declaration.Modifiers.Add("public");
        declaration.Modifiers.Add("static");

        Assert.Equal("public static Widget operator +(Widget a, Widget b);", Emit(declaration));
    }

    [Fact]
    public void IndexerBindsItsBracketedParameterList()
    {
        var declaration = new IndexerDeclaration(
            TypeRef.Of(new PredefinedType("int")),
            new BracketedParameterList
            {
                Parameters = { new Parameter { Type = TypeRef.Of(new PredefinedType("int")), Identifier = "i" } },
            })
        {
            AccessorList = new AccessorList
            {
                Accessors = { new AccessorDeclaration("get") { SemicolonToken = true } },
            },
        };

        declaration.Modifiers.Add("public");

        Assert.Equal("public int this[int i]\n{\n    get;\n}\n", Emit(declaration));
    }

    // Target-typed `new` and implicit arrays: `new` binds its own brackets and parentheses.

    [Fact]
    public void TargetTypedNewBindsItsParentheses()
    {
        var expression = new ImplicitObjectCreationExpression(new ArgumentList());

        Assert.Equal("new()", Emit(expression));
    }

    [Fact]
    public void ImplicitArrayBindsItsBrackets()
    {
        var expression = new ImplicitArrayCreationExpression(new InitializerExpression
        {
            Expressions = { Literal.Int(1), Literal.Int(2) },
        });

        Assert.Equal("new[] { 1, 2 }", Emit(expression));
    }

    // Directives own their line and bind to their keyword.

    [Fact]
    public void RegionDirectiveBindsItsHash()
    {
        Assert.Equal("#region", Emit(new RegionDirectiveTrivia()));
    }

    [Fact]
    public void NullableDirectiveBindsItsHashAndSpacesItsSetting()
    {
        Assert.Equal("#nullable enable", Emit(new NullableDirectiveTrivia("enable")));
    }

    // Constraints, generics and array ranks together.

    [Fact]
    public void GenericMethodWithEveryConstraintForm()
    {
        var declaration = new MethodDeclaration(
            TypeRef.Of(new ArrayType(TypeRef.Of(new PredefinedType("int"))) { RankSpecifiers = { Rank(2) } }),
            "Grid",
            new ParameterList())
        {
            TypeParameterList = new TypeParameterList { Parameters = { new TypeParameter("T") } },
            ConstraintClauses =
            {
                new TypeParameterConstraintClause(Type("", "T"))
                {
                    Constraints =
                    {
                        new ClassOrStructConstraint("struct"),
                        new TypeConstraint(Type("System", "IComparable")),
                        new ConstructorConstraint(),
                    },
                },
            },
            SemicolonToken = true,
        };

        declaration.Modifiers.Add("public");

        Assert.Equal(
            "public int[,] Grid<T>()\n" +
            "    where T : struct, IComparable, new();",
            Emit(declaration));
    }

    // The escape hatch must not become a culture bug.

    [Fact]
    public void RawRendersNumbersInvariantlyUnderAnyCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("1.5", Emit(new Raw(1.5)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void LiteralRendersNumbersInvariantlyUnderAnyCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("1.5f", Emit(Literal.Float(1.5f)));
            Assert.Equal("1.5", Emit(Literal.Double(1.5)));
            Assert.Equal("1.5m", Emit(Literal.Decimal(1.5m)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void StringLiteralIsEscaped()
    {
        Assert.Equal("\"he said \\\"hi\\\"\"", Emit(Literal.String("he said \"hi\"")));
        Assert.Equal("\"a\\\\b\"", Emit(Literal.String("a\\b")));
        Assert.Equal("\"line\\nbreak\"", Emit(Literal.String("line\nbreak")));
    }

    [Fact]
    public void RawStillDefersTheTypesItCarries()
    {
        var context = new OutputContext();

        new Raw("var x = new ", new TypeDefinition(TypeDefinitionEnum.ClassDefinition, "System.Text", "StringBuilder", false), "();")
            .WriteOutput(context);

        context.GenerateUsingStatements();

        var output = context.Output().Replace("\r\n", "\n");

        Assert.Contains("using System.Text;", output);
        Assert.Contains("var x = new StringBuilder();", output);
    }

    // The one name in the generated grammar that collides with a BCL type.

    [Fact]
    public void AttributeNodeIsUsableWhenSystemIsInScope()
    {
        var list = new AttributeList { Attributes = { new Attr(Type("System", "Obsolete")) } };

        Assert.Equal("[Obsolete]", Emit(list));
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
