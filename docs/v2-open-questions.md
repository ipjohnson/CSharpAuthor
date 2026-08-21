# V2 open questions

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
