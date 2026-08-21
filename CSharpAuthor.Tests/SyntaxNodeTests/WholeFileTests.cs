using System.Collections.Generic;
using CSharpAuthor.Syntax;
using Xunit;
using static CSharpAuthor.Tests.SyntaxNodeTests.NodeEmit;
using Attr = CSharpAuthor.Syntax.Attribute;

namespace CSharpAuthor.Tests.SyntaxNodeTests;

/// <summary>
/// One whole file, asserted line for line.
/// </summary>
/// <remarks>
/// <para>
/// The per-rule tests each pin down one decision. This pins down all of them interacting,
/// which is where a layout policy actually goes wrong: a blank line that only appears after
/// a block, an indent that survives one construct and not the next, a break that doubles
/// when two rules both ask for one.
/// </para>
/// <para>
/// The expectation below was compiled by the C# 12 compiler before being pasted here -
/// <c>dotnet build</c> on the emitted text, zero errors - so this is a statement about
/// valid C#, not just about characters.
/// </para>
/// </remarks>
public class WholeFileTests
{
    [Fact]
    public void EmitsACompleteCompilableFile()
    {
        Assert.Equal(
            """
            using System;
            using System.Collections.Generic;

            namespace Acme.Widgets;

            [Serializable]
            public sealed partial class Widget
            {
                private readonly List<int> _items = new();

                public string Name
                {
                    get;
                    set;
                }
                = "";

                public int Count => _items.Count;

                public int this[int index] => _items[index];

                public int Sum()
                {
                    int total = 0;
                    for (int i = 0; i < _items.Count; i++)
                    {
                        total += i;
                    }
                    foreach (int value in _items)
                    {
                        if (value is >= 0 and < 10)
                            total += value;
                        else
                            continue;
                    }
                    switch (total)
                    {
                        case 0:
                            break;
                        default:
                            break;
                    }
                    try
                    {
                        Console.WriteLine($"total={total}");
                    }
                    catch (Exception e) when (e.Message != null)
                    {
                    }
                    finally
                    {
                    }
                    return total;
                }

                public int[,] Grid<T>(T seed)
                    where T : class, new()
                {
                    return new int[1, 1];
                }

                public static Widget operator +(Widget a, Widget b)
                {
                    return a;
                }

                enum Mode
                {
                    Fast,
                    Slow
                }
            }

            """.Replace("\r\n", "\n"),
            Emit(BuildFile()));
    }

    private static CompilationUnit BuildFile()
    {
        var members = new List<IMemberDeclaration>();

        var field = new FieldDeclaration(new VariableDeclaration(Type("System.Collections.Generic", "List<int>"))
        {
            Variables =
            {
                new VariableDeclarator("_items")
                {
                    Initializer = new EqualsValueClause(new ImplicitObjectCreationExpression(new ArgumentList())),
                },
            },
        });
        field.Modifiers.Add("private");
        field.Modifiers.Add("readonly");
        members.Add(field);

        // An accessor list plus an initializer. The `= "";` lands on its own line because a
        // block brace closes through the context's CloseScope, which ends the line itself -
        // see docs/v2-open-questions.md. Valid C#, and it compiles.
        var name = new PropertyDeclaration(new PredefinedType("string"), "Name")
        {
            AccessorList = new AccessorList
            {
                Accessors =
                {
                    new AccessorDeclaration("get") { SemicolonToken = true },
                    new AccessorDeclaration("set") { SemicolonToken = true },
                },
            },
            Initializer = new EqualsValueClause(Literal.String("")),
            SemicolonToken = true,
        };
        name.Modifiers.Add("public");
        members.Add(name);

        var count = new PropertyDeclaration(new PredefinedType("int"), "Count")
        {
            ExpressionBody = new ArrowExpressionClause(Member(Id("_items"), "Count")),
            SemicolonToken = true,
        };
        count.Modifiers.Add("public");
        members.Add(count);

        var indexer = new IndexerDeclaration(
            new PredefinedType("int"),
            new BracketedParameterList
            {
                Parameters = { new Parameter { Type = new PredefinedType("int"), Identifier = "index" } },
            })
        {
            ExpressionBody = new ArrowExpressionClause(new ElementAccessExpression(
                Id("_items"),
                new BracketedArgumentList { Arguments = { new Argument(Id("index")) } })),
            SemicolonToken = true,
        };
        indexer.Modifiers.Add("public");
        members.Add(indexer);

        members.Add(SumMethod());
        members.Add(GridMethod());
        members.Add(PlusOperator());
        members.Add(new EnumDeclaration("Mode")
        {
            Members = { new EnumMemberDeclaration("Fast"), new EnumMemberDeclaration("Slow") },
        });

        var type = new ClassDeclaration("Widget");
        type.Modifiers.Add("public");
        type.Modifiers.Add("sealed");
        type.Modifiers.Add("partial");
        type.AttributeLists.Add(new AttributeList { Attributes = { new Attr(Type("System", "Serializable")) } });
        type.Members.AddRange(members);

        return new CompilationUnit
        {
            Usings =
            {
                new UsingDirective(Type("", "System")),
                new UsingDirective(Type("", "System.Collections.Generic")),
            },
            Members = { new FileScopedNamespaceDeclaration(Type("", "Acme.Widgets")) { Members = { type } } },
        };
    }

