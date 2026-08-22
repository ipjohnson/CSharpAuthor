using System;

namespace CSharpAuthor.Profiles;

/// <summary>
/// Everything a writer needs to decide how a version-agnostic tree becomes text: formatting,
/// qualification, the language version being targeted, and which idioms are wanted.
/// </summary>
/// <remarks>
/// <para>
/// A profile is a <em>writer</em> argument. It is never stored on the tree, because the same tree
/// has to be able to render for C# 8 and C# 12 in the same process - which is the whole point of
/// keeping a tree instead of a string. A node does not decide what it emits; it asks the profile
/// what it may emit.
/// </para>
/// <para>
/// <strong>Preference is separate from capability.</strong> <see cref="PreferCollectionExprs"/>
/// with <see cref="Target"/> at <see cref="LanguageVersion.CSharp8"/> emits
/// <c>new[] { ... }</c> - silently, and correctly. A preference is never an error. A capability
/// violation - asking for something with no downlevel at all, like <c>ref struct</c> - is, and
/// goes through <see cref="EmitSession.Require(LanguageFeature, string)"/>.
/// </para>
/// <para>
/// The presets are frozen: assigning to one throws rather than quietly changing every other
/// caller's formatting. Use <see cref="Clone"/> or <see cref="With"/> to get a mutable copy.
/// </para>
/// </remarks>
#if CSHARPAUTHOR_PUBLIC_API
public
#endif
sealed partial class EmitProfile
{
    /// <summary>The version <see cref="LanguageVersion.Default"/> resolves to.</summary>
    public const LanguageVersion DefaultTarget = LanguageVersion.CSharp12;

    private bool _frozen;

    // formatting
    private char _indentChar = ' ';
    private int _indentWidth = 4;
    private string _newLine = "\n";
    private BraceStyle _braces = BraceStyle.Allman;
    private bool _fileScopedNamespace = true;
    private bool _breakInvokeLines = true;
    private bool _generateDocumentation = true;

    // qualification
    private TypeOutputMode _typeMode = TypeOutputMode.ShortName;
    private bool _aliasCollisions = true;
    private string? _containingNamespace;

    // capability
    private LanguageVersion _target = LanguageVersion.CSharp12;

    // idiom preference
    private bool _preferTargetTypedNew = true;
    private bool _preferCollectionExprs = true;
    private bool _preferRawStrings;

    // downlevel policy
    private PolyfillMode _polyfills = PolyfillMode.Auto;
    private DownlevelCommentPlacement _downlevelComments = DownlevelCommentPlacement.Inline;
    private CapabilityViolationBehavior _onCapabilityViolation = CapabilityViolationBehavior.Throw;

    // ---- formatting ------------------------------------------------------------------------

    /// <summary>The character an indent is made of.</summary>
    public char IndentChar
    {
        get => _indentChar;
        set => Set(ref _indentChar, value);
    }

    /// <summary>
    /// How many <see cref="IndentChar"/> make one level. One, when indenting with tabs.
    /// </summary>
    public int IndentWidth
    {
        get => _indentWidth;
        set => Set(ref _indentWidth, value);
    }

