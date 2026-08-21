# V2 open questions

Decisions taken under §8.4 - the spec was silent, the V1-source-compatible option was taken, and
it is written down here rather than asked about.

## profiles (`EmitProfile`, §4)

| # | Question | Decision | Why |
|---|---|---|---|
| 1 | §4 declares the profile's members as public fields. | Public properties, and the presets are frozen: assigning to `EmitProfile.Default` throws and names `Clone()`. | The presets are shared. A field lets `EmitProfile.Default.IndentWidth = 2` change every other caller's formatting, and the place that gets found is a diff of a generated file. No V1 code exists to break. |
| 2 | What profile applies when a writer is given none? | `EmitProfile.V1Compatible`: block namespace, `Target = Latest`, no polyfills, no downlevel comments. Not `EmitProfile.Default`. | A caller who passed no profile must emit what it emitted before, byte for byte. `Default.FileScopedNamespace` is `true`, and defaulting to it would rewrite every consumer snapshot on formatting alone. |
| 3 | Where does a `// DOWNLEVEL:` comment go? | On the line above the member, by default. `DownlevelCommentPlacement.FileHeader` and `.None` are the alternatives. | A comment 200 lines from the property it is about is a comment nobody connects to anything. The header form is kept because the prototype used it. |
| 4 | When is a polyfill emitted? | `PolyfillMode.Auto` - the default - emits one when the target is the version that introduced the feature. `Always` and `None` are explicit. | Whether `IsExternalInit` is already there is a *target framework* question and a profile only knows the language version, so `Auto` is a proxy, not an answer. A netstandard2.0 generator emitting C# 12 wants `Always`. **This is the weakest guess in the slice** and the one most worth revisiting if the profile ever learns the target framework. |
| 5 | What happens on a capability violation? | Throw, by default. `CapabilityViolationBehavior.EmitErrorDirective` collects and writes `#error` instead. | A source generator cannot usefully throw, but it must not emit something that means something else either. Both branches end with somebody being told. |
| 6 | §4 has one `PreferExpressionBodied`; .editorconfig has seven `csharp_style_expression_bodied_*` keys. | Read `_methods`, falling back to `_properties`, then `_accessors`. | One flag cannot carry seven answers. Methods dominate generated files. |
| 7 | Three `csharp_style_var_*` keys, one `PreferVar`. | Read `_when_type_is_apparent`, falling back to `_elsewhere`, then `_for_built_in_types`. | Generated code declares locals where the type is apparent far more often than anywhere else. |
| 8 | `csharp_new_line_before_open_brace` accepts a comma list of contexts. | `all` -> Allman, `none` -> K&R, a partial list -> Allman if it names `types` or `methods`. | One brace style, and those are the two contexts a generated file is mostly made of. |
| 9 | Are `record` and `record struct` downlevellable? | No - categorised **impossible**. | Writing `class` instead compiles and is not a record: no value equality, no `with`, no deconstructor. Nothing in this library generates those, so there is no downlevel to take. |
| 10 | A primary constructor is *free* in the table, but `ClassDefinition` has no way to write it out as fields and a constructor. | `ClassDefinition` **demands** it rather than asking. | Dropping the parameters leaves a type with no way to construct it. A writer with no alternative is in the same position as one facing a `ref struct`, whatever the table says is possible in principle. |
| 11 | What are the labels a downlevelled labeled jump targets called? | `{label}_break` and `{label}_continue`, and only the ones something actually jumps to are declared. | Declaring both every time trades a language feature for a pair of CS0164 warnings on every loop. The names can collide with a caller's own label; that is the cost of the downlevel existing at all. |
| 12 | When is a raw string literal used? | Only when it saves escaping, the value has no carriage return or control character, and - for the single-line form - does not start or end with a quote. | A single-line raw literal whose content touches a quote cannot be fenced, and the padding trick that looks like it works pads the content. Declining is always safe: raw strings are a preference. |
| 13 | May the test project reference Roslyn? | Yes - `Microsoft.CodeAnalysis.CSharp` 4.14.0, test-time only. | §3 forbids the *shipped library* gaining a dependency; it still targets netstandard2.0 with none. Gate 3 - "every test that emits output parses and semantically compiles it" - cannot be met without a compiler in the test project. |
| 14 | Where does `EmitProfile.FromEditorConfig(AnalyzerConfigOptions)` live? | `CSharpAuthor/Roslyn/EmitProfile.Roslyn.cs`, as a partial of `EmitProfile`, compiled only when `PackageCSharpAuthorIncludeSource` **and** `PackageCSharpAuthorIncludeRoslyn` are both set. | §4 declares it as a member, which a separate assembly cannot add. A partial gives the declared signature; the cost is that the bridge is source-include-only, which is what §3 says it is anyway. |
| 15 | `LanguageVersion.Default` is `0`, and `target >= CSharp9` would make it mean "nothing is supported". | `EffectiveTarget` resolves it to C# 12; every capability check uses `EffectiveTarget`. | Silent wrongness is the defect class. An unspecified version resolving to "no features at all" would downlevel an entire file without saying anything. |

### Left undone, on purpose

- `BraceStyle.KAndR` is carried, mapped from .editorconfig and queryable, but `OutputContext`
  writes characters as it goes and can only produce Allman. `proto/deferred/DeferredContext.cs`
  already implements both; the profile is the object it should take instead of its own
  `StyleOptions`, whose fields are already named identically.
- `PreferVar`, `PreferExpressionBodied`, `FieldKeyword` and `ParamsCollections` are answered
  correctly by the capability table but no writer in this slice consults them - the writers that
  own those constructs have to.
