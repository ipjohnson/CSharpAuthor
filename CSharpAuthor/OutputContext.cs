using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpAuthor;

/// <summary>
/// How a type reference is written, and therefore whether the file needs <c>using</c> directives at
/// all.
/// </summary>
/// <remarks>
/// <para>
/// This is the single most consequential option in the library, because it decides whether the
/// generated file can be broken by code it does not contain. Take one file with two <c>Result</c>
/// types in it, from <c>Sample.Models</c> and <c>Other.Models</c>:
/// </para>
/// <example>
/// <see cref="Global"/>:
/// <code>
/// namespace Sample
/// {
///     public class Greeter
///     {
///         public global::Sample.Models.Result A { get; set; }
///
///         public global::Other.Models.Result B { get; set; }
///     }
/// }
/// </code>
/// <see cref="ShortName"/>:
/// <code>
/// using Sample.Models;
/// using ModelsResult = Other.Models.Result;
///
/// namespace Sample
/// {
///     public class Greeter
///     {
///         public Result A { get; set; }
///
///         public ModelsResult B { get; set; }
///     }
/// }
/// </code>
/// </example>
/// <para>
/// <strong><see cref="Global"/> is the safer default for a generator, and is what this library's
/// maintainer uses.</strong> A fully qualified name cannot be captured by a type someone adds to
/// the consuming project later, cannot be shadowed by a type parameter, and cannot become ambiguous
/// because two <c>using</c> directives brought in the same short name. None of those failures are
/// in the generated file, which is why they are the ones that survive review.
/// </para>
/// <para>
/// <see cref="ShortName"/> is for output a person is meant to read - a scaffolded file that is
/// committed and then edited by hand. It is safe against the collisions the file itself contains,
/// because <see cref="OutputContextOptions.AliasCollisions"/> gives the loser of a contested name a
/// <c>using X = Ns.X;</c> alias rather than emitting a reference that is ambiguous. It cannot be
/// safe against a name the file has never seen.
/// </para>
/// </remarks>
public enum TypeOutputMode
{
    /// <summary>
    /// <c>global::Sample.Models.Result</c>. Qualifies every type and emits no derived
    /// <c>using</c> directives, because the qualification already says everything one would.
    /// </summary>
    /// <remarks>
    /// The mode that cannot be broken from outside the file: <c>global::</c> is resolved from the
    /// root alias, so it is immune even to a namespace that shadows the one being named. Namespaces
    /// asked for by name through <see cref="BaseOutputComponent.AddUsingNamespace"/> are still
    /// written - an extension method is reached through a directive and no other way - unless
    /// <see cref="OutputContextOptions.EmitExplicitUsings"/> is off.
    /// </remarks>
    Global,

    /// <summary>
    /// <c>Sample.Models.Result</c>. Fully qualified, but without the <c>global::</c> prefix, and no
    /// derived <c>using</c> directives.
    /// </summary>
    /// <remarks>
    /// Reads better than <see cref="Global"/> and is weaker: an ordinary qualified name is still
    /// resolved relative to the enclosing namespaces, so a nested namespace of the right name
    /// captures it. Choose it when the output is read by people and <c>global::</c> is noise, not
    /// when correctness is the reason for qualifying.
    /// </remarks>
    FullName,

    /// <summary>
    /// <c>Result</c>, with <c>using Sample.Models;</c> derived from the types the file actually
    /// wrote, and an alias for any short name two namespaces both claim.
    /// </summary>
    /// <remarks>
    /// The default, and the mode a hand-written file uses. The <c>using</c> list is derived rather
    /// than declared, so a type cannot reach the output without its namespace reaching the header.
    /// See <see cref="OutputContextOptions.AliasCollisions"/> for what happens when two types
    /// contest a name.
    /// </remarks>
    ShortName,
}

/// <summary>
/// Everything about a generated file that is decided when it is serialized rather than when it is
/// written: layout, and how type names are spelled.
/// </summary>
/// <remarks>
/// None of this reaches the tree. The same <see cref="CSharpFileDefinition"/> written into two
/// contexts with different options produces two different files, so a generator can offer a style
/// switch without threading it through every writer.
/// </remarks>
public class OutputContextOptions
{
    /// <summary>
    /// The character one indent level is made of. A space by default; set it to <c>'\t'</c> for
    /// tabs, with <see cref="IndentCharCount"/> at 1.
    /// </summary>
    public char IndentChar { get; set; } = ' ';

