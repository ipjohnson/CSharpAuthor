namespace CSharpAuthor.Profiles;

/// <summary>
/// A C# feature a node can ask an <see cref="EmitProfile"/> about.
/// </summary>
/// <remarks>
/// The tree is version-agnostic: a node describes what it means, then asks the profile what it may
/// emit. This enum is that question's vocabulary.
/// </remarks>
public enum LanguageFeature
{
    // ---- free: a downlevel rendering exists that means the same thing --------------------------

    /// <summary><c>[1, 2, 3]</c>. Downlevels to <c>new T[] { 1, 2, 3 }</c>.</summary>
    CollectionExpressions,

    /// <summary><c>new()</c>. Downlevels to <c>new T()</c>.</summary>
    TargetTypedNew,

    /// <summary><c>namespace N;</c>. Downlevels to <c>namespace N { }</c>.</summary>
    FileScopedNamespaces,

    /// <summary><c>"""..."""</c>. Downlevels to an escaped string literal.</summary>
    RawStringLiterals,

    /// <summary><c>nameof(x)</c>. Downlevels to the string literal it would produce.</summary>
    NameOf,

    /// <summary><c>using var x = ...;</c>. Downlevels to <c>using (var x = ...) { }</c>.</summary>
    UsingDeclarations,

    /// <summary><c>break outer;</c> / <c>continue outer;</c>. Downlevels to <c>goto</c>.</summary>
    LabeledJumps,

    /// <summary><c>class C(int x)</c>. Downlevels to a constructor with backing fields.</summary>
    PrimaryConstructors,

    /// <summary>The <c>field</c> keyword. Downlevels to an explicit backing field.</summary>
    FieldKeyword,

    /// <summary><c>params ReadOnlySpan&lt;T&gt;</c>. Downlevels to <c>params T[]</c>.</summary>
    ParamsCollections,

    /// <summary><c>x switch { ... }</c>. Downlevels to a conditional chain.</summary>
    SwitchExpressions,

    /// <summary>
    /// <c>nint</c> / <c>nuint</c>. Downlevels to <c>IntPtr</c> / <c>UIntPtr</c>, which is the same
    /// type - the keyword is a spelling, not a different runtime type.
    /// </summary>
    /// <remarks>
    /// Which spelling was meant cannot be recovered by reflection: a reference built from
    /// <c>typeof(IntPtr)</c> and one built from <c>nint</c> are the same reference. The emitter's
    /// choice here is a preference, not a recovered fact.
    /// </remarks>
    NativeIntegerKeywords,

    /// <summary><c>var</c>. Downlevels to the type written out.</summary>
    ImplicitlyTypedLocals,

    /// <summary><c>=&gt;</c> on a method. Downlevels to a block body.</summary>
    ExpressionBodiedMethods,

    /// <summary><c>=&gt;</c> on a property or accessor. Downlevels to a block body.</summary>
    ExpressionBodiedProperties,

    // ---- polyfillable: needs a type the target framework may not have -------------------------

    /// <summary><c>{ get; init; }</c>. Needs <c>IsExternalInit</c>; below C#9 loses immutability.</summary>
    InitOnlyProperties,

    /// <summary><c>required</c>. Needs <c>RequiredMemberAttribute</c>; below C#11 loses enforcement.</summary>
    RequiredMembers,

    // ---- impossible: no downlevel exists, so emitting anything would be wrong -----------------

    /// <summary><c>ref struct</c>. No downlevel: stack-only-ness cannot be expressed otherwise.</summary>
    RefStructs,

    /// <summary><c>static abstract</c> interface members. No downlevel.</summary>
    StaticAbstractInterfaceMembers,

    /// <summary>An interface member with a body. No downlevel.</summary>
    DefaultInterfaceMembers,

    /// <summary><c>delegate*&lt;...&gt;</c>. No downlevel.</summary>
    FunctionPointers,

    /// <summary><c>[InlineArray(n)]</c>. No downlevel.</summary>
    InlineArrays,

    /// <summary>
    /// <c>record</c>. No downlevel <em>here</em>: writing <c>class</c> instead drops value
    /// equality, <c>with</c> and the deconstructor, and nothing in this library generates them.
    /// </summary>
    Records,

    /// <summary><c>record struct</c>. As <see cref="Records"/>.</summary>
    RecordStructs
}

/// <summary>
/// What happens to a feature when the target does not have it.
/// </summary>
public enum FeatureCategory
{
    /// <summary>
    /// A different rendering means the same thing. The downlevel is silent - it is a rendering
    /// choice, not a defect.
    /// </summary>
    Free,

