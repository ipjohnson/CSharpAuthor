# CSharpAuthor 2.0

Built autonomously overnight by a fleet of agents against `V2-HANDOFF.md`. Every number below
was measured on this machine, by running the thing it describes. Where a gate fails, it says so.

**110 commits · 224 files · see `git diff --shortstat 7ab7145..HEAD`**

---

## 1. Gates (§6)

| # | Gate | Bar | Measured | |
|---|---|---|---|---|
| 1 | `dotnet test CSharpAuthor.Tests` | 139/139, existing tests unmodified | **1546 passed, 0 failed, 93 skipped** (1639 total). All 139 originals pass **unmodified** — verified mechanically | ✅ |
| 2 | Adversary suite | every case passing or listed as a known gap | **found 170 / fixed 77 / outstanding 93** — see the caveat below | ⚠️ |
| 3 | Round-trip | every emitting test parses **and semantically compiles** it | **a 305-case adversary suite compiles its output with Roslyn; the remaining ~1,330 cases assert strings.** Not met as written | ❌ |
| §3 | `verify-roslyn-packaging.sh` | library takes no Roslyn dependency | **exit 0, 13 checks pass.** Zero `PackageReference`; resolved graph is `NETStandard.Library` + `Microsoft.NETCore.Platforms` only | ✅ |
| 4 | DependencyModules | 735/735 | **725/735** ×2 TFMs, with the documented patch. 10 justified diffs | ⚠️ |
| 5 | Hardened.SourceGenerator.Tests | 383/383 → **the real bar is 468** | **468/468** | ✅ |
| 6 | Snapshot diffs | zero, or every one justified | 10, every one justified below. **Nothing re-baselined** | ⚠️ |
| 7 | C# coverage | reported with gaps named | §9(a) **250/250 = 100%**; §9(b) **1,315/1,373 = 95.8%** | ✅ |
| 8 | Migration doc | every breaking change with its mechanical fix | `docs/migration-v1-v2.md`, 1,398 lines, incl. both consumer patches | ✅ |
| 9 | Perf | no worse than V1 | **+7.1% allocation, +66..76% time.** Absolute time bar passed (0.0181 ≤ 0.048 ms); absolute allocation bar missed by 6.3% (82.9 vs 78 KB) | ❌ |

### Two corrections to the handoff, both measured

- **Gate 5's bar of 383 is stale.** `Hardened.SourceGenerator.Tests` runs **468** tests. 383 is a
  different suite, `Hardened.OpenApi.BuildTask.Tests`.
- **§9(a)'s "233 / 252 = 96.7%" was wrong in both numbers.** The real figure is **250 / 250**.
  The prototype's `nodes.json` came from a regex extractor that silently dropped every field
  nested inside `<Choice>`/`<Sequence>` — costing 9 whole node types and, from the survivors,
  *every method body, `for` initialiser, lambda body and property accessor list*. The generator
  now parses `Syntax.xml` directly. All 8 previously hand-written nodes are generated.

### Gate 2's honest shape

93 outstanding sounds worse than it is, and better than it is, depending which half you read:
**61 of the 93 are `Assert.True(false, "no API for …")` placeholders that can never pass however
the feature is built.** They inventory missing features; they do not specify them. Only **32 are
executable and genuinely outstanding.** Full ledger in `docs/adversary-findings.md`.

Every adversary finding is proven by **compiling the emitted string with Roslyn**, not by comparing
it to a string somebody typed.

**This ledger was wrong until an independent verifier re-ran it.** It claimed 74 fixed / 96
outstanding and asserted it had been self-validated. Stripping every `Skip` actually produced 93
failures: three findings were passing with their `Skip` still attached, closed by the
nullable-position work rather than by anything aimed at them. They are now un-skipped and live.
Re-validated after correction: **93 skips → 93 failures, 61 placeholders counted per-test.** The
lesson generalises — a ledger not re-validated after every merge goes stale in the direction that
flatters the work.

---

## 2. C# coverage (§9)

### (a) Node coverage — 250 / 250 = 100%

`python3 tools/grammar/gen_all.py --report`. Regeneration is verified **byte-identical**, which
is the proof that `Nodes.g.cs` has never been hand-edited (rule §8.3).

Named gaps, all structural rather than missing work:
- `//` and `/* */` comments and `#if` spans are **not expressible as nodes by construction** —
  Roslyn models them as trivia, and a region wraps an arbitrary span rather than containing it.
  Both are reachable through `Raw`.
