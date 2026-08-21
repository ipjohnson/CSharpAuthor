using System;

namespace CSharpAuthor;

/// <summary>
/// <c>nameof(x)</c>, or the string it would have produced on a target that predates it.
/// </summary>
public class NameOfStatement : BaseOutputComponent
{
    private readonly string _symbol;

    /// <summary>The name of <paramref name="symbol"/>, which may be dotted.</summary>
    public NameOfStatement(string symbol)
    {
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
    }

    /// <summary>What the name is for, named in any diagnostic it produces.</summary>
    public string? Context { get; set; }

    /// <inheritdoc />
    protected override void WriteComponentOutput(IOutputContext outputContext)
    {
        var session = outputContext.EmitSession();

        if (session.MayEmit(LanguageFeature.NameOf, outputContext, Context))
        {
            outputContext.Write("nameof(");
            outputContext.Write(_symbol);
            outputContext.Write(")");

            return;
        }

        // nameof(A.B.C) is "C", not "A.B.C" - the downlevel is the string the compiler would
        // have produced, not the text inside the parentheses.
        var lastDot = _symbol.LastIndexOf('.');

        outputContext.Write(
            StringLiteralStatement.Quote(lastDot < 0 ? _symbol : _symbol.Substring(lastDot + 1)));
    }
}