    /// <summary>The line ending. Not the platform's - the host project's.</summary>
    public string NewLine
    {
        get => _newLine;
        set => Set(ref _newLine, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Where an opening brace goes.</summary>
    public BraceStyle Braces
    {
        get => _braces;
        set => Set(ref _braces, value);
    }

    /// <summary>Emit <c>namespace X;</c> rather than a braced block.</summary>
    /// <remarks>
    /// A preference, resolved against <see cref="LanguageFeature.FileScopedNamespaces"/>: true
    /// here with a C# 8 target still emits a block, because that is what compiles.
    /// </remarks>
    public bool FileScopedNamespace
    {
        get => _fileScopedNamespace;
        set => Set(ref _fileScopedNamespace, value);
    }

    /// <summary>Break long invocation chains across lines. Carried from V1's output options.</summary>
    public bool BreakInvokeLines
    {
        get => _breakInvokeLines;
        set => Set(ref _breakInvokeLines, value);
    }

    /// <summary>Emit XML documentation comments. Carried from V1's output options.</summary>
    public bool GenerateDocumentation
    {
        get => _generateDocumentation;
        set => Set(ref _generateDocumentation, value);
    }

    // ---- qualification ---------------------------------------------------------------------

    /// <summary>Short names with usings, full names, or <c>global::</c>.</summary>
    public TypeOutputMode TypeMode
    {
        get => _typeMode;
        set => Set(ref _typeMode, value);
    }

    /// <summary>Alias colliding short names rather than emitting an ambiguous reference.</summary>
    public bool AliasCollisions
    {
        get => _aliasCollisions;
        set => Set(ref _aliasCollisions, value);
    }

    /// <summary>The namespace the file itself declares; usings it already covers are dropped.</summary>
    public string? ContainingNamespace
    {
        get => _containingNamespace;
        set => Set(ref _containingNamespace, value);
    }

    // ---- capability ------------------------------------------------------------------------

    /// <summary>
    /// The language version the output has to compile at.
    /// </summary>
    /// <remarks>
    /// This is capability, not preference: it is the ceiling every <c>Prefer*</c> resolves
    /// against. Detected from the consumer's <c>CSharpParseOptions.LanguageVersion</c> where the
    /// Roslyn-gated source folder is included; otherwise whatever the caller sets.
    /// </remarks>
    public LanguageVersion Target
    {
        get => _target;
        set => Set(ref _target, value);
    }

    /// <summary><see cref="Target"/> with <see cref="LanguageVersion.Default"/> resolved.</summary>
    public LanguageVersion EffectiveTarget =>
        _target == LanguageVersion.Default ? DefaultTarget : _target;

    // ---- idiom preference ------------------------------------------------------------------

    /// <summary>Prefer <c>new()</c> to <c>new T()</c>.</summary>
    public bool PreferTargetTypedNew
    {
        get => _preferTargetTypedNew;
        set => Set(ref _preferTargetTypedNew, value);
    }

    /// <summary>Prefer <c>[a, b]</c> to <c>new T[] { a, b }</c>.</summary>
    public bool PreferCollectionExprs
    {
        get => _preferCollectionExprs;
        set => Set(ref _preferCollectionExprs, value);
    }

    /// <summary>Prefer <c>"""raw"""</c> literals where they avoid escaping.</summary>
    public bool PreferRawStrings
    {
        get => _preferRawStrings;
        set => Set(ref _preferRawStrings, value);
    }

    // ---- downlevel policy ------------------------------------------------------------------

    /// <summary>When to emit a support type such as <c>IsExternalInit</c> alongside the output.</summary>
    public PolyfillMode Polyfills
    {
        get => _polyfills;
        set => Set(ref _polyfills, value);
    }

    /// <summary>Where a <c>// DOWNLEVEL:</c> comment goes, if anywhere.</summary>
    public DownlevelCommentPlacement DownlevelComments
    {
        get => _downlevelComments;
        set => Set(ref _downlevelComments, value);
    }

    /// <summary>What happens when a feature with no downlevel is asked for below its version.</summary>
    public CapabilityViolationBehavior OnCapabilityViolation
    {
        get => _onCapabilityViolation;
        set => Set(ref _onCapabilityViolation, value);
    }

    // ---- the question a node asks ----------------------------------------------------------

    /// <summary>
    /// Whether <see cref="Target"/> has this feature at all. Capability.
    /// </summary>
    public bool Supports(LanguageFeature feature) =>
        EffectiveTarget >= LanguageFeatures.MinimumVersion(feature);

    /// <summary>
    /// Whether this feature is wanted, ignoring whether it is available. Preference.
    /// </summary>
    public bool Prefers(LanguageFeature feature)
    {
        switch (feature)
        {
            case LanguageFeature.CollectionExpressions:
                return PreferCollectionExprs;
            case LanguageFeature.TargetTypedNew:
                return PreferTargetTypedNew;
            case LanguageFeature.RawStringLiterals:
                return PreferRawStrings;
            case LanguageFeature.FileScopedNamespaces:
                return FileScopedNamespace;
            default:
                // Everything else is not a style choice: a node that asks for `init` means `init`.
                // `var` and expression-bodied members land here too: the caller picks those at the
                // call site - ToVar against ToLocal, LambdaSyntax against a block body - so there
                // is no profile-level veto to exercise and never was one.
                return true;
        }
    }

    /// <summary>
    /// Whether a node may emit this feature: wanted <em>and</em> available.
    /// </summary>
    /// <remarks>
    /// The whole resolution rule in one line. A false answer for a
    /// <see cref="FeatureCategory.Free"/> feature is not a problem - it means "write the other
    /// form". For a <see cref="FeatureCategory.Impossible"/> one there is no other form, which is
    /// why those go through <see cref="EmitSession.Require(LanguageFeature, string)"/> instead.
    /// </remarks>
    public bool CanEmit(LanguageFeature feature) => Supports(feature) && Prefers(feature);

    // ---- copies ----------------------------------------------------------------------------

    /// <summary>Whether assigning to this profile throws. True for the presets.</summary>
    public bool IsFrozen => _frozen;

    /// <summary>Makes this profile read-only and returns it.</summary>
    public EmitProfile Freeze()
    {
        _frozen = true;
        return this;
    }

    /// <summary>An unfrozen copy.</summary>
    public EmitProfile Clone() =>
        new EmitProfile
        {
            _indentChar = _indentChar,
            _indentWidth = _indentWidth,
            _newLine = _newLine,
            _braces = _braces,
            _fileScopedNamespace = _fileScopedNamespace,
            _breakInvokeLines = _breakInvokeLines,
            _generateDocumentation = _generateDocumentation,
            _typeMode = _typeMode,
            _aliasCollisions = _aliasCollisions,
            _containingNamespace = _containingNamespace,
            _target = _target,
            _preferTargetTypedNew = _preferTargetTypedNew,
            _preferCollectionExprs = _preferCollectionExprs,
            _preferRawStrings = _preferRawStrings,
            _polyfills = _polyfills,
            _downlevelComments = _downlevelComments,
            _onCapabilityViolation = _onCapabilityViolation
        };

    /// <summary>
    /// An unfrozen copy with changes applied: <c>EmitProfile.Conservative.With(p =&gt; p.NewLine = "\r\n")</c>.
    /// </summary>
    public EmitProfile With(Action<EmitProfile> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var copy = Clone();

        configure(copy);

        return copy;
    }

    /// <summary>The V1 output options this profile's formatting half describes.</summary>
    public OutputContextOptions ToOutputContextOptions() =>
        new OutputContextOptions
        {
            IndentChar = IndentChar,
            IndentCharCount = IndentWidth,
            NewLine = NewLine,
            TypeOutputMode = TypeMode,
            BreakInvokeLines = BreakInvokeLines,
            GenerateDocumentation = GenerateDocumentation,

            // These three were declared on the profile and never carried, so a profile asking for
            // K&R braces, or turning aliasing off, or naming its containing namespace, was read and
            // then dropped on the floor. FromEditorConfig made it worse: it parsed
            // csharp_new_line_before_open_brace into Braces, where nothing would ever look at it.
            BraceStyle = Braces,
            AliasCollisions = AliasCollisions,
            ContainingNamespace = ContainingNamespace
        };

    /// <summary>
    /// A profile carrying the formatting an existing <see cref="OutputContextOptions"/> describes,
    /// so a V1 call site can adopt profiles without restating its formatting.
    /// </summary>
    public static EmitProfile FromOutputContextOptions(OutputContextOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var profile = V1Compatible.Clone();

        profile.IndentChar = options.IndentChar;
        profile.IndentWidth = options.IndentCharCount;
        profile.NewLine = options.NewLine;
        profile.TypeMode = options.TypeOutputMode;
        profile.BreakInvokeLines = options.BreakInvokeLines;
        profile.GenerateDocumentation = options.GenerateDocumentation;
        profile.Braces = options.BraceStyle;
        profile.AliasCollisions = options.AliasCollisions;
        profile.ContainingNamespace = options.ContainingNamespace;

        return profile;
    }

    // ---- presets ---------------------------------------------------------------------------

    /// <summary>
    /// C# 8, block namespace, no sugar. What a source generator ships when it does not know what
    /// its consumers compile at.
    /// </summary>
    public static readonly EmitProfile Conservative =
        new EmitProfile
        {
            _target = LanguageVersion.CSharp8,
            _fileScopedNamespace = false,
            _preferTargetTypedNew = false,
            _preferCollectionExprs = false,
            _preferRawStrings = false
        }.Freeze();

    /// <summary>C# 12 with the defaults from the handoff.</summary>
    public static readonly EmitProfile Default =
        new EmitProfile { _target = LanguageVersion.CSharp12 }.Freeze();

    /// <summary>
    /// Every feature this library knows about.
    /// </summary>
    /// <remarks>
    /// <see cref="LanguageVersion.Latest"/> means "assume it is there" - the core library has no
    /// compiler to ask. Where the Roslyn-gated source folder is included,
    /// <c>CSharpAuthor.Roslyn.EmitProfileRoslynExtensions.LatestSupported()</c> resolves it to
    /// what the referenced compiler actually parses, which for Roslyn 4.14.0 is C# 13.
    /// </remarks>
    public static readonly EmitProfile Latest =
        new EmitProfile { _target = LanguageVersion.Latest }.Freeze();

    /// <summary>
    /// V1's behaviour exactly: block namespaces, no downlevelling, no comments, no polyfills.
    /// </summary>
    /// <remarks>
    /// The fallback for a write that was given no profile, so that adopting profiles cannot change
    /// what an existing call site emits. V1 never downlevelled anything, so its target is
    /// <see cref="LanguageVersion.Latest"/>: a tree that asks for <c>init</c> gets <c>init</c>.
    /// </remarks>
    public static readonly EmitProfile V1Compatible =
        new EmitProfile
        {
            _target = LanguageVersion.Latest,
            _fileScopedNamespace = false,
            _polyfills = PolyfillMode.None,
            _downlevelComments = DownlevelCommentPlacement.None
        }.Freeze();

    private void Set<T>(ref T field, T value)
    {
        if (_frozen)
        {
            throw new InvalidOperationException(
                "This EmitProfile is frozen - the presets are shared, so assigning to one would " +
                "change every other caller's output. Call Clone() or With(...) for a copy you own.");
        }

        field = value;
    }
}