    private static MethodDeclaration SumMethod()
    {
        var loop = new ForStatement(new Block
        {
            Statements = { Statement(new AssignmentExpression(Id("total"), "+=", Id("i"))) },
        })
        {
            Declaration = new VariableDeclaration(new PredefinedType("int"))
            {
                Variables = { new VariableDeclarator("i") { Initializer = new EqualsValueClause(Literal.Int(0)) } },
            },
            Condition = new BinaryExpression(Id("i"), "<", Member(Id("_items"), "Count")),
            Incrementors = { new PostfixUnaryExpression(Id("i"), "++") },
        };

        var each = new ForEachStatement(new PredefinedType("int"), "value", Id("_items"), new Block
        {
            Statements =
            {
                new IfStatement(
                    new IsPatternExpression(Id("value"), new BinaryPattern(
                        new RelationalPattern(">=", Literal.Int(0)),
                        "and",
                        new RelationalPattern("<", Literal.Int(10)))),
                    Statement(new AssignmentExpression(Id("total"), "+=", Id("value"))))
                {
                    Else = new ElseClause(new ContinueStatement()),
                },
            },
        });

        var choose = new SwitchStatement(Id("total"))
        {
            Sections =
            {
                new SwitchSection
                {
                    Labels = { new CaseSwitchLabel(Literal.Int(0)) },
                    Statements = { new BreakStatement() },
                },
                new SwitchSection
                {
                    Labels = { new DefaultSwitchLabel() },
                    Statements = { new BreakStatement() },
                },
            },
        };

        var guarded = new TryStatement(new Block
        {
            Statements =
            {
                Statement(Call(
                    Member(Id("Console"), "WriteLine"),
                    new InterpolatedStringExpression("$\"", "\"")
                    {
                        Contents = { new InterpolatedStringText("total="), new Interpolation(Id("total")) },
                    })),
            },
        })
        {
            Catches =
            {
                new CatchClause(new Block())
                {
                    Declaration = new CatchDeclaration(Type("System", "Exception")) { Identifier = "e" },
                    Filter = new CatchFilterClause(
                        new BinaryExpression(Member(Id("e"), "Message"), "!=", Literal.Null())),
                },
            },
            Finally = new FinallyClause(new Block()),
        };

        var method = new MethodDeclaration(new PredefinedType("int"), "Sum", new ParameterList())
        {
            Body = new Block
            {
                Statements =
                {
                    new LocalDeclarationStatement(new VariableDeclaration(new PredefinedType("int"))
                    {
                        Variables =
                        {
                            new VariableDeclarator("total") { Initializer = new EqualsValueClause(Literal.Int(0)) },
                        },
                    }),
                    loop,
                    each,
                    choose,
                    guarded,
                    new ReturnStatement { Expression = Id("total") },
                },
            },
        };

        method.Modifiers.Add("public");

        return method;
    }

    private static MethodDeclaration GridMethod()
    {
        var grid = new ArrayType(new PredefinedType("int"))
        {
            RankSpecifiers = { new ArrayRankSpecifier { Sizes = { new OmittedArraySizeExpression(), new OmittedArraySizeExpression() } } },
        };

        var sized = new ArrayType(new PredefinedType("int"))
        {
            RankSpecifiers = { new ArrayRankSpecifier { Sizes = { Literal.Int(1), Literal.Int(1) } } },
        };

        var method = new MethodDeclaration(
            grid,
            "Grid",
            new ParameterList { Parameters = { new Parameter { Type = Type("", "T"), Identifier = "seed" } } })
        {
            TypeParameterList = new TypeParameterList { Parameters = { new TypeParameter("T") } },
            ConstraintClauses =
            {
                new TypeParameterConstraintClause(Type("", "T"))
                {
                    Constraints = { new ClassOrStructConstraint("class"), new ConstructorConstraint() },
                },
            },
            Body = new Block
            {
                Statements = { new ReturnStatement { Expression = new ArrayCreationExpression(sized) } },
            },
        };

        method.Modifiers.Add("public");

        return method;
    }

    private static OperatorDeclaration PlusOperator()
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
            Body = new Block { Statements = { new ReturnStatement { Expression = Id("a") } } },
        };

        declaration.Modifiers.Add("public");
        declaration.Modifiers.Add("static");

        return declaration;
    }

    private static MemberAccessExpression Member(IExpression target, string name) =>
        new(target, ".", TypeRef.Of(Id(name)));
}
