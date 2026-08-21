// The hand-written half of the importer. Everything the generator cannot derive from
// Syntax.xml lives here, never inside a .g.cs file (V2-HANDOFF.md 8.3).
#nullable enable
using System;
using System.Collections.Generic;
using CSharpAuthor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoundTrip;

/// <summary>How a Roslyn TypeSyntax is turned into an ITypeDefinition.</summary>
public enum TypeImportMode
{
    /// <summary>Decompose into the real type model (namespace, name, type arguments, array,
    /// nullable). A form the model cannot represent is a failure. This is the honest setting:
    /// it measures the type model as well as the emitter.</summary>
    Model,

    /// <summary>Fall back to carrying the type's source text verbatim when the model cannot
    /// represent it. Diagnostic only - it isolates emitter/grammar failures from type-model
    /// failures, and the difference between the two runs IS the type model's contribution.</summary>
    Verbatim,
}

/// <summary>One failure, classified. The three buckets are kept apart on purpose: an
/// importer that cannot build a tree and an emitter that produces unparseable text are
/// very different findings.</summary>
public enum Bucket
{
    /// <summary>(a) the importer could not build a tree for a node kind.</summary>
    Import,

    /// <summary>(b) a tree was built but the emitted text does not re-parse.</summary>
    Reparse,

    /// <summary>(c) the re-parsed tree differs structurally from the original.</summary>
    Structure,
}

public sealed class Failure
{
    public Bucket Bucket;
    public string Kind = "";      // the node kind (or diagnostic id for bucket b)
    public string Reason = "";
    public override string ToString() => $"{Bucket}/{Kind}: {Reason}";
}

public sealed class ImportReport
{
    public readonly List<Failure> Failures = new();
    private readonly HashSet<string> _seen = new();

    public void Add(Bucket bucket, string kind, string reason)
    {
        // One row per distinct (bucket, kind, reason) per file: a file with 400 using
        // directives must not out-vote a file with one, or the histogram measures file
        // shape instead of grammar coverage.
        var key = bucket + "" + kind + "" + reason;
        if (!_seen.Add(key)) return;
        Failures.Add(new Failure { Bucket = bucket, Kind = kind, Reason = reason });
    }

    public void Unsupported(string kind, string reason) => Add(Bucket.Import, kind, reason);

    public bool ImportFailed
    {
        get
        {
            foreach (var f in Failures)
                if (f.Bucket == Bucket.Import)
                    return true;
            return false;
        }
    }
}

public abstract class ImporterBase
{
    protected ImporterBase(ImportReport report, TypeImportMode typeMode)
    {
        Report = report;
        TypeMode = typeMode;
    }

    public ImportReport Report { get; }
    public TypeImportMode TypeMode { get; }

    /// <summary>Cast an imported child to the interface the field declares. A failure here
    /// is bucket (a) and it means the node layer's interface hierarchy cannot express a
    /// relationship the grammar requires.</summary>
    protected T? As<T>(object? value, SyntaxNode? source, string where) where T : class
    {
        if (value == null) return null;
        if (value is T t) return t;
        Report.Unsupported(
            source?.Kind().ToString() ?? "unknown",
            $"{where}: imported as {value.GetType().Name}, field needs {typeof(T).Name}");
        return null;
    }

    // -- TypeSyntax -> ITypeDefinition -----------------------------------------------
    // gen_all.py routes every scalar TypeSyntax/NameSyntax/SimpleNameSyntax/
    // IdentifierNameSyntax field to ITypeDefinition - that is the deferral point, the
    // crown jewel of 1. Inverting it means decomposing Roslyn's type syntax back into the
    // type model. Whatever the model cannot hold is a real gap, reported as such.
    protected ITypeDefinition? ImportType(TypeSyntax? type, string where)
    {
        if (type == null) return null;
        var result = TryImportType(type, out var reason);
        if (result != null) return result;

        if (TypeMode == TypeImportMode.Verbatim)
            return new VerbatimTypeDefinition(Flatten(type));

        Report.Unsupported(type.Kind().ToString(), $"{where}: {reason}");
        return null;
    }

