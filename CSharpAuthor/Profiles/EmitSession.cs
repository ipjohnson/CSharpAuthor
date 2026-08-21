using System;
using System.Collections.Generic;

namespace CSharpAuthor.Profiles;

/// <summary>
/// One rendering of one tree: the profile in force, plus everything the writer decided or could
/// not do along the way.
/// </summary>
/// <remarks>
/// <para>
/// This is the diagnostic channel. A node never decides on its own that a feature is unavailable
/// and quietly writes something else - it asks here, and asking is what records the decision.
/// Three answers are possible, and they are the three categories from the handoff:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="MayEmit(LanguageFeature,string)"/> returns false for a <em>free</em> feature: write
/// the other form, nothing is lost, an <see cref="EmitSeverity.Info"/> diagnostic records it.
/// </description></item>
/// <item><description>
/// <see cref="MayEmit(LanguageFeature,string)"/> returns false for a <em>polyfillable</em> one:
/// write the other form, an <see cref="EmitSeverity.Warning"/> records what the reader lost, and
/// a <c>// DOWNLEVEL:</c> comment says so in the output itself.
/// </description></item>
/// <item><description>
/// <see cref="Require"/> returns false: there is no other form. That is an
/// <see cref="EmitSeverity.Error"/>, and it either throws or puts <c>#error</c> in the file. It
/// never returns quietly, because that is the V1 defect class.
/// </description></item>
/// </list>
/// <para>
/// A session is not thread-safe; one belongs to one write.
/// </para>
/// </remarks>
public sealed class EmitSession
{
    private readonly List<EmitDiagnostic> _diagnostics = new List<EmitDiagnostic>();
    private readonly List<string> _notes = new List<string>();
    private readonly List<PolyfillType> _polyfills = new List<PolyfillType>();
    private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _labels = new HashSet<string>(StringComparer.Ordinal);
    private readonly bool _discard;

    /// <summary>
    /// A session for a write that was given no profile: V1's behaviour, recording nothing.
    /// </summary>
    /// <remarks>
    /// Shared, and safe to share because it never mutates. Its profile is
    /// <see cref="EmitProfile.V1Compatible"/>, whose target is
    /// <see cref="LanguageVersion.Latest"/>, so no node ever downlevels and there is nothing to
    /// record. This is what keeps a V1 call site that knows nothing about profiles emitting
    /// exactly what it emitted before.
    /// </remarks>
    public static readonly EmitSession Discarding = new EmitSession(EmitProfile.V1Compatible, discard: true);

    /// <summary>Starts a session for a profile.</summary>
    public EmitSession(EmitProfile? profile = null)
        : this(profile ?? EmitProfile.Default, discard: false)
    {
    }

    private EmitSession(EmitProfile profile, bool discard)
    {
        Profile = profile;
        _discard = discard;
    }

    /// <summary>The profile this write is rendering for.</summary>
    public EmitProfile Profile { get; }