- `UnsafeExpressionSyntax` emits but no shipping compiler parses it (experimental).
- `BadNamespaceMemberDeclarationSyntax` is declared only inside an XML comment.
- 76 nodes take a caller-supplied token value.

### (b) Round-trip fidelity — 1,315 / 1,373 = 95.8%

The harness prints three per-corpus lines and no total; the total below is summed by hand.

`source → Roslyn parse → import → emit → Roslyn parse → compare trees`, over **1,373 real files
with zero exclusions** — CSharpAuthor's own 131, DependencyModules' 277, Hardened's 965.

| corpus | |
|---|---|
| CSharpAuthor | 123 / 131 (93.9%) |
| DependencyModules | 265 / 277 (95.7%) |
| Hardened.Framework | 927 / 965 (96.1%) |
| **total** | **1,315 / 1,373 (95.8%)** |

**Buckets (a) and (b) are both empty**: every one of 1,373 files imports into the tree, and every
emitted file re-parses. All 58 remaining failures are structural differences.

For scale, the same harness scored the original prototype layer at **1 / 1,359 (0.1%)** — note
the denominator differs (the corpus grew as this branch added files), so it is indicative rather
than like-for-like.

**What this number does and does not mean.** Equivalence is
`SyntaxNode.IsEquivalentTo(topLevel: false)`: same node kinds, same shape, same token *text*
(`0x1F` ≠ `31`, `@class` ≠ `class`, `1.5f` ≠ `1.5`), **all trivia ignored**. So comments, XML docs
and `#region`/`#pragma`/`#nullable`/`#if` are *not* tested. This is syntax-tree fidelity, not file
fidelity. A **more permissive** cross-check verdict is printed beside the headline; the headline
uses the stricter `IsEquivalentTo` figure, so it can only understate.

**Ceiling:** `Microsoft.CodeAnalysis.CSharp` 4.14.0 parsing at `LanguageVersion.CSharp13`.
**Nothing above C# 13 is validated and no C# 14/15 claim is made from it.**

**The 58 remaining failures, root-caused:**
- **48 — raw interpolated strings.** In `$$"""…"""` the brace count must equal the `$` count, but
  `Interpolation` hard-codes `{`/`}`. The grammar genuinely cannot encode this; it needs a writer
  rule keyed on the enclosing `StringStartToken`, like the spacing policy.
- **9 — a Roslyn one-element `SyntaxList` green-representation artefact**, not emitter output
  (4 `UsingDirective`, 3 `FileScopedNamespaceDeclaration`, 2 `Block`). Down from 47 earlier in the
  run. The cross-check disagrees on exactly these 9, which independently confirms the attribution.
- 1 — an `InterpolatedStringText` whitespace token.

---

## 3. Benchmark (§10) — gate 9 fails, and here is exactly what it costs

V1 and V2 measured **in one interleaved invocation**, which is the only comparison that means
anything: the handoff's 0.0477 ms / 78.4 KB were taken on other hardware, and this machine is
~4× faster. Allocation *does* transfer between machines (77.4 KB here vs the handoff's 78.4 —
1.2% apart), which is the evidence the payload really is §10's payload. Time does not.

```
target       scenario   runs  ms/file  trimmed     min     max  spread  KB/file
1-bench-v1   tree          9   0.0109   0.0110  0.0105  0.0120   13.7%     77.4
2-v2         tree          9   0.0181   0.0182  0.0177  0.0190    7.6%     82.9
1-bench-v1   stringbuilder 9   0.0011                                      21.4

  ms/file : 0.0109 -> 0.0181  (+66.1%)     [+75.7% on a busier second run]
  KB/file : 77.4  -> 82.9    (+7.1%)       [identical across runs - allocation is noise-free]
  verdict : FAIL
```

**Read against the handoff's absolute bar instead, V2 passes on time (0.0181 ≤ 0.048 ms) and
misses on allocation by 6.3% (82.9 vs 78 KB).** It fails either way; this is how narrowly.

It started far worse. The first measurement of the merged branch was **+134.9% time and +136.0%
allocation**. Getting from there to +7.1% took eight optimisations, each with its own gate run and
byte-identical output (hash `21e5a0d39135d398`, verified against the StringBuilder reference every
time), plus a differential fuzz of up to **180,000 generated files** across every `TypeOutputMode`
and both alias settings. **The deferral was never weakened** — the segment record is smaller and
the name plan lazier, not removed.

