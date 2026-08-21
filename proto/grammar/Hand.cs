#nullable enable
using CSharpAuthor;

namespace GeneratedGrammar.Full;

// ---------------------------------------------------------------------------
// The hand-written layer. Eight nodes carry a VALUE rather than a fixed token,
// so the grammar cannot generate their body. They live here, beside the
// generated file, never inside it — so regeneration can never clobber them.
// ---------------------------------------------------------------------------

public sealed class IdentifierName : IExpression, IType
{
    public string Name { get; }
    public IdentifierName(string name) => Name = name;
    public void AddUsingNamespace(string ns) { }
    public void WriteOutput(IOutputContext c) => Tk.W(c, Name);
}

public sealed class LiteralExpression : IExpression
{
    private readonly string _text;
    private LiteralExpression(string text) => _text = text;

    public static LiteralExpression String(string v) => new(Quote(v));
    public static LiteralExpression Int(int v) => new(v.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static LiteralExpression Bool(bool v) => new(v ? "true" : "false");
    public static LiteralExpression Null() => new("null");

    private static string Quote(string v)
    {
        var sb = new System.Text.StringBuilder("\"");
        foreach (var ch in v)
            sb.Append(ch switch
            {
                '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n",
                '\r' => "\\r", '\t' => "\\t", '\0' => "\\0",
                _ => ch.ToString()
            });
        return sb.Append('"').ToString();
    }

    public void AddUsingNamespace(string ns) { }
    public void WriteOutput(IOutputContext c) => c.Write(_text);
}

/// <summary>Last-resort escape. Still a node, so it composes and still defers its types.</summary>
public sealed class Raw : IExpression, IStatement, IPattern, IMemberDeclaration
{
    private readonly object[] _parts;
    public Raw(params object[] parts) => _parts = parts;
    public void AddUsingNamespace(string ns) { }
    public void WriteOutput(IOutputContext c)
    {
        foreach (var p in _parts)
        {
            if (p is ITypeDefinition t) c.Write(t);           // still deferred
            else if (p is IOutputComponent o) o.WriteOutput(c);
            else c.Write(p?.ToString() ?? "");
        }
    }
}
