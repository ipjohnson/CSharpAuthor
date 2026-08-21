using System;

namespace CSharpAuthor;

/// <summary>
/// A disposable held for the rest of a scope: <c>using var x = ...;</c> on C# 8, and the braced
/// <c>using (var x = ...) { ... }</c> before it.
/// </summary>
/// <remarks>
/// The two forms differ in shape, not in meaning - which is why the statements that follow belong
/// to this node rather than to whatever contains it. A writer that emitted only the header would
/// have nothing to put inside the braces when it had to downlevel.
/// </remarks>
public class UsingDeclarationStatement : BaseBlockDefinition
{
    private readonly IOutputComponent _declaration;

    /// <summary>Holds <paramref name="declaration"/> - typically <c>var x = new Thing()</c>.</summary>
    public UsingDeclarationStatement(IOutputComponent declaration)
    {
        _declaration = declaration ?? throw new ArgumentNullException(nameof(declaration));
    }

    /// <inheritdoc cref="UsingDeclarationStatement(IOutputComponent)" />
    public UsingDeclarationStatement(string declaration)
        : this(new CodeOutputComponent(declaration) { Indented = false })
    {
    }

    /// <summary>What is being held, named in any diagnostic it produces.</summary>
    public string? Context { get; set; }

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        if (session.MayEmit(LanguageFeature.UsingDeclarations, outputContext, Context))
        {
            outputContext.WriteIndent("using ");
            _declaration.WriteOutput(outputContext);
            outputContext.WriteLine(";");

            foreach (var statement in StatementList)
            {
                statement.WriteOutput(outputContext);
            }

            return;
        }

        outputContext.WriteIndent("using (");
        _declaration.WriteOutput(outputContext);
        outputContext.WriteLine(")");

        WriteBlock(outputContext);
    }
}
