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
