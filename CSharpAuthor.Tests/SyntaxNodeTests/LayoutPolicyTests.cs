using CSharpAuthor.Syntax;
using Xunit;
using static CSharpAuthor.Tests.SyntaxNodeTests.NodeEmit;

namespace CSharpAuthor.Tests.SyntaxNodeTests;

/// <summary>
/// Line breaks and blank lines - the structural half of the policy. Where
/// <see cref="SpacingPolicyTests"/> pins down what happens between two tokens, these pin
/// down what happens between two <em>constructs</em>.
/// </summary>
public class LayoutPolicyTests
{
    // R11 - member lists take a blank line between elements.

    [Fact]
    public void MembersAreSeparatedByABlankLine()
    {
        var type = new ClassDeclaration("Widget");

        type.Modifiers.Add("public");
        type.Members.Add(Field("first"));
        type.Members.Add(Field("second"));

        Assert.Equal(
            "public class Widget\n" +
            "{\n" +
            "    int first;\n" +
            "\n" +
            "    int second;\n" +
            "}\n",
            Emit(type));
    }

    [Fact]
    public void FirstMemberDoesNotGetALeadingBlankLine()
    {
        var type = new ClassDeclaration("Widget") { Members = { Field("only") } };

        Assert.Equal("class Widget\n{\n    int only;\n}\n", Emit(type));
    }

    [Fact]
    public void ClosingBraceDoesNotInheritTheBlankLineAfterTheLastMember()
    {
        var type = new ClassDeclaration("Widget") { Members = { Field("a"), Field("b") } };

        Assert.EndsWith("int b;\n}\n", Emit(type));
    }

    // R11 - usings break between and leave one blank line after the block.

    [Fact]
    public void UsingsAreOnePerLineWithABlankLineAfterTheBlock()
    {
        var unit = new CompilationUnit
        {
            Usings =
            {
                new UsingDirective(Type("", "System")),
                new UsingDirective(Type("", "System.Text")),
            },
            Members = { new ClassDeclaration("Widget") },
        };

        Assert.Equal(
            "using System;\n" +
            "using System.Text;\n" +
            "\n" +
            "class Widget\n" +
            "{\n" +
            "}\n",
            Emit(unit));
    }

    [Fact]
    public void TypesInACompilationUnitAreSeparatedByABlankLine()
    {
        var unit = new CompilationUnit
        {
            Members = { new ClassDeclaration("First"), new ClassDeclaration("Second") },
        };

        Assert.Equal(
            "class First\n" +
            "{\n" +
            "}\n" +
            "\n" +
            "class Second\n" +
            "{\n" +
            "}\n",
            Emit(unit));
    }

    // R9 / R13 - nesting indents through the context's scope markers.

    [Fact]
    public void NestedTypesIndentThroughTheContextScope()
    {
        var inner = new ClassDeclaration("Inner") { Members = { Field("value") } };
        var outer = new ClassDeclaration("Outer") { Members = { inner } };

        var unit = new NamespaceDeclaration(Type("", "Acme")) { Members = { outer } };

        Assert.Equal(
            "namespace Acme\n" +
            "{\n" +
            "    class Outer\n" +
            "    {\n" +
            "        class Inner\n" +
            "        {\n" +
            "            int value;\n" +
            "        }\n" +
            "    }\n" +
            "}\n",
            Emit(unit));
    }

    [Fact]
    public void FileScopedNamespaceDoesNotIndentItsMembers()
    {
        var unit = new FileScopedNamespaceDeclaration(Type("", "Acme"))
        {
            Members = { new ClassDeclaration("Widget") },
        };

        Assert.Equal(
            "namespace Acme;\n" +
            "\n" +
            "class Widget\n" +
            "{\n" +
            "}\n",
            Emit(unit));
    }

    // R11 - an attribute list on a member owns its line; on a parameter it does not.

    [Fact]
    public void AttributeOnAMemberOwnsItsLine()
    {
        var field = Field("count");

        field.AttributeLists.Add(new AttributeList { Attributes = { new Attribute(Type("", "Obsolete")) } });

        Assert.Equal("[Obsolete]\nint count;", Emit(field));
    }

    [Fact]
    public void AttributeOnAParameterStaysInline()
    {
        var parameter = new Parameter
        {
            Type = TypeRef.Of(new PredefinedType("int")),
            Identifier = "value",
            AttributeLists = { new AttributeList { Attributes = { new Attribute(Type("", "In")) } } },
        };

        Assert.Equal("[In] int value", Emit(parameter));
    }

    // R11 - constraint clauses take one indented line each.

    [Fact]
    public void ConstraintClausesTakeTheirOwnIndentedLines()
    {
        var type = new ClassDeclaration("Cache")
        {
            TypeParameterList = new TypeParameterList { Parameters = { new TypeParameter("TKey"), new TypeParameter("TValue") } },
            ConstraintClauses =
            {
                new TypeParameterConstraintClause(Type("", "TKey")) { Constraints = { new ClassOrStructConstraint("notnull") } },
                new TypeParameterConstraintClause(Type("", "TValue")) { Constraints = { new ClassOrStructConstraint("class") } },
            },
        };

        Assert.Equal(
            "class Cache<TKey, TValue>\n" +
            "    where TKey : notnull\n" +
            "    where TValue : class\n" +
            "{\n" +
            "}\n",
            Emit(type));
    }