    /// <summary>
    /// How many <see cref="IndentChar"/> make one indent level. Four by default.
    /// </summary>
    public int IndentCharCount { get; set; } = 4;

    /// <summary>
    /// The line separator. <c>"\n"</c> by default, on every platform.
    /// </summary>
    /// <remarks>
    /// Fixed rather than taken from <see cref="Environment.NewLine"/>, so a generator produces the
    /// same bytes on every machine and a snapshot test of its output is not a test of what CI runs
    /// on.
    /// </remarks>
    public string NewLine { get; set; } = "\n";

    /// <summary>
    /// Whether a call with several arguments is broken across lines, one argument each.
    /// </summary>
    /// <remarks>
    /// <example>
    /// On:
    /// <code>
    /// Api.Register(
    ///     "a",
    ///     "b",
    ///     "c"
    /// );
    /// </code>
    /// Off, the same call is one line.
    /// </example>
    /// </remarks>
    public bool BreakInvokeLines { get; set; } = true;

    /// <summary>
    /// Whether <see cref="BaseOutputComponent.Comment"/> is written as a <c>///</c> documentation
    /// comment.
    /// </summary>
    /// <remarks>
    /// Turning it off drops every comment in the file, including the <c>&lt;param&gt;</c> and
    /// <c>&lt;returns&gt;</c> elements. For generated code nobody reads it is smaller output;
    /// for a public API it is documentation a consumer's IDE will not show.
    /// </remarks>
    public bool GenerateDocumentation { get; set; } = true;

    /// <summary>
    /// How type names are spelled, and therefore whether the file carries derived <c>using</c>
    /// directives. See <see cref="CSharpAuthor.TypeOutputMode"/> - it is the option worth reading
    /// about before choosing.
    /// </summary>
    /// <remarks>
    /// <see cref="TypeOutputMode.ShortName"/> by default, because that is what version 1 did.
    /// <see cref="TypeOutputMode.Global"/> is the safer choice for a generator: it cannot be broken
    /// by a type someone adds to the consuming project later.
    /// </remarks>
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
    /// <para>
    /// Without this the file emits both names bare and the compiler reports CS0104. The collision is
    /// only visible once the whole file has been written, which is why nothing is committed to text
    /// before then.
    /// </para>
    /// <example>
    /// Two <c>Result</c> types, from <c>Sample.Models</c> and <c>Other.Models</c>:
    /// <code>
    /// using Sample.Models;
    /// using ModelsResult = Other.Models.Result;
    /// ...
    ///     public Result A { get; set; }
    ///
    ///     public ModelsResult B { get; set; }
    /// </code>
    /// The alias name is derived from the losing type's namespace, so it is stable across runs
    /// rather than a counter that shifts when an unrelated type is added.
    /// </example>
    /// <para>
    /// Only meaningful in <see cref="TypeOutputMode.ShortName"/>; the qualifying modes have no
    /// collisions to resolve.
    /// </para>
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

    // -----------------------------------------------------------------------------------------
    // What the name plan needs, gathered as the types are written rather than found again
    // afterwards.
    //
    // Reading it back off the record meant walking every recorded code - five in six of which are
    // text - to reach the one write in fourteen that names a type, and doing it to discover, in
    // nearly every file, that no two names were the same. The types arrive at Write already; the
    // only thing the walk added was finding them a second time.
    //
    // All of it is allocated on the first type written, so a context that writes none - and this
    // library makes one per rendered expression - still allocates nothing for it.
    // -----------------------------------------------------------------------------------------

    /// <summary>Types already seen, by identity. Keeps the list below to one entry per type.</summary>
    private HashSet<ITypeDefinition>? _seenTypes;

    /// <summary>Every distinct type written, in write order, arguments included.</summary>
    private List<ITypeDefinition>? _writtenTypes;

    /// <summary>The namespaces those types bring with them.</summary>
    private HashSet<string>? _derivedNamespaces;

    /// <summary>
    /// Whether every type was written in the mode the name plan is built for, so the gathering
    /// above is the whole story.
    /// </summary>
    /// <remarks>
    /// A mode that qualifies its types derives no namespaces and needs no plan, so nothing is
    /// gathered while one is in force. A caller that changes the mode after writing - which is
    /// allowed, because nothing is decided until serialization - leaves the gathering incomplete,
    /// and the plan is then built by reading the types back off the record exactly as before.
    /// </remarks>
    private bool _typesGathered = true;

    private int _indentIndex;
    private bool _generateUsings;