The biggest single win: `Write` read `IndentString`/`SingleIndent` as expression-bodied
`new string(...)` — **two string allocations per token written**.

### Where the remaining 5.4 KB goes

| | V1 | V2 | Δ |
|---|---|---|---|
| build the tree | 47.58 | 48.83 | +1.25 |
| writers | 10.04 | 2.85 | **−7.19** |
| **record the file** (deferral) | — | 8.85 | **+8.85** |
| **plan the names** (deferral) | — | 3.30 | **+3.30** |
| produce the text | 19.81 | 18.92 | −0.89 |
| **total** | **77.43** | **82.75** | **+5.32** |

**12.15 KB (+15.7%) is irreducible**, bought back to +7.1% by beating V1 elsewhere. Recording 808
writes plus 577 references has an information floor of 7.85 KB for this payload; the store sits at
8.85 KB, 113% of it. The 3.30 KB name plan is what buys derived `using` directives and collision
aliasing — it cannot decide anything until it knows every type the file wrote.

Time is the weaker number and is also mostly structural: of the 6.0 µs gap, **2.9 µs is the second
pass** (V1 appends as it writes; V2 walks the record and renders it) and **2.4 µs is the name plan**
— 88% of it. 1.25 KB of the allocation gap is the type model growing, not the output context.

**This is the price of §2 invariant 2.** Deferred rendering is what lets one option flip a file
between short names and `global::`, and it is what makes a missing `using` structurally impossible.
Whether +7% allocation is worth that is a product decision, not one an agent should take.

---

## 4. Migration

`docs/migration-v1-v2.md` (1,398 lines). It includes the two consumer patches, both verified to
`git apply --check` cleanly against pristine clones:
- `docs/consumer-patches/dependencymodules-v2.patch` — 5 files
- `docs/consumer-patches/hardened-v2.patch` — 13 files

### The §1 interlock, and what it costs consumers

V2 enforces invariant 1: a type reaches output only through `IOutputContext.Write(ITypeDefinition)`,
and namespaces are *derived*. `AddImportNamespace` has left `IOutputContext` entirely, so a writer
physically cannot call it. Neither consumer had a single call site.

Fixing that removes the stray `using` in `TypeOutputMode.Global` — and **both consumers depended on
it**, exactly as the handoff predicted. Unpatched, V2 takes DependencyModules to 383/735 and
Hardened to 265/468. Every failure is one of two shapes, both mechanical:

- **Shape A — extension methods (CS1061).** `global::` cannot name an extension method.
  **10 `AddUsingNamespace` lines** (3 in the DM patch, 7 in Hardened's) fixed ~330 of DM's 351
  failures and ~120 of Hardened's 203,
  plus all 12 assemblies that could not build at all.
- **Shape B — type names written as raw strings (CS0246/CS0103).** Fixed with the new type-aware
  `CodeOutputComponent.Get(ITypeDefinition, member)` / `FromParts`, which carry an unrendered type
  into the name plan.

With the patches: **DependencyModules 725/735, Hardened 468/468.**

Across all 35 assemblies: **6,166 passing, 20 failing, 0 unable to run** (the 20 are DM's 10 × 2 TFMs).

**Caveat a reviewer needs:** these numbers come from consumer trees with accumulated build state.
A cold `git clone` plus the patch does **not** reproduce them — but neither does the same cold-clone
setup against **V1**, which lands equally broken. So this is a bootstrap property of those repos,
not a V2 regression; the handoff's "both suites run clean from a fresh clone with zero setup" does
not hold in this environment for either version.

### V2 exposed a latent DependencyModules bug

`AttributeModel` quotes strings and then hands them to `CollectionSyntaxDeclaration`, which quotes
them again. Under V1 `QuoteString` did not escape, so the double call emitted `""a""` — **invalid
C#** — and the test's `Assert.Contains("\"a\"")` matched a substring of it and passed. V2 escapes
correctly, so the defect finally surfaces. Two lines, in the patch.

---

## 5. Snapshot diffs — 10, none re-baselined

`UPDATE_SNAPSHOTS` and `APPROVE_PUBLIC_API` were never set.

- **9 × `ModuleGenerationSnapshotTests`** — Global-mode files that previously resolved bare
  `[ExcludeFromCodeCoverage]`, `[DynamicDependency]` and `ServiceLifetime.*` off a stray derived
  `using` now emit them `global::`-qualified. These are the §1 fix working. **Improvements, but
  your call.**
- **1 × `PublicApiTests.SourceGeneratorApi`** — CSharpAuthor's own V2 surface recorded in a
  DependencyModules baseline. No consumer patch can restore it. The diff is **+725 / −9**, and it is
  **not** purely additive. The nine removals:
  - `IOutputContext.AddImportNamespace(ITypeDefinition)` and `AddImportNamespaces(…)` — deliberate,
    this is invariant 1 being enforced structurally
  - **`BaseTypeDefinition.MakeArray()` stopped being `abstract`/virtual.** Callers bind exactly as
    before, but **an external subclass that `override`s it now gets CS0506.** This is a real
    breaking change for anyone subclassing the type model, and it was not previously written down
  - `Equals` / `GetHashCode` / `MakeArray()` overrides hoisted from `TypeDefinition` and
    `GenericTypeDefinition` to the base — behaviour preserved, recorded surface changed

`LiteralFormatter`, `CSharpIdentifier`, `ComponentModifierExtensions` and `MethodDefinition.IsBodyless`
were demoted to `internal`/`private`. But the added surface is **not** "only what §7 requires" —
most of it is mandated by **§4** (`EmitProfile`, `LanguageVersion`, `BraceStyle`, the `EmitSession`
diagnostic channel, `LanguageFeature`) and **§11** (the `Ex`/`Pat`/`Raw` expression layer). Some of
it reads as plumbing that should have been `internal`: `TypeDefinitionIdentity`,
`ProfiledOutputContext`, `ExPrecedence`/`PatPrecedence`, and **`CSharpText`, which re-exposes
publicly the same literal/identifier surface that `LiteralFormatter` and `CSharpIdentifier` were
demoted to hide.** That demotion is therefore partly cosmetic. Worth a pass before release.
The ~250 generated `CSharpAuthor.Syntax` node types are `internal` behind
`#if CSHARPAUTHOR_PUBLIC_SYNTAX`, which only `CSharpAuthor.csproj` defines — **verified: 0 entries
leak into the consumer's public API.** That guard failed open once during the run and the consumer
snapshot caught it, which is exactly why the tripwire exists.

---

## 6. Defaults taken (§8.4)

`docs/v2-open-questions.md`, 733 lines. The load-bearing one: **calling `Output()` with no profile
means `EmitProfile.V1Compatible`, not `EmitProfile.Default`** — so a caller who passes no profile
gets V1 behaviour byte for byte, which is why `Default.FileScopedNamespace = true` diffs nothing.

---

## 6b. What an independent verifier caught, and what it cost

A verifier with authority to reject re-ran every gate against this branch. It reproduced every
headline number — and rejected the PR body. Four things it found are worth stating, because they
are the kind of thing an autonomous run gets wrong:

1. **Gate 2's ledger was stale and its self-validation claim was false.** It said 74 fixed / 96
   outstanding "self-validated"; stripping the skips actually gave 93 failures, because three
   findings were passing with `Skip` still attached. Corrected to **170 / 77 / 93**, re-validated,
   and the lesson written into `docs/adversary-findings.md`.
2. **Two undisclosed breakages that V2 itself causes**, neither in the shipped patches:
   - The Roslyn bridge landed *inside* the glob in Hardened's own `CSharpAuthor.props`, compiling
     it into three build-task projects with no Roslyn reference. **Three projects and six test
     assemblies failed to build** — and `--scope full` silently reported 29 assemblies instead of
     35 rather than saying so. `docs/migration-v1-v2.md` had predicted this exact hazard, named
     that exact file, then reasoned itself out of it on a premise the bridge's location falsified.
   - `CSharpAuthor.LanguageVersion` did not merely risk CS0104 — it **failed the
     `DependencyModules.Benchmarks` build** at three sites. Fixing it turned up a fourth site and a
     second latent collision, `EmitResult` ↔ `Microsoft.CodeAnalysis.Emit.EmitResult`, found by
     reflecting over Roslyn 4.10 and 4.14 rather than by guessing. All profile types now live in
     **`CSharpAuthor.Profiles`**; the mechanical fix is one `using` per file.
3. **`verify-roslyn-packaging.sh` exited 1 on a false positive** — it grepped prose, matching
   `Microsoft.CodeAnalysis` inside XML doc comments. It now strips comments and literals, and
   carries a permanent negative control that plants a real reference and requires the scanner to
   catch it.
4. **The consumer runner could not see an assembly that never built.** It reported a compile
   failure only when a run produced *no* summaries; in a solution-wide run the assemblies that did
   build still printed theirs, so a lost project left no trace in the totals. That is precisely how
   breakage 2 hid for an entire night. Fixed, and the fix is what makes the 35/6,166 figure above
   trustworthy.

None of this was caught by the 1,639-test suite. It was caught by running the consumers' *whole*
solution and by someone assuming the report was wrong.

---

## 7. What is not finished — stated plainly

1. **Gate 9 — see §3.**
2. **Raw interpolated strings (`$$"""…"""`)** are the single largest round-trip gap (48 files) and
   are not fixed. The grammar cannot express the rule; it needs a writer rule keyed on the
   enclosing string-start token.
3. **35 executable adversary findings remain outstanding**, and 61 more are placeholders marking
   features that do not exist (all 13 pattern forms among them). `docs/adversary-findings.md`.
4. **7 `Policy`-category downlevel branches answer and diagnose correctly but no component renders
   the alternative** — `primary constructors`, `field` keyword, `params` collections, `var`,
   expression-bodied members, `nint`/`nuint`.
5. **Five fixes were implemented and then reverted because an existing test pins the old
   behaviour**: bodyless `partial` methods, indexers, `AddBaseType` keeping its arguments, and
   `ToString()`'s leading dot. Rule §8.1 forbade changing those tests. **One of them,
   `SimplePropertyDefinitionTests.IndexedGetSetDefinition`, asserts `public int Test[string index]`
   — which is CS1519, not valid C#.** A named indexer must be `this[...]`. That original test
   currently blocks the indexer fix and is yours to rule on.
6. **`InterfaceMethodDefinition.WriteEndOfMethodSignature` overrides without calling base**, so
   `WhereStatement` and every `ConstraintDefinition` are silently dropped from interface methods.
   Found late, not fixed.
7. **~~`CSharpAuthor.LanguageVersion` collides with Roslyn's~~ — FIXED.** All profile types moved
   to `CSharpAuthor.Profiles`. Kept here because the mechanical fix matters to anyone who pulled an
   intermediate commit: add `using CSharpAuthor.Profiles;`. A `using X = …` alias **cannot** fix
   the collision inside a namespace nested under `CSharpAuthor`, because enclosing-namespace
   members outrank aliases — which is why the type had to move rather than be aliased around.
8. **Two `Raw` types exist** — public `CSharpAuthor.Expressions.Raw` and internal
   `CSharpAuthor.Syntax.Raw`. Safe (different namespaces, one internal) but duplicated. Unifying
   them is a design decision, deliberately not taken at 3am.
9. **`--scope core` cannot see a whole class of consumer bug.** `typeof({ControllerType.Name})` in
   Hardened is Shape B but invisible to gate 5, because there the generated file and the controller
   share a namespace. Only `--scope full` catches it. `Hardened.Amz` needs the same one-line Shape A
   fix and is not testable from this checkout.
10. **The Hardened patch updates 13 inline `Assert.Contains` literals** (that repo has no snapshot
    mechanism — they are string literals in test bodies). **8 of the 13 are not caused by the patch**;
    they were hidden behind build failures. **Hardened is 463/468 without those edits, 468/468 with
    them.** Listed individually in `docs/migration-v1-v2.md` §5 so you can veto them one by one.

---

## 8. Reproducing any number here

```bash
dotnet test CSharpAuthor.Tests
python3 tools/grammar/gen_all.py --report                      # §9(a)
./scripts/run-roundtrip.sh <checkout> --corpus all             # §9(b)
./scripts/run-consumer-tests.sh <checkout> --scope core        # gates 4 and 5
./scripts/run-benchmark.sh <v1-checkout> <v2-checkout>         # gate 9
./scripts/verify-roslyn-packaging.sh                           # §3
```

`run-consumer-tests.sh` asserts via `-getItem:Compile` that the checkout under test is genuinely
in the consumer's compile set. Without that check, `dotnet test` against a prebuilt DLL returns a
clean **735/735 while silently measuring the published 1.1.1010 package** — a false green that
nearly made it into this report.
