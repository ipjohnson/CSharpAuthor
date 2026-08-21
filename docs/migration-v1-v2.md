# V1 -> V2 migration

## profiles (`EmitProfile`, §4)

### Snapshot diffs

One, and it is not generated code.

| Snapshot | Diff | Justification |
|---|---|---|
| `DependencyModules.Tests` `PublicApiTests.SourceGeneratorApi.verified.txt` | **+338 lines, -0 lines** | Purely additive. `EmitProfile` and everything around it are new public types, and this snapshot is the public surface of `CSharpAuthor` as source-compiled into the generator. Nothing was removed or changed shape, so no existing call site can break. **Not re-baselined** - the human approves this once, at the end, for all builders at once, since every builder that adds a public type lands in this same file. |

All nine of DependencyModules' **generator-output** snapshots are unchanged, and
`Hardened.SourceGenerator.Tests` is 468/468. Measured at commit `76b8dd0`:
DependencyModules 734/735 (net8.0 and net10.0), the one failure being the snapshot above.

### Behaviour changes

None for a caller who passes no profile. That is the rule the slice is built to, not a
coincidence:

- `EmitSession.For(context)` answers `EmitProfile.V1Compatible` when the context carries no
  profile - block namespaces, `Target = Latest`, no polyfills, no downlevel comments. Every
  writer that now asks the profile a question gets V1's answer back.
- `ProfileEmitter` is new API. Nothing existing routes through it.

With a profile in force, three existing writers behave differently, all newly:

| Writer | With a profile | V1 |
|---|---|---|
| `PropertyDefinition` | `init` becomes `set` below C# 9, with a `// DOWNLEVEL:` comment; the new `IsRequired` writes `required` from C# 11 | always `init`, no `required` at all |
| `InterfaceMethodDefinition` | a member with a body is a capability violation below C# 8; the new `IsStaticAbstract` writes `static abstract` from C# 11 | emits the body regardless, and drops every modifier |
| `ClassDefinition` | `record` demands C# 9, `record struct` C# 10, a primary constructor on a class C# 12, the new `IsRefStruct` C# 7.2 | emits all of them regardless of target |

### New API worth knowing about

- `EmitProfile.Conservative` / `.Default` / `.Latest` / `.V1Compatible`, `Clone()`, `With(...)`.
- `EmitProfile.FromEditorConfig(...)`, `FromEditorConfigText(...)`, `ToOutputContextOptions()`,
  `FromOutputContextOptions(...)`.
- `EmitSession`, `EmitDiagnostic`, `EmitCapabilityException`, `ProfileEmitter`, `EmitResult`.
- `ProfiledOutputContext`, `IProfiledOutputContext`, and the `EmitSession()` / `EmitProfile()`
  extensions on `IOutputContext`.
- `StringLiteralStatement.Quote(string)` - the escaping `SyntaxHelpers.QuoteString` never did.
  `QuoteString` is left alone; it is used by existing call sites and replacing it is the type
  model's call, not this slice's.

### For whoever wires the namespace writer

`EmitProfile.Default.FileScopedNamespace` is `true`, as §4 specifies. `NamespaceDefinition` does
**not** consult the profile today - deliberately. The moment it does, every consumer snapshot that
uses the default profile changes on formatting alone. Use `EmitProfile.V1Compatible` as the
no-profile fallback, which is what `EmitSession.For` already returns.
