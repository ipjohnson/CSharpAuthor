---
title: Emit profiles
---

# Emit profiles

An @CSharpAuthor.Profiles.EmitProfile is one object that says how a tree should be turned into a
file: how it is formatted, how types are qualified, **which version of C# it may use**, and which
of several legal spellings you prefer.

It is a *writer* argument. It is never stored on the tree, which is what lets the same tree be
emitted twice against two different targets.

```csharp
using CSharpAuthor.Profiles;

EmitResult result = ProfileEmitter.Emit(file, EmitProfile.Conservative);

string code = result.Code;
```

> [!NOTE]
> Every profile type lives in **`CSharpAuthor.Profiles`**, not in `CSharpAuthor`. Two of the names
> collide with Roslyn's — `LanguageVersion` with `Microsoft.CodeAnalysis.CSharp.LanguageVersion`,
> and `EmitResult` with `Microsoft.CodeAnalysis.Emit.EmitResult` — and inside a namespace nested
> under `CSharpAuthor` a `using X = …` alias cannot rescue you, because enclosing-namespace
> members outrank aliases. Hence the separate namespace, and one `using` per file.

## The presets

| | Target | Notes |
|---|---|---|
| `EmitProfile.Default` | C# 12 | file-scoped namespaces, `var`, target-typed `new`, collection expressions |
| `EmitProfile.Conservative` | C# 8 | block namespaces, no sugar |
| `EmitProfile.Latest` | latest | whatever the emitter knows about |
| `EmitProfile.V1Compatible` | latest | 1.x behaviour byte for byte: block namespaces, no polyfills, no downlevel comments |

**Calling `Output()` with no profile means `V1Compatible`**, not `Default`. That is deliberate: a
1.x call site that never mentions profiles keeps producing exactly what it produced before.

The four presets are **frozen**. Assigning to one throws, because they are shared and a mutation
would change every other caller's output. Take a copy:

```csharp
EmitProfile profile = EmitProfile.Conservative.With(p => p.NewLine = "\r\n");
```

`With(configure)` clones and configures; `Clone()` gives you an unfrozen copy; `Freeze()` makes one
read-only. A profile you construct yourself with `new EmitProfile { … }` is mutable until you
freeze it.

## Preference resolves against capability

The profile carries two different kinds of setting, and confusing them is the main way to get
surprised.

- **Capability** is `Target`. It says what the compiler on the other end will accept.
- **Preference** is `PreferVar`, `PreferCollectionExprs`, `PreferTargetTypedNew`,
  `PreferExpressionBodied`, `PreferRawStrings`, `FileScopedNamespace`. Each says which of several
  legal spellings you would rather have.

A preference is **never an error**. `PreferCollectionExprs = true` with `Target = CSharp8` emits
`new[] { … }`, silently and correctly, because that is what the same meaning looks like at C# 8. A
*capability* violation is a different matter, and is covered below.

Three query methods, all non-recording:

```csharp
profile.Supports(LanguageFeature.CollectionExpressions);   // does the target allow it
profile.Prefers(LanguageFeature.CollectionExpressions);    // do you want it
profile.CanEmit(LanguageFeature.CollectionExpressions);    // both
```

## Downlevelling

Take one tree and emit it twice:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/EmitProfileSamples.cs#same-tree-two-targets)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/emit-profiles-same-tree-two-targets.txt)]

Two different things happened there, and the difference is the point.

The file-scoped namespace became a block namespace **silently**. Those two mean the same thing, so
there is nothing to warn about.

`init` became `set`, and that is **not** the same thing — the property is no longer immutable
after construction. Where a downlevel changes meaning, the emitter says so in the file:

```text
// DOWNLEVEL: Name: 'init' unavailable below C#9 — emitted as a settable property, immutability lost
```

Every feature falls into one of three categories:

| Category | Examples | What happens below the minimum |
|---|---|---|
| **Free** | collection expressions, target-typed `new`, file-scoped namespaces, raw strings, `nameof`, `using` declarations, switch expressions, labeled jumps | the older spelling, no comment, an informational diagnostic |
| **Polyfillable** | `init`, `required` | the older spelling, a `// DOWNLEVEL:` comment, a warning, and optionally a support type |
| **Impossible** | `ref struct`, static abstract interface members, default interface members, function pointers, inline arrays, `record`, `record struct` | an **error**. Never wrong output |

### Polyfills

`init` at C# 9 works if `IsExternalInit` exists, which on an older target framework it may not.
`Polyfills = PolyfillMode.Auto` (the default) emits it when the target looks like it needs it:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/EmitProfileSamples.cs#polyfilled-init)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/emit-profiles-polyfilled-init.txt)]

