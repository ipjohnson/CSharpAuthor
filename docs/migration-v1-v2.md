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
Every behaviour change, with the mechanical fix.

<!-- Each build area appends its own section. Keep sections separate so they merge cleanly. -->

## Type model

### Snapshot diff: `PublicApiTests.SourceGeneratorApi` (DependencyModules) — 1 test, per TFM

`DependencyModules.Tests` snapshots the **public API surface** of its generator assembly, and
CSharpAuthor is source-compiled into it, so every public member of this library is in that snapshot.
Adding the three `ITypeDefinition` members §7 requires therefore changes it, by construction. It is
the one consumer test that fails, once on `net8.0` and once on `net10.0`:

```
DependencyModules.Tests   net8.0    734 passed   1 failed
DependencyModules.Tests   net10.0   734 passed   1 failed
Hardened.SourceGenerator.Tests      468 passed   0 failed
```

**The load-bearing fact: all nine of DependencyModules' generator-*output* snapshots stay clean.**
`ModuleGenerationSnapshotTests.{SimpleModule, RecordModule, GenericServiceRegistrations,
KeyedAndAsRegistrations, RegistrationTypeVariants, ModuleWithAllServiceLifetimes,
ModuleWithConstructorParametersAndProperties, ModuleWithEnvironmentConditions,
ModuleWithCoverageExclusionDisabled}` all pass, so **the generated C# is byte-identical** — this is an
API-shape diff, not an output diff. The only `.received.txt` the run harvests is the API one.

This is the expected shape for any branch that adds public surface: the `declarations` branch was
measured independently and produced the same single failure.

Not re-baselined (rule 8.1). `APPROVE_PUBLIC_API` was never set.

#### The diff, and why each part of it has to be there

Every line is surface §7 mandates. Incidental helpers were demoted rather than approved: the write
and rank helpers on `BaseTypeDefinition` and its two rank-carrying constructors are now
`private protected`, the rank-carrying `TypeParameterDefinition` constructor is `internal`, and the
`ToEquatableArray` extension method was deleted in favour of `EquatableArray<T>.From`. That follows
§3's precedent for generated nodes, and costs the consumers nothing — both source-include the
library. Widening any of them later is not a breaking change; the reverse would be.

**`ITypeDefinition` — three members (the §7 defects):**

```
+   IReadOnlyList<int> ArrayRanks { get; }        // int[] vs int[][] vs int[,] - a bool cannot
+   ITypeDefinition? ContainingType { get; }      // Outer.Inner, not Inner
+   ITypeDefinition MakeArray(int rank);          // int[,], and MakeArray().MakeArray() == int[][]
```

These could not be given default bodies: `netstandard2.0` has no default interface members. Verified
that neither consumer implements `ITypeDefinition` — both construct through `TypeDefinition.Get` and
`new GenericTypeDefinition`, whose existing signatures are untouched.

**Their implementations**, on `BaseTypeDefinition`, `TypeDefinition`, `GenericTypeDefinition` and
`TypeParameterDefinition`, plus one constructor and two factories to build the shapes they describe:

```
+   public static TypeDefinition Get(TypeDefinitionEnum, string ns, string name,
+                                    IReadOnlyList<int>? arrayRanks, bool isNullable = false,
+                                    ITypeDefinition? containingType = null)
+   public static TypeDefinition GetNested(ITypeDefinition containingType, string name,
+                                          TypeDefinitionEnum definitionEnum = ClassDefinition)
+   public GenericTypeDefinition(TypeDefinitionEnum, string ns, string name,
+                                IReadOnlyList<ITypeDefinition> closingTypes,
+                                IReadOnlyList<int>? arrayRanks, bool isNullable = false,
+                                ITypeDefinition? containingType = null)
+   public TypeDefinition(TypeDefinitionEnum, string ns, string name,
+                         IReadOnlyList<int>? arrayRanks, bool isNullable = false,
+                         ITypeDefinition? containingType = null)
```

**Moves, not removals.** `Equals`, `GetHashCode` and `MakeArray()` appear as removed from
`TypeDefinition` and `GenericTypeDefinition` and added on `BaseTypeDefinition`. They are the same
members, deduplicated onto the base class; every call site binds exactly as it did. `ToString()` stays
where it was, on both subclasses, in its 1.x shape.

**`CSharpAuthor.Collections.EquatableArray<T>`** — new, and §7 asks for it by name. It is in a
sub-namespace rather than `CSharpAuthor`; see `docs/v2-open-questions.md` for why.

### `typeof(IntPtr)` and `typeof(UIntPtr)` now write `nint` and `nuint`

`float`, `char` and `sbyte` used to reach output under their reflection names — `Single`, `Char`,
`SByte` — which is legal C# naming a different-looking type and pulling `using System;` into files
that had no other reason to hold it. All the predefined types now write as their C# keyword.

