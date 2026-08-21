using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

public enum TypeOutputMode
{
    Global,
    FullName,
    ShortName,
}

public class OutputContextOptions
{
    public char IndentChar { get; set; } = ' ';

    public int IndentCharCount { get; set; } = 4;

    public string NewLine { get; set; } = "\n";

    public bool BreakInvokeLines { get; set; } = true;

    public bool GenerateDocumentation { get; set; } = true;

    public TypeOutputMode TypeOutputMode { get; set; } = TypeOutputMode.ShortName;

    /// <summary>
    /// Where the opening brace of a scope is written. Decided at serialization, so it restyles a
    /// file that was already written.
    /// </summary>
    public BraceStyle BraceStyle { get; set; } = BraceStyle.Allman;

    /// <summary>
    /// When two types written by short name have the same name and different namespaces, give the
    /// second one a <c>using X = Ns.X;</c> alias instead of emitting a reference that is ambiguous.
    /// </summary>
    /// <remarks>
    /// Without this the file emits both names bare and the compiler reports CS0104. The collision is
    /// only visible once the whole file has been written, which is why nothing is committed to text
    /// before then.
    /// </remarks>
    public bool AliasCollisions { get; set; } = true;

    /// <summary>
    /// Whether namespaces asked for by name - <see cref="BaseOutputComponent.AddUsingNamespace"/> and
    /// <see cref="IOutputContext.AddImportNamespace(string)"/> - are written in a mode that qualifies
    /// every type it writes.
    /// </summary>
    /// <remarks>
    /// A namespace asked for by name is not always replaceable by qualification: an extension method
    /// is found through a <c>using</c> and nothing else, so a file that calls one needs the directive
    /// even in <see cref="TypeOutputMode.Global"/>. Namespaces <em>derived</em> from the types written
    /// are a different thing and are never emitted in a qualifying mode - the qualification already
    /// says everything the directive would have.
    /// </remarks>
    public bool EmitExplicitUsings { get; set; } = true;

    /// <summary>
    /// The namespace the file itself declares. A using for it is redundant and is dropped.
    /// </summary>
    /// <remarks>
    /// Off unless set, because dropping a directive a caller was relying on is worse than leaving a
    /// redundant one in.
    /// </remarks>
    public string? ContainingNamespace { get; set; }
}

/// <summary>
/// Records what was written as segments - text, indent depth, scope markers and <em>unrendered</em>
/// type references - and turns them into text in <see cref="Output"/>.
/// </summary>
/// <remarks>
/// Two things follow from nothing becoming text until the end, and neither is possible otherwise.
///
/// The <c>using</c> directives are derived from the types that were actually written, so a type
/// cannot reach the output without its namespace reaching the header: a missing using is not a bug
/// that can happen. Nothing has to remember to declare a namespace, which is what the calls to
/// <c>AddImportNamespace</c> scattered through the writers used to be for - and those calls ran
/// whatever the output mode was, which is how a file that qualifies every name still ended up with
/// a stray <c>using</c> in it.
///
/// And a name is only chosen once the whole file is known, so two types with the same short name
/// can be told apart: the second gets an alias rather than an ambiguous reference.
/// </remarks>
public class OutputContext : IOutputContext
{
    private enum SegmentKind : byte
    {
        Text,
        NewLine,
        Indent,
        ScopeOpen,
        ScopeClose,
        TypeReference,
    }

    /// <summary>
    /// One recorded write. A struct so the whole file is one array rather than one object per write.
    /// </summary>
    private readonly struct Segment
    {
        public Segment(SegmentKind kind, string? text, int depth, ITypeDefinition? type)
        {
            Kind = kind;
            Text = text;
            Depth = depth;
            Type = type;
        }

        public readonly SegmentKind Kind;
        public readonly string? Text;
        public readonly int Depth;
        public readonly ITypeDefinition? Type;
    }

    private readonly List<Segment> _segments = new List<Segment>();

    /// <summary>Namespaces asked for by name. User intent, not derived from anything written.</summary>
    private readonly HashSet<string> _explicitNamespaces = new HashSet<string>();