    /// <summary>
    /// The feature works below its version if a support type is emitted alongside it, and is
    /// lossy below the version where the syntax itself was introduced.
    /// </summary>
    Polyfillable,

    /// <summary>
    /// Nothing else means the same thing. Emitting a substitute would be wrong, so this produces
    /// a diagnostic instead - see <see cref="EmitSession.Require(LanguageFeature, string)"/>.
    /// </summary>
    Impossible
}

/// <summary>
/// How completely this library can currently render a feature's downlevel form.
/// </summary>
/// <remarks>
/// Recorded per feature so the table can be printed honestly: a feature whose downlevel is
/// <see cref="Policy"/> answers <c>Supports</c>/<c>CanEmit</c> correctly and records the right
/// diagnostics, but no component in this library writes the alternative form yet - the writer that
/// owns that construct has to.
/// </remarks>
public enum DownlevelSupport
{
    /// <summary>No alternative form exists (every <see cref="FeatureCategory.Impossible"/> feature).</summary>
    None,

    /// <summary>The capability answer and the diagnostics are here; the rendering is the caller's.</summary>
    Policy,

    /// <summary>A component in this library writes both forms.</summary>
    Writer
}

/// <summary>
/// One row of the capability table.
/// </summary>
public sealed class FeatureInfo
{
    internal FeatureInfo(
        LanguageFeature feature,
        LanguageVersion minimumVersion,
        FeatureCategory category,
        DownlevelSupport downlevel,
        string syntax,
        string consequence)
    {
        Feature = feature;
        MinimumVersion = minimumVersion;
        Category = category;
        Downlevel = downlevel;
        Syntax = syntax;
        Consequence = consequence;
    }

    /// <summary>The feature this row describes.</summary>
    public LanguageFeature Feature { get; }

    /// <summary>The first language version that has it.</summary>
    public LanguageVersion MinimumVersion { get; }

    /// <summary>Free, polyfillable, or impossible.</summary>
    public FeatureCategory Category { get; }

    /// <summary>How much of the downlevel this library performs itself.</summary>
    public DownlevelSupport Downlevel { get; }

    /// <summary>How the feature is written, as it appears in a diagnostic: <c>init</c>, <c>ref struct</c>.</summary>
    public string Syntax { get; }

    /// <summary>
    /// What the reader loses when the downlevel is taken, or an empty string when nothing is lost.
    /// A non-empty value is what makes the downlevel worth a <c>// DOWNLEVEL:</c> comment.
    /// </summary>
    public string Consequence { get; }

    /// <summary>Whether taking the downlevel changes meaning.</summary>
    public bool IsLossy => Consequence.Length > 0;
}

