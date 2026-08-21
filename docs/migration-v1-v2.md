# Migrating from CSharpAuthor 1.x to 2.0

This is the migration record for the V2 build, and it is written by the **migrator** agent
(V2-HANDOFF.md §5). It exists to satisfy two gates:

- **Gate 8** — every breaking change, with its mechanical fix. §4 below.
- **Gate 6** — every consumer snapshot diff, with a justification. §5 below.

The two consumer repositories are the oracle for all of it:

| | Repository | Gate | Suite |
|---|---|---|---|
| DM | `github.com/ipjohnson/DependencyModules` | 4 | `tests/DependencyModules.Tests` |
| HF | `github.com/ipjohnson/Hardened.Framework` | 5 | `src/SourceGenerators/Hardened.SourceGenerator.Tests` |

> **Rule §8.1 — never re-baseline a snapshot.** If a consumer's committed output changes,
> the change goes in §5 with a justification. It does not go into the `.verified.txt`.
>
> **Rule §8.2 — never commit to the consumer repositories.** Scratch clones only. The
> runner in §2 does not even write to their working trees; see §3.

---

## 1. Status at a glance

Everything below was measured, not estimated. Run 0 is the V1 baseline: `feature/v2` at
`7ab7145`, which is still V1 code plus the handoff and the prototypes.

| | Measured | Handoff said | Verdict |
|---|---|---|---|
| DM `DependencyModules.Tests`, net8.0 | **735 / 735** | 735 | accurate |
| DM `DependencyModules.Tests`, net10.0 | **735 / 735** | — | second TFM, not mentioned |
| HF `Hardened.SourceGenerator.Tests`, net8.0 | **468 / 468** | 383 | **stale — the real number is 468** |

Gate 5's bar is therefore **468/468**, not 383/383. `383` is the current count of a
*different* Hardened suite, `Hardened.OpenApi.BuildTask.Tests`, which is probably where the
number came from. Both are green today.

Beyond the two gate projects, both solutions are fully green against a local checkout, and
cheaply enough to run every time — 35 test assemblies, **6,186 tests, 0 failures, ~36 s**.
See §2 `--scope full`. The extra suites matter because the integration tests actually
*compile and execute* generated code, which the gate projects mostly do not.

<details>
<summary>Full-scope baseline, run 0 — all 35 assemblies</summary>

| Assembly | TFM | Passed |
|---|---|---|
| DependencyModules.Tests | net8.0 | 735 |
| DependencyModules.Tests | net10.0 | 735 |
| SutProject.Tests | net8.0 | 135 |
| SutProject.Tests | net10.0 | 135 |
| SutProject.NUnitTests | net8.0 | 34 |
| SutProject.NUnitTests | net10.0 | 34 |
| WebApiApp.Tests | net8.0 | 2 |
| WebApiApp.Tests | net10.0 | 2 |
| Hardened.Requests.Runtime.Tests | net8.0 | 803 |
| Hardened.SourceGenerator.Tests | net8.0 | 468 |
| Hardened.OpenApi.SourceGenerator.Tests | net8.0 | 407 |
| Hardened.OpenApi.BuildTask.Tests | net8.0 | 383 |
| Hardened.Web.SourceGenerator.Tests | net8.0 | 358 |
| Hardened.Shared.Runtime.Tests | net8.0 | 339 |
| Hardened.Web.Runtime.Tests | net8.0 | 322 |
| Hardened.Requests.Abstract.Tests | net8.0 | 193 |
| Hardened.IntegrationTests.WebApp.SUT.Tests | net8.0 | 149 |
| Hardened.Web.StaticContent.Tests | net8.0 | 142 |
| Hardened.Web.AspNetCore.Runtime.Tests | net8.0 | 119 |
| Hardened.Shared.Testing.Tests | net8.0 | 117 |
| Hardened.Web.Kestrel.Runtime.Tests | net8.0 | 114 |
| Hardened.Smithy.BuildTask.Tests | net8.0 | 73 |
| Hardened.IntegrationTests.OpenApi.SUT.Tests | net8.0 | 68 |
| Hardened.Web.StaticContent.BuildTask.Tests | net8.0 | 62 |
| Hardened.Requests.Serializers.Newtonsoft.Tests | net8.0 | 52 |
| Hardened.Web.Testing.Tests | net8.0 | 38 |
| Hardened.IntegrationTests.StaticContent.SUT.Tests | net8.0 | 28 |
| Hardened.IntegrationTests.StaticContent.Manifest.SUT.Tests | net8.0 | 28 |
| Hardened.IntegrationTests.Benchmark.SUT.Tests | net8.0 | 23 |
| Hardened.IntegrationTests.Smithy.SUT.Tests | net8.0 | 20 |
| Hardened.Templates.RazorBlade.Tests | net8.0 | 19 |
| Hardened.Validation.SourceGenerator.Tests | net8.0 | 16 |
| Hardened.PublicApi.Tests | net8.0 | 14 |
| Hardened.IntegrationTests.Authorization.SUT.Tests | net8.0 | 10 |
| Hardened.SourceGeneration.Testing.Tests | net8.0 | 9 |
| **Total** | | **6,186** |