    /// <summary>
    /// Namespaces asked for by handing over a type rather than writing it. Kept apart from the
    /// explicit ones because they are derived, and a mode that qualifies its types does not want them.
    /// </summary>
    private readonly HashSet<string> _typeNamespaces = new HashSet<string>();

    /// <summary>The namespaces the file itself declares. A using for one of them says nothing.</summary>
    private readonly HashSet<string> _declaredNamespaces = new HashSet<string>();

    private int _indentIndex;
    private bool _generateUsings;

    // The two indent strings, remembered rather than rebuilt. Both are read on every Write - that is
    // how a component that hands over its own indent is recognised - and both used to allocate a
    // string per read, which is a string per token written. The cache is keyed on everything they
    // are made of, so a caller that changes the indent style half way through still gets the new
    // one; only the depth normally moves, and only then is anything allocated.
    private string _indentStringCache = "";
    private string _singleIndentCache = "";
    private char _indentCacheChar;
    private int _indentCacheWidth = -1;
    private int _indentCacheDepth;

    public OutputContextOptions Options { get; }

    public OutputContext(OutputContextOptions? options = null)
    {
        Options = options ?? new OutputContextOptions();
    }

    public string SingleIndent
    {
        get
        {
            if (_indentCacheWidth != Options.IndentCharCount || _indentCacheChar != Options.IndentChar)
            {
                RebuildIndentCache();
            }

            return _singleIndentCache;
        }
    }

    public string IndentString
    {
        get
        {
            if (_indentCacheWidth != Options.IndentCharCount || _indentCacheChar != Options.IndentChar ||
                _indentCacheDepth != _indentIndex)
            {
                RebuildIndentCache();
            }

            return _indentStringCache;
        }
    }

    private void RebuildIndentCache()
    {
        // Built before anything is stored, so a bad depth throws exactly where it always did and
        // leaves no half-updated cache behind.
        var single = new string(Options.IndentChar, Options.IndentCharCount);
        var full = new string(Options.IndentChar, Options.IndentCharCount * _indentIndex);

        _singleIndentCache = single;
        _indentStringCache = full;
        _indentCacheChar = Options.IndentChar;
        _indentCacheWidth = Options.IndentCharCount;
        _indentCacheDepth = _indentIndex;
    }

    /// <summary>The current indent depth, in indents rather than characters.</summary>
    public int IndentDepth => _indentIndex;

    public void IncrementIndent()
    {
        _indentIndex++;
    }

    public void DecrementIndent()
    {
        _indentIndex--;
    }

    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // A component that hands over its own indent string is describing structure, not characters.
        // Recorded as an indent so the file can still be restyled after it has been written.
        // The first character is tested before the strings are: a token that does not begin with the
        // indent character cannot be an indent, and that is very nearly every token in the file.
        if (_indentIndex > 0 && Options.IndentCharCount > 0 && text[0] == Options.IndentChar)
        {
            if (text == IndentString)
            {
                _segments.Add(new Segment(SegmentKind.Indent, null, _indentIndex, null));

                return;
            }

            if (text == SingleIndent)
            {
                _segments.Add(new Segment(SegmentKind.Indent, null, 1, null));

                return;
            }
        }

