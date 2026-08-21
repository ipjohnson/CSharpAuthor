# V2 open questions

Every decision taken under V2-HANDOFF.md §8.4 — "when the spec is silent, choose the option
that keeps V1 source-compatible, and record it here. Do not stop to ask."

One section per agent, so nine clones merging into one branch do not fight over the same
lines. Append to your own.

---

## migrator

### 1. `EmitProfile.Default.FileScopedNamespace = true` vs. the existing `Output()` overloads

**Silent on:** whether the profile defaults apply to the `Output()` calls consumers already
make, or only to calls that pass a profile explicitly.

**Observed:** every one of DependencyModules' 9 generator snapshots is block-scoped
(`namespace TestNamespace {`). §4 of the handoff gives `EmitProfile.Default` a
`FileScopedNamespace = true`. If `Default` is what an un-parameterised `Output()` picks up,
all 9 snapshots diff on formatting alone, on day one, for no semantic reason — and the noise
buries whatever real diff arrives next.

**Default taken (V1-source-compatible):** the existing `Output()` overloads keep V1's
behaviour, i.e. block-scoped namespaces and V1's current formatting. `EmitProfile.Default`
describes what a *new* caller gets when it asks for a profile; it does not retroactively
change what an old call site emits. File-scoped namespaces are opt-in.

**Who decides otherwise:** Ian. If he wants the new default to be the default everywhere, it
is one line and a snapshot review — but that review is a human's call under §8.1, not an
agent's.

### 2. Namespace for `EquatableArray<T>`

**Silent on:** where §7's new `EquatableArray<T>` lives.

**Observed:** Hardened's generator assemblies already source-include an `EquatableArray<T>`
from `ValidationModules.SourceGenerator.Impl` (used at
`Hardened.SourceGenerator/Validation/HandlerValidationFrontEnd.cs:96,181`). Today no file in
that assembly imports both namespaces, so there is no collision — but `CSharpAuthor.EquatableArray<T>`
would put one `using CSharpAuthor;` between Hardened and CS0104, in a file nobody would think
to look at.

**Default taken (V1-source-compatible, and consumer-safe):** do not put it in the root
`CSharpAuthor` namespace. A sub-namespace, or a name that cannot collide, costs nothing now
and cannot break a consumer later. Recorded in `docs/migration-v1-v2.md` §4.1 A4.

Decisions taken under §8.4 (the spec was silent; the V1-source-compatible option was taken)
and recorded here rather than blocking on a question.

## Gate 9 — the performance benchmark

1. **§10 says "no worse than V1: ≤ 0.048 ms and ≤ 78 KB per file". Absolute or relative?**
   Taken as **relative**, and the harness reports it that way. Measured on this machine, V1
   itself runs the §10 payload at 0.0125 ms/file — roughly four times under the absolute time
   bar — so a V2 three times slower than V1 would still "pass" an absolute reading of it. The
   allocation figure does transfer (77.4 KB here vs the handoff's 78.4 KB), because allocation
   is a property of the code rather than of the machine. `scripts/run-benchmark.sh` therefore
   takes two checkouts and measures them interleaved in one run, and refuses to issue a gate
   verdict from a single checkout. Recorded numbers: `benchmarks/baseline-v1.txt`.

2. **Which statistic is "ms/file"?** The **median** of the per-iteration samples. On a machine
   running other work, the mean of 2,000 samples is set by a handful of multi-millisecond
   outliers (OS descheduling, gen2 GC) rather than by the code; medians reproduce to within
   ~3% across runs where means swing by 50%. Mean and a 5%-trimmed mean are both printed
   alongside it, so nothing is hidden. Allocation is reported as a straight mean, since
   `GC.GetAllocatedBytesForCurrentThread()` deltas are deterministic — 77.430 KB in every
   run so far.

3. **What exactly is inside the timed region?** Building the payload tree *and* serialising it
   — one call is one generated file. The `ITypeDefinition` instances are constructed once and
   hoisted to statics, because real generators hold their types in a static holder and because
   `TypeDefinition.Get(typeof(T))` is `System.Type` reflection that is identical in V1 and V2.

4. **The §10 payload's exact contents.** §10 fixes the shape (one class, 25 init-only
   properties, a constructor assigning all of them, a method with 27 statements) but not the
   names or types. The harness pins them in `benchmarks/CSharpAuthor.Benchmark/TreePayload.cs`:
   11 distinct property types across `System` and `System.Collections.Generic`, and 27
   top-level statements of which 5 open a nested block (if/else, foreach, while, try/catch,
   if). That file is always taken from the harness's own checkout, never from the library
   checkout under measurement, so V1 and V2 are handed the identical payload.

