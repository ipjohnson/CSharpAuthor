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