        _segments.Add(new Segment(SegmentKind.Text, text, 0, null));
    }

    /// <summary>
    /// The point everything else depends on: the type is recorded, not rendered.
    /// </summary>
    public void Write(ITypeDefinition typeDefinition)
    {
        if (typeDefinition == null)
        {
            return;
        }

        _segments.Add(new Segment(SegmentKind.TypeReference, null, 0, typeDefinition));
    }

    public void WriteLine()
    {
        _segments.Add(new Segment(SegmentKind.NewLine, null, 0, null));
    }

    public void WriteLine(string text)
    {
        Write(text);

        _segments.Add(new Segment(SegmentKind.NewLine, null, 0, null));
    }

    public void WriteSpace()
    {
        _segments.Add(new Segment(SegmentKind.Text, " ", 0, null));
    }

    public void WriteIndent(string text = "")
    {
        _segments.Add(new Segment(SegmentKind.Indent, null, _indentIndex, null));

        Write(text);
    }

    public void WriteIndentedLine(string text)
    {
        _segments.Add(new Segment(SegmentKind.Indent, null, _indentIndex, null));

        Write(text);

        _segments.Add(new Segment(SegmentKind.NewLine, null, 0, null));
    }

    public void OpenScope()
    {
        _segments.Add(new Segment(SegmentKind.ScopeOpen, null, _indentIndex, null));

        _indentIndex++;
    }

    public void CloseScope()
    {
        _indentIndex--;

        _segments.Add(new Segment(SegmentKind.ScopeClose, null, _indentIndex, null));
    }

    public void AddImportNamespace(string ns)
    {
        if (string.IsNullOrEmpty(ns))
        {
            return;
        }

        _explicitNamespaces.Add(ns);
    }

    /// <summary>
    /// Kept for callers written against version 1, which is why it is on the class and not on
    /// <see cref="IOutputContext"/>. Nothing in this library calls it: a type that is written
    /// declares its own namespace, and one that is not written does not need one. A writer holding
    /// the interface cannot reach it, which is how invariant 1 is enforced rather than asked for.
    /// </summary>
    /// <remarks>
    /// It does nothing in a mode that qualifies every type it writes. A namespace derived from a
    /// type says nothing a <c>global::</c> name has not already said, and emitting it anyway is
    /// what let an unqualified name resolve in a file where nothing else was unqualified - the
    /// directive and the bare name each hid the other's absence.
    /// </remarks>
    public void AddImportNamespace(ITypeDefinition typeDefinition)
    {
        if (typeDefinition == null || Options.TypeOutputMode != TypeOutputMode.ShortName)
        {
            return;
        }

        foreach (var knownNamespace in typeDefinition.KnownNamespaces)
        {
            if (!string.IsNullOrEmpty(knownNamespace))
            {
                _typeNamespaces.Add(knownNamespace);
            }
        }
    }

    public void AddImportNamespaces(IEnumerable<string> namespaces)
    {
        if (namespaces == null)
        {
            return;
        }

        foreach (var ns in namespaces)
        {
            AddImportNamespace(ns);
        }
    }

    public void AddImportNamespaces(IEnumerable<ITypeDefinition> typeDefinitions)
    {
        if (typeDefinitions == null)
        {
            return;
        }

        foreach (var typeDefinition in typeDefinitions)
        {
            AddImportNamespace(typeDefinition);
        }
    }

    public void GenerateUsingStatements()
    {
        _generateUsings = true;
    }

    /// <summary>
    /// Says that the file declares this namespace, so a <c>using</c> for it is redundant.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="CSharpFileDefinition"/> with the namespace it is about to open. Anything
    /// declared inside that namespace is in scope without a directive, and one that names the file's
    /// own namespace back at it is noise. Only the outermost namespace counts: a nested one does not
    /// enclose its siblings, so dropping it could drop a directive something else in the file needs.
    /// </remarks>
    public void DeclareContainingNamespace(string ns)
    {
        if (!string.IsNullOrEmpty(ns))
        {
            _declaredNamespaces.Add(ns);
        }
    }

    public char? LastCharacter
    {
        get
        {
            for (var i = _segments.Count - 1; i >= 0; i--)
            {
                var segment = _segments[i];

                switch (segment.Kind)
                {
                    case SegmentKind.Text:
                        if (!string.IsNullOrEmpty(segment.Text))
                        {
                            return segment.Text![segment.Text!.Length - 1];
                        }

                        break;

                    case SegmentKind.NewLine:
                        if (Options.NewLine.Length > 0)
                        {
                            return Options.NewLine[Options.NewLine.Length - 1];
                        }

                        break;

                    case SegmentKind.Indent:
                        if (segment.Depth > 0 && Options.IndentCharCount > 0)
                        {
                            return Options.IndentChar;
                        }

                        break;

                    case SegmentKind.ScopeOpen:
                    case SegmentKind.ScopeClose:
                        // The scope marker writes its brace and then a line break.
                        if (Options.NewLine.Length > 0)
                        {
                            return Options.NewLine[Options.NewLine.Length - 1];
                        }

                        return segment.Kind == SegmentKind.ScopeOpen ? '{' : '}';

                    case SegmentKind.TypeReference:
                        var builder = new StringBuilder();

                        segment.Type!.WriteTypeName(builder, Options.TypeOutputMode);

                        if (builder.Length > 0)
                        {
                            return builder[builder.Length - 1];
                        }

                        break;
                }
            }

            return null;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Everything below runs after the whole file has been written. That is the only reason the
    // using list can be derived rather than declared, and the only reason a collision can be seen.
    // -----------------------------------------------------------------------------------------

    public string Output()
    {
        var namePlan = BuildNamePlan();

        var builder = new StringBuilder();

        if (_generateUsings)
        {
            WriteUsings(builder, namePlan);
        }

        Serialize(builder, namePlan);

        return builder.ToString();
    }

    private void WriteUsings(StringBuilder builder, NamePlan namePlan)
    {
        var wroteAny = false;

        // A namespace segment that is a keyword needs the same @ the declaration gets. The two used
        // to disagree: `namespace Company.@event.Models` was written correctly and
        // `using Company.event.Models;` was not, which is CS1001 in a file that otherwise compiles.
        foreach (var ns in namePlan.Namespaces)
        {
            builder.Append("using ").Append(CSharpIdentifier.EscapeQualified(ns))
                .Append(';').Append(Options.NewLine);

            wroteAny = true;
        }

        foreach (var alias in namePlan.Aliases)
        {
            builder.Append("using ").Append(CSharpIdentifier.Escape(alias.Key))
                .Append(" = ").Append(CSharpIdentifier.EscapeQualified(alias.Value))
                .Append(';').Append(Options.NewLine);

            wroteAny = true;
        }

        if (wroteAny)
        {
            builder.Append(Options.NewLine);
        }
    }

    private void Serialize(StringBuilder builder, NamePlan namePlan)
    {
        var indentChar = Options.IndentChar;
        var indentWidth = Options.IndentCharCount;
        var newLine = Options.NewLine;
        var kAndR = Options.BraceStyle == BraceStyle.KAndR;

        for (var i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];

            switch (segment.Kind)
            {
                case SegmentKind.Text:
                    builder.Append(segment.Text);
                    break;

                case SegmentKind.NewLine:
                    builder.Append(newLine);
                    break;

                case SegmentKind.Indent:
                    if (segment.Depth > 0)
                    {
                        builder.Append(indentChar, indentWidth * segment.Depth);
                    }

                    break;

                case SegmentKind.ScopeOpen:
                    if (kAndR)
                    {
                        TrimLineEnd(builder);
                        builder.Append(' ').Append('{').Append(newLine);
                    }
                    else
                    {
                        builder.Append(indentChar, indentWidth * segment.Depth);
                        builder.Append('{').Append(newLine);
                    }

                    break;

                case SegmentKind.ScopeClose:
                    builder.Append(indentChar, indentWidth * segment.Depth);
                    builder.Append('}').Append(newLine);
                    break;

                case SegmentKind.TypeReference:
                    if (namePlan.HasRenames)
                    {
                        AppendPlannedName(builder, segment.Type!, namePlan);
                    }
                    else
                    {
                        segment.Type!.WriteTypeName(builder, Options.TypeOutputMode);
                    }

                    break;
            }
        }
    }

    /// <summary>Removes the line break the previous line ended with, so a brace can join it.</summary>
    private void TrimLineEnd(StringBuilder builder)
    {
        var newLine = Options.NewLine;

        if (newLine.Length > 0 && builder.Length >= newLine.Length)
        {
            var matches = true;

            for (var i = 0; i < newLine.Length; i++)
            {
                if (builder[builder.Length - newLine.Length + i] != newLine[i])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                builder.Length -= newLine.Length;
            }
        }

        while (builder.Length > 0 && (builder[builder.Length - 1] == ' ' || builder[builder.Length - 1] == '\t'))
        {
            builder.Length--;
        }
    }

    /// <summary>
    /// What each type is written as, once the whole file is known: the aliases the collisions
    /// forced, and the types that had to be qualified because no alias could express them.
    /// </summary>
    private sealed class NamePlan
    {
        /// <summary>Types written as an alias rather than as their own short name.</summary>
        public readonly Dictionary<ITypeDefinition, string> AliasFor =
            new Dictionary<ITypeDefinition, string>();

        /// <summary>Types written with their namespace in front, because an alias cannot name them.</summary>
        public readonly HashSet<ITypeDefinition> Qualified = new HashSet<ITypeDefinition>();

        public readonly SortedDictionary<string, string> Aliases =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        // Ordinal, so the file a generator writes does not depend on the culture it ran under.
        public readonly SortedSet<string> Namespaces = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Whether any type is written as something other than what it writes itself as.</summary>
        public bool HasRenames => AliasFor.Count > 0 || Qualified.Count > 0;
    }

    /// <summary>One short name several types want, and the namespaces wanting it.</summary>
    private sealed class NameGroup
    {
        public string ShortName = "";
        public bool IsGeneric;
        public readonly List<string> Namespaces = new List<string>();
        public readonly List<ITypeDefinition> Types = new List<ITypeDefinition>();

        /// <summary>The namespace that keeps the plain name, or null once nothing does.</summary>
        public string? Winner;
    }

    private NamePlan BuildNamePlan()
    {
        var namePlan = new NamePlan();

        if (Options.TypeOutputMode == TypeOutputMode.ShortName)
        {
            var written = CollectWrittenTypes();

            CollectDerivedNamespaces(namePlan);

            var groups = GroupByShortName(written);

            ResolveCollisions(namePlan, groups);

            DropAliasedNamespaces(namePlan, written);

            // A namespace that had to stay - something else in it is still written plainly - puts
            // the ambiguity back. The name that was left plain then has to be aliased as well.
            if (AliasTheWinners(namePlan, groups))
            {
                DropAliasedNamespaces(namePlan, written);
            }
        }

        // A namespace asked for by name survives a qualifying mode, because a using is the only way
        // to reach an extension method and qualification cannot stand in for it. The option turns
        // that off for a file that must carry none; it has nothing to say about short-name mode,
        // where the directive is how the name resolves at all.
        if (Options.EmitExplicitUsings || Options.TypeOutputMode == TypeOutputMode.ShortName)
        {
            foreach (var ns in _explicitNamespaces)
            {
                namePlan.Namespaces.Add(ns);
            }
        }

        namePlan.Namespaces.Remove("");

        if (!string.IsNullOrEmpty(Options.ContainingNamespace))
        {
            namePlan.Namespaces.Remove(Options.ContainingNamespace!);
        }

        foreach (var ns in _declaredNamespaces)
        {
            namePlan.Namespaces.Remove(ns);
        }

        return namePlan;
    }

    /// <summary>Every type written, plus every type reached as an argument of one, in write order.</summary>
    private List<ITypeDefinition> CollectWrittenTypes()
    {
        var written = new List<ITypeDefinition>();
        var seen = new HashSet<ITypeDefinition>();

        for (var i = 0; i < _segments.Count; i++)
        {
            if (_segments[i].Kind == SegmentKind.TypeReference)
            {
                CollectType(_segments[i].Type!, written, seen);
            }
        }

        return written;
    }

    private static void CollectType(ITypeDefinition type, List<ITypeDefinition> written, HashSet<ITypeDefinition> seen)
    {
        if (type == null || !seen.Add(type))
        {
            return;
        }

        written.Add(type);

        var typeArguments = type.TypeArguments;

        if (typeArguments == null)
        {
            return;
        }

        for (var i = 0; i < typeArguments.Count; i++)
        {
            CollectType(typeArguments[i], written, seen);
        }
    }

    /// <summary>
    /// The namespaces the file needs, taken from the types it actually wrote. Nothing declares
    /// them, so nothing can forget to.
    /// </summary>
    private void CollectDerivedNamespaces(NamePlan namePlan)
    {
        for (var i = 0; i < _segments.Count; i++)
        {
            if (_segments[i].Kind != SegmentKind.TypeReference)
            {
                continue;
            }

            foreach (var knownNamespace in _segments[i].Type!.KnownNamespaces)
            {
                if (!string.IsNullOrEmpty(knownNamespace))
                {
                    namePlan.Namespaces.Add(knownNamespace);
                }
            }
        }

        foreach (var ns in _typeNamespaces)
        {
            namePlan.Namespaces.Add(ns);
        }
    }

    /// <summary>
    /// Groups the types by the name they compete for. Arity is part of it, because two types of the
    /// same name and different arity do not compete: <c>Box</c> and <c>Box&lt;T&gt;</c> resolve
    /// separately.
    /// </summary>
    private List<NameGroup> GroupByShortName(List<ITypeDefinition> written)
    {
        var groups = new List<NameGroup>();

        if (!Options.AliasCollisions || written.Count < 2)
        {
            return groups;
        }

        var byKey = new Dictionary<string, NameGroup>(StringComparer.Ordinal);

        foreach (var type in written)
        {
            var shortName = BareName(type);

            if (shortName.Length == 0)
            {
                continue;
            }

            var argumentCount = type.TypeArguments?.Count ?? 0;
            var key = shortName + "`" + argumentCount;

            if (!byKey.TryGetValue(key, out var group))
            {
                byKey[key] = group = new NameGroup { ShortName = shortName, IsGeneric = argumentCount > 0 };
                groups.Add(group);
            }

            group.Types.Add(type);

            var ns = type.Namespace ?? "";

            if (!group.Namespaces.Contains(ns))
            {
                group.Namespaces.Add(ns);
            }
        }

        groups.RemoveAll(group => group.Namespaces.Count < 2);

        return groups;
    }

    private void ResolveCollisions(NamePlan namePlan, List<NameGroup> groups)
    {
        foreach (var group in groups)
        {
            // A generic cannot be aliased: a using alias names a closed type, and closing it here
            // would name the wrong thing everywhere else. Every contender is qualified instead.
            if (group.IsGeneric)
            {
                group.Winner = null;

                foreach (var type in group.Types)
                {
                    if (!string.IsNullOrEmpty(type.Namespace))
                    {
                        namePlan.Qualified.Add(type);
                    }
                }

                continue;
            }

            // A type with no namespace names itself - a keyword type, or a generic parameter, which
            // is in scope by declaration and wins over anything a using brings in. It cannot be
            // aliased and does not need to be.
            group.Winner = group.Namespaces.Contains("") ? "" : group.Namespaces[0];

            foreach (var type in group.Types)
            {
                var ns = type.Namespace ?? "";

                if (ns.Length == 0 || string.Equals(ns, group.Winner, StringComparison.Ordinal))
                {
                    continue;
                }

                namePlan.AliasFor[type] = AliasFor(namePlan, group, type, ns);
            }
        }
    }

    /// <summary>
    /// Aliases the name that was left plain, for a group whose other namespaces are still imported.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    private bool AliasTheWinners(NamePlan namePlan, List<NameGroup> groups)
    {
        var changed = false;

        foreach (var group in groups)
        {
            if (string.IsNullOrEmpty(group.Winner))
            {
                continue;
            }

            var stillAmbiguous = false;

            foreach (var ns in group.Namespaces)
            {
                if (!string.Equals(ns, group.Winner, StringComparison.Ordinal) &&
                    namePlan.Namespaces.Contains(ns))
                {
                    stillAmbiguous = true;
                    break;
                }
            }

            if (!stillAmbiguous)
            {
                continue;
            }

            foreach (var type in group.Types)
            {
                if (string.Equals(type.Namespace ?? "", group.Winner, StringComparison.Ordinal))
                {
                    namePlan.AliasFor[type] = AliasFor(namePlan, group, type, group.Winner!);

                    changed = true;
                }
            }

            group.Winner = null;
        }

        return changed;
    }

    private static string AliasFor(NamePlan namePlan, NameGroup group, ITypeDefinition type, string ns)
    {
        var target = ns + "." + type.Name;

        foreach (var existing in namePlan.Aliases)
        {
            if (string.Equals(existing.Value, target, StringComparison.Ordinal))
            {
                return existing.Key;
            }
        }

        var alias = MakeAlias(ns, group.ShortName, namePlan);

        namePlan.Aliases[alias] = target;

        return alias;
    }

    /// <summary>
    /// An alias built out of the namespace it disambiguates, taking as few segments as it takes to
    /// be unique - the way a reader would name it.
    /// </summary>
    private static string MakeAlias(string ns, string shortName, NamePlan namePlan)
    {
        var segments = ns.Split('.');

        for (var take = 1; take <= segments.Length; take++)
        {
            var builder = new StringBuilder();

            for (var i = segments.Length - take; i < segments.Length; i++)
            {
                builder.Append(segments[i]);
            }

            builder.Append(shortName);

            var candidate = builder.ToString();

            if (!namePlan.Aliases.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        var fallback = shortName + "Alias";
        var suffix = 2;

        while (namePlan.Aliases.ContainsKey(fallback))
        {
            fallback = shortName + "Alias" + suffix++;
        }

        return fallback;
    }

    /// <summary>
    /// Drops the using for a namespace every reference to which now goes through an alias.
    /// Importing it would put back the ambiguity the alias exists to remove.
    /// </summary>
    private void DropAliasedNamespaces(NamePlan namePlan, List<ITypeDefinition> written)
    {
        if (namePlan.AliasFor.Count == 0)
        {
            return;
        }

        var aliasedAway = new HashSet<string>(StringComparer.Ordinal);
        var stillPlain = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in written)
        {
            var ns = type.Namespace;

            if (string.IsNullOrEmpty(ns))
            {
                continue;
            }

            if (namePlan.AliasFor.ContainsKey(type))
            {
                aliasedAway.Add(ns);
            }
            else
            {
                stillPlain.Add(ns);
            }
        }

        foreach (var ns in aliasedAway)
        {
            if (!stillPlain.Contains(ns) && !_explicitNamespaces.Contains(ns) && !_typeNamespaces.Contains(ns))
            {
                namePlan.Namespaces.Remove(ns);
            }
        }
    }

    /// <summary>
    /// Writes a type by short name, substituting whatever the plan decided for any part of it that
    /// the plan touched. Only reached when the file has a collision in it; otherwise every type
    /// writes itself, exactly as it always did.
    /// </summary>
    private static void AppendPlannedName(StringBuilder builder, ITypeDefinition type, NamePlan namePlan)
    {
        if (!NeedsPlannedName(type, namePlan))
        {
            type.WriteTypeName(builder, TypeOutputMode.ShortName);

            return;
        }

        if (namePlan.AliasFor.TryGetValue(type, out var alias))
        {
            builder.Append(alias);
        }
        else
        {
            if (namePlan.Qualified.Contains(type) && !string.IsNullOrEmpty(type.Namespace))
            {
                builder.Append(type.Namespace).Append('.');
            }

            builder.Append(BareName(type));
        }

        var typeArguments = type.TypeArguments;

        if (typeArguments != null && typeArguments.Count > 0)
        {
            builder.Append('<');

            for (var i = 0; i < typeArguments.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                AppendPlannedName(builder, typeArguments[i], namePlan);
            }

            builder.Append('>');
        }

        if (type.IsArray)
        {
            builder.Append("[]");
        }

        if (type.IsNullable)
        {
            builder.Append('?');
        }
    }

    private static bool NeedsPlannedName(ITypeDefinition type, NamePlan namePlan)
    {
        if (namePlan.AliasFor.ContainsKey(type) || namePlan.Qualified.Contains(type))
        {
            return true;
        }

        var typeArguments = type.TypeArguments;

        if (typeArguments == null)
        {
            return false;
        }

        for (var i = 0; i < typeArguments.Count; i++)
        {
            if (NeedsPlannedName(typeArguments[i], namePlan))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The name a type wants for itself, with its arguments and its array and null marks off.</summary>
    private static string BareName(ITypeDefinition type)
    {
        var builder = new StringBuilder();

        type.WriteTypeName(builder, TypeOutputMode.ShortName);

        var name = builder.ToString();

        var index = name.IndexOf('<');

        if (index > 0)
        {
            name = name.Substring(0, index);
        }

        return name.TrimEnd('?', '[', ']');
    }
}