</details>

---

## 2. Running the oracle

```
scripts/run-consumer-tests.sh <path-to-a-CSharpAuthor-checkout> [options]
```

```bash
# the two gate suites, ~15 s
./scripts/run-consumer-tests.sh ../../v2

# everything both solutions have, ~36 s. Use this before declaring a phase done.
./scripts/run-consumer-tests.sh ../../v2 --scope full -q

# one consumer while iterating
./scripts/run-consumer-tests.sh ../../v2 --only hardened
```

Options: `--only dm|hardened|both`, `--scope core|full`, `--consumers DIR`, `--log-dir DIR`,
`-q`. Exit code is 0 only if every suite asked for actually ran and reported zero failures.
It streams `dotnet` output unless `-q`, and always keeps a log per consumer in the log
directory.

The script refuses to report a number it cannot trust:

- it fails if `<root>/CSharpAuthor/CSharpAuthor.csproj` is missing;
- before each suite it evaluates the consumer's generator project and asserts the compiler
  is really being handed files from `<root>/CSharpAuthor/` — a mis-wired run would otherwise
  go green against the published 1.1.1010 package, which is worse than a red;
- it `unset`s `UPDATE_SNAPSHOTS` and `APPROVE_PUBLIC_API` so no test can rewrite its own
  baseline (§8.1);
- it checks `git status` in both consumer clones before and after, and shouts if either is
  dirty (§8.2);
- a build failure is reported as `BUILD FAILED - CS0117 …`, never as a test count.

If you would rather drive `dotnet` yourself, these are the two commands the script runs.
`$CSA` is an absolute path to a CSharpAuthor checkout.

**DependencyModules**

```bash
cd <clone>/DependencyModules
dotnet test tests/DependencyModules.Tests/DependencyModules.Tests.csproj --nologo -v q \
  /p:PackageCSharpAuthorIncludeSource=false \
  /p:CustomAfterMicrosoftCommonTargets=<repo>/scripts/local-csharpauthor.targets \
  /p:LocalCSharpAuthorRoot=$CSA \
  "/p:LocalCSharpAuthorProjects=|DependencyModules.SourceGenerator|DependencyModules.SourceGenerator.Impl|"
```

**Hardened.Framework**

```bash
cd <clone>/Hardened.Framework
dotnet test src/SourceGenerators/Hardened.SourceGenerator.Tests/Hardened.SourceGenerator.Tests.csproj \
  --nologo -v q /p:UseLocalCSharpAuthor=true /p:CSharpAuthorRoot=$CSA
```

Swap the project for `DependencyModules.sln` / `src/Hardened.Framework.sln` to get the full
scope.

---

## 3. How the consumers are pointed at a local checkout

Both routes reproduce exactly what the published package does. `Package/CSharpAuthor.targets`
is four lines:

```xml
<ItemGroup Condition="'$(PackageCSharpAuthorIncludeSource)' == 'true'">
  <Compile Include="$(MSBuildThisFileDirectory)../src/**/*.cs" Visible="false"/>
</ItemGroup>
```

and the package's `src/CSharpAuthor/` is packed from the repository's `CSharpAuthor/**/*.cs`
(excluding `obj/` and `bin/`). So "use a local checkout" means: compile
`<root>/CSharpAuthor/**/*.cs` straight into the generator assembly. There is no
`ProjectReference` route — an analyzer is loaded by the compiler with no probing path, so a
sibling `CSharpAuthor.dll` is a `FileNotFoundException` at generator initialisation, and the
build then carries on emitting nothing.