`nint` and `nuint` come with a caveat that the other keywords do not: they are the *same runtime
types* as `IntPtr` and `UIntPtr`, so reflection cannot tell them apart, and `TypeDefinition.Get(typeof(IntPtr))`
now writes `nint`. That is the same type to the compiler, but **the keyword form requires C# 9 in the
consuming code**.

**Fix, if you emit for a pre-C#9 target:** write the type by name instead of by `Type` —
`TypeDefinition.Get("System", "IntPtr")` — until the emit profile gates keyword selection on language
version (see `docs/v2-open-questions.md`).

### Nested types now carry their containing type

`TypeDefinition.Get(typeof(Outer.Inner))` used to produce a type named `Inner` with `Outer` nowhere in
it, so it reached output as `Inner` — and as `global::Ns.Inner` in `TypeOutputMode.Global`. Both name
a type that does not exist. It now writes `Outer.Inner` and `global::Ns.Outer.Inner`.

`Name` and `Namespace` are unchanged (`Inner` and `Ns`, matching reflection); the container is a new
`ContainingType` on `ITypeDefinition`. Two consequences:

- `Equals` and `GetHashCode` include the container. Two nested types that differ only in their
  container used to compare **equal**; they no longer do. If you keyed a dictionary on a type
  definition and relied on that collision, you were relying on a bug. `ToString()` is unchanged — it
  is still namespace-and-name, with no container in it (see `docs/v2-open-questions.md`).
- Rebuilding a type from its parts — `TypeDefinition.Get(t.TypeDefinitionEnum, t.Namespace, t.Name, t.IsArray)`
  — drops the container and the array ranks, exactly as it always dropped generic arguments.
  **Fix:** use the type definition you already have, or the new
  `TypeDefinition.Get(enumValue, ns, name, arrayRanks, isNullable, containingType)` overload.

### `TypeDefinition.Get` on a generic parameter returns a `TypeParameterDefinition`

`TypeDefinition.Get(typeof(List<>).GetGenericArguments()[0])` used to return a `TypeDefinition` named
`T` in namespace `System.Collections.Generic` — reflection reports a parameter's namespace as its
declaring type's — so it wrote `global::System.Collections.Generic.T` in `Global` mode. It now returns
a `TypeParameterDefinition`, which writes `T` in every mode.

This also keeps the nested-type change from making it worse: a generic parameter's `DeclaringType` is
the type that declares it, and treating that as a container would have written `List.T`.

### Array ranks are modelled, not a flag

`IsArray` was a `bool`, so the model could not tell `int[]` from `int[][]` from `int[,]`. Three
outputs were wrong and none of them threw:

| Input | 1.x wrote | 2.0 writes |
|---|---|---|
| `typeof(int[,])` | `Int32[,][]` | `int[,]` |
| `typeof(int[][])` | `Int32[][][]` | `int[][]` |
| `MakeArray().MakeArray()` | `int[]` | `int[][]` |

`IsArray` still exists and still means "is this an array"; it is now `ArrayRanks.Count > 0`.
`MakeArray()` still means "make an array of this", and now composes — it adds an outer rank instead of
setting a flag. `MakeArray(int rank)` is new, for `int[,]`.

**Fix:** none for `IsArray` readers. Code that *round-tripped* an array through the four-argument
`TypeDefinition.Get(..., isArray: true)` flattens a jagged or multidimensional array to a single `[]`;
pass `ArrayRanks` to the new overload instead.

### `SyntaxHelpers.Is` writes the whole type

`Is(value, type)` took `type.Name` while building the tree, so it wrote a bare `Task` for
`Task<string>`, a bare `Inner` for `Outer.Inner`, `int` for `int[][]`, and — having decided on a short
name before any output mode was known — an unqualified name in `TypeOutputMode.Global`, propped up by
a `using` it added itself. It now writes the type through `IOutputContext.Write(ITypeDefinition)` like
every other construct, and the `using` follows from what was written.

**Fix:** none. Output only changes where it was previously wrong; a simple named type in short-name
mode reads exactly as before.

### `ITypeDefinition` gained three members

`ContainingType`, `ArrayRanks` and `MakeArray(int rank)`. Callers are unaffected. **Anyone
implementing `ITypeDefinition` from outside the library must add the three members** —
`netstandard2.0` has no default interface members, so they could not be given bodies. Neither
consumer repository implements the interface; both build unchanged.
# Migration V1 → V2

## Declarations, literals and statements

Owner: `declarations` builder. Branch `v2/declarations`.

**No existing test asserted buggy behaviour.** All 139 tests in `CSharpAuthor.Tests`
passed unmodified through every change in this area, and none was edited or
re-baselined. Where a change alters emitted output, it is listed below.

Three existing tests did reject an early version of the identifier escaping —
`IndexerPropertyTests` — and they were right: an indexer is a `PropertyDefinition`
named `this`, where `this` is the keyword and not a name, so it must not become
`@this`. The library was fixed, not the tests.

### Source-compatible additions

Nothing in the public surface was removed or renamed. New API:

| Added | Why |
|---|---|
| `ComponentModifier.ProtectedInternal`, `.PrivateProtected` | Names for the existing flag pairs. Callers already writing `Protected \| Internal` need no change. |
| `ComponentModifierExtensions` | `GetAccessibilityKeywords`, `GetAccessorAccessibilityKeywords`, `GetModifierKeywords`. |
| `LiteralFormatter` | One place that turns a value into C# literal text. |
| `CSharpIdentifier` | `Escape`, `EscapeReference`, `EscapeQualified`, `IsReservedKeyword`. |
| `MethodDefinition.OmitBody` | Declares the defining half of a `partial` method. There was no way to express one. |
| `ForDefinition` constructors, `Variable`, `Initializer`, `Condition`, `Increment` | The class existed but wrote nothing. |
| `BaseBlockDefinition.For(...)`, `.Continue()` | Nothing returned a `ForDefinition`; `Continue` had no equivalent to `Break`. |

### Output changes

Each of these changes generated text. The first five are the ones most likely to
appear in a consumer snapshot diff.

| # | Was | Now | Why |
|---|---|---|---|
| 1 | `1.5` for a `double` | `1.5d` | Without a suffix, `Get(1.0d)` and `Get(1)` both emit `1`, so the source type is lost. Where the emitted text is an argument, `1` binds `Foo(int)` and `1d` binds `Foo(double)` — a silently different overload. Consistent with `f`, `m`, `L`, `U`, `UL`. |
| 2 | `1.5` for a `float` | `1.5f` | `float f = 1.5;` is CS0664. |
| 3 | `1` for `long`/`uint`/`ulong` | `1L` / `1U` / `1UL` | A value above `int.MaxValue` did not fit the literal it was written as. |
| 4 | `readonly static` on a field | `static readonly` | The conventional order. |
| 5 | `a` for a `char` | `'a'` | `char c = a;` is CS0103. |
| 6 | `"he said "hi""` | `"he said \"hi\""` | Only affects values that contain something needing an escape. |
| 7 | `internal` for `Protected \| Internal` | `protected internal` | |
| 8 | `protected` for `Private \| Protected` | `private protected` | **Widened access.** See below. |
| 9 | `{ }` after an `abstract` method | `;` | CS0500. |
| 10 | `partial`, `readonly` dropped | emitted | |
| 11 | `override` for `Sealed \| Override` | `sealed override` | |
| 12 | a name that is a keyword | `@name` | Only affects names that are one of C#'s 77 reserved words. |
| 13 | property setter: `internal set` emitted nothing | `internal set` | Only `private` and `protected` were handled. |
| 14 | property with a bodied `init` accessor emitted `set` | `init` | `IsInit` was honoured only on the auto-property path. |

### The one to read twice

`Private | Protected` emitted `protected`. `private protected` is the most
restrictive accessibility C# has — a derived type **in this assembly**.
`protected` is reachable from a derived type in **any** assembly. Every member a
generator declared as `private protected` was published outside its own
assembly, and because the result compiled, nothing reported it.

If a consumer's snapshot changes from `protected` to `private protected`, that
snapshot was recording the defect.

### Not changed, deliberately

- **A `string` passed to `CodeOutputComponent.Get` is still emitted unquoted.**
  Throughout this library a string argument is a fragment of code —
  `AddCode("Foo()")`, `Get("Lifetime.Scoped")` — and quoting it would turn every
  one of them into text. `SyntaxHelpers.QuoteString` is how a caller asks for a
  string literal, and that now escapes.
- **Modifier combinations are not validated.** `abstract sealed` on a method is
  CS0238 and gets written anyway, consistent with the rest of the library, which
  writes what it is handed. A modifier the compiler rejects is better than one
  dropped in silence.
- **An indexer's name is not escaped.** `this[int index]`, not `@this[...]`.

### Verification

Beyond the unit tests, the emitted output was compiled by the real C# compiler:
a file exercising every fix above was generated **under `de-DE`**, compiled
clean on `net8.0`, and then executed to confirm every literal equals the value
it was generated from — including a string containing `"`, `\`, CRLF, tab, NUL,
a control character, and a surrogate pair.

### Culture-dependent APIs (RS1035)

Both consumers build with `EnforceExtendedAnalyzerRules=true`, and this library
is compiled **into** them from source, so a culture-dependent API here is a hard
error in their build and invisible to `dotnet test CSharpAuthor.Tests`. The whole
library was swept, not only the numeric emission sites:

| Site | Was | Now |
|---|---|---|
| `BaseBlockDefinition.AddCode` | `IndexOf(placeholder, StringComparison.CurrentCulture)` ×2 | `StringComparison.Ordinal` |
| `BaseBlockDefinition.AddCode` | `"{arg" + (index + 1)` ×2 — `int.ToString()` under the current culture | `.ToString(CultureInfo.InvariantCulture)` |
| `MethodDefinition.GetUniqueVariable` | `prefix + VariableCount++` | `.ToString(CultureInfo.InvariantCulture)` |
| `AttributeDefinition.WriteBody` | `EndsWith(string)` — the culture-sensitive overload | `EndsWith(string, StringComparison.Ordinal)` |

Locating a fixed placeholder is an exact-text question, so ordinal is also the
correct answer on the merits, not only for the analyzer.

Clean already, and confirmed by sweep: no `ToLower`/`ToUpper`, no `Parse`, no
`Convert`, no `string.Format`, no file I/O, no `Environment`. `string.Compare`
was already explicit `StringComparison.Ordinal` everywhere. The `IndexOf(char)`
overloads are ordinal by definition and were left alone.

netstandard2.0 was respected throughout: surrogate pairs are handled manually
with `char.IsHighSurrogate`/`IsLowSurrogate` (there is no `Rune`), and nothing
uses `string.Contains(char)` or `HashCode`.

### Public API surface

`DependencyModules`' `PublicApiTests` snapshot legitimately changes, because §7
requires new public API. After demoting every incidental helper to `internal`,
the diff is exactly:

```
BaseBlockDefinition:  + Continue()
                      + For(IOutputComponent?, IOutputComponent?, IOutputComponent?)
                      + For(string, object, object)
