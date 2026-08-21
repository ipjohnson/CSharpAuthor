using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpAuthor;

namespace Deferred;

public enum BraceStyle { Allman, KAndR }

public sealed class StyleOptions
{
    public char IndentChar { get; set; } = ' ';
    public int IndentWidth { get; set; } = 4;
    public string NewLine { get; set; } = "\n";
    public BraceStyle Braces { get; set; } = BraceStyle.Allman;
    public TypeOutputMode TypeMode { get; set; } = TypeOutputMode.ShortName;
    /// <summary>Alias colliding short names instead of emitting an ambiguous reference.</summary>
    public bool AliasCollisions { get; set; } = true;
    /// <summary>Emit `namespace X;` rather than a braced block.</summary>
    public bool FileScopedNamespace { get; set; } = true;

    /// <summary>Drop usings the file's own namespace already covers.</summary>
    public string? ContainingNamespace { get; set; }
}

// ---- segments: nothing is committed to text until Output() ----
internal abstract class Seg { }
internal sealed class TextSeg : Seg { public string Text = ""; }
internal sealed class IndentSeg : Seg { public int Depth; }
internal sealed class NewLineSeg : Seg { }
internal sealed class ScopeOpenSeg : Seg { public int Depth; }
internal sealed class ScopeCloseSeg : Seg { public int Depth; }
/// <summary>A type reference held UNRENDERED. This is what makes aliasing possible.</summary>
internal sealed class TypeSeg : Seg { public ITypeDefinition Type = null!; }

/// <summary>
/// Implements V1's IOutputContext exactly, so every existing component writes into it
/// unmodified — but records segments instead of characters. Name resolution, collision
/// aliasing and style policy all happen at Output(), when the whole file is known.
/// </summary>
public sealed class DeferredOutputContext : IOutputContext
{
    private readonly List<Seg> _segs = new();
    private readonly HashSet<string> _explicitNamespaces = new();
    private readonly StyleOptions _style;
    private int _depth;

    public DeferredOutputContext(StyleOptions? style = null)
    {
        _style = style ?? new StyleOptions();
        Options = new OutputContextOptions
        {
            IndentChar = _style.IndentChar,
            IndentCharCount = _style.IndentWidth,
            NewLine = _style.NewLine,
            TypeOutputMode = _style.TypeMode,
        };
    }

    public OutputContextOptions Options { get; }
    public string SingleIndent => new(_style.IndentChar, _style.IndentWidth);
    public string IndentString => new(_style.IndentChar, _style.IndentWidth * _depth);