**Hardened.Framework already ships the switch** in
`src/SourceGenerators/CSharpAuthor.props`: `CSharpAuthorRoot` picks the checkout,
`UseLocalCSharpAuthor` turns the mode on, and the `PackageReference` is conditioned out. The
runner passes both, `UseLocalCSharpAuthor=true` deliberately, so a wrong path is a build
error rather than a silent fall back to the package.

**DependencyModules has no switch.** Its two generator projects hard-code
`PackageCSharpAuthorIncludeSource=true` and a `PackageReference Include="CSharpAuthor"
Version="1.1.1010"`. Rather than edit them — the clone must stay pristine under §8.2 — the
runner uses two MSBuild facts:

1. a property set on the command line is *global* and cannot be overridden by a project
   body, so `/p:PackageCSharpAuthorIncludeSource=false` switches the package's own source
   inclusion off from outside, leaving the `PackageReference` an inert no-op
   (`IncludeAssets=build` and nothing to include);
2. `CustomAfterMicrosoftCommonTargets` imports an arbitrary `.targets` file after the
   project body, which is where `scripts/local-csharpauthor.targets` adds the same `Compile`
   glob against the checkout.

Which projects get the glob is discovered by grepping the consumer's `.csproj` files for the
package reference, so a third generator project would be picked up without editing anything.

Net effect: `git status` in both scratch clones stays empty across any number of runs.

### If V2 adds a second source root

§3 of the handoff says the Roslyn bridge ships as a second source folder in the same package,
gated on `PackageCSharpAuthorIncludeRoslyn`. Anything that lives outside
`<root>/CSharpAuthor/` will **not** be compiled by either route above. When that lands, three
files have to learn about it together:

- `CSharpAuthor/Package/CSharpAuthor.targets` (the package)
- `Hardened.Framework/src/SourceGenerators/CSharpAuthor.props` (a real change to a consumer
  repo, needing its own PR there)
- `scripts/local-csharpauthor.targets` (this repository)

---

## 4. Breaking changes and their mechanical fixes — gate 8

Status values: **measured** = observed in a consumer run and named here; **predicted** = a
direct consequence of a §7 defect fix, not yet observed; **watch** = a hazard found by
reading the consumers, not yet triggered.

### 4.1 API surface

Both consumers were surveyed for what they actually touch, so a builder can tell a free
change from an expensive one. Counts are call sites outside `obj/` and `bin/`.

| API | DM | HF | Note |
|---|---:|---:|---|
| `ITypeDefinition` | 245 | 201 | the product. Preserve. |
| `TypeDefinition` | 126 | 299 | ditto |
| `KnownTypes` | 72 | 127 | |
| `ClassDefinition` | 73 | 125 | |
| `GenericTypeDefinition` | 51 | 32 | |
| `CodeOutputComponent.Get` | 43 | 25 | raw text escape hatch — see 4.2 |
| `AddIndentedStatement` | 23 | 44 | |
| `MakeNullable` | 14 | 46 | |
| `TypeOutputMode.*` | 8 | 18 | |
| `MakeArray` | 3 | 7 | rank bug, §7 |
| `IOutputContext` | 2 | 0 | |
| `WrapStatement` | 6 | 0 | |
| `AddLeadingTrait` | 5 | 0 | |
| **`AddImportNamespace`** | **0** | **0** | **nobody calls it** |
| `AddCode(` | 0 | 0 | |

