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