    private ITypeDefinition? TryImportType(TypeSyntax type, out string reason)
    {
        reason = "";
        switch (type)
        {
            case PredefinedTypeSyntax p:
                return TypeDefinition.Get("", p.Keyword.Text);

            case IdentifierNameSyntax id:
                return TypeDefinition.Get("", id.Identifier.Text);

            case GenericNameSyntax g:
            {
                var args = new List<ITypeDefinition>();
                foreach (var a in g.TypeArgumentList.Arguments)
                {
                    var at = TryImportType(a, out reason);
                    if (at == null)
                    {
                        reason = "type argument: " + reason;
                        return null;
                    }
                    args.Add(at);
                }
                return new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, "", g.Identifier.Text, args);
            }

            case QualifiedNameSyntax q:
            {
                // The left-hand side becomes the namespace and the right-hand side the name -
                // exactly what the model can hold. Preserved as written, so a name qualified
                // in the source re-renders qualified in TypeOutputMode.FullName.
                if (!TryDottedPath(q.Left, out var ns))
                {
                    reason = "qualified name whose left side is not a plain dotted path";
                    return null;
                }
                switch (q.Right)
                {
                    case IdentifierNameSyntax rid:
                        return TypeDefinition.Get(ns, rid.Identifier.Text);
                    case GenericNameSyntax rg:
                    {
                        var args = new List<ITypeDefinition>();
                        foreach (var a in rg.TypeArgumentList.Arguments)
                        {
                            var at = TryImportType(a, out reason);
                            if (at == null)
                            {
                                reason = "type argument: " + reason;
                                return null;
                            }
                            args.Add(at);
                        }
                        return new GenericTypeDefinition(TypeDefinitionEnum.ClassDefinition, ns, rg.Identifier.Text, args);
                    }
                    default:
                        reason = "qualified name whose right side is " + q.Right.Kind();
                        return null;
                }
            }

            case NullableTypeSyntax nt:
            {
                var inner = TryImportType(nt.ElementType, out reason);
                if (inner == null) return null;
                return inner.MakeNullable();
            }

            case ArrayTypeSyntax at2:
            {
                // The model carries one bool, so it can express exactly one rank-1 [].
                // int[,] and int[][] cannot be held - a defect on 7's list, measured here.
                if (at2.RankSpecifiers.Count != 1)
                {
                    reason = $"array with {at2.RankSpecifiers.Count} rank specifiers - ITypeDefinition.IsArray is one bool";
                    return null;
                }
                var rank = at2.RankSpecifiers[0];
                if (rank.Rank != 1)
                {
                    reason = $"array of rank {rank.Rank} - ITypeDefinition.IsArray is one bool";
                    return null;
                }
                foreach (var s in rank.Sizes)
                {
                    if (s is not OmittedArraySizeExpressionSyntax)
                    {
                        reason = "array type with an explicit size";
                        return null;
                    }
                }
                var el = TryImportType(at2.ElementType, out reason);
                if (el == null) return null;
                if (el.IsArray)
                {
                    reason = "jagged array - MakeArray() on an array drops a rank";
                    return null;
                }
                return el.MakeArray();
            }

            default:
                reason = "no ITypeDefinition representation for " + type.Kind();
                return null;
        }
    }

    private static bool TryDottedPath(NameSyntax name, out string path)
    {
        switch (name)
        {
            case IdentifierNameSyntax id:
                path = id.Identifier.Text;
                return true;
            case QualifiedNameSyntax q when q.Right is IdentifierNameSyntax rid:
                if (TryDottedPath(q.Left, out var left))
                {
                    path = left + "." + rid.Identifier.Text;
                    return true;
                }
                break;
        }
        path = "";
        return false;
    }

    /// <summary>The node's text with all trivia collapsed to single spaces. Used only by
    /// TypeImportMode.Verbatim.</summary>
    protected static string Flatten(SyntaxNode node)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in node.DescendantTokens())
        {
            if (sb.Length > 0 && NeedsSpace(sb[sb.Length - 1], t.Text)) sb.Append(' ');
            sb.Append(t.Text);
        }
        return sb.ToString();
    }

    private static bool NeedsSpace(char previous, string next)
    {
        if (next.Length == 0) return false;
        var a = char.IsLetterOrDigit(previous) || previous == '_';
        var b = char.IsLetterOrDigit(next[0]) || next[0] == '_' || next[0] == '@';
        return a && b;
    }
}

/// <summary>An ITypeDefinition that carries pre-rendered text. Only ever constructed in
/// TypeImportMode.Verbatim, which is not the headline measurement.</summary>
public sealed class VerbatimTypeDefinition : ITypeDefinition
{
    private readonly string _text;
    public VerbatimTypeDefinition(string text) => _text = text;

    public TypeDefinitionEnum TypeDefinitionEnum => TypeDefinitionEnum.ClassDefinition;
    public bool IsNullable => false;
    public bool IsArray => false;
    public string Name => _text;
    public string Namespace => "";
    public IEnumerable<string> KnownNamespaces { get { yield break; } }
    public void WriteTypeName(System.Text.StringBuilder builder, TypeOutputMode mode = TypeOutputMode.ShortName)
        => builder.Append(_text);
    public ITypeDefinition MakeNullable(bool nullable = true) => new VerbatimTypeDefinition(_text + (nullable ? "?" : ""));
    public ITypeDefinition MakeArray() => new VerbatimTypeDefinition(_text + "[]");
    public IReadOnlyList<ITypeDefinition> TypeArguments => Array.Empty<ITypeDefinition>();
    public int CompareTo(ITypeDefinition other) => string.CompareOrdinal(_text, other?.Name);
}