5. **Which API the payload uses.** Only V1 surface: `CSharpFileDefinition`, `AddClass`,
   `AddProperty` + `Set.IsInit`, `AddConstructor`/`AddParameter`, `Assign().To()`/`.ToVar()`,
   `AddMethod`/`SetReturnType`, `AddIndentedStatement`, `If`/`Else`/`ForEach`/`While`/`Try`,
   `SyntaxHelpers`, `OutputContext`. Nothing was missing — the payload expresses §10 exactly,
   with no substitutions. **If V2 changes any of these signatures the harness stops compiling,
   which is itself the source-compatibility signal.**
Defaults taken where the handoff was silent, each with the reasoning, for a human to confirm or
overturn. Every one of these took the option that keeps V1 source-compatible.

<!-- Each build area appends its own section. Keep sections separate so they merge cleanly. -->

## Type model

### `nint`/`nuint` need a language-version gate that only the profile can apply

§7 lists `nint`→`IntPtr` as a missing-keyword defect, so `typeof(IntPtr)` now writes `nint`.
Unlike `float`, `char` and `sbyte` — C# 1 keywords, safe everywhere — `nint` and `nuint` need
**C# 9** in the consuming code, and reflection cannot distinguish `nint` from `IntPtr` to let the
caller choose.

**Taken:** always write the keyword, as §7 asks.
**For the `profiles` agent:** this is a capability-gated keyword. `EmitProfile.Target < CSharp9`
should select `IntPtr`/`UIntPtr`. The choice belongs in the writer, not the tree — the type model
holds one value for the type either way.

### `EquatableArray<T>` lives in `CSharpAuthor.Collections`, not `CSharpAuthor`

§7 says the type belongs "beside `ITypeDefinition`", which reads as the `CSharpAuthor` namespace. It is
in `CSharpAuthor.Collections` instead.

CSharpAuthor is *source-compiled into* its consumers, and `Hardened.Framework`'s generators already
source-include an `EquatableArray<T>` of their own (`ValidationModules.SourceGenerator.Impl`, used in
`HandlerValidationFrontEnd.cs`). Nothing breaks today because no file there imports both namespaces —
but in the bare `CSharpAuthor` namespace the two would be one `using CSharpAuthor;` away from CS0104
in a repo that includes both, and the point of adding the type is to let those generators *delete*
their hand-written comparers.

**Taken:** a sub-namespace. Consumers add `using CSharpAuthor.Collections;` where they want it, and
can adopt it file by file while their own version still exists. If the human prefers it in
`CSharpAuthor`, the move is one line plus a `using` in each consumer that adopts it.

### New public surface trimmed to what §7 mandates

`DependencyModules.Tests` snapshots the generator assembly's public API, and CSharpAuthor is
source-compiled into it, so every public member here lands in that snapshot. Rather than approve a
wider diff, anything §7 does not name was demoted before the diff was recorded: the write and rank
helpers on `BaseTypeDefinition` and its two rank-carrying constructors are `private protected`, the
rank-carrying `TypeParameterDefinition` constructor is `internal`, and the `ToEquatableArray`
extension method was deleted in favour of `EquatableArray<T>.From`.