The emitted support type is `internal`, sits in a block namespace, and is written in pre-C# 6
syntax, so emitting it can never itself need a newer language version. `PolyfillMode.None` turns
it off; `Always` emits regardless of target.

Note the namespace became block-scoped: C# does not allow a file-scoped namespace beside another
namespace declaration, so a file that gains a polyfill is re-rendered with block namespaces and
records an informational diagnostic saying so.

### Impossible features are errors

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/EmitProfileSamples.cs#capability-violation)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/emit-profiles-capability-violation.txt)]

There is no "carry on quietly" option. @CSharpAuthor.Profiles.CapabilityViolationBehavior has two
values: `Throw` (the default, raising `EmitCapabilityException`) and `EmitErrorDirective`, which
writes `#error` into the output and records the diagnostic. **A generator wants the second**, so it
can surface the problem against the user's compilation instead of crashing the build.

## The diagnostic channel

Everything the writer decided is recorded, whether or not it also appeared in the file:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/EmitProfileSamples.cs#diagnostic-channel)]

[!code-text[](../samples/CSharpAuthor.Docs.Samples/expected/emit-profiles-diagnostic-channel.txt)]

| Id | Severity | Meaning |
|---|---|---|
| `CSA0001` | Info | a free downlevel was taken; same meaning |
| `CSA0002` | Warning | a downlevel that changes meaning |
| `CSA0003` | Info | a support type was emitted alongside the output |
| `CSA1001` | Error | the target lacks the feature and there is no equivalent form |
| `CSA1002` | Error | the one available downlevel was ruled out by your settings |

Each diagnostic carries `Id`, `Severity`, `Feature`, `RequiredVersion`, `Target`, `Context` and
`Message`. In a generator, `ToDiagnostic(location)` from the [Roslyn
bridge](source-generators.md#the-roslyn-bridge) converts one into a
`Microsoft.CodeAnalysis.Diagnostic` you can report.

`DownlevelComments` controls where the `// DOWNLEVEL:` text goes: `Inline` (the default), or
`FileHeader` to collect them at the top, or `None` to emit no comments at all — the diagnostics
are still recorded either way.

## Matching the host project's formatting

`.editorconfig` is where a project already says how it wants its code laid out, so a profile can
be read straight out of it:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/EmitProfileSamples.cs#from-editorconfig)]

[!code-text[](../samples/CSharpAuthor.Docs.Samples/expected/emit-profiles-from-editorconfig.txt)]

Keys honoured: `indent_style`, `indent_size`, `tab_width`, `end_of_line`,
`csharp_new_line_before_open_brace`, `csharp_style_namespace_declarations`, the three
`csharp_style_var_*` keys, and the three `csharp_style_expression_bodied_*` keys. A trailing
`:severity` is stripped.

**Formatting only.** `Target` is never set from `.editorconfig`, because there is no
`.editorconfig` key for the language version. Inside a generator it comes from the compilation
instead — see [the Roslyn bridge](source-generators.md#the-roslyn-bridge).

### Known gap: three profile settings do not reach the writer

`EmitProfile.Braces`, `EmitProfile.AliasCollisions` and `EmitProfile.ContainingNamespace` are
stored on the profile and read from `.editorconfig`, but `EmitProfile.ToOutputContextOptions()`
carries six fields across and not those three. So a profile emitted through `ProfileEmitter` or
@CSharpAuthor.Profiles.ProfiledOutputContext formats with Allman braces regardless of what
`Braces` says — which you can see in the sample above, where `csharp_new_line_before_open_brace =
none` gave `profile.Braces=KAndR` and Allman output.

Until that is fixed, set those three on @CSharpAuthor.OutputContextOptions directly:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/EmitProfileSamples.cs#brace-style)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/emit-profiles-brace-style.txt)]

Brace style is applied at serialization time, like everything else, so it restyles a tree that was
already written.

## What "targets C# 15" does and does not mean

@CSharpAuthor.Profiles.LanguageVersion goes up to `CSharp15`, and the writer will emit for it.

Validation is another matter. The round-trip validator parses what it emits with
`Microsoft.CodeAnalysis.CSharp` 4.14.0, which knows language versions **up to C# 13**. Its
`LanguageVersion.Preview` cannot parse constructs a newer SDK compiler accepts. So:

> **Nothing above C# 13 is validated, and no C# 14 or C# 15 conformance is claimed from it.**

`LanguageVersionExtensions.IsValidatableByRoslyn414(version)` is the check in code.

## Reference

- @CSharpAuthor.Profiles.EmitProfile
- @CSharpAuthor.Profiles.ProfileEmitter
- @CSharpAuthor.Profiles.EmitSession — the lower-level channel, if you are writing a component
  that has to ask whether it may emit something
- @CSharpAuthor.Profiles.LanguageFeature — the capability table