    // -----------------------------------------------------------------------------------------
    // The fast path: a mode that qualifies every type it writes.
    //
    // Nothing recorded is ever re-decided there. A qualified name is the same name whatever else
    // the file turns out to contain, so there is no plan to make and no second pass to make it in
    // - the record and the walk over it are pure overhead, and the file can go straight into the
    // builder it will be returned from, with the directives inserted afterwards at the offset the
    // header ended at.
    //
    // What it must not lose is the promise that indentation, line endings and brace placement are
    // decided when the file is SERIALIZED. So the options those three come from are snapshotted
    // when the path is taken and checked again on every write that spends one, and once more
    // before the string is handed back. Any disagreement turns the stream back into the record it
    // would have been - see MaterialiseStream - and the ordinary serializer finishes the job. The
    // journal below is what makes that possible: one entry per write that is not plain text,
    // holding where in the stream it starts and what it was.
    // -----------------------------------------------------------------------------------------

    /// <summary>The file so far, already styled. Null unless the fast path was taken.</summary>
    private StringBuilder? _stream;

    /// <summary>Where each non-text write starts in <see cref="_stream"/>, and what it was.</summary>
    /// <remarks>
    /// Text is not journalled: it is whatever lies between one entry and the next, which is what
    /// makes the entries the small part. Only <see cref="MaterialiseStream"/> reads this.
    /// </remarks>
    private ChunkChain<long> _journal;

    /// <summary>Which of the two the writes are taking, decided once at the first of them.</summary>
    /// <remarks>
    /// One field rather than two flags, because every write reads it: on the recording path that is
    /// a load and a compare, which is as small as asking can be made.
    /// </remarks>
    private int _path;

    private const int PathUndecided = 0;
    private const int PathRecord = 1;
    private const int PathStream = 2;

    /// <summary>Where the file header ended, in characters, on the fast path.</summary>
    private int _headerOffset;

    // What the fast path committed to when it took over. Every one of them is settled at
    // serialization on the ordinary path, so any of them moving is what has to be caught.
    private char _snapIndentChar;
    private int _snapIndentWidth;
    private string _snapNewLine = "";
    private BraceStyle _snapBraceStyle;
    private TypeOutputMode _snapMode;

    /// <summary>
    /// How many segments belong above the generated <c>using</c> directives.
    /// </summary>
    /// <remarks>
    /// The usings are worked out after the whole file has been written, so they are prepended - and
    /// anything the file wrote before its namespace was pushed below them. That is wrong for exactly
    /// one thing and it matters: <c>// &lt;auto-generated/&gt;</c> has to be line one, because
    /// analyzers, StyleCop and the IDE all read line one to decide whether to skip the file. This
    /// records where the header ended so the directives go after it rather than before it.
    /// </remarks>
    private int _headerSegmentCount;

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

    /// <summary>
    /// The layout and naming decisions for this file. Read by everything that writes into the
    /// context, and read again in <see cref="Output"/>.
    /// </summary>
    /// <remarks>
    /// Settable up to the moment <see cref="Output"/> is called: the file is recorded as segments,
    /// so changing the brace style or the output mode after everything has been written still
    /// changes the text that comes out.
    /// </remarks>
    public OutputContextOptions Options { get; }

    /// <summary>
    /// A context to write one file into. Defaults to
    /// <see cref="TypeOutputMode.ShortName"/> with four-space Allman formatting.
    /// </summary>
    /// <remarks>
    /// One context per file. Writing two <see cref="CSharpFileDefinition"/> into one produces a
    /// single text with both namespaces in it, which is legal C# and almost never what was meant.
    /// </remarks>
    public OutputContext(OutputContextOptions? options = null)
    {
        Options = options ?? new OutputContextOptions();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void IncrementIndent()
    {
        _indentIndex++;
    }

    /// <inheritdoc />
    public void DecrementIndent()
    {
        _indentIndex--;
    }

    /// <inheritdoc />
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
                EmitIndent(_indentIndex);

                return;
            }