| # | Change | Status | Mechanical fix |
|---|---|---|---|
| A1 | **Invariant 1: writers may no longer call `AddImportNamespace`; namespaces are derived from `IOutputContext.Write(ITypeDefinition)`.** | measured (as a non-event) | **None needed for these two consumers — neither calls it, at all.** The invariant can be enforced by making the method non-public without touching either repo. Any *other* consumer that calls it replaces `ctx.AddImportNamespace(ns); ctx.Write("Foo")` with `ctx.Write(TypeDefinition.Get(ns, "Foo"))`. |
| A2 | Generated grammar nodes are `internal` when source-included (§3). | predicted | None. `CSharpAuthor.Syntax` is new surface; nothing depends on it. Keeping it `internal` is what stops it leaking into a consumer's own public API — which `Hardened.PublicApi.Tests` and `PublicApiTests.SourceGeneratorApi.verified.txt` (1,783 lines) would both catch if it did. |
| A3 | `EmitProfile` is a writer argument, never stored on the tree (§4). | predicted | None if the existing `Output()` overloads stay. **Do not** replace `TypeOutputMode` parameters with a required `EmitProfile`: DM has 8 and HF 18 call sites passing `TypeOutputMode.*` positionally. Add profile-taking overloads instead. |
| A4 | `EquatableArray<T>` added beside `ITypeDefinition` (§7). | **watch** | Hardened's generators already source-include an `EquatableArray<T>` from `ValidationModules.SourceGenerator.Impl` (used in `Hardened.SourceGenerator/Validation/HandlerValidationFrontEnd.cs:96,181`). Today no file imports both namespaces, so there is no collision — but a `CSharpAuthor.EquatableArray<T>` puts one `using CSharpAuthor;` away from CS0104 in that assembly. Prefer a nested namespace or a distinct name; if it ships as `CSharpAuthor.EquatableArray<T>`, say so here and the consumer fix is `using EquatableArray = ValidationModules.SourceGenerator.Impl.EquatableArray;`. DM's `ServiceModelComparer` (`src/DependencyModules.SourceGenerator.Impl/Models/ServiceModel.cs:93`, 4 call sites) is the 60-line comparer §7 says this deletes — that deletion is a DM PR, not a V2 change. |
| A5 | `LangVersion` of the consuming generator projects. | measured | V2 source is compiled *into* the consumer, so it must build at **their** language version, not its own: `DependencyModules.SourceGenerator.Impl` pins `LangVersion 10`, `DependencyModules.SourceGenerator` pins `11`, and both set `Nullable=enable` and `EnforceExtendedAnalyzerRules=true` on `netstandard2.0`. V1's own csproj is `LangVersion 10`. **A V2 file using C# 12 syntax will not compile in DependencyModules** even though CSharpAuthor's own build is fine. Fix: keep library source at C# 10, or raise `LangVersion` in the consumer (a real PR there). This is the single most likely way to break gate 4 without breaking gate 1. |

### 4.2 Emitted-output changes

These change what consumers' generators *produce*, so they are what §5's snapshot diffs will
be made of. Each is a §7 defect whose fix is deliberate.

| # | Change | Status | Mechanical fix for a consumer |
|---|---|---|---|
| B1 | **`TypeOutputMode.Global` stops emitting `using` directives.** | predicted, with a named victim | This is the §1 bug, and DependencyModules' committed snapshots contain it: `ModuleGenerationSnapshotTests.SimpleModule.verified.txt` is a `Global`-mode file that still opens with `using Microsoft.Extensions.DependencyInjection;` and then relies on it for the bare `[ExcludeFromCodeCoverage]`, `[DynamicDependency]` and `ServiceLifetime.*` it emits. Remove the usings alone and DM's generated code stops compiling. The two defects hold each other up — see B2. Expect all 9 `ModuleGenerationSnapshotTests.*` snapshots to diff. |
| B2 | Raw-string type references stop resolving once B1 lands. | predicted | DM's own generator writes `CodeOutputComponent.Get("ServiceLifetime.Singleton")` at `src/DependencyModules.SourceGenerator.Impl/DependencyFileWriter.cs:296,299,302,375,378,381`. A raw string tracks no namespace, so it can never be qualified. **Fix is on the consumer side:** replace with a real reference, e.g. `TypeDefinition.Get("Microsoft.Extensions.DependencyInjection", "ServiceLifetime")` plus the member name, so the type flows through `IOutputContext.Write` and is qualified or imported according to the mode. Same treatment for the `[Browsable(false)]` traits at `DependencyModuleWriter.cs:193,262,286,377`. HF has 25 `CodeOutputComponent.Get` sites to audit the same way. Until that DM PR exists, V2 must keep `Global` mode's generated code compiling — which the `SutProject*` and `WebApiApp` integration suites (306 tests) will prove, and the gate-4 suite alone will not. |
| B3 | `private protected` no longer widens to `protected`; `protected internal` no longer narrows to `internal`. | predicted | None — strictly a correctness fix. A snapshot containing either is a snapshot of a bug. |
| B4 | Nested types emit `Outer.Inner`, not `Inner`. | predicted | None. Any snapshot that changes here was previously ambiguous or wrong. |
| B5 | Array ranks: `int[,]` no longer emits `Int32[,][]`; `MakeArray().MakeArray()` no longer drops a rank. | predicted | None. DM has 3 `MakeArray` sites, HF 7 — small blast radius. |
| B6 | Keyword type names: `float` not `Single`, `char` not `Char`, `sbyte` not `SByte`, `nint` not `IntPtr`. | predicted | None, but this is **cosmetic and repo-wide**: it will diff many snapshots at once for no semantic reason. Worth landing in its own commit so the snapshot review stays legible. |
| B7 | String literals escaped; numbers emitted with `InvariantCulture`; `char`/`float` literals get their quotes/suffix. | predicted | None. Pure correctness. Any consumer working around it by pre-escaping will double-escape — grep for manual `Replace("\"", "\\\"")` before landing. |
| B8 | `abstract` methods emit `;` rather than a body; `partial`/`readonly`/`sealed`/`static` modifiers stop being dropped. | predicted | None. |
| B9 | Same-name collisions auto-alias instead of emitting ambiguous `CS0104` code. | predicted | None, and it is what the prototype already demonstrates: `proto/deferred/DeferredContext.cs` compiles cases where V1 fails. New `using X = A.B.X;` lines in output are expected. |
| B10 | File-scoped namespaces / other `EmitProfile` defaults. | **open** | `EmitProfile.Default` in §4 sets `FileScopedNamespace = true`, but every DM snapshot today is block-scoped (`namespace TestNamespace {`). If the default flips, all 9 generator snapshots diff for a formatting reason alone. **Recommendation, per §8.4 (keep V1 source-compatible): the default used by the existing `Output()` overloads must stay block-scoped**, and file-scoped is opt-in via an explicit profile. Record in `docs/v2-open-questions.md` if taken. |

