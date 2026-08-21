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
    // -----------------------------------------------------------------------------------------
    // The recorded file, in two stores.
    //
    // A code per write - three bits saying which kind it is and, for the kinds that have one, the
    // indent depth - and, in a store of its own, the value the two kinds that refer to something
    // refer to: the string that was written, or the type that has not been rendered yet.
    //
    // One array of segment structs is the obvious shape and it is the expensive one. A struct
    // holding a reference is sixteen bytes whatever else is in it, because the reference forces
    // eight-byte alignment - so a line break, which needs nothing, costs as much as a string does.
    // Four bytes for every write plus eight for the two writes in three that name something is a
    // little over half of that, measured on this library's own payload: 15.7 KB down to 9.7 KB.
    //
    // Both stores grow by adding a chunk rather than by copying into a bigger array, because a
    // List<T> that doubles allocates about twice what it ends up holding and throws half of it
    // away.
    // -----------------------------------------------------------------------------------------

    /// <summary>The kinds of write that are recorded. Three bits of a code; do not exceed eight.</summary>
    private enum SegmentKind
    {
        Text,
        NewLine,
        Indent,
        ScopeOpen,
        ScopeClose,
        TypeReference,
    }

    private const int KindBits = 3;
    private const int KindMask = (1 << KindBits) - 1;

    /// <summary>
    /// A list that grows by adding a chunk rather than by copying everything it holds into a bigger
    /// array, so recording an N-entry file allocates N rather than about 2N.
    /// </summary>
    /// <remarks>
    /// A struct, and held in a field rather than handed around, so that a context that is never
    /// written to - and this library makes one per rendered expression - allocates nothing at all
    /// for the two of them.
    /// </remarks>
    private struct ChunkChain<T>
    {
        /// <summary>Small, because most contexts record a handful of entries and are then thrown away.</summary>
        private const int FirstCapacity = 32;

        /// <summary>
        /// 8192 entries is 64 KB of references or 32 KB of codes - either way the largest chunk that
        /// stays off the large object heap.
        /// </summary>
        private const int MaxCapacity = 8192;

        private T[]? _chunk;
        private List<T[]>? _filled;
        private int _used;

        public int Count;

        /// <summary>How many chunks hold entries; only the last of them is partly filled.</summary>
        public int ChunkCount => Count == 0 ? 0 : (_filled?.Count ?? 0) + 1;

        public void Add(T value)
        {
            var chunk = _chunk;
            var index = _used;

            if (chunk == null || index == chunk.Length)
            {
                chunk = Grow();
                index = 0;
            }

            chunk[index] = value;
            _used = index + 1;
            Count++;
        }

        /// <summary>The chunk at <paramref name="chunkIndex"/> and how many of its slots are in use.</summary>
        public T[] ChunkAt(int chunkIndex, out int used)
        {
            var filled = _filled;

            if (filled != null && chunkIndex < filled.Count)
            {
                var chunk = filled[chunkIndex];

                used = chunk.Length;

                return chunk;
            }

            used = _used;

            return _chunk!;
        }

        private T[] Grow()
        {
            var current = _chunk;

            if (current == null)
            {
                return _chunk = new T[FirstCapacity];
            }

            (_filled ??= new List<T[]>()).Add(current);

            _used = 0;

            // A quarter more each time rather than twice as much. Doubling overshoots: the chunk that
            // takes the store past what it needs is as big as everything before it, so a file that
            // ends a little over a boundary holds most of a chunk it never fills. Nothing here is
            // copied on growth, so the only thing more chunks cost is a list entry apiece.
            var next = current.Length + Math.Max(current.Length >> 2, 8);

            return _chunk = new T[next < MaxCapacity ? next : MaxCapacity];
        }
    }

    private ChunkChain<int> _codes;
    private ChunkChain<object> _values;

    // A running tally of how long the file will be, kept apart from the things whose width is only
    // decided at serialization - line breaks, indents and braces are counted, not measured, because
    // the newline string, the indent width and the brace style can all still change. It buys the
    // output builder one allocation of the right size instead of the ten doublings a default
    // StringBuilder does.
    private int _textChars;
    private int _typeNameChars;
    private int _typeQualifierChars;
    private int _typeRefCount;
    private int _lineCount;
    private int _indentUnits;
    private int _braceCount;

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
        // The first character is tested before the strings are, and before the indent width is even
        // read: a token that does not begin with the indent character cannot be an indent, and that
        // is very nearly every token in the file, so very nearly every write asks Options one
        // question rather than two.
        if (_indentIndex > 0 && text[0] == Options.IndentChar && Options.IndentCharCount > 0)
        {
            if (text == IndentString)
            {
                RecordIndent(_indentIndex);

                return;
            }

            if (text == SingleIndent)
            {
                RecordIndent(1);

                return;
            }
        }

        _textChars += text.Length;

        RecordValue(SegmentKind.Text, text);
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

        // What the name will be is not known yet - that is the point of the whole design - so the
        // parts it could be built from are counted separately and the output mode decides, at
        // serialization, which of them to believe.
        _typeRefCount++;
        _typeNameChars += NameAllowance(typeDefinition);
        _typeQualifierChars += typeDefinition.Namespace.Length + 1;

        RecordValue(SegmentKind.TypeReference, typeDefinition);
    }

    public void WriteLine()
    {
        RecordNewLine();
    }

    public void WriteLine(string text)
    {
        Write(text);

        RecordNewLine();
    }

    public void WriteSpace()
    {
        _textChars++;

        RecordValue(SegmentKind.Text, " ");
    }

    public void WriteIndent(string text = "")
    {
        RecordIndent(_indentIndex);

        Write(text);
    }

    public void WriteIndentedLine(string text)
    {
        RecordIndent(_indentIndex);

        Write(text);

        RecordNewLine();
    }

    public void OpenScope()
    {
        RecordScope(SegmentKind.ScopeOpen, _indentIndex);

        _indentIndex++;
    }

    public void CloseScope()
    {
        _indentIndex--;

        RecordScope(SegmentKind.ScopeClose, _indentIndex);
    }

    private void RecordIndent(int depth)
    {
        _indentUnits += depth;

        _codes.Add(Code(SegmentKind.Indent, depth));
    }

    private void RecordNewLine()
    {
        _lineCount++;

        _codes.Add(Code(SegmentKind.NewLine, 0));
    }

    private void RecordScope(SegmentKind kind, int depth)
    {
        _indentUnits += depth;
        _braceCount++;
        _lineCount++;

        _codes.Add(Code(kind, depth));
    }

    /// <summary>
    /// Records a write that names something. The value goes in the value store and the code says
    /// only what kind it is: the two stores advance together, so a walk over the codes knows which
    /// value each one meant without either of them saying so.
    /// </summary>
    private void RecordValue(SegmentKind kind, object value)
    {
        _codes.Add(Code(kind, 0));
        _values.Add(value);
    }

    private static int Code(SegmentKind kind, int depth)
    {
        return (int)kind | (depth << KindBits);
    }

    private static SegmentKind KindOf(int code)
    {
        return (SegmentKind)(code & KindMask);
    }

    /// <summary>The indent depth a code carries. An arithmetic shift, so an unbalanced negative depth survives.</summary>
    private static int DepthOf(int code)
    {
        return code >> KindBits;
    }

    /// <summary>
    /// Room for a type's own name, its arguments included, without its namespace.
    /// </summary>
    private static int NameAllowance(ITypeDefinition type)
    {
        // 4 covers the angle brackets, a `?` and a `[]`; the arguments are counted because they are
        // written inside this reference rather than recorded as ones of their own.
        var allowance = type.Name.Length + 4;

        var typeArguments = type.TypeArguments;

        if (typeArguments != null)
        {
            for (var i = 0; i < typeArguments.Count; i++)
            {
                allowance += NameAllowance(typeArguments[i]) + 1;
            }
        }

        return allowance;
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
            // Backwards over the codes, and backwards over the values in step with them: a code that
            // names something took the value before the one the code after it took.
            var valueChunkIndex = _values.ChunkCount - 1;
            var valueChunk = Array.Empty<object>();
            var valueIndex = 0;

            if (valueChunkIndex >= 0)
            {
                valueChunk = _values.ChunkAt(valueChunkIndex, out valueIndex);
            }

            for (var chunkIndex = _codes.ChunkCount - 1; chunkIndex >= 0; chunkIndex--)
            {
                var chunk = _codes.ChunkAt(chunkIndex, out var used);

                for (var i = used - 1; i >= 0; i--)
                {
                    var code = chunk[i];

                    switch (KindOf(code))
                    {
                        case SegmentKind.Text:
                            if (valueIndex == 0)
                            {
                                valueChunk = _values.ChunkAt(--valueChunkIndex, out valueIndex);
                            }

                            var text = (string)valueChunk[--valueIndex];

                            if (text.Length > 0)
                            {
                                return text[text.Length - 1];
                            }

                            break;

                        case SegmentKind.NewLine:
                            if (Options.NewLine.Length > 0)
                            {
                                return Options.NewLine[Options.NewLine.Length - 1];
                            }

                            break;

                        case SegmentKind.Indent:
                            if (DepthOf(code) > 0 && Options.IndentCharCount > 0)
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

                            return KindOf(code) == SegmentKind.ScopeOpen ? '{' : '}';

                        case SegmentKind.TypeReference:
                            if (valueIndex == 0)
                            {
                                valueChunk = _values.ChunkAt(--valueChunkIndex, out valueIndex);
                            }

                            var builder = new StringBuilder();

                            ((ITypeDefinition)valueChunk[--valueIndex]).WriteTypeName(builder, Options.TypeOutputMode);

                            if (builder.Length > 0)
                            {
                                return builder[builder.Length - 1];
                            }

                            break;
                    }
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

        var builder = new StringBuilder(EstimateOutputLength(namePlan));

        if (_generateUsings)
        {
            WriteUsings(builder, namePlan);
        }

        Serialize(builder, namePlan);

        return builder.ToString();
    }

    /// <summary>
    /// Roughly how long the file will be, so the output builder is allocated once at the right size.
    /// </summary>
    /// <remarks>
    /// A StringBuilder given no capacity starts at 16 characters and reaches a few thousand by
    /// allocating a chunk per doubling, and the chunk it allocates is as long as everything written
    /// so far - so it ends up holding about twice the file in memory it will never use. On this
    /// library's own benchmark payload that was 16 KB of chunks for an 8.7 KB file. Nothing here has
    /// to be exact: an estimate that is short only costs the growth it would have cost anyway.
    /// </remarks>
    private int EstimateOutputLength(NamePlan namePlan)
    {
        var estimate = _textChars + _typeNameChars
            + _lineCount * Options.NewLine.Length
            + _indentUnits * Options.IndentCharCount
            + _braceCount;

        // A short name carries no namespace; the other two modes write one in front of every
        // reference, and Global writes `global::` as well.
        if (Options.TypeOutputMode != TypeOutputMode.ShortName)
        {
            estimate += _typeQualifierChars;
        }

        if (Options.TypeOutputMode == TypeOutputMode.Global)
        {
            estimate += _typeRefCount * 8;
        }

        if (_generateUsings)
        {
            estimate += namePlan.UsingsLength(Options.NewLine.Length);
        }

        // A sixteenth of headroom. An estimate that is short costs a growth, and a growth allocates
        // as much again as everything written so far, so it is worth a little slack - but only a
        // little, because slack is memory the file never uses.
        return estimate + (estimate >> 4) + 16;
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
        var typeOutputMode = Options.TypeOutputMode;
        var hasRenames = namePlan.HasRenames;

        // The value store is walked in step with the code store rather than indexed into: the two
        // were filled together, so the next value is always the one the next naming code meant.
        var valueChunkIndex = -1;
        var valueChunk = Array.Empty<object>();
        var valueUsed = 0;
        var valueIndex = 0;

        for (var chunkIndex = 0; chunkIndex < _codes.ChunkCount; chunkIndex++)
        {
            var chunk = _codes.ChunkAt(chunkIndex, out var used);

            for (var i = 0; i < used; i++)
            {
                var code = chunk[i];

                switch (KindOf(code))
                {
                    case SegmentKind.Text:
                        if (valueIndex == valueUsed)
                        {
                            valueChunk = _values.ChunkAt(++valueChunkIndex, out valueUsed);
                            valueIndex = 0;
                        }

                        builder.Append((string)valueChunk[valueIndex++]);
                        break;

                    case SegmentKind.NewLine:
                        builder.Append(newLine);
                        break;

                    case SegmentKind.Indent:
                        var depth = DepthOf(code);

                        if (depth > 0)
                        {
                            builder.Append(indentChar, indentWidth * depth);
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
                            builder.Append(indentChar, indentWidth * DepthOf(code));
                            builder.Append('{').Append(newLine);
                        }

                        break;

                    case SegmentKind.ScopeClose:
                        builder.Append(indentChar, indentWidth * DepthOf(code));
                        builder.Append('}').Append(newLine);
                        break;

                    case SegmentKind.TypeReference:
                        if (valueIndex == valueUsed)
                        {
                            valueChunk = _values.ChunkAt(++valueChunkIndex, out valueUsed);
                            valueIndex = 0;
                        }

                        var type = (ITypeDefinition)valueChunk[valueIndex++];

                        if (hasRenames)
                        {
                            AppendPlannedName(builder, type, namePlan);
                        }
                        else
                        {
                            type.WriteTypeName(builder, typeOutputMode);
                        }

                        break;
                }
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

        /// <summary>How many characters the directives at the top of the file will take.</summary>
        public int UsingsLength(int newLineLength)
        {
            // "using " and ";" around a namespace; " = " as well around an alias.
            var length = 0;

            foreach (var ns in Namespaces)
            {
                length += 7 + ns.Length + newLineLength;
            }

            foreach (var alias in Aliases)
            {
                length += 10 + alias.Key.Length + alias.Value.Length + newLineLength;
            }

            return length == 0 ? 0 : length + newLineLength;
        }
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
            var written = CollectWrittenTypes(namePlan);

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

    /// <summary>
    /// Every type written, plus every type reached as an argument of one, in write order - and, in
    /// the same pass, the namespaces they bring with them.
    /// </summary>
    /// <remarks>
    /// The namespaces are taken from the deduplicated list rather than from every reference.
    /// <c>KnownNamespaces</c> is an iterator on every implementation of it, so asking a type that has
    /// already answered allocates a state machine for an answer that is already held - and a file
    /// writes the same handful of types over and over. Nothing declares these namespaces, so nothing
    /// can forget to.
    /// </remarks>
    private List<ITypeDefinition> CollectWrittenTypes(NamePlan namePlan)
    {
        var written = new List<ITypeDefinition>();
        var seen = new HashSet<ITypeDefinition>(ReferenceComparer.Instance);

        // Over the codes rather than over the values, counting past the strings. Asking each value
        // whether it is a type instead means an interface type test per written token, and five
        // tokens in six are text.
        var valueChunkIndex = -1;
        var valueChunk = Array.Empty<object>();
        var valueUsed = 0;
        var valueIndex = 0;

        for (var chunkIndex = 0; chunkIndex < _codes.ChunkCount; chunkIndex++)
        {
            var chunk = _codes.ChunkAt(chunkIndex, out var used);

            for (var i = 0; i < used; i++)
            {
                var kind = KindOf(chunk[i]);

                if (kind != SegmentKind.Text && kind != SegmentKind.TypeReference)
                {
                    continue;
                }

                if (valueIndex == valueUsed)
                {
                    valueChunk = _values.ChunkAt(++valueChunkIndex, out valueUsed);
                    valueIndex = 0;
                }

                var value = valueChunk[valueIndex++];

                if (kind == SegmentKind.TypeReference)
                {
                    CollectType((ITypeDefinition)value, written, seen);
                }
            }
        }

        for (var i = 0; i < written.Count; i++)
        {
            foreach (var knownNamespace in written[i].KnownNamespaces)
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

        return written;
    }

    /// <summary>
    /// Identity, not equality: two type definitions that name the same type are kept apart here.
    /// </summary>
    /// <remarks>
    /// The set exists only to keep the list short - everything downstream of it treats two entries
    /// naming the same type the way it treats one, and the collision test below is written so that a
    /// repeat is not mistaken for an ambiguity. Equality would be the wrong tool for it: hashing a
    /// type definition builds its fully qualified name, and a generator that calls
    /// <c>TypeDefinition.Get</c> per use hands over a fresh instance every time, so it would pay for
    /// a name per written reference to save a list entry.
    /// </remarks>
    private sealed class ReferenceComparer : IEqualityComparer<ITypeDefinition>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();

        public bool Equals(ITypeDefinition x, ITypeDefinition y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(ITypeDefinition obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
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

    /// <summary>The answer for a file no two of whose types want the same name, which is nearly all of them.</summary>
    private static readonly List<NameGroup> NoGroups = new List<NameGroup>();

    /// <summary>
    /// Groups the types by the name they compete for. Arity is part of it, because two types of the
    /// same name and different arity do not compete: <c>Box</c> and <c>Box&lt;T&gt;</c> resolve
    /// separately.
    /// </summary>
    /// <remarks>
    /// Two passes, because the first one nearly always ends the matter. Grouping means a group
    /// object with two lists in it for every distinct name in the file, and a string for each - all
    /// of it to discover, in the overwhelming majority of files, that no two names were the same.
    /// The first pass instead hashes each name as it is rendered and sorts the hashes: no strings,
    /// no groups, one small array. Only a repeat - a real collision, or a hash that happens to
    /// match, which the second pass then rejects - makes it group anything.
    /// </remarks>
    private List<NameGroup> GroupByShortName(List<ITypeDefinition> written)
    {
        if (!Options.AliasCollisions || written.Count < 2)
        {
            return NoGroups;
        }

        var builder = new StringBuilder();

        if (!MayHaveCollision(written, builder))
        {
            return NoGroups;
        }

        var groups = new List<NameGroup>();
        var byKey = new Dictionary<string, NameGroup>(StringComparer.Ordinal);

        foreach (var type in written)
        {
            var shortName = BareName(type, builder);

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

    /// <summary>
    /// Whether two of the written types might want the same name and arity, told from a sorted array
    /// of hashes rather than from the names themselves.
    /// </summary>
    /// <remarks>
    /// One-sided on purpose. Two equal names always hash the same, so it never says no to a real
    /// collision; two different names very occasionally hash the same, and the grouping pass that
    /// then runs compares the names properly and finds nothing.
    /// </remarks>
    private static bool MayHaveCollision(List<ITypeDefinition> written, StringBuilder builder)
    {
        // The name and arity in the high half, the namespace in the low half. Sorting brings equal
        // names together, and an ambiguity is two of them that disagree about the namespace - so a
        // type written twice, which is every file, reads as the repeat it is rather than as a
        // collision with itself.
        var keys = new long[written.Count];

        for (var i = 0; i < written.Count; i++)
        {
            var type = written[i];

            builder.Length = 0;

            type.WriteTypeName(builder, TypeOutputMode.ShortName);

            var name = (long)(uint)BareNameHash(builder, type.TypeArguments?.Count ?? 0);
            var space = (uint)StringHash(type.Namespace);

            keys[i] = (name << 32) | space;
        }

        Array.Sort(keys);

        for (var i = 1; i < keys.Length; i++)
        {
            if ((keys[i] >> 32) == (keys[i - 1] >> 32) && keys[i] != keys[i - 1])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>FNV-1a over a string. Its own, because string.GetHashCode is randomised per process.</summary>
    private static int StringHash(string? value)
    {
        unchecked
        {
            var hash = (int)2166136261;

            if (value != null)
            {
                for (var i = 0; i < value.Length; i++)
                {
                    hash = (hash ^ value[i]) * 16777619;
                }
            }

            return hash;
        }
    }

    /// <summary>
    /// FNV-1a over the part of a rendered name that <see cref="BareName"/> would keep, plus the arity.
    /// </summary>
    private static int BareNameHash(StringBuilder builder, int argumentCount)
    {
        var length = BareNameEnd(builder, 0);

        unchecked
        {
            var hash = (int)2166136261;

            for (var i = 0; i < length; i++)
            {
                hash = (hash ^ builder[i]) * 16777619;
            }

            return (hash ^ argumentCount) * 16777619;
        }
    }

    /// <summary>
    /// Where the name a type rendered at <paramref name="start"/> ends: before its argument list, and
    /// before any trailing array and null marks.
    /// </summary>
    private static int BareNameEnd(StringBuilder builder, int start)
    {
        var end = builder.Length;

        for (var i = start + 1; i < end; i++)
        {
            if (builder[i] == '<')
            {
                end = i;

                break;
            }
        }

        while (end > start)
        {
            var character = builder[end - 1];

            if (character != '?' && character != '[' && character != ']')
            {
                break;
            }

            end--;
        }

        return end;
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

            // Rendered straight into the output and cut back, rather than through a name of its own.
            var start = builder.Length;

            type.WriteTypeName(builder, TypeOutputMode.ShortName);

            builder.Length = BareNameEnd(builder, start);
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
    /// <remarks>
    /// Cut on the builder rather than through the string, and into a builder the caller lends it
    /// rather than one of its own: the old form built a StringBuilder, a string, a substring and a
    /// params array for the trim, per type, per file.
    /// </remarks>
    private static string BareName(ITypeDefinition type, StringBuilder builder)
    {
        builder.Length = 0;

        type.WriteTypeName(builder, TypeOutputMode.ShortName);

        builder.Length = BareNameEnd(builder, 0);

        return builder.ToString();
    }
}