**Taken:** demote now. §3 sets the precedent ("mark generated node types `internal` when
source-included so they don't leak into consumer API surface"), both consumers source-include the
library so `internal` remains fully usable to them, and widening later is not a breaking change while
narrowing is. The 1.x `protected` `BaseTypeDefinition` constructor is untouched, so an outside
subclass keeps the entry point it always had.

### Nullability sits on the array, not on the element

`ITypeDefinition` carries one `IsNullable` flag, and it is written after the array ranks, so
`Get(typeof(int)).MakeNullable().MakeArray()` writes `int[]?` — a nullable array of `int` — not
`int?[]`, an array of nullable `int`. The two are different types. `MakeArray().MakeNullable()` also
writes `int[]?`, which is right, so the flag is not wrong so much as unable to express one of the two
readings.

**Taken:** V1 behaviour preserved exactly — nullability always applies to the outermost array. Fixing
it means a nullability marker per array rank plus one for the element, which changes `IsNullable`'s
meaning for every caller. Not in the §7 defect list, and no consumer writes `int?[]` today.

### Interface additions over base-class-only extension

`ContainingType`, `ArrayRanks` and `MakeArray(int rank)` went on `ITypeDefinition`, which breaks
outside implementors of the interface (`netstandard2.0` has no default interface members).

**Taken:** put them on the interface. Everything in the library and in both consumers passes types
around as `ITypeDefinition`, so members reachable only through `BaseTypeDefinition` would be
unreachable at every call site that matters — the bridge could build a nested type but nothing
downstream could read it. Verified: neither `DependencyModules` nor `Hardened.Framework` implements
`ITypeDefinition`; both only construct through `TypeDefinition.Get` and `new GenericTypeDefinition`,
whose existing signatures are untouched.

### `ToString()` on a type definition keeps its 1.x shape

`$"{Namespace}.{Name}"` hashes `int` and `int[]` — and `Ns.Outer.Inner` and `Ns.Other.Inner` — to the
same value, because `GetHashCode` was `ToString().GetHashCode()`. The first attempt made `ToString()`
the fully qualified C# name; **`Hardened.SourceGenerator.Tests` caught it**.
`HardenedMethodDefinition` builds its own `ToString()` and its cache key out of the return type's, and
asserts the result is `"System.Void Configure()"` — where C# says `void`.

**Taken:** `ToString()` reverted to the 1.x shape exactly, and hashing moved to a private key that is
the fully qualified C# name with containers, generic arguments and array shape in it. Equal values
always agree on either form, so the equality contract holds under both; the private key just stops
every newly distinguishable type landing in one bucket. `WriteTypeName` remains the only thing that
produces C#.

This is worth a human's attention: **`ToString()` on a type definition is public API that a consumer
asserts on**, so it is not a debugger convenience and cannot be improved silently.
Defaults taken under V2-HANDOFF.md §8.4, where the spec was silent. Each took the
option that keeps V1 source-compatible, and each is cheap to reverse.

## Declarations, literals and statements

Owner: `declarations` builder.

### 1. `double` literals carry a `d` suffix

`1.5` became `1.5d`. A bare `1.0` for a `double` emits `1`, which is
indistinguishable from `Get(1)` — so the source type is lost, and where the text
lands in an argument position, `1` binds `Foo(int)` while `1d` binds
`Foo(double)`. Suffixing keeps the literal denoting the type it came from, and
matches `f`, `m`, `L`, `U`, `UL`.

Reversible: drop the suffix in `LiteralFormatter.FormatDouble`. **This is the
change most likely to show up in a consumer snapshot diff.**

### 2. Float and double use the `"R"` format

`"R"` is shortest-round-trippable on .NET Core. On .NET Framework — which is
where a source generator runs inside Visual Studio — `"R"` has a known precision
bug for which the documented workaround is `G9`/`G17`. `G17` was not chosen
because it prints `0.1` as `0.10000000000000001`, which is worse for every
ordinary value.

If a Framework-hosted generator is ever shown to emit a wrong double, switch to
`G9`/`G17`.

### 3. `partial` alone does not remove a method body

Marking a method `partial` still writes its body. The defining half — the one
that ends at `;` — is asked for with the new `MethodDefinition.OmitBody`.

The alternative, inferring "no statements means defining declaration", would
silently change what an existing caller emits, and a partial *implementation*
with an empty body is equally legal, so the inference has no right answer.

### 4. `for(` matches the existing house style

The library emits `while(` and `foreach(` with no space, so `for(` was written
to match rather than introducing a third convention. C# convention is `for (`.

All three should change together when the formatting pass lands (§4
`EmitProfile`), not one at a time.

### 5. Only reserved words are `@`-escaped

C#'s contextual keywords — `value`, `var`, `record`, `async`, `where`, `nint`,
`required`, `init` — are legal identifiers as they stand. Escaping them would
add noise to the output for no gain. Only the 77 reserved words are escaped.

### 6. Reference sites leave `this`, `base`, `null`, `true`, `false`, `default` alone

These arrive at `InstanceDefinition` as expressions rather than as names, and
`@this.Foo()` is not `this.Foo()`. Declaration sites escape them, because a
parameter genuinely named `this` does need the prefix.

The risk in the other direction is a caller who really did name a field `default`
and refers to it through a bare `InstanceDefinition`; that reference will not be
escaped. Naming a field after one of these six and reaching it without a
qualifier is rare enough to prefer not breaking expressions.

### 7. Non-finite floats emit their named form

There is no C# literal for NaN or infinity, so `float.NaN`,
`float.PositiveInfinity` and the `double` equivalents are emitted as member
accesses. This is the only place the formatter emits something that is not a
literal.

### 8. The incidental helpers are `internal`, not `public`

`LiteralFormatter`, `CSharpIdentifier` and `ComponentModifierExtensions` were
written `public` and are now `internal`. None of them is something §7 asks for:
§7 mandates the *behaviour* (escaped strings, invariant numbers, suffixed
literals, `@`-prefixed keywords), and that reaches callers through API that was
already public — `CodeOutputComponent.Get`, `SyntaxHelpers.QuoteString`, and the
writers themselves.

Making them public would have committed the project to supporting three helper
surfaces forever in exchange for nothing. The consumers **source-include** this
library (§3), so they can still reach every one of them; only the compiled
package's public surface shrinks. §3 already sets this precedent for the
generated grammar nodes: *"mark generated node types `internal` when
source-included so they don't leak into consumer API surface."*

`CSharpAuthor.Tests` reaches them through an `InternalsVisibleTo` declared as an
**MSBuild item**, not a source attribute, so the attribute is generated into
`obj/` and is not one of the `.cs` files a consumer compiles in.

Reversible: `internal` → `public` is not a breaking change, so any of these can
be promoted later if a consumer turns out to want it.

### 9. `MethodDefinition.OmitBody` stays public

It is the only way to express the *defining* half of a `partial` method — the
one that ends at `;`. §7 requires `partial` on methods to work, and emitting the
keyword alone does not achieve that: two implementing halves is CS0111. So this
is mandated in substance even though §7 does not name it.

It is the one member in the public-API diff that is a judgement call rather than
a literal §7 line item.

### 10. `MethodDefinition.IsBodyless` is `private`, not `protected virtual`

Written `protected virtual` out of habit. Nothing overrides it, and `private` →
`protected` is a non-breaking change later while the reverse is not, so it
starts private and stays off the public surface. `OmitBody` already gives
callers the control.

### 11. `AddCode` placeholder matching is ordinal

`AddCode` located its `{argN}` and `[argN]` placeholders with
`StringComparison.CurrentCulture`. Finding a fixed placeholder is an exact-text
question, so it is now `Ordinal`.

This also matters for a reason invisible to gate 1: both consumers build with
`EnforceExtendedAnalyzerRules=true`, which makes culture-dependent APIs hard
errors, and the library is compiled *into* them from source.
> Defaults taken under §8.4 — "when the spec is silent, choose the option that keeps V1 source
> compatible". Sections below are contributed by the `output-context` builder; other builders add
> their own.

## Output context

### 1. "`Global` mode emits no usings" — does that include the ones the caller asked for?

**Taken:** no. A namespace derived from a type is not emitted in a qualifying mode; a namespace asked
for by name (`AddUsingNamespace`, `AddImportNamespace(string)`) still is, controlled by
`OutputContextOptions.EmitExplicitUsings`, default `true`.

**Why:** an extension method is only reachable through a `using`; `global::` cannot name one. Both
consumers depend on this — `DependencyFileWriter` asks for
`Microsoft.Extensions.DependencyInjection.Extensions` by name so `TryAddSingleton` resolves, and
`Hardened.SourceGenerator` does the same in a dozen places. Dropping those would break files that a
purely derived model has no way to fix. The stray directive the handoff identifies is the *derived*
one, and that is gone unconditionally.

**If the human disagrees:** set `EmitExplicitUsings = false` in the two generators' options, and the
mode emits nothing at all.

### 2. Should the file's own namespace be dropped from the using list?

**Taken:** only when asked. `OutputContextOptions.ContainingNamespace` is `null` by default, so the
V1 output — which imports the file's own namespace if a type in it is written — is unchanged.

**Why:** `NamespaceDefinition` knows the name and could set it automatically, but a redundant
directive is harmless and a dropped one that someone was relying on is not. Turning it on is one
line at each call site.

### 3. Which side of a collision keeps the plain name?

**Taken:** the one written first, unless a type with no namespace is in the group, in which case that
one keeps it (a keyword type or a generic parameter names itself and cannot be aliased). If the
losing namespace has to stay imported because something else in it is still written plainly, *both*
sides are aliased.

**Why:** deterministic — it depends only on write order, which depends only on the tree — and it
keeps the common case reading naturally. The alternative, aliasing every contender always, is
uglier for no gain when the losing namespace can simply be dropped.

### 4. What does an alias get called?

**Taken:** the last segment of the namespace, then the last two, and so on until it is unique, with
the short name appended: `Second.Model` → `SecondModel`; `Company.Web.Models.Widget` → `ModelsWidget`.
Falls back to `NameAlias`, `NameAlias2`, … if the namespace runs out.

**Why:** it is what a person writing the alias by hand would pick, and it is stable across runs.
Not specified anywhere; if a house style wants something else, `MakeAlias` is the one place.

### 5. Colliding generics

**Taken:** written with their namespace in front (`First.Box<int>`) rather than aliased.

**Why:** a `using` alias names a closed type, so aliasing `Box<T>` would have to pick one closing and
would then be wrong everywhere else. Qualifying is correct in every case. Both sides are qualified,
not just one, because leaving one bare with both namespaces imported is still CS0104.

### 6. Perf against gate 9 (≤ 0.048 ms and ≤ 78 KB per file)

**Not measured.** There is no benchmark project in this repository, and the handoff does not say
where the §10 payload lives. The segment list is a `List<Segment>` of a readonly struct — one array,
no object per write — and the no-collision path calls `ITypeDefinition.WriteTypeName` straight into
the output builder, exactly as V1 did. Allocation is one array of ~32-byte structs plus V1's
`StringBuilder`, so a regression is possible and is **unverified either way**. Whoever owns gate 9
should measure this before the PR claims it.

### 7. Ordering of the `using` list

**Taken:** ordinal.

**Why:** V1 sorted with `List<string>.Sort()`, which is culture-aware, so the order of a generated
file could depend on the culture the generator ran under — the same defect class as §7's
culture-dependent numbers. For every namespace either consumer actually emits, ordinal and
culture-aware agree, so this is a determinism fix with no observed output change. A namespace pair
that differs only in punctuation (`A.B` against `AB`) could order differently than under V1.

### 8. `CSharpIdentifier` is copied, not shared

`CSharpAuthor/CSharpIdentifier.cs` is the `declarations` builder's file, copied byte-identical into
this branch so it compiles standalone — the `using` directives need the same escaping the namespace
declaration gets, and writing a second escaper would be worse. The two copies are the same file and
merge cleanly; if `declarations` changes it, theirs wins.
# V2 open questions — defaults taken where the spec was silent

Each entry records a decision made under handoff rule 8.4: the spec did not say, so the
option that keeps V1 source-compatible was taken and written down rather than asked about.

<!-- Sections are agent-scoped so that concurrent builders append rather than collide. -->

## expressions

### 1. The expression layer lives in `CSharpAuthor.Expressions`

§2 names three namespaces — `CSharpAuthor`, `CSharpAuthor.Syntax`, `CSharpAuthor.Roslyn` —
and does not say which one holds the expression combinators.

Taken: a new `CSharpAuthor.Expressions`. The root namespace already contains V1 type names
(`NewStatement`, `TypeStatement`) that a combinator layer would collide with, and
`CSharpAuthor.Syntax` belongs to the generated grammar, whose node names include
`BinaryExpression`, `SwitchExpression` and so on. V1 source compatibility is exact:
nothing in the root namespace was added, removed or changed.

### 2. Role interfaces are `IExpressionNode` / `IStatementNode` / `IPatternNode`

Invariant 4 says `Raw` implements `IExpression`, `IStatement` and `IPattern`. The generated
grammar declares interfaces with exactly those names in `CSharpAuthor.Syntax`, and two
identically named interfaces in two imported namespaces make every call site ambiguous.

Taken: distinct names here, and every public type in the layer is `partial`. Attaching the
generated interfaces at integration is a one-line file per type and needs no edit to the
expression sources. See the integration note in the agent report.

### 3. Parentheses are minimal, not defensive

The spec asks that emitted text re-parse to the same tree. It does not say whether to add
brackets that are merely reassuring.

Taken: minimal. A bracket appears exactly where dropping it would change the tree.
`a ?? b ?? c` and `a ? b : c ? d : e` are emitted bare, because `??` and `?:` are
right-associative and those nestings are the ones the language already gives for free.
`Ex.Paren` is available where an author wants grouping for a reader rather than a parser,
and explicit brackets are always preserved.

### 4. `Raw` infers its precedence, and defaults to bracketing when it cannot

V1's `CodeOutputComponent` treats every fragment as an atom. That is wrong for
`Get("a + b")` used as an operand, and wrong silently.

Taken: `Raw` reads the shape of its own text. A member chain, a call, a literal or a
keyword primary is `Primary`; a `?.` chain is `NullChain`; a prefix operator is `Unary`;
anything with a token left over is `Lowest`, which brackets. This diverges from V1's
assumption deliberately, and in the safe direction — the failure mode becomes a redundant
pair of brackets instead of a reassociated expression. Two carve-outs keep V1 behaviour:
an opaque `IOutputComponent` part is still treated as an atom, because its text cannot be
inspected, and `Raw.At` lets an author asserts a precedence outright.

### 5. `-(-a)` rather than `- -a`

Both are valid C#, and `--a` is neither. Taken: brackets. A space is load-bearing
punctuation that a later reformat can eat; a bracket is not.

The hazard is per-operator, so `!` and `~` do not bracket a unary operand — `!!a` and
`~-a` are emitted bare, since neither can re-lex.

### 6. Switch expressions render one arm per line by default

`Ex.Switch` writes multi-line at the surrounding indent, which is how the construct is
normally read. `Ex.SwitchInline` writes one line, which is what an assertion in a test
usually wants.

### 7. Argument modifiers and `throw` are never bracketed

`f((out x))` and `a ?? (throw new T())` are both compile errors — the second is CS8115,
verified. Nodes for `out`/`ref`/`in`/named arguments and for `throw` expressions carry a
flag that suppresses bracketing entirely, so an operand rule cannot make them invalid.
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
Defaults taken under V2-HANDOFF.md §8.4 — *when the spec is silent, choose the option
that keeps V1 source-compatible, record it, and do not stop to ask.*

One section per agent. When this file conflicts on merge, the resolution is to keep
both sides' sections; nothing here depends on anything above it.

---

## grammar

### 1. Accessor lists use block braces, so an auto-property spans four lines

**Chosen:** `AccessorListSyntax` gets Allman block braces like any other node whose braces
enclose an unseparated list of nodes.

```csharp
public int Count
{
    get;
    set;
}
```

**Why:** the alternative — inline braces, giving the familiar `public int Count { get; set; }`
— is better for auto-properties and clearly worse for an accessor with a body, which then
reads as

```csharp
public int Count { get
{
    return _count;
}
}
```

Deciding per-instance needs to know whether any accessor has a body *before* the opening
brace is written, and the writer streams into `IOutputContext` without buffering, so it
cannot look ahead and cannot retract a newline it has already emitted. Block braces are
always valid and never ugly; inline braces are sometimes prettier and sometimes bad.

**Nothing regresses today:** V1's `PropertyDefinition` facade still emits `{ get; set; }`
and that is what both consumers use. All nine DependencyModules generator-output snapshots
are byte-identical.

**To choose differently:** this is `EmitProfile` territory (§4) — a formatting preference,
not a capability. Either add a brace-style option that the writer consults, or give the
writer a bounded lookahead: a marker interface the generator assigns structurally to any
node that has both a body field and a semicolon field, exposing "am I body-less", plus a
`BraceGroup` emission for the shape `{ OpenBrace, SyntaxList<T>, CloseBrace }`. The
generator can express both; neither was worth destabilising a green consumer run for.

### 2. A property initializer after an accessor list lands on its own line

**Chosen:** accepted.

```csharp
public string Name
{
    get;
    set;
}
= "";
```

**Why:** same root cause as (1). `IOutputContext.CloseScope()` writes the indent, the
brace and a newline as one operation, so anything following a block brace necessarily
starts a new line. Using `CloseScope` is deliberate — V2-HANDOFF.md asks for indentation
through the context's scope markers rather than a depth counter in the writer, and a
segment-based context needs those markers to restyle output later.

Valid C#, and verified to compile.

**To choose differently:** make `CloseBrace` request a pending line break instead of
emitting one, and let the containing list style supply the break. That works for
statements, members and catch clauses, but changes `}` + `else` from Allman to K&R, so it
needs the brace-style option from (1) landing first.

### 3. Brace style is Allman, and not configurable yet

**Chosen:** Allman, unconditionally, matching V1's existing output.

**Why:** `OutputContextOptions` has no `BraceStyle` today, and `EmitProfile` (§4) is the
profiles agent's slice. The writer funnels every block brace through two methods, so the
switch point is one place when the option arrives.

### 4. `CSharpAuthor.Syntax.Attribute` collides with `System.Attribute`

**Chosen:** keep the grammar's own name. `AttributeSyntax` → `Attribute`, as with every
other node.

**Why:** it is the only collision — checked against every common `System` type, 1 of 250
class names. Renaming one node to dodge it would make the mapping from `Syntax.xml` to
class name conditional, which is exactly the kind of special case that stops a
regeneration being mechanical. A caller who has `using System;` in scope writes
`using Attr = CSharpAuthor.Syntax.Attribute;`, and there is a test covering it.

**To choose differently:** keep the `Syntax` suffix on every generated class name
(`AttributeSyntax`, `ClassDeclarationSyntax`, …). Collision-free and familiar to anyone who
knows Roslyn, at the cost of verbosity everywhere.

### 5. `SimpleNameSyntax` / `IdentifierNameSyntax` slots take a `TypeRef`

**Chosen:** every type-shaped slot in the grammar — `TypeSyntax`, `NameSyntax`,
`SimpleNameSyntax`, `IdentifierNameSyntax`, `ArrayTypeSyntax` — takes a `TypeRef`, which
holds either an unrendered `ITypeDefinition` or a type node.

**Why:** it makes the deferral point uniform, and `TypeRef` converts implicitly from
`string`, so `new MemberAccessExpression(x, ".", "Length")` reads naturally. The oddity is
that a *member* name is not a type; it travels through
`IOutputContext.Write(ITypeDefinition)` with an empty namespace, which is a no-op for
namespace derivation.

**To choose differently:** split the slot kinds — `TypeRef` for `TypeSyntax`/`NameSyntax`,
a plain `ISimpleName?` node reference for the rest. More correct, more verbose at every
call site.

### 6. A trailing line break at the very end of output is dropped

**Chosen:** a line break with nothing after it never materialises, so a fragment never
carries trailing whitespace. A file that ends in `}` still ends with the newline that
`CloseScope` wrote, so `CompilationUnit` output does end in a newline in practice; a file
ending in a top-level statement does not.

**Why:** it makes trailing whitespace structurally impossible rather than merely unlikely,
and it keeps exact-text assertions honest.

### 7. `UnsafeExpressionSyntax` is emitted but no shipping compiler parses it

**Chosen:** emit it. It is a concrete `<Node>` in the grammar like any other; the generator
does not judge.

**Why:** filtering on `ExperimentalUrl` would mean the node set silently changes shape when
a feature ships. `--report` names it instead.
Defaults taken where the specification was silent, recorded per §8.4. Each keeps V1 source
compatible unless the handoff said otherwise.

## Roslyn bridge (`CSharpAuthor.Roslyn`)

### Decisions taken

1. **A type in the global namespace has no namespace.** Roslyn writes `global::GlobalThing`;
   the type model spells "no namespace" as the empty string, and an empty namespace cannot
   carry a `global::` prefix. `Global` mode therefore writes `GlobalThing`, which is valid C#
   but not maximally qualified — a generated file that declares its own `GlobalThing` would
   shadow it. Fixing this properly means letting `TypeDefinition` distinguish "global
   namespace" from "no namespace", which is the type model's call, not the bridge's.

2. **Structs, records and delegates convert to `TypeDefinitionEnum.ClassDefinition`.** The
   enum has three members and neither consumer has ever had more; both already do this.

3. **`System.Void` keeps the `("System", "Void")` identity** rather than becoming a keyword
   with an empty namespace like the other special types. It already renders as `void` in
   every mode, and changing the pair would stop it comparing equal to
   `TypeDefinition.Get(typeof(void))`.

4. **`System.IntPtr` and `System.UIntPtr` convert to `nint` and `nuint`.** §7 asks for it, and
   it is what Roslyn's own fully-qualified display produces on a runtime where the two are
   unified — the symbol carries no flag that would let the bridge tell `IntPtr` from `nint`
   there. A consumer emitting for C# 8, where `nint` does not exist, needs the profile to
   downlevel it; the bridge does not decide language versions.

5. **Namespaces are not `@`-escaped; type names are.** `INamespaceSymbol.ToDisplayString()` is
   unescaped and DependencyModules compares namespaces against that spelling, so escaping here
   would break the comparison. A namespace segment that is a keyword still needs escaping at
   the point the `using` is written, which is the output context's job.

6. **The bridge's types are public.** §3 marks *generated node* types internal when source
   included; the bridge is different — DependencyModules splits its generator across two
   assemblies and the converted types cross that boundary. Only a consumer that set
   `PackageCSharpAuthorIncludeRoslyn` sees them at all.

7. **`Nullable<T>` gets its own type; a nullable annotation does not.** `int?` converts to
   `NullableValueTypeDefinition`, which derives from `TypeDefinition` so it still compares
   equal to a hand-built `TypeDefinition.Get(typeof(int)).MakeNullable()` in both directions
   and hashes the same. `string?` stays a plain type with `IsNullable` set. The two are
   distinguishable through `IsNullableValueType()`, which is what an emitter needs before it
   can drop one `?` and keep the other.

8. **A plain `T[]` keeps the model's flattened array shape.** Only what the flag cannot express
   — rank above one, jagged, or an annotation on an array level — becomes an
   `ArrayTypeDefinition`. This keeps the common case comparing equal to what callers build by
   hand.

### Merging with the type model

The bridge's five type implementations already answer `ArrayRanks`, `ContainingType` and
`MakeArray(int)` — the members the type model is adding — in the terms the model asks them in,
so they satisfy the wider interface without an edit. Compiling the bridge folder against that
work produces exactly one error, and it is in `NullableValueTypeDefinition`: it derives from
`TypeDefinition`, and the no-argument `MakeArray()` it overrides stops being virtual there. The
two overloads collapse into `public override ITypeDefinition MakeArray(int rank)`. With that one
edit the two compile clean together, warnings-as-errors included — measured, not assumed.

Once the model carries ranks and a containing type of its own, `ArrayTypeDefinition` and
`NestedTypeDefinition` stop earning their place: the conversion can build the model's own type
directly and the bridge sheds two classes. That is a simplification, not a fix, and it belongs
after both are merged.

### Still open

- `ArrayTypeDefinition`, `TupleTypeDefinition`, `PointerTypeDefinition`,
  `FunctionPointerTypeDefinition` and `NestedTypeDefinition` have no Roslyn dependency. They
  live in the bridge because that is what produces them, but they are type-model types and
  would be more useful in `CSharpAuthor` proper, where a caller who is not a source generator
  could reach them.
- A pointer type has no legal home in a generated class: `ComponentModifier` has no `Unsafe`,
  so a field of type `int*` cannot be emitted even though the type converts correctly.

## Opting in

The bridge is a second source folder in the same package, not a second package. A consumer
adds one property to the project that already references CSharpAuthor:

```xml
<PropertyGroup>
  <PackageCSharpAuthorIncludeRoslyn>true</PackageCSharpAuthorIncludeRoslyn>
</PropertyGroup>
```

It implies `PackageCSharpAuthorIncludeSource` unless that is set explicitly, because the
package is normally referenced with `IncludeAssets="build"` and brings no assembly with it.
The project needs `Microsoft.CodeAnalysis.CSharp`, which a generator project already has.

`scripts/verify-roslyn-packaging.sh` checks both directions: that the package carries no
Roslyn-dependent source in the folder every consumer compiles, and that a project which opts
in compiles the bridge on netstandard2.0 at LangVersion 10 under `TreatWarningsAsErrors` and
`EnforceExtendedAnalyzerRules`.