    /// <summary>Everything recorded, in the order it happened.</summary>
    public IReadOnlyList<EmitDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Whether anything could not be emitted.</summary>
    public bool HasErrors
    {
        get
        {
            foreach (var diagnostic in _diagnostics)
            {
                if (diagnostic.Severity == EmitSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The <c>// DOWNLEVEL:</c> comments this write produced, deduplicated.</summary>
    public IReadOnlyList<string> DownlevelNotes => _notes;

    /// <summary>Support types this write needs emitted beside it.</summary>
    public IReadOnlyList<PolyfillType> RequiredPolyfills => _polyfills;

    // ---- the three answers -----------------------------------------------------------------

    /// <summary>
    /// Whether a node may emit a feature, recording the decision either way.
    /// </summary>
    /// <param name="feature">What the node wants to write.</param>
    /// <param name="context">
    /// The member or expression asking, used in the diagnostic and in the <c>// DOWNLEVEL:</c>
    /// comment. A property passes its name.
    /// </param>
    /// <returns>
    /// True to write the modern form. False to write the downlevel form - which is a normal
    /// answer, not a failure.
    /// </returns>
    public bool MayEmit(LanguageFeature feature, string? context = null) =>
        MayEmit(feature, null, context);

    /// <summary>
    /// <see cref="MayEmit(LanguageFeature,string)"/>, additionally writing an inline
    /// <c>// DOWNLEVEL:</c> comment where the profile asks for one.
    /// </summary>
    /// <remarks>
    /// Call this at the start of the member's line, before anything else is written for it -
    /// the comment is a line of its own and cannot be inserted into a half-written one.
    /// </remarks>
    public bool MayEmit(LanguageFeature feature, IOutputContext? outputContext, string? context = null)
    {
        var info = LanguageFeatures.Get(feature);

        if (info.Category == FeatureCategory.Impossible)
        {
            // Asking "may I?" about something with no alternative is the wrong question: there is
            // nothing to fall back to, so it has to go through the error path instead.
            return Require(feature, outputContext, context);
        }

        if (Profile.CanEmit(feature))
        {
            RequestPolyfills(feature);

            return true;
        }

        if (info.IsLossy && !Profile.Supports(feature))
        {
            var note = FormatDownlevelComment(info, context);

            Report(new EmitDiagnostic(
                EmitDiagnostic.LossyDownlevelId,
                EmitSeverity.Warning,
                feature,
                info.MinimumVersion,
                Profile.EffectiveTarget,
                context,
                note.Substring("// DOWNLEVEL: ".Length)));

            AddNote(note);

            if (outputContext != null && Profile.DownlevelComments == DownlevelCommentPlacement.Inline)
            {
                outputContext.WriteIndentedLine(note);
            }
        }
        else
        {
            Report(new EmitDiagnostic(
                EmitDiagnostic.PreferenceResolvedId,
                EmitSeverity.Info,
                feature,
                info.MinimumVersion,
                Profile.EffectiveTarget,
                context,
                Describe(info, context) + " - emitted in its pre-" + info.MinimumVersion.ToDisplayName() +
                " form, which means the same thing."));
        }

        return false;
    }

    /// <summary>
    /// Demands a feature the caller has no alternative form for.
    /// </summary>
    /// <remarks>
    /// Every <see cref="FeatureCategory.Impossible"/> feature goes through here, and so does a
    /// free one whose alternative <em>this writer</em> cannot produce - a primary constructor is
    /// free in principle, but a writer that would have to drop the parameters has no more of an
    /// alternative than one facing a <c>ref struct</c>.
    /// </remarks>
    /// <returns>
    /// True when the target has it. False only in
    /// <see cref="CapabilityViolationBehavior.EmitErrorDirective"/> mode - otherwise this throws,
    /// because there is nothing correct left to write.
    /// </returns>
    /// <exception cref="EmitCapabilityException">
    /// The target does not have the feature and the profile asks to throw.
    /// </exception>
    public bool Require(LanguageFeature feature, string? context = null) =>
        Require(feature, null, context);

    /// <summary>
    /// <see cref="Require(LanguageFeature,string)"/>, additionally writing <c>#error</c> into the
    /// output in <see cref="CapabilityViolationBehavior.EmitErrorDirective"/> mode.
    /// </summary>
    public bool Require(LanguageFeature feature, IOutputContext? outputContext, string? context = null)
    {
        var info = LanguageFeatures.Get(feature);

        if (Profile.Supports(feature))
        {
            RequestPolyfills(feature);

            return true;
        }

        var message =
            Describe(info, context) + " requires " + info.MinimumVersion.ToDisplayName() +
            " and the target is " + Profile.EffectiveTarget.ToDisplayName() +
            ". There is no downlevel form, so anything emitted here would mean something else.";

        var diagnostic = new EmitDiagnostic(
            EmitDiagnostic.CapabilityViolationId,
            EmitSeverity.Error,
            feature,
            info.MinimumVersion,
            Profile.EffectiveTarget,
            context,
            message);

        Report(diagnostic);

        if (Profile.OnCapabilityViolation == CapabilityViolationBehavior.Throw)
        {
            throw new EmitCapabilityException(diagnostic);
        }

        outputContext?.WriteIndentedLine("#error " + diagnostic);

        return false;
    }

    /// <summary>
    /// Records that a downlevel this library knows how to take was ruled out by the caller.
    /// </summary>
    /// <remarks>
    /// For the cases where the alternative form is only correct under a condition the writer
    /// cannot check - a switch expression downlevels to a conditional chain only if its arms
    /// are constants. Saying so is an error; guessing is not.
    /// </remarks>
    public bool NoDownlevelAvailable(LanguageFeature feature, IOutputContext? outputContext, string? context, string reason)
    {
        var info = LanguageFeatures.Get(feature);

        var diagnostic = new EmitDiagnostic(
            EmitDiagnostic.NoDownlevelAvailableId,
            EmitSeverity.Error,
            feature,
            info.MinimumVersion,
            Profile.EffectiveTarget,
            context,
            Describe(info, context) + " requires " + info.MinimumVersion.ToDisplayName() +
            ", the target is " + Profile.EffectiveTarget.ToDisplayName() + ", and " + reason);

        Report(diagnostic);

        if (Profile.OnCapabilityViolation == CapabilityViolationBehavior.Throw)
        {
            throw new EmitCapabilityException(diagnostic);
        }

        outputContext?.WriteIndentedLine("#error " + diagnostic);

        return false;
    }

    // ---- comments and support types ---------------------------------------------------------

    /// <summary>
    /// The exact comment the handoff specifies:
    /// <c>// DOWNLEVEL: Name: 'init' unavailable below C#9 — emitted as a settable property, immutability lost</c>.
    /// </summary>
    public string FormatDownlevelComment(FeatureInfo info, string? context)
    {
        var prefix = string.IsNullOrEmpty(context) ? "" : context + ": ";

        return "// DOWNLEVEL: " + prefix + "'" + info.Syntax + "' unavailable below " +
               info.MinimumVersion.ToDisplayName() + " — " + info.Consequence;
    }

    /// <summary>
    /// Writes the collected <c>// DOWNLEVEL:</c> comments, when the profile puts them at the top
    /// of the file. Comments can precede the using directives; a namespace declaration cannot,
    /// which is why the support types go at the other end.
    /// </summary>
    public void WriteDownlevelHeader(IOutputContext outputContext)
    {
        if (outputContext == null)
        {
            throw new ArgumentNullException(nameof(outputContext));
        }

        if (Profile.DownlevelComments != DownlevelCommentPlacement.FileHeader || _notes.Count == 0)
        {
            return;
        }

        foreach (var note in _notes)
        {
            outputContext.WriteIndentedLine(note);
        }

        outputContext.WriteLine();
    }

    /// <summary>
    /// Writes the support types this write asked for, each in its own block namespace.
    /// </summary>
    /// <remarks>
    /// At the end of the file: a <c>using</c> has to precede every other element, so anything
    /// declaring a namespace cannot go above them.
    /// </remarks>
    public void WritePolyfills(IOutputContext outputContext)
    {
        if (outputContext == null)
        {
            throw new ArgumentNullException(nameof(outputContext));
        }

        foreach (var polyfill in _polyfills)
        {
            outputContext.WriteLine();

            Polyfill.Write(outputContext, polyfill);
        }
    }

    // ---- label bookkeeping for the labeled-jump downlevel ------------------------------------

    /// <summary>
    /// Records that a <c>goto</c> standing in for a labeled jump targets this label, so the loop
    /// only declares the labels something actually jumps to.
    /// </summary>
    /// <remarks>
    /// Declaring both synthetic labels unconditionally would trade a language feature for a pile
    /// of CS0164 warnings - a downlevel that compiles but that nobody would have written.
    /// </remarks>
    public void MarkLabelUsed(string label)
    {
        if (_discard)
        {
            return;
        }

        _labels.Add(label);
    }

    /// <summary>Whether <see cref="MarkLabelUsed"/> saw this label.</summary>
    public bool WasLabelUsed(string label) => _labels.Contains(label);

    /// <summary>Forgets a label once its declaration has been written.</summary>
    public void ClearLabel(string label)
    {
        if (_discard)
        {
            return;
        }

        _labels.Remove(label);
    }

    // ---- plumbing ---------------------------------------------------------------------------

    /// <summary>Records a diagnostic, ignoring an exact duplicate.</summary>
    public void Report(EmitDiagnostic diagnostic)
    {
        if (diagnostic == null)
        {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        if (_discard)
        {
            // The shared session records nothing at all: it is a static, and a static that
            // accumulates is a leak. An error still throws, and the exception carries the
            // diagnostic with it.
            return;
        }

        if (!_reported.Add(diagnostic.Id + "|" + diagnostic.Message))
        {
            return;
        }

        _diagnostics.Add(diagnostic);
    }

    /// <summary>The session attached to a context, or the one that records nothing.</summary>
    /// <remarks>
    /// This is how a node reaches its profile without the profile ever being on the tree, and
    /// without <see cref="IOutputContext"/> having to change shape.
    /// </remarks>
    public static EmitSession For(IOutputContext? outputContext) =>
        outputContext is IProfiledOutputContext profiled ? profiled.Session : Discarding;

    private void RequestPolyfills(LanguageFeature feature)
    {
        if (_discard || Profile.Polyfills == PolyfillMode.None)
        {
            return;
        }

        if (Profile.Polyfills == PolyfillMode.Auto && !IsPolyfillLikelyNeeded(feature))
        {
            return;
        }

        foreach (var polyfill in Polyfill.For(feature))
        {
            if (_polyfills.Contains(polyfill))
            {
                continue;
            }

            _polyfills.Add(polyfill);

            Report(new EmitDiagnostic(
                EmitDiagnostic.PolyfillEmittedId,
                EmitSeverity.Info,
                feature,
                LanguageFeatures.MinimumVersion(feature),
                Profile.EffectiveTarget,
                null,
                Polyfill.NamespaceOf(polyfill) + "." + Polyfill.NameOf(polyfill) +
                " emitted so " + LanguageFeatures.Get(feature).Syntax + " compiles at " +
                Profile.EffectiveTarget.ToDisplayName() + "."));
        }
    }

    private bool IsPolyfillLikelyNeeded(LanguageFeature feature)
    {
        // A language-version proxy for a target-framework question. The version that introduced
        // the feature is the one whose framework is most likely to predate the support type.
        var minimum = (int)LanguageFeatures.MinimumVersion(feature);

        return (int)Profile.EffectiveTarget < minimum + 100;
    }

    private bool AddNote(string note)
    {
        foreach (var existing in _notes)
        {
            if (string.Equals(existing, note, StringComparison.Ordinal))
            {
                return false;
            }
        }

        _notes.Add(note);

        return true;
    }

    private static string Describe(FeatureInfo info, string? context) =>
        string.IsNullOrEmpty(context)
            ? "'" + info.Syntax + "'"
            : "'" + info.Syntax + "' on " + context;
}