ComponentModifier:    + ProtectedInternal = 1026
                      + PrivateProtected  = 6
ForDefinition:        + two constructors
                      + Condition, Increment, Initializer, Variable
MethodDefinition:     + OmitBody
```

Every line is a §7 line item except `MethodDefinition.OmitBody`, which is the
only way to declare the defining half of a `partial` method and so is mandated in
substance — see `docs/v2-open-questions.md` §9.

Demoted to `internal` and therefore **absent** from the diff: `LiteralFormatter`,
`CSharpIdentifier`, `ComponentModifierExtensions` (whole classes), and
`MethodDefinition.IsBodyless` (now `private`). That removed 25 of the 38 added
lines.

**This snapshot has not been re-baselined and must not be** (rule 8.1). Adding
public API is exactly the kind of change that is the human's call, so
`DependencyModules` sits at **733/735** — the two failures are the same single
`PublicApiTests` test, counted once per target framework. It cannot reach 735/735
without someone approving the addition above.

### Proof that `private protected` no longer widens access

A string assertion can be satisfied by a coincidence, so this was checked against
the compiler as a differential. Two types were generated that differ **only** in
that modifier, and one separate assembly was compiled against each, deriving from
the type and touching a method, a field and a property:

| Generated as | Cross-assembly derived access |
|---|---|
| `protected` | **compiles** — correct, it is reachable from a derived type in any assembly |
| `private protected` | **fails** — the members do not resolve at all |

Same generator, same consumer source, one modifier different, opposite results.
Had the emitter still widened `private protected` to `protected`, both would have
compiled.
# Migration, V1 → V2

> Sections below are contributed by the `output-context` builder. Other builders add their own;
> merge rather than replace.

## Output context — what changed

### Source compatibility

#### Breaking: two members left `IOutputContext`

```diff
  void AddImportNamespace(string ns);
- void AddImportNamespace(ITypeDefinition typeDefinition);
  void AddImportNamespaces(IEnumerable<string> namespaces);