            if (text == SingleIndent)
            {
                EmitIndent(1);

                return;
            }
        }

        if (Streaming)
        {
            // Text is the same characters whatever the file turns out to be, so on the fast path it
            // goes straight in and is not journalled at all.
            _stream!.Append(text);

            return;
        }

        _textChars += text.Length;

        RecordValue(SegmentKind.Text, text);
    }

    /// <summary>
    /// The point everything else depends on: the type is recorded, not rendered - unless the mode
    /// in force renders it the same way whatever else the file contains.
    /// </summary>
    public void Write(ITypeDefinition typeDefinition)
    {
        if (typeDefinition == null)
        {
            return;
        }

        if (Streaming)
        {
            var mode = Options.TypeOutputMode;

            if (mode == _snapMode)
            {
                var stream = _stream!;
                var start = stream.Length;

                typeDefinition.WriteTypeName(stream, mode);

                _journal.Add(JournalEntry(start, SegmentKind.TypeReference, stream.Length - start));
                _values.Add(typeDefinition);

                return;
            }

            MaterialiseStream();
        }

        // What the name will be is not known yet - that is the point of the whole design - so the
        // parts it could be built from are counted separately and the output mode decides, at
        // serialization, which of them to believe.
        _typeRefCount++;
        _typeNameChars += NameAllowance(typeDefinition);
        _typeQualifierChars += typeDefinition.Namespace.Length + 1;

        // Everything the name plan will want from this type, taken while it is here. Nothing is
        // derived in a mode that qualifies, so nothing is gathered while one is in force - but the
        // mode can still change before serialization, and then the record is read instead.
        if (_typesGathered)
        {
            if (Options.TypeOutputMode == TypeOutputMode.ShortName)
            {
                NoteWrittenType(typeDefinition);
            }
            else
            {
                _typesGathered = false;
            }
        }

        RecordValue(SegmentKind.TypeReference, typeDefinition);
    }

    /// <summary>
    /// Takes what the name plan will want from a type as it is written: its place in the list of
    /// written types, and the namespaces it brings with it.
    /// </summary>
    /// <inheritdoc cref="CollectType" />
    private void NoteWrittenType(ITypeDefinition type)
    {
        if (type == null)
        {
            return;
        }

        var seen = _seenTypes ??= new HashSet<ITypeDefinition>(ReferenceComparer.Instance);

        if (!seen.Add(type))
        {
            return;
        }

        (_writtenTypes ??= new List<ITypeDefinition>()).Add(type);

        AddKnownNamespaces(type);

        var typeArguments = type.TypeArguments;

        if (typeArguments == null)
        {
            return;
        }

        for (var i = 0; i < typeArguments.Count; i++)
        {
            NoteWrittenType(typeArguments[i]);
        }
    }

    /// <summary>The namespaces a type brings with it, into the derived set.</summary>
    /// <remarks>
    /// <c>KnownNamespaces</c> is an iterator on every implementation of it, and a state machine per
    /// type is most of what deriving the using list costs. The two definitions this library hands
    /// out answer it with their own namespace and their parts' - and the parts are walked here
    /// anyway - so the exact classes are answered directly and everything else, including a
    /// consumer's own <see cref="ITypeDefinition"/>, is asked as before.
    /// </remarks>
    private void AddKnownNamespaces(ITypeDefinition type)
    {
        var runtimeType = type.GetType();

        if (type.ContainingType == null &&
            (runtimeType == typeof(TypeDefinition) || runtimeType == typeof(GenericTypeDefinition)))
        {
            AddDerivedNamespace(type.Namespace);

            return;
        }

        foreach (var knownNamespace in type.KnownNamespaces)
        {
            AddDerivedNamespace(knownNamespace);
        }
    }

    private void AddDerivedNamespace(string? ns)
    {
        if (!string.IsNullOrEmpty(ns))
        {
            (_derivedNamespaces ??= new HashSet<string>(StringComparer.Ordinal)).Add(ns!);
        }
    }

    /// <inheritdoc />
    public void WriteLine()
    {
        EmitNewLine();
    }

    /// <inheritdoc />
    public void WriteLine(string text)
    {
        Write(text);

        EmitNewLine();
    }

    /// <inheritdoc />
    public void WriteSpace()
    {
        if (Streaming)
        {
            _stream!.Append(' ');

            return;
        }

        _textChars++;

        RecordValue(SegmentKind.Text, " ");
    }

    /// <inheritdoc />
    public void WriteIndent(string text = "")
    {
        EmitIndent(_indentIndex);

        Write(text);
    }

    /// <inheritdoc />
    public void WriteIndentedLine(string text)
    {
        EmitIndent(_indentIndex);

        Write(text);

        EmitNewLine();
    }

    /// <inheritdoc />
    public void OpenScope()
    {
        EmitScope(SegmentKind.ScopeOpen, _indentIndex);

        _indentIndex++;
    }

    /// <inheritdoc />
    public void CloseScope()
    {
        _indentIndex--;

        EmitScope(SegmentKind.ScopeClose, _indentIndex);
    }

    // -----------------------------------------------------------------------------------------
    // The three writes whose characters depend on an option. Each one streams while the options
    // still say what they said when the fast path took over, and turns the stream back into a
    // record the moment they do not - so the answer is the serializer's either way.
    // -----------------------------------------------------------------------------------------

    private void EmitIndent(int depth)
    {
        if (Streaming && StreamIndent(depth))
        {
            return;
        }

        RecordIndent(depth);
    }

    private void EmitNewLine()
    {
        if (Streaming && StreamNewLine())
        {
            return;
        }

        RecordNewLine();
    }

    private void EmitScope(SegmentKind kind, int depth)
    {
        if (Streaming && StreamScope(kind, depth))
        {
            return;
        }

        RecordScope(kind, depth);
    }

    private bool StreamIndent(int depth)
    {
        if (Options.IndentChar != _snapIndentChar || Options.IndentCharCount != _snapIndentWidth)
        {
            MaterialiseStream();

            return false;
        }

        var stream = _stream!;

        _journal.Add(JournalEntry(stream.Length, SegmentKind.Indent, depth));

        if (depth > 0)
        {
            stream.Append(_snapIndentChar, _snapIndentWidth * depth);
        }

        return true;
    }

    private bool StreamNewLine()
    {
        if (!NewLineUnchanged())
        {
            MaterialiseStream();

            return false;
        }

        var stream = _stream!;

        _journal.Add(JournalEntry(stream.Length, SegmentKind.NewLine, 0));

        stream.Append(_snapNewLine);

        return true;
    }

    private bool StreamScope(SegmentKind kind, int depth)
    {
        // A negative depth is an unbalanced scope, and what it does - throw, out of the serializer -
        // is the record's answer to give, not this one's.
        if (depth < 0 || Options.BraceStyle != BraceStyle.Allman ||
            Options.IndentChar != _snapIndentChar || Options.IndentCharCount != _snapIndentWidth ||
            !NewLineUnchanged())
        {
            MaterialiseStream();

            return false;
        }

        var stream = _stream!;

        _journal.Add(JournalEntry(stream.Length, kind, depth));

        stream.Append(_snapIndentChar, _snapIndentWidth * depth);
        stream.Append(kind == SegmentKind.ScopeOpen ? '{' : '}').Append(_snapNewLine);

        return true;
    }

    /// <summary>
    /// Whether the line ending is still the one the stream was written with, re-anchoring on an
    /// equal string that happens to be a different instance.
    /// </summary>
    private bool NewLineUnchanged()
    {
        var newLine = Options.NewLine;

        if (ReferenceEquals(newLine, _snapNewLine))
        {
            return true;
        }

        if (!string.Equals(newLine, _snapNewLine, StringComparison.Ordinal))
        {
            return false;
        }

        _snapNewLine = newLine;

        return true;
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

    // -----------------------------------------------------------------------------------------
    // Choosing between the two, once, at the first write.
    // -----------------------------------------------------------------------------------------

    /// <summary>Whether this write goes straight into the output, deciding it if it is the first.</summary>
    private bool Streaming
    {
        get
        {
            var path = _path;

            if (path == PathRecord)
            {
                return false;
            }

            if (path == PathUndecided)
            {
                ChoosePath();

                path = _path;
            }

            return path == PathStream;
        }
    }

    /// <summary>
    /// Takes the fast path when the file about to be written cannot need a second look.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="TypeOutputMode.ShortName"/> is excluded because a short name is only decided once
    /// the whole file is known - that is the crown jewel and it is not for sale.
    /// </para>
    /// <para>
    /// <see cref="BraceStyle.KAndR"/> is excluded for a smaller reason: joining a brace to the line
    /// above it trims characters back off the output, and characters trimmed out of a stream cannot
    /// be given back to a record built from it. Nothing about that style is decided any earlier
    /// than it was - such a file simply takes the ordinary path, exactly as it did before.
    /// </para>
    /// </remarks>
    private void ChoosePath()
    {
        _path = PathRecord;

        var options = Options;

        if (options.TypeOutputMode == TypeOutputMode.ShortName ||
            options.BraceStyle != BraceStyle.Allman ||
            options.NewLine == null)
        {
            return;
        }

        _snapMode = options.TypeOutputMode;
        _snapIndentChar = options.IndentChar;
        _snapIndentWidth = options.IndentCharCount;
        _snapNewLine = options.NewLine;
        _snapBraceStyle = options.BraceStyle;

        // Small: most contexts write a handful of tokens and are thrown away, and a builder grows
        // by adding a block rather than by copying what it holds.
        _stream = new StringBuilder(256);
        _path = PathStream;

        // Nothing was gathered for a name plan while the stream ran, so a mode that turns out to
        // need one reads the types back off the record the stream is turned into.
        _typesGathered = false;
    }

    /// <summary>Where a non-text write starts in the stream, and what it was.</summary>
    private static long JournalEntry(int start, SegmentKind kind, int payload)
    {
        return ((long)start << 32) | (uint)Code(kind, payload);
    }

    /// <summary>How many characters a journalled write put into the stream.</summary>
    /// <remarks>
    /// Read from the snapshot rather than from the options, which is sound because every one of
    /// these writes checked the snapshot before it streamed anything.
    /// </remarks>
    private int StreamedLength(int code)
    {
        switch (KindOf(code))
        {
            case SegmentKind.NewLine:
                return _snapNewLine.Length;

            case SegmentKind.Indent:
                return DepthOf(code) > 0 ? _snapIndentWidth * DepthOf(code) : 0;

            case SegmentKind.ScopeOpen:
            case SegmentKind.ScopeClose:
                return _snapIndentWidth * DepthOf(code) + 1 + _snapNewLine.Length;

            case SegmentKind.TypeReference:
                return DepthOf(code);

            default:
                return 0;
        }
    }

    /// <summary>
    /// Turns the stream back into the record it would have been, and abandons the fast path.
    /// </summary>
    /// <remarks>
    /// Every write that streamed anything a style option decides left an entry saying where in the
    /// stream it began and what it was, and everything between two entries is text - so the record
    /// is the entries, in order, with the gaps between them cut out of the stream as the strings
    /// they were written from. From here on the file is served by the ordinary serializer, which
    /// applies the options as they stand when <see cref="Output"/> is called, which is the whole
    /// point of there being a record at all.
    /// </remarks>
    private void MaterialiseStream()
    {
        var stream = _stream!;
        var streamLength = stream.Length;
        var journal = _journal;
        var streamedTypes = _values;

        _stream = null;
        _path = PathRecord;
        _journal = default;
        _values = default;

        // The header boundary was a character offset while the stream ran; the serializer wants it
        // as a segment count, so it is found again as the record is laid out.
        var headerOffset = _headerOffset;
        var headerFound = false;
        var cursor = 0;

        var typeChunkIndex = -1;
        var typeChunk = Array.Empty<object>();
        var typeUsed = 0;
        var typeIndex = 0;

        for (var chunkIndex = 0; chunkIndex < journal.ChunkCount; chunkIndex++)
        {
            var chunk = journal.ChunkAt(chunkIndex, out var used);

            for (var i = 0; i < used; i++)
            {
                var entry = chunk[i];
                var start = (int)(entry >> 32);
                var code = (int)entry;

                RecoverText(stream, cursor, start, headerOffset, ref headerFound);

                if (!headerFound && start >= headerOffset)
                {
                    _headerSegmentCount = _codes.Count;
                    headerFound = true;
                }

                _codes.Add(code);

                if (KindOf(code) == SegmentKind.TypeReference)
                {
                    if (typeIndex == typeUsed)
                    {
                        typeChunk = streamedTypes.ChunkAt(++typeChunkIndex, out typeUsed);
                        typeIndex = 0;
                    }

                    _values.Add(typeChunk[typeIndex++]);
                }

                cursor = start + StreamedLength(code);
            }
        }

        RecoverText(stream, cursor, streamLength, headerOffset, ref headerFound);

        if (!headerFound)
        {
            _headerSegmentCount = _codes.Count;
        }

        // The estimate the output builder is sized from. What was streamed is already the width it
        // will be written at, near enough - the only thing that moves it is the option change that
        // brought us here, and an estimate that is a little out only costs a growth.
        _textChars += streamLength;
    }

    /// <summary>
    /// Cuts the text between two journal entries out of the stream and records it, splitting it
    /// where the file header ended if the header ended inside it.
    /// </summary>
    private void RecoverText(StringBuilder stream, int from, int to, int headerOffset, ref bool headerFound)
    {
        if (to <= from)
        {
            return;
        }

        if (!headerFound && from < headerOffset && headerOffset < to)
        {
            RecordValue(SegmentKind.Text, stream.ToString(from, headerOffset - from));

            _headerSegmentCount = _codes.Count;
            headerFound = true;

            RecordValue(SegmentKind.Text, stream.ToString(headerOffset, to - headerOffset));

            return;
        }

        if (!headerFound && from >= headerOffset)
        {
            _headerSegmentCount = _codes.Count;
            headerFound = true;
        }

        RecordValue(SegmentKind.Text, stream.ToString(from, to - from));
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void GenerateUsingStatements()
    {
        _generateUsings = true;
    }

    /// <summary>
    /// Everything written so far is the file's header, and the generated <c>using</c> directives
    /// belong after it.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="CSharpFileDefinition"/> once its leading traits and its comment have
    /// been written and before its namespace is opened. Without it a
    /// <c>// &lt;auto-generated/&gt;</c> marker attached to the file ends up on the line after the
    /// last <c>using</c>, where nothing reads it.
    /// </remarks>
    public void MarkEndOfFileHeader()
    {
        _headerSegmentCount = _codes.Count;

        if (_path == PathStream)
        {
            _headerOffset = _stream!.Length;
        }
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

    /// <inheritdoc />
    public char? LastCharacter
    {
        get
        {
            if (_path == PathStream)
            {
                var stream = _stream!;

                return stream.Length == 0 ? (char?)null : stream[stream.Length - 1];
            }

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

    /// <summary>
    /// The generated C#, as text. This is where the file is actually produced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything before this point recorded segments; nothing chose a name or a brace. Two things
    /// follow, and neither is possible if text is committed as it is written.
    /// </para>
    /// <para>
    /// The <c>using</c> directives are derived from the types the file actually wrote, so a type
    /// cannot reach the output without its namespace reaching the header - a missing using is not a
    /// bug that can happen. And a name is only chosen once the whole file is known, so two types
    /// with the same short name can be told apart: the second gets an alias rather than an
    /// ambiguous reference.
    /// </para>
    /// <example>
    /// <code>
    /// var file = new CSharpFileDefinition("Sample");
    /// file.AddClass("Greeter");
    ///
    /// var context = new OutputContext();
    /// file.WriteOutput(context);
    /// return context.Output();
    /// </code>
    /// </example>
    /// <para>
    /// Calling it more than once is fine and gives the same text each time - it does not consume
    /// the recording. Changing <see cref="Options"/> between two calls changes the second answer,
    /// which is how one tree becomes both a qualified file and a short-name one.
    /// </para>
    /// </remarks>
    public string Output()
    {
        if (_path == PathStream)
        {
            if (StyleStillSettled())
            {
                return StreamOutput();
            }

            // The promise is that these are decided here, not while the file was written. One of
            // them moved since, so the stream becomes the record it would have been and the
            // ordinary serializer decides all of them, now, exactly as it always did.
            MaterialiseStream();
        }

        var namePlan = BuildNamePlan();

        var builder = new StringBuilder(EstimateOutputLength(namePlan));

        var headerEnd = _headerSegmentCount > _codes.Count ? _codes.Count : _headerSegmentCount;

        // The header - a leading trait, a file comment - is written before the directives it must
        // stay above, and everything else after them.
        Serialize(builder, namePlan, 0, headerEnd);

        if (_generateUsings)
        {
            WriteUsings(builder, namePlan);
        }

        Serialize(builder, namePlan, headerEnd, _codes.Count);

        return builder.ToString();
    }

    /// <summary>
    /// Whether everything the fast path committed to when it took over still says what it said.
    /// </summary>
    private bool StyleStillSettled()
    {
        var options = Options;

        return options.TypeOutputMode == _snapMode
               && options.BraceStyle == _snapBraceStyle
               && options.IndentChar == _snapIndentChar
               && options.IndentCharCount == _snapIndentWidth
               && string.Equals(options.NewLine, _snapNewLine, StringComparison.Ordinal);
    }

    /// <summary>
    /// The streamed file, with the generated directives put in where the header ended.
    /// </summary>
    /// <remarks>
    /// The insertion is undone before returning, so the context is left exactly as it was found and
    /// asking for the output twice answers twice - which the record path has always done because it
    /// never wrote into itself at all.
    /// </remarks>
    private string StreamOutput()
    {
        var stream = _stream!;

        if (!_generateUsings)
        {
            return stream.ToString();
        }

        var namePlan = BuildNamePlan();

        if (namePlan.Namespaces.Count == 0 && namePlan.Aliases.Count == 0)
        {
            return stream.ToString();
        }

        var directives = new StringBuilder(namePlan.UsingsLength(Options.NewLine.Length));

        WriteUsings(directives, namePlan);

        var text = directives.ToString();
        var at = _headerOffset > stream.Length ? stream.Length : _headerOffset;

        stream.Insert(at, text);

        var output = stream.ToString();

        stream.Remove(at, text.Length);

        return output;
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

    private void Serialize(StringBuilder builder, NamePlan namePlan, int start, int end)
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

        // [start, end) is how the file header is kept above the generated usings: Output() renders
        // the header, writes the directives, then renders the rest. A skipped code still has to
        // consume its value, or the two stores fall out of step and every later name is the wrong
        // one - so this skips the append, never the read.
        var index = 0;

        for (var chunkIndex = 0; chunkIndex < _codes.ChunkCount; chunkIndex++)
        {
            var chunk = _codes.ChunkAt(chunkIndex, out var used);

            for (var i = 0; i < used; i++)
            {
                if (index >= end)
                {
                    return;
                }

                var emit = index >= start;

                index++;

                var code = chunk[i];

                switch (KindOf(code))
                {
                    case SegmentKind.Text:
                        if (valueIndex == valueUsed)
                        {
                            valueChunk = _values.ChunkAt(++valueChunkIndex, out valueUsed);
                            valueIndex = 0;
                        }

                        var text = (string)valueChunk[valueIndex++];

                        if (emit)
                        {
                            builder.Append(text);
                        }

                        break;

                    case SegmentKind.NewLine:
                        if (emit)
                        {
                            builder.Append(newLine);
                        }

                        break;

                    case SegmentKind.Indent:
                        var depth = DepthOf(code);

                        if (emit && depth > 0)
                        {
                            builder.Append(indentChar, indentWidth * depth);
                        }

                        break;

                    case SegmentKind.ScopeOpen:
                        if (!emit)
                        {
                            break;
                        }

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
                        if (emit)
                        {
                            builder.Append(indentChar, indentWidth * DepthOf(code));
                            builder.Append('}').Append(newLine);
                        }

                        break;

                    case SegmentKind.TypeReference:
                        if (valueIndex == valueUsed)
                        {
                            valueChunk = _values.ChunkAt(++valueChunkIndex, out valueUsed);
                            valueIndex = 0;
                        }

                        var type = (ITypeDefinition)valueChunk[valueIndex++];

                        if (!emit)
                        {
                            break;
                        }

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

    /// <summary>The answer for a file that wrote no types at all.</summary>
    private static readonly List<ITypeDefinition> NoTypes = new List<ITypeDefinition>();

    private NamePlan BuildNamePlan()
    {
        var namePlan = new NamePlan();

        if (Options.TypeOutputMode == TypeOutputMode.ShortName)
        {
            List<ITypeDefinition> written;

            if (_typesGathered)
            {
                written = _writtenTypes ?? NoTypes;

                if (_derivedNamespaces != null)
                {
                    foreach (var ns in _derivedNamespaces)
                    {
                        namePlan.Namespaces.Add(ns);
                    }
                }

                foreach (var ns in _typeNamespaces)
                {
                    namePlan.Namespaces.Add(ns);
                }
            }
            else
            {
                written = CollectWrittenTypes(namePlan);
            }

            // Grouping means a group object with two lists in it for every distinct name in the
            // file, and a string for each - all of it to discover, in the overwhelming majority of
            // files, that no two names were the same. So it only runs once something says two
            // names might be.
            if (Options.AliasCollisions && written.Count > 1 &&
                MayHaveCollision(written, new StringBuilder()))
            {
                var groups = GroupByShortName(written);

                ResolveCollisions(namePlan, groups);

                DropAliasedNamespaces(namePlan, written);

                // A namespace that had to stay - something else in it is still written plainly -
                // puts the ambiguity back. The name that was left plain then has to be aliased too.
                if (AliasTheWinners(namePlan, groups))
                {
                    DropAliasedNamespaces(namePlan, written);
                }
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

    /// <summary>
    /// Groups the types by the name they compete for. Arity is part of it, because two types of the
    /// same name and different arity do not compete: <c>Box</c> and <c>Box&lt;T&gt;</c> resolve
    /// separately.
    /// </summary>
    private List<NameGroup> GroupByShortName(List<ITypeDefinition> written)
    {
        var builder = new StringBuilder();

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

        // The shape and the annotations, from the one place that knows how they interleave. Writing
        // "[]" for IsArray and a "?" after it loses the rank of int[,] and moves the annotation of
        // int?[] - so an aliased type would be spelled differently from the same type written by
        // itself, which is the one thing an alias must not do.
        BaseTypeDefinition.WriteArraySuffix(builder, type.ArrayRanks, type.NullableAnnotations);
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
