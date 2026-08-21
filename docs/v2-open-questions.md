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
