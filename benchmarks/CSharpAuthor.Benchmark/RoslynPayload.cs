#if BENCH_ROSLYN
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Bench;

/// <summary>
/// The §10 reference point: the same file built with Roslyn's <c>SyntaxFactory</c> and rendered
/// with <c>NormalizeWhitespace().ToFullString()</c>.
/// </summary>
/// <remarks>
/// Opt-in - build with <c>-p:IncludeRoslynReference=true</c>. Structure (declarations, blocks,
/// statements) is built node by node; leaf types and a handful of compound expressions go through
/// <c>ParseTypeName</c> / <c>ParseExpression</c>, which is what generators actually write. Its
/// output is equivalent C# but not textually identical to the other two scenarios: Roslyn's
/// normaliser puts braces and blank lines where it wants them, which is the point of the
/// comparison rather than a flaw in it.
/// </remarks>
internal static class RoslynPayload
{
    private static readonly string[] Types =
    {
        "string", "string", "string", "string", "string",
        "int", "int", "int",
        "bool", "bool", "bool",
        "Guid", "Guid",
        "DateTime", "DateTime",
        "decimal", "decimal",
        "double", "double",
        "long", "long",
        "TimeSpan", "TimeSpan",
        "IReadOnlyList<string>", "IReadOnlyDictionary<string,int>",
    };

    private static readonly string[] Names =
    {
        "Id", "Name", "Category", "Description", "ScopeName",
        "Order", "Version", "RetryLimit",
        "IsEnabled", "IsTransient", "AllowsNull",
        "Key", "CorrelationId",
        "CreatedAt", "ModifiedAt",
        "Amount", "Discount",
        "Ratio", "Weight",
        "Ticks", "Sequence",
        "Duration", "Timeout",
        "Tags", "Counters",
    };

    private static readonly string[] Parameters =
    {
        "id", "name", "category", "description", "scopeName",
        "order", "version", "retryLimit",
        "isEnabled", "isTransient", "allowsNull",
        "key", "correlationId",
        "createdAt", "modifiedAt",
        "amount", "discount",
        "ratio", "weight",
        "ticks", "sequence",
        "duration", "timeout",
        "tags", "counters",
    };

    public static string Generate()
    {
        var members = new List<MemberDeclarationSyntax> { Constructor() };

        for (var i = 0; i < Names.Length; i++)
        {
            members.Add(
                PropertyDeclaration(ParseTypeName(Types[i]), Identifier(Names[i]))
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddAccessorListAccessors(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                        AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))));
        }

        members.Add(ExecuteMethod());

        var classDeclaration = ClassDeclaration("BenchmarkPayload")
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddMembers(members.ToArray());

        var namespaceDeclaration = NamespaceDeclaration(ParseName("CSharpAuthor.Benchmark.Generated"))
            .AddMembers(classDeclaration);

        var compilationUnit = CompilationUnit()
            .AddUsings(
                UsingDirective(ParseName("System")),
                UsingDirective(ParseName("System.Collections.Generic")),
                UsingDirective(ParseName("System.Globalization")),
                UsingDirective(ParseName("System.Text")))
            .AddMembers(namespaceDeclaration);

        return compilationUnit.NormalizeWhitespace().ToFullString();
    }

    private static ConstructorDeclarationSyntax Constructor()
    {
        var parameters = new ParameterSyntax[Names.Length];
        var assignments = new StatementSyntax[Names.Length];

        for (var i = 0; i < Names.Length; i++)
        {
            parameters[i] = Parameter(Identifier(Parameters[i])).WithType(ParseTypeName(Types[i]));

            assignments[i] = ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(Names[i]),
                    IdentifierName(Parameters[i])));
        }

        return ConstructorDeclaration("BenchmarkPayload")
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(parameters)
            .WithBody(Block(assignments));
    }

    private static MethodDeclarationSyntax ExecuteMethod()
    {
        var statements = new List<StatementSyntax>
        {
            // 1 - 6
            Local("builder", ObjectCreationExpression(ParseTypeName("StringBuilder")).WithArgumentList(ArgumentList())),
            Local("timestamp", ParseExpression("DateTime.UtcNow")),
            Local("attempts", LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))),
            Local("completed", LiteralExpression(SyntaxKind.FalseLiteralExpression)),
            Local("identifier", ParseExpression("Key.ToString()")),
            Local("separator", LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(";"))),

            // 7 - 12
            Append(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("Id="))),
            Append(IdentifierName("Id")),
            Append(IdentifierName("separator")),
            Append(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("Name="))),
            Append(IdentifierName("Name")),
            Append(IdentifierName("separator")),

            // 13
            IfStatement(
                    ParseExpression("IsEnabled && verbose"),
                    Block(Append(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("enabled")))))
                .WithElse(ElseClause(
                    Block(Append(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("disabled")))))),

            // 14
            ForEachStatement(
                IdentifierName("var"),
                Identifier("tag"),
                IdentifierName("Tags"),
                Block(Append(IdentifierName("tag")), Append(IdentifierName("separator")))),

            // 15
            WhileStatement(
                ParseExpression("attempts < retryCount"),
                Block(ExpressionStatement(ParseExpression("attempts = attempts + 1")))),

            // 16 - 18
            Local("total", ParseExpression("Order * Version")),
            Local("ratioText", ParseExpression("Ratio.ToString(CultureInfo.InvariantCulture)")),
            Local("amountText", ParseExpression("Amount.ToString(CultureInfo.InvariantCulture)")),

            // 19 - 20
            Append(IdentifierName("ratioText")),
            Append(IdentifierName("amountText")),

            // 21
            TryStatement()
                .WithBlock(Block(Append(ParseExpression("Counters.Count"))))
                .AddCatches(
                    CatchClause()
                        .WithDeclaration(CatchDeclaration(ParseTypeName("Exception"), Identifier("exception")))
                        .WithBlock(Block(Append(ParseExpression("exception.Message"))))),

            // 22
            IfStatement(
                ParseExpression("total > 100"),
                Block(ExpressionStatement(ParseExpression("completed = true")))),

            // 23 - 26
            Append(ParseExpression("timestamp.ToString(\"O\")")),
            Append(IdentifierName("identifier")),
            Append(IdentifierName("completed")),
            Append(ParseExpression("Description ?? \"none\"")),

            // 27
            ReturnStatement(ParseExpression("builder.ToString()")),
        };

        return MethodDeclaration(ParseTypeName("string"), Identifier("Execute"))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(Identifier("retryCount")).WithType(ParseTypeName("int")),
                Parameter(Identifier("verbose")).WithType(ParseTypeName("bool")))
            .WithBody(Block(statements));
    }

    private static StatementSyntax Local(string name, ExpressionSyntax value) =>
        LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .AddVariables(VariableDeclarator(Identifier(name)).WithInitializer(EqualsValueClause(value))));

    private static StatementSyntax Append(ExpressionSyntax argument) =>
        ExpressionStatement(
            InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("builder"),
                        IdentifierName("Append")))
                .AddArgumentListArguments(Argument(argument)));
}
#endif