    // R9 - a method body is a block; the signature line ends before the brace.

    [Fact]
    public void MethodBodyIsAnAllmanBlock()
    {
        var method = new MethodDeclaration(TypeRef.Of(new PredefinedType("void")), "Run", new ParameterList())
        {
            Body = new Block { Statements = { Statement(Call(Id("Go"))) } },
        };

        method.Modifiers.Add("public");

        Assert.Equal("public void Run()\n{\n    Go();\n}\n", Emit(method));
    }

    [Fact]
    public void AbstractMethodEndsWithASemicolonAndNoBody()
    {
        var method = new MethodDeclaration(TypeRef.Of(new PredefinedType("void")), "Run", new ParameterList())
        {
            SemicolonToken = true,
        };

        method.Modifiers.Add("protected");
        method.Modifiers.Add("abstract");

        Assert.Equal("protected abstract void Run();", Emit(method));
    }

    // R9 - an accessor list is a block, so an accessor with a body nests correctly.

    [Fact]
    public void AutoPropertyAccessorsTakeOneLineEach()
    {
        var property = new PropertyDeclaration(TypeRef.Of(new PredefinedType("int")), "Count")
        {
            AccessorList = new AccessorList
            {
                Accessors =
                {
                    new AccessorDeclaration("get") { SemicolonToken = true },
                    new AccessorDeclaration("set") { SemicolonToken = true },
                },
            },
        };

        property.Modifiers.Add("public");

        Assert.Equal("public int Count\n{\n    get;\n    set;\n}\n", Emit(property));
    }

    [Fact]
    public void AccessorWithABodyNestsInsideTheAccessorList()
    {
        var property = new PropertyDeclaration(TypeRef.Of(new PredefinedType("int")), "Count")
        {
            AccessorList = new AccessorList
            {
                Accessors =
                {
                    new AccessorDeclaration("get")
                    {
                        Body = new Block { Statements = { new ReturnStatement { Expression = Id("_count") } } },
                    },
                },
            },
        };

        Assert.Equal("int Count\n{\n    get\n    {\n        return _count;\n    }\n}\n", Emit(property));
    }

    // R11 - a switch section has no braces of its own, so its statements indent.

    [Fact]
    public void SwitchSectionsIndentTheirStatements()
    {
        var statement = new SwitchStatement(Id("value"))
        {
            Sections =
            {
                new SwitchSection
                {
                    Labels = { new CaseSwitchLabel(Literal.Int(1)) },
                    Statements = { new BreakStatement() },
                },
                new SwitchSection
                {
                    Labels = { new DefaultSwitchLabel() },
                    Statements = { new BreakStatement() },
                },
            },
        };

        Assert.Equal(
            "switch (value)\n" +
            "{\n" +
            "    case 1:\n" +
            "        break;\n" +
            "    default:\n" +
            "        break;\n" +
            "}\n",
            Emit(statement));
    }

    // R9 - try/catch/finally chain their blocks without running blank lines together.

    [Fact]
    public void TryCatchFinallyChainsItsBlocks()
    {
        var statement = new TryStatement(new Block { Statements = { Statement(Call(Id("Risky"))) } })
        {
            Catches =
            {
                new CatchClause(new Block())
                {
                    Declaration = new CatchDeclaration(Type("System", "Exception")) { Identifier = "e" },
                },
            },
            Finally = new FinallyClause(new Block { Statements = { Statement(Call(Id("Cleanup"))) } }),
        };

        Assert.Equal(
            "try\n" +
            "{\n" +
            "    Risky();\n" +
            "}\n" +
            "catch (Exception e)\n" +
            "{\n" +
            "}\n" +
            "finally\n" +
            "{\n" +
            "    Cleanup();\n" +
            "}\n",
            Emit(statement));
    }

    // R11 - an enum's members are comma-separated and one per line.

    [Fact]
    public void EnumMembersAreOnePerLine()
    {
        var declaration = new EnumDeclaration("Colour")
        {
            Members = { new EnumMemberDeclaration("Red"), new EnumMemberDeclaration("Green") },
        };

        Assert.Equal("enum Colour\n{\n    Red,\n    Green\n}\n", Emit(declaration));
    }

    // A top-level statement is a member, not an embedded statement, so it must not indent.

    [Fact]
    public void TopLevelStatementIsNotIndented()
    {
        var unit = new CompilationUnit
        {
            Members =
            {
                new GlobalStatement(Statement(Call(Id("First")))),
                new GlobalStatement(Statement(Call(Id("Second")))),
            },
        };

        Assert.Equal("First();\n\nSecond();", Emit(unit));
    }

    private static FieldDeclaration Field(string name) =>
        new(new VariableDeclaration(TypeRef.Of(new PredefinedType("int")))
        {
            Variables = { new VariableDeclarator(name) },
        });
}