- void AddImportNamespaces(IEnumerable<ITypeDefinition> typeDefinition);
```

This is invariant 1 made structural rather than asked for: a writer holds an `IOutputContext`, so a
writer can no longer declare the namespace of a type. It writes the type and the namespace follows.

Both are still **public on `OutputContext` itself**, so `new OutputContext().AddImportNamespace(type)`
compiles exactly as before, and they now do nothing in a mode that qualifies its types.

**Mechanical fix**, for code that called them through the interface:

```diff
- context.AddImportNamespace(someType);   // then wrote the type as a string
+ context.Write(someType);                // write the type; the namespace is derived
```

or, for a namespace that is genuinely not derivable from a type — an extension method — use the
string overload, which is unchanged:

```csharp
context.AddImportNamespace("Microsoft.Extensions.DependencyInjection");
```

Measured: **0 call sites** across `DependencyModules` and `Hardened.Framework`, so neither consumer
is affected by this removal.

Everything else on `IOutputContext` is unchanged, so the other 198+ references compile untouched.

`OutputContext` keeps its constructor, its properties and all its members. Internally it records
segments and turns them into text in `Output()` instead of appending to a `StringBuilder`.

`OutputContextOptions` gains four properties, all defaulting to the V1 behaviour:

| Property | Default | Effect |
|---|---|---|
| `BraceStyle` | `BraceStyle.Allman` | Where the opening brace goes. |
| `AliasCollisions` | `true` | Alias a contested short name instead of emitting CS0104. |
| `EmitExplicitUsings` | `true` | Whether a namespace asked for *by name* survives a qualifying mode. |
| `ContainingNamespace` | `null` | The file's own namespace, dropped from the using list when set. |

New public types: `BraceStyle`, `AttributeTypeReference`.

New public members: `OutputContext.IndentDepth`, `CodeOutputComponent.FromParts`,
`CodeOutputComponent(ITypeDefinition, string?)`, `CodeOutputComponent.Get(ITypeDefinition, string, bool)`.

### Output differences — read this before re-baselining anything

These change what a generator emits. Each is a fix for something on the §7 defect list; none is a
snapshot to re-baseline without reading.

#### 1. A file in `TypeOutputMode.Global` or `FullName` no longer emits usings derived from its types

This is the one the handoff opens with. `ParameterDefinition`, `FieldDefinition`, `EventDefinition`,
`MethodDefinition` (return type and explicit interface implementation) and `AttributeDefinition` all
called `AddImportNamespace` beside the type they were about to write, and that call ran whatever the
output mode was. A file that qualified every name still got

```csharp
using Microsoft.Extensions.DependencyInjection;   // in a file where nothing is unqualified
```

Namespaces are now derived from the types the file actually wrote, and a derived namespace is not
emitted in a mode that qualifies — the qualification already says everything the directive would.

**A namespace asked for by name is not affected.** `AddUsingNamespace("…​.Extensions")` still emits
its directive in `Global` mode, because an extension method is found through a `using` and no other
way; qualification cannot stand in for it. Set `EmitExplicitUsings = false` for a file that really
must have none. Both consumers rely on this: `DependencyFileWriter` asks for
`Microsoft.Extensions.DependencyInjection.Extensions` by name so that `TryAddSingleton` resolves.

This is a deliberate reading of the defect list entry "`Global` mode → still emits usings → none":
the *stray, derived* directive goes; the one the caller asked for stays. Recorded in
`docs/v2-open-questions.md`.

#### 2. `ServiceLifetime.Transient` and anything else written as a bare string

The other half of the same defect. `CodeOutputComponent.Get("ServiceLifetime.Transient")` is a
string; it tracks no namespace, so it resolved *only* because of the stray directive above. Removing
either alone breaks the generator, which is why both moved together.

**Mechanical fix, in the generator, per call site:**

```diff
- parameters.Add(CodeOutputComponent.Get("ServiceLifetime.Transient"));
+ parameters.Add(CodeOutputComponent.Get(KnownTypes.Microsoft.ServiceLifetime, "Transient"));
```

where the first argument is any `ITypeDefinition` for
`Microsoft.Extensions.DependencyInjection.ServiceLifetime`. The component then writes
`global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient` in a qualifying file and
`ServiceLifetime.Transient` plus its using in a short-name one.

`DependencyFileWriter.cs` lines 296, 299, 302, 375, 378, 381 in `DependencyModules` are the six call
sites. **Until they are changed, files generated in `Global` mode emit a bare `ServiceLifetime` with
no directive to resolve it and will not compile.** The string overload still exists and still
behaves as it did; nothing forces the change except correctness.

#### 3. `AddCode` no longer renders its types at the call site

```csharp
method.AddCode("var value = new {arg1}();", someType);
```

V1 replaced `{arg1}` with the type's short name immediately, so the statement was already text before
the file knew its output mode. It now holds the type, which means:

- in `Global`/`FullName` mode the substituted type is **qualified** where it used to be bare;
- the type takes part in the name plan, so it is aliased when its name is contested;
- in `ShortName` mode the output is unchanged.

`[argN]` substitution is unchanged — it is text by definition.

#### 4. An attribute is written as a type

`AttributeDefinition` wrote its type's name as a string and declared the namespace separately. It now
writes an `AttributeTypeReference` through `IOutputContext.Write(ITypeDefinition)`. In `ShortName`
mode the output is unchanged (`[Marker]`, plus the using). In a qualifying mode it becomes
`[global::Sample.Annotations.Marker]` where it used to be `[Marker]` with a stray directive.

`AttributeTypeReference` writes whatever the type writes and then takes the `Attribute` postfix off
its simple name, rather than rebuilding the name out of `Namespace` and `Name`. Everything a type
knows about itself that a bare name does not now survives into the attribute list:

- a **generic** attribute writes its type arguments — `[Marker<int>]` where V1 wrote `[Marker]`;
- a **nested** attribute keeps its container — `[Outer.Inner]` where V1 wrote `[Inner]`;
- anything the type model learns to write later is written here too, with no change to this file.

`Name` deliberately keeps the postfix, because an alias has to name the type that exists:
`using SecondMarker = Second.MarkerAttribute;`, not `Second.Marker`, which is not a type.

#### 5. Same-name collisions are aliased

Two types with the same short name and different namespaces used to emit both names bare, with both
namespaces imported: CS0104, ambiguous reference. The second now gets
`using SecondModel = Second.Model;` and is written as `SecondModel`, and `using Second;` is dropped
where nothing else needs it.

Where the other namespace cannot be dropped — something else in it is still written plainly — *both*
sides are aliased, because leaving one bare would put the ambiguity straight back.

A **generic** cannot be aliased (a `using` alias names a closed type), so colliding generics are
written with their namespace in front instead: `First.Box<int>` and `Second.Box<int>`.

Set `AliasCollisions = false` to get the V1 behaviour back.

#### 6. `WriteLine(string)` and `WriteIndentedLine(string)` use `Options.NewLine`

They called `StringBuilder.AppendLine`, which uses `Environment.NewLine`. On Windows that emitted
`\r\n` from those two methods and `\n` from `WriteLine()` in the same file, whatever `Options.NewLine`
said. All line breaks now come from `Options.NewLine`. **On Linux and macOS nothing changes**; on
Windows a file that previously had mixed endings now has consistent ones.

#### 7. `Write((ITypeDefinition)null)` no longer throws

V1 threw `NullReferenceException` from inside `AddImportNamespace` in `ShortName` mode and wrote
nothing in the other modes. It now writes nothing in every mode.

#### 8. A `using` for a namespace with a keyword segment is escaped

`GenerateUsingStatements` wrote `$"using {ns};"` straight from the raw string. A namespace with a
segment that is a C# keyword produced `using Company.event.Models;` — CS1001 — above a namespace
*declaration* that was correctly written as `namespace Company.@event.Models`. Both halves now go
through `CSharpIdentifier.EscapeQualified`, and alias names and alias targets are escaped too.

Found by the `declarations` builder; `CSharpIdentifier` is their file, copied verbatim into this
branch so it compiles standalone. The two copies are byte-identical and merge cleanly.

#### 9. A file no longer imports its own namespace

`CSharpFileDefinition` tells the context which namespace it is about to open, and a `using` naming
it is dropped. Everything the file declares is in scope without it. Only the file's outermost
namespace counts — a nested one does not enclose its siblings, so dropping it could drop a directive
something else in the file needs. `OutputContextOptions.ContainingNamespace` does the same for a
caller writing into a context directly, with no `CSharpFileDefinition` to notice.

---

## Measured against the consumer suites

Run from this branch with
`scripts/run-consumer-tests.sh <clone> --scope core`, `UPDATE_SNAPSHOTS` unset.

| Suite | Baseline (published V1) | This branch |
|---|---|---|
| `DependencyModules.Tests` net8.0 | 735 passed / 0 failed | **384 passed / 351 failed** |
| `DependencyModules.Tests` net10.0 | 735 passed / 0 failed | **384 passed / 351 failed** |
| `Hardened.SourceGenerator.Tests` net8.0 | 468 passed / 0 failed | **265 passed / 203 failed** |

**With the `Global`-mode fix in place and the consumers' source unchanged, neither consumer's
generated code compiles.** That is not a snapshot diff to justify; it is the other half of the §1
interlock coming due, and it is larger than §1 described. Every failure is one of two shapes, and
both are the same defect: **a name written as a string, resolved by a `using` that nothing asked
for.** The directive was there only because some type from that namespace happened to be written
somewhere in the file, and removing it is what makes the string visible.

### Shape A — an extension method invoked as an instance method (CS1061)

An extension method is reached through a `using` and no other way; `global::` cannot name one. The
consumers never asked for these namespaces — they came free with a derived import.

| Consumer | Call | Namespace it needs |
|---|---|---|
| both | `AddSingleton`, `AddScoped`, `AddTransient`, `AddKeyedSingleton` on `IServiceCollection` | `Microsoft.Extensions.DependencyInjection` |
| both | `GetRequiredService`, `GetRequiredKeyedService` on `IServiceProvider` | `Microsoft.Extensions.DependencyInjection` |
| Hardened | `AddLogging`, `BuildServiceProvider` on `ServiceCollection` | `Microsoft.Extensions.DependencyInjection` |
| Hardened | `Get` on `IDictionary<string, StringValues>` | Hardened's own extensions namespace |

**Mechanical fix**, one line per generated file, beside the request the writer already makes:

```diff
  classDefinition.AddUsingNamespace("Microsoft.Extensions.DependencyInjection.Extensions");