---

## 5. Snapshot diffs — gate 6

**Nothing here yet: run 0 produced zero snapshot diffs.**

Procedure when one appears:

1. The runner harvests DM's `.received.txt` files (written to
   `tests/DependencyModules.Tests/bin/Debug/<tfm>/Snapshots/`) into
   `<log-dir>/received-<run-id>/`. Diff against the committed `.verified.txt`.
2. Add a row below. **Do not touch the `.verified.txt`.**
3. If the diff is a *regression*, it is a bug — fix the emitter, do not justify it.

| Run | Snapshot | What changed | Cause (change # from §4) | Verdict |
|---|---|---|---|---|
| — | — | — | — | — |

Verdicts: `improvement` (V1 was wrong), `neutral` (cosmetic, semantically identical),
`regression` (fix the emitter), `human` (needs Ian's call — leave it and keep going).

---

## 6. Snapshot inventory — rule §8.1

30 committed baseline files, 6,623 lines. **None of them is ever to be regenerated by an
agent.** Both harvest mechanisms are disabled by the runner.

### DependencyModules — 17 files, 3,334 lines

All in `tests/DependencyModules.Tests/Snapshots/`. Compared by
`tests/DependencyModules.Tests/Infrastructure/Snapshot.cs`, which reads the copy in the build
output; on mismatch it writes `<name>.received.txt` beside it and fails. Re-baselining is
`UPDATE_SNAPSHOTS=1`, which writes back to the source tree — **never set it**.

**Generator output — 9 files, 981 lines. These are the CSharpAuthor oracle.**

| File | Lines |
|---|---:|
| `ModuleGenerationSnapshotTests.ModuleWithEnvironmentConditions.verified.txt` | 123 |
| `ModuleGenerationSnapshotTests.ModuleWithConstructorParametersAndProperties.verified.txt` | 114 |
| `ModuleGenerationSnapshotTests.RegistrationTypeVariants.verified.txt` | 114 |
| `ModuleGenerationSnapshotTests.ModuleWithAllServiceLifetimes.verified.txt` | 112 |
| `ModuleGenerationSnapshotTests.KeyedAndAsRegistrations.verified.txt` | 109 |
| `ModuleGenerationSnapshotTests.GenericServiceRegistrations.verified.txt` | 108 |
| `ModuleGenerationSnapshotTests.SimpleModule.verified.txt` | 104 |
| `ModuleGenerationSnapshotTests.ModuleWithCoverageExclusionDisabled.verified.txt` | 103 |
| `ModuleGenerationSnapshotTests.RecordModule.verified.txt` | 94 |

**Public API surface — 8 files, 2,353 lines.** These snapshot DependencyModules' *own*
shipped API, not generated code. A diff here means V2 leaked something into a consumer's
public surface (see A2) — that is a bug, not a formatting change.

| File | Lines |
|---|---:|
| `PublicApiTests.SourceGeneratorApi.verified.txt` | 1,783 |
| `PublicApiTests.RuntimeApi.verified.txt` | 364 |
| `PublicApiTests.TestingApi.verified.txt` | 86 |
| `PublicApiTests.NUnitApi.verified.txt` | 48 |
| `PublicApiTests.XUnitApi.verified.txt` | 44 |
| `PublicApiTests.MoqApi.verified.txt` | 10 |
| `PublicApiTests.FakeItEasyApi.verified.txt` | 9 |
| `PublicApiTests.NSubstituteApi.verified.txt` | 9 |

### Hardened.Framework — 13 files, 3,289 lines

**There is no `.verified.txt` anywhere in Hardened.Framework.** Its baselines are
`.approved.txt`, all in `src/PublicApi/Hardened.PublicApi.Tests/Approved/`, compared by
`PublicApiSurfaceTests.cs`. Re-baselining is `APPROVE_PUBLIC_API=1` — **never set it**.

Like DM's `PublicApiTests.*`, these snapshot Hardened's own runtime assemblies rather than
generator output, so they are an A2 tripwire rather than an emitter oracle: `Hardened.Requests.Abstract`,
`Hardened.Requests.Runtime`, `Hardened.Requests.Serializers.Newtonsoft`, `Hardened.Requests.Testing`,
`Hardened.Shared.Runtime`, `Hardened.Shared.Testing`, `Hardened.SourceGeneration.Testing`,
`Hardened.Templates.RazorBlade`, `Hardened.Web.AspNetCore.Runtime`, `Hardened.Web.Kestrel.Runtime`,
`Hardened.Web.Runtime`, `Hardened.Web.StaticContent`, `Hardened.Web.Testing`.

Hardened's generator suites do not use file snapshots at all — they assert on emitted
strings and, in `IntegrationTests/`, compile and run the generated code. That is why a
Hardened failure usually looks like a compile error rather than a diff.

---

## 7. Toolchain notes

Constraints that will produce a red that is **not** a V2 regression. Check these before
reporting one.

- **SDKs on the build machine:** 8.0.401, 10.0.302, 11.0.100-preview.7.26381.103.
- **DependencyModules pins 10.0.302** via `global.json`
  (`rollForward: latestFeature`, `allowPrerelease: false`).
- **Hardened.Framework has no `global.json`** and therefore resolves to the newest installed
  SDK — 11.0.100-preview today. Both suites are green on that, but a red that appears only in
  Hardened and mentions the SDK, MSBuild, or a workload is a toolchain issue, not a
  CSharpAuthor one. Pin with `DOTNET_ROLL_FORWARD` or a temporary `global.json` to confirm.
- **`DependencyModules.Tests` multi-targets `net8.0;net10.0`** via `$(LibraryTargetFrameworks)`,
  so one "735" is really two runs of 735. Report both.
- **`Hardened.SourceGenerator.Tests` targets `net8.0` only.**
- Generator projects are `netstandard2.0` with `EnforceExtendedAnalyzerRules=true`: analyzer
  rules such as RS1035 (no `System.IO` from an analyzer) apply to CSharpAuthor's source once
  it is compiled in, even though they do not apply to CSharpAuthor's own assembly build.
- Both consumer repos restore from nuget.org and a `github-ipjohnson` feed. Restores are
  warm; the runner sets `NUGET_INTERACTIVE=false` so a feed asking for credentials fails
  rather than hangs.
- V1's `CSharpAuthor/` is 76 `.cs` files. If the compile-item count the runner reports
  collapses toward zero, the glob is wrong, not the code.

---

## 8. Run log

| Run | CSharpAuthor | Scope | DM (net8/net10) | HF SourceGenerator.Tests | Result |
|---|---|---|---|---|---|
| 0 | `7ab7145` `feature/v2` — V1 code, baseline | core | 735/735, 735/735 | 468/468 | PASS, 15 s |
| 0 | `7ab7145` `feature/v2` — V1 code, baseline | full | 735/735, 735/735 | 468/468 | PASS — 35 assemblies, 6,186 tests, 0 failures, 36 s |
