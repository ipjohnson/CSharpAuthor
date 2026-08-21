using System.Text;
using CSharpAuthor;
using CSharpAuthor.Profiles;

namespace CSharpAuthor.Docs.Samples;

/// <summary>Samples for docfx/docs/emit-profiles.md.</summary>
public static class EmitProfileSamples
{
    /// <summary>One tree, two language versions.</summary>
    public static string SameTreeTwoTargets()
    {
        #region same-tree-two-targets
        static CSharpFileDefinition BuildFile()
        {
            var file = new CSharpFileDefinition("Acme.Model");

            var pet = file.AddClass("Pet");
            pet.Modifiers |= ComponentModifier.Public;

            var name = pet.AddProperty(TypeDefinition.Get(typeof(string)), "Name");
            name.Modifiers |= ComponentModifier.Public;
            name.Set!.IsInit = true;        // `init` - C# 9 and above

            return file;
        }

        // C# 12: `init` is available, and the namespace can be file-scoped.
        EmitResult modern = ProfileEmitter.Emit(BuildFile(), EmitProfile.Default);

        // C# 8: neither is. The same tree, unchanged.
        EmitResult conservative = ProfileEmitter.Emit(BuildFile(), EmitProfile.Conservative);
        #endregion

        return "=== EmitProfile.Default (C#12) ===\n" + modern.Code
             + "\n=== EmitProfile.Conservative (C#8) ===\n" + conservative.Code;
    }

    /// <summary>`init` at C# 9, where it costs a support type rather than a meaning change.</summary>
    public static string PolyfilledInit()
    {
        #region polyfilled-init
        var file = new CSharpFileDefinition("Acme.Model");

        var pet = file.AddClass("Pet");
        pet.Modifiers |= ComponentModifier.Public;

        var name = pet.AddProperty(TypeDefinition.Get(typeof(string)), "Name");
        name.Modifiers |= ComponentModifier.Public;
        name.Set!.IsInit = true;

        var profile = EmitProfile.Default.With(p =>
        {
            p.Target = LanguageVersion.CSharp9;
            p.Polyfills = PolyfillMode.Auto;    // the default
        });

        EmitResult result = ProfileEmitter.Emit(file, profile);
        #endregion

        return result.Code;
    }

    /// <summary>Everything the writer decided, and why.</summary>
    public static string DiagnosticChannel()
    {
        #region diagnostic-channel
        var file = new CSharpFileDefinition("Acme.Model");

        var pet = file.AddClass("Pet");
        pet.Modifiers |= ComponentModifier.Public;

        var name = pet.AddProperty(TypeDefinition.Get(typeof(string)), "Name");
        name.Modifiers |= ComponentModifier.Public;
        name.Set!.IsInit = true;
        name.IsRequired = true;             // `required` - C# 11 and above

        EmitResult result = ProfileEmitter.Emit(file, EmitProfile.Conservative);

        foreach (EmitDiagnostic diagnostic in result.Diagnostics)
        {
            // diagnostic.Id      CSA0001 info / CSA0002 warning / CSA0003 info
            //                    CSA1001 error / CSA1002 error
            // diagnostic.Feature which language feature was asked for
            // diagnostic.RequiredVersion / .Target  what it needs, and what you asked for
        }

        bool everythingEmitted = !result.HasErrors;
        #endregion

        var report = new StringBuilder();
        report.Append("HasErrors: ").Append(everythingEmitted ? "false" : "true").Append("\n\n");

        foreach (var diagnostic in result.Diagnostics)
        {
            report.Append(diagnostic.Severity).Append("  ").Append(diagnostic.Id)
                  .Append("  ").Append(diagnostic.Feature)
                  .Append("  needs ").Append(diagnostic.RequiredVersion.ToDisplayName())
                  .Append(", targeting ").Append(diagnostic.Target.ToDisplayName())
                  .Append("\n    ").Append(diagnostic.Message).Append('\n');
        }

        return report.ToString();
    }

    /// <summary>A feature with no downlevel form is an error, never wrong output.</summary>
    public static string CapabilityViolation()
    {
        #region capability-violation
        var file = new CSharpFileDefinition("Acme.Model");

        // `record` did not exist before C# 9, and there is no way to write one that does.
        var pet = file.AddRecord("Pet");
        pet.Modifiers |= ComponentModifier.Public;
        pet.AddProperty(TypeDefinition.Get(typeof(string)), "Name").Modifiers |= ComponentModifier.Public;

        var profile = EmitProfile.Conservative.With(p =>
        {
            // The default is CapabilityViolationBehavior.Throw, which raises
            // EmitCapabilityException. A generator usually wants the diagnostic instead, so it
            // can report it against the user's compilation rather than crash the build.
            p.OnCapabilityViolation = CapabilityViolationBehavior.EmitErrorDirective;
        });

        EmitResult result = ProfileEmitter.Emit(file, profile);

        bool failed = result.HasErrors;     // true
        #endregion

        var report = new StringBuilder();
        report.Append("HasErrors: ").Append(failed ? "true" : "false").Append('\n');

        foreach (var diagnostic in result.Diagnostics)
        {
            report.Append(diagnostic.Severity).Append("  ").Append(diagnostic).Append('\n');
        }

        report.Append("\n=== emitted ===\n").Append(result.Code);

        return report.ToString();
    }

    /// <summary>Matching the host project's formatting instead of guessing at it.</summary>
    public static string FromEditorConfig()
    {
        #region from-editorconfig
        const string EditorConfig = """
            root = true

            [*.cs]
            indent_style = space
            indent_size = 2
            end_of_line = lf
            csharp_new_line_before_open_brace = none
            csharp_style_namespace_declarations = block_scoped:suggestion
            """;

        EmitProfile profile = EmitProfile.FromEditorConfigText(EditorConfig);

        var file = new CSharpFileDefinition("Acme.Model");

        var pet = file.AddClass("Pet");
        pet.Modifiers |= ComponentModifier.Public;
        pet.AddProperty(TypeDefinition.Get(typeof(string)), "Name").Modifiers |= ComponentModifier.Public;

        EmitResult result = ProfileEmitter.Emit(file, profile);
        #endregion

        return "profile.IndentWidth=" + profile.IndentWidth
             + "  profile.Braces=" + profile.Braces
             + "  profile.FileScopedNamespace=" + profile.FileScopedNamespace
             + "\n\n" + result.Code;
    }

    /// <summary>
    /// Brace style, collision aliasing and the containing namespace reach the writer through
    /// <see cref="OutputContextOptions"/>, not through the profile. See the note in
    /// docfx/docs/emit-profiles.md.
    /// </summary>
    public static string BraceStyleThroughOptions()
    {
        #region brace-style
        var file = new CSharpFileDefinition("Acme.Model");

        var pet = file.AddClass("Pet");
        pet.Modifiers |= ComponentModifier.Public;
        pet.AddProperty(TypeDefinition.Get(typeof(string)), "Name").Modifiers |= ComponentModifier.Public;

        var output = new OutputContext(new OutputContextOptions
        {
            BraceStyle = BraceStyle.KAndR,
            IndentCharCount = 2,
        });

        file.WriteOutput(output);
        string code = output.Output();
        #endregion

        return code;
    }
}