+ classDefinition.AddUsingNamespace("Microsoft.Extensions.DependencyInjection");
```

`DependencyFileWriter.cs:119` is the DependencyModules line to add it beside. The same is needed in
whichever writer emits `*.Interceptors.g.cs` and `*.ConventionDependencies.g.cs`, and in
Hardened's `ServiceProviderFileGenerator`, `ApplicationEntryPointFileWriter`,
`FunctionIncrementalGenerator` and the request-binding writers.

A namespace asked for by name is **kept** in `Global` mode precisely so this fix works — see
`docs/v2-open-questions.md` §1.

### Shape B — a type name written as a string (CS0246 / CS0103)

| Consumer | Written as | Where |
|---|---|---|
| DependencyModules | `"ServiceLifetime.Transient"` etc. | `DependencyFileWriter.cs:296,299,302,375,378,381`; `DependencyModuleWriter.cs:193,262,286,377` |
| Hardened | `$"new ExecutionRequestHandlerInfo(…)"` | `Requests/HandlerInfoCodeGenerator.cs` |
| Hardened | `Exception` in the application-root writer | `Application.ApplicationRoot` output |

**Mechanical fix** — hand over the type instead of its name:

```diff
- parameters.Add(CodeOutputComponent.Get("ServiceLifetime.Transient"));
+ parameters.Add(CodeOutputComponent.Get(KnownTypes.Microsoft.DependencyInjection.ServiceLifetime, "Transient"));
```

```diff
- field.InitializeValue = new CodeOutputComponent($"new ExecutionRequestHandlerInfo({args})");
+ field.InitializeValue = CodeOutputComponent.FromParts(
+     new object[] { "new ", KnownTypes.Requests.ExecutionRequestHandlerInfo, $"({args})" });
```

Either of those, or one `AddUsingNamespace` per file, makes the file compile. Handing the type over
is the one that keeps working when the output mode changes.

### Snapshot diffs

One snapshot produced a `.received.txt`: `PublicApiTests.SourceGeneratorApi`, which tracks the
public surface the source-included library adds to the generator assembly. Its diff is exactly the
API table above — twenty added lines, two removed (`AddImportNamespace(ITypeDefinition)` and
`AddImportNamespaces(IEnumerable<ITypeDefinition>)`) — and nothing else.

The nine generator-output snapshots did not reach their comparison: they assert the generated code
compiles first, and it does not, for the reasons above. **Nothing was re-baselined.**
# Migrating V1 to V2

<!-- Sections are agent-scoped so that concurrent builders append rather than collide. -->

## expressions

### Breaking changes

None. The expression layer is additive: it introduces the namespace
`CSharpAuthor.Expressions` and does not add to, remove from or alter any type in the
`CSharpAuthor` namespace. Existing code compiles and emits byte-for-byte identical output.

### Snapshot diff — `DependencyModules.Tests.PublicApiTests.SourceGeneratorApi`

**Not re-baselined.** Recorded here per rule 8.1; approving it is the human's call.

CSharpAuthor is source-compiled into `DependencyModules.SourceGenerator`, so every public
type it declares appears in that assembly's approved public-API snapshot. The new
expression layer is public by design — it is the ergonomic front door a generator author
uses — and therefore shows up.

Measured against
`tests/DependencyModules.Tests/Snapshots/PublicApiTests.SourceGeneratorApi.verified.txt`:

| | |
|---|---|
| Lines added | 296 |
| Lines removed | **0** |
| Lines changed | **0** |

Every added line is inside `namespace CSharpAuthor.Expressions`. Nothing existing moved.
The diff is a pure addition of new API surface, not a change to old API surface, and no
behaviour of any V1 type is affected.

Consumer results with the layer in place:

| Suite | Result |
|---|---|
| `DependencyModules.Tests` (net8.0) | 734 passed, 1 failed — the API snapshot above |
| `DependencyModules.Tests` (net10.0) | 734 passed, 1 failed — the same test |
| `Hardened.SourceGenerator.Tests` (net8.0) | 468 passed, 0 failed |

If the intent is that a source-included CSharpAuthor should not widen the consumer's public
surface at all, the mechanical fix is to compile the layer as `internal` under the
`PackageCSharpAuthorIncludeSource` condition. Internal types are still fully usable by the
consumer that source-includes them; they simply stop appearing in its public API. That
choice affects every public type V1 already ships, not just this layer, so it is left open
rather than taken unilaterally.

### New API worth knowing about

- `Ex` — expressions with automatic, precedence-correct parenthesisation. A bare `string`
  converts to an *identifier*; literals are explicit (`Ex.Str`, `Ex.Int`, `Ex.Char`).
- `Pat` — patterns, including the `and`/`or`/`not` combinators and their own precedence.
- `Raw` — the escape hatch, which holds `ITypeDefinition` references unrendered, exposes
  them through `Raw.TypeReferences`, and infers its own precedence.
- `CSharpText` — invariant-culture numeric literals, escaped string and character
  literals, and keyword-escaped identifiers. Available directly for writers that are not
  yet using `Ex`.

`CSharpText` also supersedes `SyntaxHelpers.QuoteString`, which does not escape its input
and so emits `"he said "hi""` for a string containing quotes. `QuoteString` is left alone
here because it is another agent's territory; it is reported as a live defect.
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

---

## Wave 2, defect sweep — behaviour changes and blocked improvements

### Behaviour that changed

Every one of these turns output that did not compile, or compiled and meant something else, into
output that means what the caller asked for. None of them moved a consumer test: `DependencyModules`
and `Hardened.SourceGenerator.Tests` report the same counts before and after (383/735 and 265/468 —
both already failing for the invariant-1 reason recorded above, and neither made worse).

| Call | Was | Is | Why it matters |
|---|---|---|---|
| `Property(StaticCast(T, x), "M")` | `(T)x.M` | `((T)x).M` | `(T)x.M` parses as `(T)(x.M)`. The cast applied to the member, not to the receiver, and it compiles whenever the wrong reading is well typed. Same for `Invoke` and `Index` on a cast. |
| `Property(Await(x), "Y")` | `await x.Y` | `(await x).Y` | awaited the member rather than the value |
| `CodeOutputComponent.Get(someEnum)` | `Singleton` | `Lifetime.Singleton`, with the type unrendered so the namespace is derived | §1's `ServiceLifetime` defect at its source. Flags become `A \| B`; a value with no name becomes `(Lifetime)5`. |
| `AddCode("{arg1}", someEnum)` | `Singleton` | `Lifetime.Singleton` | the same, through the substitution path. `[argN]` — the raw hatch — is unchanged. |
| `CodeOutputComponent.Get(new[,] { … })` | flattened into a rank-1 initializer | `new int[,] { { 1, 2 }, { 3, 4 } }` | a different value of a different type |
| `CodeOutputComponent.Get(new int[0])` | `new int[]` | `new int[] { }` | CS1586 |
| `CodeOutputComponent.Get(null)` | the empty string | `null` | `private string f = ;`, CS1525 |
| `new GenericTypeDefinition(…, no arguments)` | `Thing<>` | `Thing` | CS1031 outside `typeof`. `MakeOpenType()` is unchanged. |
| a type or type parameter named after a keyword | `event`, `class Box<int>` | `@event`, `class Box<@int>` | CS1001. Keyword aliases (`int`, `string`) are untouched: only a type with a namespace or a container is escaped. |
| `Modifiers = Public \| Internal` | `internal` | `public` | silently narrowing |
| `Modifiers = NoAccessibility` on a field or property | `     int f;` | `    int f;` | a stray space, in the diff of every snapshot such a member appears in |
| a leading trait on `CSharpFileDefinition` | written **below** the usings | first line of the file | `// <auto-generated/>` only marks a file from line one |
| `Comment` on `CSharpFileDefinition` or `NamespaceDefinition` | dropped in silence | written | |
| `a.CompareTo(b)` across type kinds | asymmetric, not a total order | one shared ordering | `List.Sort` may throw "IComparer.Compare() method returns inconsistent results", and which elements it compares depends on the input order |