    public void IncrementIndent() => _depth++;
    public void DecrementIndent() => _depth--;

    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        // V1 components sometimes pass IndentString straight to Write(); treat pure
        // indentation as structural so the serializer can restyle it.
        if (text.Length > 0 && text.Trim().Length == 0 && text[0] == _style.IndentChar)
        {
            _segs.Add(new IndentSeg { Depth = text.Length / Math.Max(1, _style.IndentWidth) });
            return;
        }
        _segs.Add(new TextSeg { Text = text });
    }

    // THE deferral point: the type is stored, not rendered.
    public void Write(ITypeDefinition typeDefinition)
    {
        if (typeDefinition == null) return;
        _segs.Add(new TypeSeg { Type = typeDefinition });
    }

    public void WriteLine() => _segs.Add(new NewLineSeg());
    public void WriteLine(string text) { Write(text); _segs.Add(new NewLineSeg()); }
    public void WriteSpace() => _segs.Add(new TextSeg { Text = " " });

    public void WriteIndent(string text = "")
    {
        _segs.Add(new IndentSeg { Depth = _depth });
        if (!string.IsNullOrEmpty(text)) Write(text);
    }

    public void WriteIndentedLine(string text)
    {
        _segs.Add(new IndentSeg { Depth = _depth });
        Write(text);
        _segs.Add(new NewLineSeg());
    }

    public void OpenScope() { _segs.Add(new ScopeOpenSeg { Depth = _depth }); _depth++; }
    public void CloseScope() { _depth--; _segs.Add(new ScopeCloseSeg { Depth = _depth }); }

    public void AddImportNamespace(string ns) { if (!string.IsNullOrEmpty(ns)) _explicitNamespaces.Add(ns); }
    public void AddImportNamespace(ITypeDefinition t) { /* no-op: collected from TypeSegs, cannot drift */ }
    public void AddImportNamespaces(IEnumerable<string> ns) { foreach (var n in ns) AddImportNamespace(n); }
    public void AddImportNamespaces(IEnumerable<ITypeDefinition> t) { }

    private bool _emitUsings;
    public void GenerateUsingStatements() => _emitUsings = true;

    public char? LastCharacter
    {
        get
        {
            for (var i = _segs.Count - 1; i >= 0; i--)
            {
                switch (_segs[i])
                {
                    case TextSeg t when t.Text.Length > 0: return t.Text[t.Text.Length - 1];
                    case NewLineSeg: return '\n';
                    case ScopeCloseSeg: return '}';
                    case ScopeOpenSeg: return '{';
                    case TypeSeg ts: return LastCharOf(ts.Type);
                }
            }
            return null;
        }
    }

    private static char? LastCharOf(ITypeDefinition t)
    {
        var sb = new StringBuilder();
        t.WriteTypeName(sb);
        return sb.Length == 0 ? null : sb[sb.Length - 1];
    }

    // ------------------------------------------------------------------
    // Everything below runs AFTER the whole tree has been written, which is
    // the only reason aliasing, using-minimisation and restyling are possible.
    // ------------------------------------------------------------------
    public string Output()
    {
        var types = _segs.OfType<TypeSeg>().Select(s => s.Type).ToList();
        var plan = ResolveNames(types);
        var body = Serialize(plan);

        if (!_emitUsings) return body;

        var usings = BuildUsings(plan);
        return usings.Length == 0 ? body : usings + _style.NewLine + body;
    }

    /// <summary>Short name to use per type, plus any aliases the collisions forced.</summary>
    public sealed class NamePlan
    {
        public Dictionary<ITypeDefinition, string> Rendered = new();
        public SortedDictionary<string, string> Aliases = new();   // alias -> full name
        public SortedSet<string> Namespaces = new();
        public HashSet<string> AliasedAway = new();
    }

    private NamePlan ResolveNames(List<ITypeDefinition> types)
    {
        var plan = new NamePlan();

        if (_style.TypeMode != TypeOutputMode.ShortName)
        {
            foreach (var t in types.Distinct())
                plan.Rendered[t] = Render(t, _style.TypeMode, null);
            return plan;   // global::/FullName need no usings at all
        }

        // Group distinct types by the short name they all want.
        var byShort = new Dictionary<string, List<ITypeDefinition>>();
        foreach (var t in types.Distinct())
        {
            var shortName = BareName(t);
            if (!byShort.TryGetValue(shortName, out var list))
                byShort[shortName] = list = new List<ITypeDefinition>();
            if (!list.Any(x => string.Equals(x.Namespace, t.Namespace, StringComparison.Ordinal)))
                list.Add(t);
        }

        foreach (var kvp in byShort)
        {
            var contenders = kvp.Value;

            if (contenders.Count == 1)
            {
                var t = contenders[0];
                plan.Rendered[t] = Render(t, TypeOutputMode.ShortName, null);
                AddNs(plan, t);
                continue;
            }

            // Collision. First keeps the plain name; the rest get namespace-derived
            // aliases, the way KotlinPoet resolves the same situation.
            for (var i = 0; i < contenders.Count; i++)
            {
                var t = contenders[i];
                if (i == 0)
                {
                    plan.Rendered[t] = Render(t, TypeOutputMode.ShortName, null);
                    AddNs(plan, t);
                    continue;
                }

                var alias = MakeAlias(t, plan.Aliases.Keys);
                plan.Rendered[t] = Render(t, TypeOutputMode.ShortName, alias);
                plan.Aliases[alias] = t.Namespace + "." + BareName(t);
                // The alias replaces the import; a plain using would re-introduce
                // the very ambiguity the alias exists to remove.
                plan.AliasedAway.Add(t.Namespace);
                foreach (var arg in t.TypeArguments) AddNs(plan, arg);
            }
        }

        // Types reached only as generic arguments still need their namespaces.
        foreach (var t in types) AddNsDeep(plan, t);

        foreach (var ns in _explicitNamespaces) plan.Namespaces.Add(ns);
        foreach (var ns in plan.AliasedAway)
        {
            // ...unless some type in that namespace is still referenced unaliased.
            var stillPlain = plan.Rendered.Any(kv =>
                string.Equals(kv.Key.Namespace, ns, StringComparison.Ordinal) &&
                !plan.Aliases.Values.Contains(ns + "." + BareName(kv.Key)));
            if (!stillPlain) plan.Namespaces.Remove(ns);
        }
        plan.Namespaces.Remove("");
        if (_style.ContainingNamespace != null) plan.Namespaces.Remove(_style.ContainingNamespace);
        return plan;
    }

    private void AddNs(NamePlan plan, ITypeDefinition t)
    {
        if (!string.IsNullOrEmpty(t.Namespace)) plan.Namespaces.Add(t.Namespace);
    }

    private void AddNsDeep(NamePlan plan, ITypeDefinition t)
    {
        if (plan.Rendered.TryGetValue(t, out var r) && r.Contains("::")) return;
        AddNs(plan, t);
        foreach (var arg in t.TypeArguments) AddNsDeep(plan, arg);
    }

    private static string BareName(ITypeDefinition t)
    {
        var sb = new StringBuilder();
        t.WriteTypeName(sb);
        var s = sb.ToString();
        var lt = s.IndexOf('<');
        if (lt > 0) s = s.Substring(0, lt);
        return s.TrimEnd('?', '[', ']');
    }

    private static string MakeAlias(ITypeDefinition t, IEnumerable<string> taken)
    {
        var segs = t.Namespace.Split('.').Where(x => x.Length > 0).ToArray();
        var bare = BareName(t);
        for (var take = 1; take <= segs.Length; take++)
        {
            var candidate = string.Concat(segs.Skip(segs.Length - take)) + bare;
            if (!taken.Contains(candidate)) return candidate;
        }
        return bare + "Alias";
    }

    /// <summary>Renders a type, substituting an alias for its bare name when one applies.</summary>
    private string Render(ITypeDefinition t, TypeOutputMode mode, string? alias)
    {
        var sb = new StringBuilder();
        t.WriteTypeName(sb, mode);
        var s = sb.ToString();
        if (alias == null) return s;
        var bare = BareName(t);
        var idx = s.IndexOf(bare, StringComparison.Ordinal);
        return idx < 0 ? s : s.Substring(0, idx) + alias + s.Substring(idx + bare.Length);
    }

    private string BuildUsings(NamePlan plan)
    {
        var sb = new StringBuilder();
        foreach (var ns in plan.Namespaces) sb.Append("using ").Append(ns).Append(';').Append(_style.NewLine);
        foreach (var kvp in plan.Aliases)
            sb.Append("using ").Append(kvp.Key).Append(" = ").Append(kvp.Value).Append(';').Append(_style.NewLine);
        return sb.ToString();
    }

    private string Serialize(NamePlan plan)
    {
        var sb = new StringBuilder();
        var atLineStart = true;

        string Ind(int d) => new(_style.IndentChar, _style.IndentWidth * d);

        for (var i = 0; i < _segs.Count; i++)
        {
            switch (_segs[i])
            {
                case IndentSeg ind:
                    if (atLineStart) { sb.Append(Ind(ind.Depth)); atLineStart = false; }
                    break;

                case NewLineSeg:
                    sb.Append(_style.NewLine);
                    atLineStart = true;
                    break;

                case TextSeg t:
                    sb.Append(t.Text);
                    if (t.Text.Length > 0) atLineStart = false;
                    break;

                case TypeSeg ts:
                    sb.Append(plan.Rendered.TryGetValue(ts.Type, out var r) ? r : Render(ts.Type, _style.TypeMode, null));
                    atLineStart = false;
                    break;

                case ScopeOpenSeg so:
                    if (_style.Braces == BraceStyle.KAndR)
                    {
                        // Brace joins the previous line: strip the newline just emitted.
                        TrimTrailingNewLine(sb);
                        sb.Append(' ').Append('{').Append(_style.NewLine);
                    }
                    else
                    {
                        if (!atLineStart) sb.Append(_style.NewLine);
                        sb.Append(Ind(so.Depth)).Append('{').Append(_style.NewLine);
                    }
                    atLineStart = true;
                    break;

                case ScopeCloseSeg sc:
                    if (!atLineStart) sb.Append(_style.NewLine);
                    sb.Append(Ind(sc.Depth)).Append('}').Append(_style.NewLine);
                    atLineStart = true;
                    break;
            }
        }
        return sb.ToString();
    }

    private void TrimTrailingNewLine(StringBuilder sb)
    {
        var nl = _style.NewLine;
        if (sb.Length >= nl.Length)
        {
            var tail = sb.ToString(sb.Length - nl.Length, nl.Length);
            if (tail == nl) sb.Length -= nl.Length;
        }
        while (sb.Length > 0 && (sb[sb.Length - 1] == ' ' || sb[sb.Length - 1] == '\t')) sb.Length--;
    }
}
