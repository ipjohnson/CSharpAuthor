#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CSharpAuthor;

namespace V2Terse;

/// <summary>
/// An expression. Concrete (not an interface) so it can carry implicit conversions
/// and operators — which is what makes the typed path shorter to write than string
/// interpolation, not merely safer.
/// </summary>
public sealed class Ex : IOutputComponent
{
    internal const int POr = 1, PAnd = 2, PEq = 3, PRel = 4, PAdd = 5, PUnary = 10, PPrimary = 20;

    private readonly Action<IOutputContext> _write;
    internal int Precedence { get; }

    internal Ex(int precedence, Action<IOutputContext> write)
    { Precedence = precedence; _write = write; }

    public void AddUsingNamespace(string ns) { }
    public void WriteOutput(IOutputContext c) => _write(c);

    // --- the conversions that remove the ceremony -------------------------
    // A bare string is an IDENTIFIER. Literals are explicit, because getting this
    // backwards is the single most common codegen bug.
    public static implicit operator Ex(string identifier) =>
        new(PPrimary, c => c.Write(Ident(identifier)));

    public static implicit operator Ex(int value) =>
        new(PPrimary, c => c.Write(value.ToString(CultureInfo.InvariantCulture)));

    public static implicit operator Ex(bool value) =>
        new(PPrimary, c => c.Write(value ? "true" : "false"));

    // A type used in expression position renders as the type name, deferred.
    public static implicit operator Ex(TypeDefinition type) =>
        new(PPrimary, c => c.Write(type));

    // --- operators --------------------------------------------------------
    public static Ex operator !(Ex e) => new(PUnary, c => { c.Write("!"); Wrap(c, e, PUnary); });
    public static Ex operator &(Ex l, Ex r) => Bin(" && ", PAnd, l, r);
    public static Ex operator |(Ex l, Ex r) => Bin(" || ", POr, l, r);
    public static Ex operator <(Ex l, Ex r) => Bin(" < ", PRel, l, r);
    public static Ex operator >(Ex l, Ex r) => Bin(" > ", PRel, l, r);
    public static Ex operator +(Ex l, Ex r) => Bin(" + ", PAdd, l, r);

    // --- fluent members ---------------------------------------------------
    public Ex Dot(string member) => new(PPrimary, c => { _write(c); c.Write("."); c.Write(Ident(member)); });

    public Ex Call(string method, params Ex[] args) =>
        new(PPrimary, c => { _write(c); c.Write("."); c.Write(Ident(method)); Args(c, args); });

    public Ex Eq(Ex other) => Bin(" == ", PEq, this, other);
    public Ex NotEq(Ex other) => Bin(" != ", PEq, this, other);
    public Ex Is(Ex pattern) => new(PRel, c => { Wrap(c, this, PRel); c.Write(" is "); pattern.WriteOutput(c); });

    // --- statics ----------------------------------------------------------
    public static readonly Ex Null = new(PPrimary, c => c.Write("null"));
    public static readonly Ex This = new(PPrimary, c => c.Write("this"));
    public static readonly Ex True = new(PPrimary, c => c.Write("true"));

    /// <summary>An escaped string literal. Explicit, always.</summary>
    public static Ex Str(string value) => new(PPrimary, c => c.Write(Quote(value)));

    /// <summary>A static call on a type. The type stays deferred.</summary>
    public static Ex On(ITypeDefinition type, string member) =>
        new(PPrimary, c => { c.Write(type); c.Write("."); c.Write(Ident(member)); });

    public static Ex Call(ITypeDefinition type, string method, params Ex[] args) =>
        new(PPrimary, c => { c.Write(type); c.Write("."); c.Write(Ident(method)); Args(c, args); });

    public static Ex TypeOf(ITypeDefinition type) =>
        new(PPrimary, c => { c.Write("typeof("); c.Write(type); c.Write(")"); });

    public static Ex NameOf(Ex target) =>
        new(PPrimary, c => { c.Write("nameof("); target.WriteOutput(c); c.Write(")"); });

    public static Ex New(ITypeDefinition type, params Ex[] args) =>
        new(PPrimary, c => { c.Write("new "); c.Write(type); Args(c, args); });

    public static Ex All(IReadOnlyList<Ex> parts)
    {
        if (parts.Count == 0) return True;
        var acc = parts[0];
        for (var i = 1; i < parts.Count; i++) acc &= parts[i];
        return acc;
    }

    /// <summary>Last resort. Still an Ex, so it composes; types stay deferred.</summary>
    public static Ex Raw(params object[] parts) =>
        new(PPrimary, c =>
        {
            foreach (var p in parts)
            {
                if (p is ITypeDefinition t) c.Write(t);
                else if (p is IOutputComponent o) o.WriteOutput(c);
                else c.Write(p?.ToString() ?? "");
            }
        });

    // --- internals --------------------------------------------------------
    private static Ex Bin(string op, int prec, Ex l, Ex r) =>
        new(prec, c => { Wrap(c, l, prec); c.Write(op); Wrap(c, r, prec); });

    private static void Wrap(IOutputContext c, Ex e, int outer)
    {
        if (e.Precedence < outer) { c.Write("("); e.WriteOutput(c); c.Write(")"); }
        else e.WriteOutput(c);
    }

    private static void Args(IOutputContext c, Ex[] args)
    {
        c.Write("(");
        for (var i = 0; i < args.Length; i++) { if (i > 0) c.Write(", "); args[i].WriteOutput(c); }
        c.Write(")");
    }

    private static readonly HashSet<string> Keywords = new()
    { "class","int","string","return","new","public","void","base","this","if","for","event","object","default","params","out","ref","in","is","as" };

    private static string Ident(string n) => Keywords.Contains(n) ? "@" + n : n;

    private static string Quote(string v)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in v)
            sb.Append(ch switch
            {
                '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n",
                '\r' => "\\r", '\t' => "\\t", '\0' => "\\0",
                _ => ch.ToString()
            });
        return sb.Append('"').ToString();
    }
}
