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

    private int _indentIndex;
    private bool _generateUsings;

    public OutputContextOptions Options { get; }

    public OutputContext(OutputContextOptions? options = null)
    {
        Options = options ?? new OutputContextOptions();
    }

    public string SingleIndent => new string(Options.IndentChar, Options.IndentCharCount);

    public string IndentString => new string(Options.IndentChar, Options.IndentCharCount * _indentIndex);

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
        if (_indentIndex > 0 && Options.IndentCharCount > 0)
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
    /// Kept for callers written against version 1. Nothing in this library calls it: a type that is
    /// written declares its own namespace, and one that is not written does not need one.
    /// </summary>
    public void AddImportNamespace(ITypeDefinition typeDefinition)
    {
        if (typeDefinition == null)
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

        foreach (var ns in namePlan.Namespaces)
        {
            builder.Append("using ").Append(ns).Append(';').Append(Options.NewLine);

            wroteAny = true;
        }

        foreach (var alias in namePlan.Aliases)
        {
            builder.Append("using ").Append(alias.Key).Append(" = ").Append(alias.Value)
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
                    if (namePlan.HasAliases)
                    {
                        AppendAliasedName(builder, segment.Type!, namePlan);
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
    /// The short name each type is written with, and the aliases the collisions forced.
    /// </summary>
    private sealed class NamePlan
    {
        public readonly Dictionary<ITypeDefinition, string> AliasFor =
            new Dictionary<ITypeDefinition, string>();

        public readonly SortedDictionary<string, string> Aliases =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        public readonly SortedSet<string> Namespaces = new SortedSet<string>();

        public bool HasAliases => AliasFor.Count > 0;
    }

    private NamePlan BuildNamePlan()
    {
        var namePlan = new NamePlan();

        if (Options.TypeOutputMode == TypeOutputMode.ShortName)
        {
            var written = CollectWrittenTypes();

            ResolveCollisions(namePlan, written);

            CollectDerivedNamespaces(namePlan, written);
        }

        // A namespace asked for by name survives a qualifying mode, because a using is the only way
        // to reach an extension method and qualification cannot stand in for it.
        if (Options.EmitExplicitUsings)
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

    private void ResolveCollisions(NamePlan namePlan, List<ITypeDefinition> written)
    {
        if (!Options.AliasCollisions || written.Count < 2)
        {
            return;
        }

        // Group by the short name every one of them wants. Insertion order is kept so the alias a
        // file gets does not depend on how a dictionary happened to hash it.
        var order = new List<string>();
        var byShortName = new Dictionary<string, List<ITypeDefinition>>(StringComparer.Ordinal);

        foreach (var type in written)
        {
            var shortName = BareName(type);

            if (shortName.Length == 0)
            {
                continue;
            }

            if (!byShortName.TryGetValue(shortName, out var contenders))
            {
                byShortName[shortName] = contenders = new List<ITypeDefinition>();
                order.Add(shortName);
            }

            contenders.Add(type);
        }

        foreach (var shortName in order)
        {
            var contenders = byShortName[shortName];

            if (contenders.Count < 2)
            {
                continue;
            }

            // A type with no namespace - a keyword type, or a generic parameter - names itself and
            // cannot be aliased, so it always keeps the plain name.
            var plainNamespace = FirstPlainNamespace(contenders);
            var aliasByNamespace = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var type in contenders)
            {
                var ns = type.Namespace ?? "";

                if (ns.Length == 0 || string.Equals(ns, plainNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!aliasByNamespace.TryGetValue(ns, out var alias))
                {
                    alias = MakeAlias(ns, shortName, namePlan, byShortName);
                    aliasByNamespace[ns] = alias;
                    namePlan.Aliases[alias] = ns + "." + shortName;
                }

                namePlan.AliasFor[type] = alias;
            }
        }
    }

    private static string FirstPlainNamespace(List<ITypeDefinition> contenders)
    {
        foreach (var type in contenders)
        {
            if (string.IsNullOrEmpty(type.Namespace))
            {
                return "";
            }
        }

        return contenders[0].Namespace;
    }

    /// <summary>
    /// An alias built out of the namespace it disambiguates, taking as few segments as it takes to
    /// be unique - the way a reader would name it.
    /// </summary>
    private static string MakeAlias(
        string ns,
        string shortName,
        NamePlan namePlan,
        Dictionary<string, List<ITypeDefinition>> byShortName)
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

            if (!namePlan.Aliases.ContainsKey(candidate) && !byShortName.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        var fallback = shortName + "Alias";
        var suffix = 2;

        while (namePlan.Aliases.ContainsKey(fallback) || byShortName.ContainsKey(fallback))
        {
            fallback = shortName + "Alias" + suffix++;
        }

        return fallback;
    }

    private void CollectDerivedNamespaces(NamePlan namePlan, List<ITypeDefinition> written)
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

        if (!namePlan.HasAliases)
        {
            return;
        }

        // An aliased namespace loses its using: importing it would put back the ambiguity the alias
        // exists to remove. It keeps it if something else in it is still written by its plain name.
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
    /// Writes a type by short name, substituting an alias for any part of it that got one. Only
    /// reached when the file actually has a collision in it; otherwise the type writes itself.
    /// </summary>
    private static void AppendAliasedName(StringBuilder builder, ITypeDefinition type, NamePlan namePlan)
    {
        if (!NeedsAliasedName(type, namePlan))
        {
            type.WriteTypeName(builder, TypeOutputMode.ShortName);

            return;
        }

        builder.Append(namePlan.AliasFor.TryGetValue(type, out var alias) ? alias : BareName(type));

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

                AppendAliasedName(builder, typeArguments[i], namePlan);
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

    private static bool NeedsAliasedName(ITypeDefinition type, NamePlan namePlan)
    {
        if (namePlan.AliasFor.ContainsKey(type))
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
            if (NeedsAliasedName(typeArguments[i], namePlan))
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