/// <summary>
/// The capability table: feature -&gt; minimum language version -&gt; category.
/// </summary>
/// <remarks>
/// One table, consulted by every node. Versions are the release the syntax shipped in, not the
/// release it was previewed in.
/// </remarks>
public static class LanguageFeatures
{
    private static readonly FeatureInfo[] Table =
    {
        // --- free ------------------------------------------------------------------------------
        Row(LanguageFeature.CollectionExpressions, LanguageVersion.CSharp12, FeatureCategory.Free,
            DownlevelSupport.Writer, "collection expression"),
        Row(LanguageFeature.TargetTypedNew, LanguageVersion.CSharp9, FeatureCategory.Free,
            DownlevelSupport.Writer, "target-typed new"),
        Row(LanguageFeature.FileScopedNamespaces, LanguageVersion.CSharp10, FeatureCategory.Free,
            DownlevelSupport.Writer, "file-scoped namespace"),
        Row(LanguageFeature.RawStringLiterals, LanguageVersion.CSharp11, FeatureCategory.Free,
            DownlevelSupport.Writer, "raw string literal"),
        Row(LanguageFeature.NameOf, LanguageVersion.CSharp6, FeatureCategory.Free,
            DownlevelSupport.Writer, "nameof"),
        Row(LanguageFeature.UsingDeclarations, LanguageVersion.CSharp8, FeatureCategory.Free,
            DownlevelSupport.Writer, "using declaration"),
        Row(LanguageFeature.LabeledJumps, LanguageVersion.CSharp15, FeatureCategory.Free,
            DownlevelSupport.Writer, "labeled jump"),
        Row(LanguageFeature.SwitchExpressions, LanguageVersion.CSharp8, FeatureCategory.Free,
            DownlevelSupport.Writer, "switch expression"),
        Row(LanguageFeature.PrimaryConstructors, LanguageVersion.CSharp12, FeatureCategory.Free,
            DownlevelSupport.Policy, "primary constructor"),
        Row(LanguageFeature.FieldKeyword, LanguageVersion.CSharp14, FeatureCategory.Free,
            DownlevelSupport.Policy, "field keyword"),
        Row(LanguageFeature.ParamsCollections, LanguageVersion.CSharp13, FeatureCategory.Free,
            DownlevelSupport.Policy, "params collection"),
        Row(LanguageFeature.ImplicitlyTypedLocals, LanguageVersion.CSharp3, FeatureCategory.Free,
            DownlevelSupport.Policy, "var"),
        Row(LanguageFeature.ExpressionBodiedMethods, LanguageVersion.CSharp6, FeatureCategory.Free,
            DownlevelSupport.Policy, "expression-bodied method"),
        Row(LanguageFeature.ExpressionBodiedProperties, LanguageVersion.CSharp7, FeatureCategory.Free,
            DownlevelSupport.Policy, "expression-bodied property"),
        Row(LanguageFeature.NativeIntegerKeywords, LanguageVersion.CSharp9, FeatureCategory.Free,
            DownlevelSupport.Policy, "nint"),

        // --- polyfillable ----------------------------------------------------------------------
        Row(LanguageFeature.InitOnlyProperties, LanguageVersion.CSharp9, FeatureCategory.Polyfillable,
            DownlevelSupport.Writer, "init",
            "emitted as a settable property, immutability lost"),
        Row(LanguageFeature.RequiredMembers, LanguageVersion.CSharp11, FeatureCategory.Polyfillable,
            DownlevelSupport.Writer, "required",
            "emitted as an ordinary member, initialisation is no longer enforced"),

        // --- impossible ------------------------------------------------------------------------
        Row(LanguageFeature.RefStructs, LanguageVersion.CSharp7_2, FeatureCategory.Impossible,
            DownlevelSupport.None, "ref struct"),
        Row(LanguageFeature.StaticAbstractInterfaceMembers, LanguageVersion.CSharp11, FeatureCategory.Impossible,
            DownlevelSupport.None, "static abstract interface member"),
        Row(LanguageFeature.DefaultInterfaceMembers, LanguageVersion.CSharp8, FeatureCategory.Impossible,
            DownlevelSupport.None, "default interface member"),
        Row(LanguageFeature.FunctionPointers, LanguageVersion.CSharp9, FeatureCategory.Impossible,
            DownlevelSupport.None, "function pointer"),
        Row(LanguageFeature.InlineArrays, LanguageVersion.CSharp12, FeatureCategory.Impossible,
            DownlevelSupport.None, "inline array"),
        Row(LanguageFeature.Records, LanguageVersion.CSharp9, FeatureCategory.Impossible,
            DownlevelSupport.None, "record"),
        Row(LanguageFeature.RecordStructs, LanguageVersion.CSharp10, FeatureCategory.Impossible,
            DownlevelSupport.None, "record struct")
    };

    private static FeatureInfo Row(
        LanguageFeature feature,
        LanguageVersion version,
        FeatureCategory category,
        DownlevelSupport downlevel,
        string syntax,
        string consequence = "") =>
        new FeatureInfo(feature, version, category, downlevel, syntax, consequence);

    /// <summary>Every row, in table order.</summary>
    public static System.Collections.Generic.IReadOnlyList<FeatureInfo> All => Table;

    /// <summary>
    /// The row for a feature.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// The feature has no row. That is a bug in this table, not in the caller - every value of
    /// <see cref="LanguageFeature"/> must be listed, which is what
    /// <c>CapabilityTableTests.EveryFeatureHasARow</c> checks.
    /// </exception>
    public static FeatureInfo Get(LanguageFeature feature)
    {
        foreach (var info in Table)
        {
            if (info.Feature == feature)
            {
                return info;
            }
        }

        throw new System.ArgumentOutOfRangeException(
            nameof(feature), feature, "No capability table row for " + feature + ".");
    }

    /// <summary>The first language version with this feature.</summary>
    public static LanguageVersion MinimumVersion(LanguageFeature feature) => Get(feature).MinimumVersion;

    /// <summary>Free, polyfillable, or impossible.</summary>
    public static FeatureCategory CategoryOf(LanguageFeature feature) => Get(feature).Category;
}