**Equality is finer than it was.** `TypeDefinition("Ns","List")` and `GenericTypeDefinition("Ns","List",[int])`
used to compare equal in one direction. They no longer do, and `string?[]` differs from `string[]?`.
A model that caches on `ITypeDefinition` will see a cache miss the first time it runs against V2 and
correct behaviour after that.

### New API

- `ITypeDefinitionExtensions.MakeArrayOfNullable(rank = 1)` — `string?[]`, which had no
  representation at all. Refuses an `ITypeDefinition` it does not recognise rather than handing back
  the other shape.
- `BaseTypeDefinition.IsElementNullable`, `TypeParameterDefinition.IsElementNullable`. Deliberately
  **not** on `ITypeDefinition` — see `docs/v2-open-questions.md` #17.
- `OutputContext.MarkEndOfFileHeader()` — where the generated `using` directives are inserted.

### Improvements found, implemented, and reverted because a test pins the other reading

Each was written, run, and taken back out. The rule is that an existing test is never edited, so
these are the human's call. Every one is a small change plus one assertion.

| Improvement | Blocked by | Note |
|---|---|---|
| `string?[]` from `MakeNullable().MakeArray()` | `TypeDefinitionTests.ArrayRankTests.NullableGoesAfterTheShape` | the type is now expressible via `MakeArrayOfNullable`; only the composition is pinned |
| a bodyless `partial` method with no statements | `ModifierTests.ModifierMatrixTests.APartialMethodStillWritesItsBodyByDefault` | Hardened works around the current behaviour by emitting that half as raw text, with a comment saying so |
| an indexed property writing `this[…]` whatever it is named | `PropertyDefinitionTests.SimplePropertyDefinitionTests.IndexedGetSetDefinition` | **an original test, and it pins output that does not compile**: `public int Test[string index]` is CS1519 |
| `AddBaseType(Pet)` then `AddBaseType(Pet, Id)` keeping the arguments | `ClassDefinitionTests.BaseTypeArgumentTests.ABaseTypeIsNotAddedTwice` | CS7036 for a positional record |
| `TypeDefinition.Get(typeof(int)).ToString()` as `int` rather than `.int` | `TypeDefinitionTests.V1CallShapeTests.ToStringKeepsItsV1Shape` | the 1.x shape is a consumer cache key; the hash no longer uses it |

### Two adversary tests assert the wrong type

`TypeNameAdversaryTests.JaggedArrayOfMultiDimensional` expects `typeof(int[,][])` to emit
`int[][,]`, and `MultiDimensionalArrayOfJagged` expects `typeof(int[][,])` to emit `int[,][]`. Both
are the **reflection** spelling, which lists the element's ranks first; C# lists the outermost first.
Verified against the runtime: `typeof(int[,][])` has `GetArrayRank() == 2` and element `int[]`. The
library emits the correct C# for both and did before wave 2. Satisfying these tests would emit the
name of a different type, silently — and would break `ArrayRankTests`.
