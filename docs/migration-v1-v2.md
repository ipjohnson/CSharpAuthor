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

### The second source root — what actually landed, and the prediction that was wrong

§3 of the handoff says the Roslyn bridge ships as a second source folder in the same package,
gated on `PackageCSharpAuthorIncludeRoslyn`. This section used to say that when it landed,
three files would have to learn about it together — and then reasoned that the hazard was
contained because *"anything that lives outside `<root>/CSharpAuthor/` will not be compiled by
either route above."*

**That reasoning did not hold, because the bridge did not land outside `<root>/CSharpAuthor/`.**
It landed *inside* it, at `CSharpAuthor/Roslyn/` — 13 files, right in the middle of the glob
every local-checkout route uses. Two of the three files were updated and the third was not:

| File | Excludes `Roslyn/**` | |
|---|---|---|
| `CSharpAuthor/CSharpAuthor.csproj` | yes | from the compile *and* from the `src/CSharpAuthor\` pack path |
| `scripts/local-csharpauthor.targets` | yes | |
| `Hardened.Framework/src/SourceGenerators/CSharpAuthor.props` | **no — this was the miss** | the consumer's own documented local-checkout mode |

The consequence, measured: the bridge was compiled into every Hardened project that uses
CSharpAuthor, including three that have no `Microsoft.CodeAnalysis.CSharp` reference at all —

```
CSharpAuthor/Roslyn/EmitProfileRoslynExtensions.cs(6,54): error CS0234:
  The type or namespace name 'CSharp' does not exist in the namespace 'Microsoft.CodeAnalysis'
  [Hardened.OpenApi.BuildTask.csproj::TargetFramework=net472]
```

`Hardened.Idl.BuildTask`, `Hardened.OpenApi.BuildTask` and `Hardened.Smithy.BuildTask` failed to
build on **net472 and net8.0 alike**, which took **six test assemblies** down with them:
`Hardened.OpenApi.BuildTask.Tests`, `Hardened.Smithy.BuildTask.Tests`,
`Hardened.Web.SourceGenerator.Tests`, and the OpenApi, Smithy and Benchmark integration SUT
suites. V1 has no `CSharpAuthor/Roslyn/` directory, so this is V2-caused, not pre-existing. It
does not show up as a red suite either — `dotnet test` on the solution simply never produces
those assemblies, so the runner's count silently drops from 35 to 29.

**Fixed** in `docs/consumer-patches/hardened-v2.patch`: `CSharpAuthor.props` now excludes
`$(CSharpAuthorRoot)/CSharpAuthor/Roslyn/**/*.cs` from the unconditional glob, which makes the
local-checkout glob equal the package's `src/CSharpAuthor/` folder again, and adds the opt-in
half — a second `Compile` item gated on `PackageCSharpAuthorIncludeRoslyn`, mirroring
`build/CSharpAuthor.targets` — so local-checkout mode can reach the bridge at all.

The rule the original prediction should have carried: **a second source root inside
`<root>/CSharpAuthor/` is picked up by every recursive glob that points at that directory, and
every one of them has to exclude it.** There are three, and `scripts/verify-roslyn-packaging.sh`
covers only the package's own two.

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
| A6 | **Every profile type is in `CSharpAuthor.Profiles`, not `CSharpAuthor`** — `EmitProfile`, `EmitSession`, `EmitDiagnostic`, `EmitResult`, `ProfileEmitter`, `ProfiledOutputContext`, `OutputContextProfileExtensions`, `LanguageVersion`, `LanguageFeature`, `Polyfill` and the eight downlevel statements. | measured | **Mechanical fix: add `using CSharpAuthor.Profiles;` to any file that names one.** No V1 code is affected — every one of these types is new in 2.0. The move exists because two of the names collide with Roslyn's: `CSharpAuthor.LanguageVersion` vs `Microsoft.CodeAnalysis.CSharp.LanguageVersion` broke DM's benchmark harness at three sites (`benchmarks/DependencyModules.Benchmarks/Program.cs:172,196,224`, all `new CSharpParseOptions(LanguageVersion.Latest)`), and `CSharpAuthor.EmitResult` shadows `Microsoft.CodeAnalysis.Emit.EmitResult`, which `DependencyModules/tests/DependencyModules.Tests/Infrastructure/GeneratedAssembly.cs:90` uses. A `using X = …` alias does **not** rescue a consumer whose own namespace is nested under `CSharpAuthor`: enclosing-namespace members outrank using-aliases, so such a file had to fully qualify every mention. `BraceStyle` and `LanguageFeature` were checked against every public type name in `Microsoft.CodeAnalysis.CSharp` 4.10.0 and 4.14.0 and collide with nothing; `BraceStyle` stays in `CSharpAuthor` because `OutputContextOptions.BraceStyle` is core surface. Full rationale in `docs/v2-open-questions.md`, profiles §16. |

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

### 4.3 The two shapes a `Global`-mode file breaks in — measured, with the patch

B1 and B2 above were written as predictions. They have now been **measured** against
`v2/output-context`, and both consumers break in exactly two shapes and no others. Ready-made
patches live beside this file:

| Patch | Applies to | Files | Lines |
|---|---|---:|---:|
| [`consumer-patches/dependencymodules-v2.patch`](consumer-patches/dependencymodules-v2.patch) | `DependencyModules` @ `642bac3` | 4 | +29 / −6 |
| [`consumer-patches/hardened-v2.patch`](consumer-patches/hardened-v2.patch) | `Hardened.Framework` @ `81a0627` | 13 | +59 / −18 |

```
git apply /path/to/dependencymodules-v2.patch      # from the repository root
```

#### Why anything broke at all

In `TypeOutputMode.Global`, V1 emitted `using` directives that no type in the file needed —
every type was already written `global::`-qualified. Those stray directives were harmless
noise *except* that two other things in the same files were quietly leaning on them. V2 stops
emitting them (§1, invariant 1: the using list is derived from the types actually written).
The moment it does, both leaners fall over.

So the breakage is not the removal. The breakage is that a bare name and a stray directive
were each hiding the other's absence, and only one of them was ever going to be fixed.

---

#### Shape A — extension methods

**What breaks.** An extension method is found through a `using` of its namespace and through
nothing else. `global::` names a *type*; there is no syntax that names an extension method.
So the one kind of `using` a `Global`-mode file genuinely needs is the one V1 was emitting by
accident, and V2 no longer emits for free.

**What the compiler says.**

```
error CS1061: 'IServiceCollection' does not contain a definition for 'AddSingleton'
and no accessible extension method 'AddSingleton' accepting a first argument of type
'IServiceCollection' could be found (are you missing a using directive or an assembly reference?)
```

Also seen as `CS0308: The non-generic method 'IServiceProvider.GetService(Type)' cannot be
used with type arguments` — the same cause, wearing a different error number because a
non-generic instance method of that name does exist.

**The fix — one line per generated file.** Ask for the namespace by name. V2 keeps that
possible on purpose: `OutputContextOptions.EmitExplicitUsings` defaults to `true`, and a
namespace asked for **by name** survives a qualifying mode. (A namespace merely *derived*
from a written type does not, and should not — the `global::` name already said everything
the directive would have.)

```csharp
var csharpFile = new CSharpFileDefinition(model.Namespace);

// An extension method is reachable only through a using of its namespace -
// global:: cannot name one.
csharpFile.AddUsingNamespace("Microsoft.Extensions.DependencyInjection");
```

`AddUsingNamespace` is on `BaseOutputComponent`, so it can go on the file, the class, the
method, or the single statement that makes the call — whichever scope you find clearest. Put
it at the file level when everything in the file needs it; put it on the statement when only
one call does. Both end up in the same header.

**Where it was needed.** Every generated file that calls one of these:

| Namespace | Extension methods the generated code calls |
|---|---|
| `Microsoft.Extensions.DependencyInjection` | `AddSingleton` `AddScoped` `AddTransient` `AddKeyedSingleton` `AddLogging` `BuildServiceProvider` `GetService<T>` `GetRequiredService` `GetRequiredKeyedService` |
| `Hardened.Requests.Runtime.Execution` | `Get` on `IDictionary<string, StringValues>` (headers) and on `IReadOnlyList<string>` (cookies) |

DependencyModules already had the pattern — `DependencyFileWriter` asked for
`Microsoft.Extensions.DependencyInjection.Extensions` so `TryAdd*` would resolve. It just
never asked for the parent namespace, because V1 handed that over for free.

---

#### Shape B — a type written as a raw string

**What breaks.** A type spelled into a string is text by the time the library sees it. It
carries no namespace, so nothing can qualify it, alias it, or count it into the using list.
In V1 it resolved whenever some other part of the file happened to drag its namespace in. In
a file that qualifies everything, nothing does.

**What the compiler says.**

```
error CS0103: The name 'ServiceLifetime' does not exist in the current context
error CS0246: The type or namespace name 'ExecutionRequestHandlerInfo' could not be found
              (are you missing a using directive or an assembly reference?)
```

**The fix — hand the type over instead of its name.** V2 adds three ways to do it, all of
which keep an `ITypeDefinition` unrendered until the file is serialized.

*A type followed by a member of it* — `CodeOutputComponent.Get(ITypeDefinition, string)`:

```csharp
// before - "ServiceLifetime" is text and resolves only by luck
parameters.Add(CodeOutputComponent.Get("ServiceLifetime.Transient"));

// after - the type is still a type at serialization
parameters.Add(CodeOutputComponent.Get(
    KnownTypes.Microsoft.DependencyInjection.ServiceLifetime, "Transient"));
```

*A statement built from mixed pieces* — `CodeOutputComponent.FromParts`. Each element is
either a `string` or an `ITypeDefinition`:

```csharp
// before
new CodeOutputComponent($"new ExecutionRequestHandlerInfo(\"{path}\", typeof({controller.Name}))");

// after
CodeOutputComponent.FromParts(new object[] {
    "new ", KnownTypes.Requests.ExecutionRequestHandlerInfo,
    $"(\"{path}\", typeof(", controller, "))"
});
```

*A statement with substitution slots* — `AddCode(string, params object[])`, where `{argN}`
takes a type and `[argN]` takes literal text:

```csharp
// before
provider.Get.AddCode("RootServiceProvider ?? throw new Exception(\"not initialized\");");

// after
provider.Get.AddCode(
    "RootServiceProvider ?? throw new {arg1}(\"not initialized\");", typeof(Exception));
```

**Do not fix Shape B with Shape A's fix.** Adding a `using` so the bare name resolves puts
the file straight back into the state V2 exists to end — a name that resolves because
something unrelated imported its namespace. Hand the type over.

---

#### A third thing the patch found, which is neither shape

`typeof({model.ControllerType.Name})` in Hardened's `HandlerInfoCodeGenerator` is Shape B, but
it was invisible in the gate-5 suite: there the generated file and the controller happen to
share a namespace, so the short name resolved. In the OpenAPI and IDL generators they do not
(`Contoso.Petstore.Generated.Generated` vs `Contoso.Petstore.Generated.Services`), and it is
`CS0246`. **`--scope core` cannot see this class of bug. Run `--scope full` before believing a
consumer is migrated.**

---

#### Measured result

`--scope core`, against `v2/output-context`, patched clones:

| Suite | TFM | Before | After |
|---|---|---|---|
| `DependencyModules.Tests` | net8.0 | 384 / 735 | **725 / 735** |
| `DependencyModules.Tests` | net10.0 | 384 / 735 | **725 / 735** |
| `Hardened.SourceGenerator.Tests` | net8.0 | 265 / 468 | **468 / 468** |

`--scope full`, all 35 assemblies:

| | Before | After |
|---|---|---|
| Assemblies that ran | 23 of 35 — 12 blocked by generated code that would not compile | **35 of 35** |
| Tests | 3,413 passed / 1,306 failed | **6,166 passed / 20 failed** |

The 20 remaining failures are 10 snapshot tests × 2 TFMs in `DependencyModules.Tests`, all of
them in §5 below. **Zero compile errors remain in generated code in either repository.**

Five suites — `Hardened.Requests.Runtime.Tests` (803), `Hardened.IntegrationTests.WebApp.SUT.Tests`
(149), `Hardened.IntegrationTests.OpenApi.SUT.Tests` (68), `Hardened.IntegrationTests.Benchmark.SUT.Tests`
(23) and `Hardened.IntegrationTests.Smithy.SUT.Tests` (20) — could not run at all before the
patch, because the SUT projects they test failed to build. They pass now.

---

#### What the patch does *not* cover

The patch fixes every writer in the two repositories. **`Hardened.Amz` is a separate repository
and needs the same treatment**: it holds the only four production subclasses of
`ApplicationEntryPointFileWriter`, and a derived writer that resolves a service emits
`GetRequiredService` and therefore needs Shape A's one line. The equivalent writer in
`Hardened.SourceGenerator.Tests` is patched here and shows the shape.

The general rule, worth applying by grep rather than by waiting for a red build:

- **`AddUsingNamespace` for every namespace whose extension methods the generated code calls.**
- **Every type reference goes through an `ITypeDefinition`, never through a string.**
  In these two repositories: `git grep -n 'CodeOutputComponent.Get("' and 'new CodeOutputComponent($"'`.

---

## 5. Snapshot diffs — gate 6

**Ten snapshots moved under `v2/output-context`. All ten are justified below and none
was re-baselined.**

Procedure when one appears:

1. The runner harvests DM's `.received.txt` files (written to
   `tests/DependencyModules.Tests/bin/Debug/<tfm>/Snapshots/`) into
   `<log-dir>/received-<run-id>/`. Diff against the committed `.verified.txt`.
2. Add a row below. **Do not touch the `.verified.txt`.**
3. If the diff is a *regression*, it is a bug — fix the emitter, do not justify it.

| Run | Snapshot | What changed | Cause (change # from §4) | Verdict |
|---|---|---|---|---|
| consumer-patch | `ModuleGenerationSnapshotTests.SimpleModule` | `using System.Diagnostics.CodeAnalysis;` and, in `*.Module.g.cs`, four more derived usings are gone; `[ExcludeFromCodeCoverage]` and `[DynamicDependency(...)]` are now written `[global::System.Diagnostics.CodeAnalysis....]` | B1 | improvement — **human** |
| consumer-patch | `…GenericServiceRegistrations` | same | B1 | improvement — **human** |
| consumer-patch | `…KeyedAndAsRegistrations` | same | B1 | improvement — **human** |
| consumer-patch | `…ModuleWithAllServiceLifetimes` | same | B1 | improvement — **human** |
| consumer-patch | `…ModuleWithConstructorParametersAndProperties` | same, plus `using System;` dropped | B1 | improvement — **human** |
| consumer-patch | `…ModuleWithCoverageExclusionDisabled` | same | B1 | improvement — **human** |
| consumer-patch | `…ModuleWithEnvironmentConditions` | same, plus `using DependencyModules.Runtime.Interfaces;` dropped from `*.Dependencies.g.cs` | B1 | improvement — **human** |
| consumer-patch | `…RecordModule` | same | B1 | improvement — **human** |
| consumer-patch | `…RegistrationTypeVariants` | same, plus `ServiceLifetime.Singleton` → `global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton` | B1 + B2 | improvement — **human** |
| consumer-patch | `PublicApiTests.SourceGeneratorApi` | CSharpAuthor's own public surface, snapshotted because its source is compiled into the generator assembly | see below | **human** |

All ten are `.verified.txt` baselines and **none of them was touched** — rule §8.1. They are
the reason `DependencyModules.Tests` reports 725/735 rather than 735/735, and every one is a
deliberate V2 behaviour change rather than a regression. Approving them is Ian's call.

**Why the nine generator snapshots moved.** Each is a `TypeOutputMode.Global` file, and in
Global mode V1 emitted `using` directives that nothing in the file needed — every type was
already `global::`-qualified — *except* for the attributes, which V1 wrote as bare short names
and which resolved only because those stray directives happened to be there. V2 derives the
using list from the types actually written (§1), so the directives go and the attributes are
qualified like everything else. The generated code compiles either way; it now compiles for a
reason rather than by coincidence. Nothing about the emitted registrations changed.

**Why the API snapshot moved.** `PublicApiTests.SourceGeneratorApi.verified.txt` records the
public surface of `DependencyModules.SourceGenerator`, which source-includes CSharpAuthor —
so V2's own API changes land in a DependencyModules baseline. No consumer patch can restore
it. What moved:

- *added*: `BraceStyle`, `AttributeTypeReference`, `OutputContextOptions.{AliasCollisions,
  BraceStyle, ContainingNamespace, EmitExplicitUsings}`, `CodeOutputComponent.{FromParts,
  Get(ITypeDefinition, string, bool), .ctor(ITypeDefinition, string?)}`,
  `CSharpFileDefinition.Namespace`, `OutputContext.{IndentDepth, DeclareContainingNamespace}`
- *removed from the `IOutputContext` interface*: `AddImportNamespace(ITypeDefinition)` and
  `AddImportNamespaces(IEnumerable<ITypeDefinition>)`. This is a **deliberate source-breaking
  change** — it is how invariant 1 is enforced rather than merely asked for. The concrete
  `OutputContext` still has both, so nothing that holds the class breaks; only code holding
  the interface does. Neither consumer holds the interface for this, so neither broke. Any
  other consumer that did replaces `ctx.AddImportNamespace(type); ctx.Write("Foo")` with
  `ctx.Write(type)`.
- *one line from the consumer patch*: `KnownTypes.Microsoft.DependencyInjection.ServiceLifetime`,
  the `ITypeDefinition` that replaces the `"ServiceLifetime"` string (§4.3, Shape B).

### Hardened.Framework — expected text updated in the patch, not re-baselined

Hardened has no `.verified.txt` mechanism for generated code; it pins expected output with
`Assert.Contains` literals in the test bodies. Sixteen of them recorded V1's output. The patch
updates them **in source, visibly**, which is a different act from letting a snapshot rewrite
its own baseline — there is no `UPDATE_SNAPSHOTS` here and nothing was absorbed silently. Each
one is listed so it can be vetoed individually.

| File | Before | After | Cause |
|---|---|---|---|
| `Hardened.OpenApi.BuildTask.Tests/JsonTypeInfoEmitterTests.cs` ×2 | `CreatePropertyInfo<global::System.Single>(`, `(global::System.Single)args[3]` | `CreatePropertyInfo<float>(`, `(float)args[3]` | B6 |
| `Hardened.OpenApi.BuildTask.Tests/JsonTypeInfoEmitterTests.cs` ×1 | `public readonly static PetstoreJsonTypeInfoResolver Instance` | `public static readonly PetstoreJsonTypeInfoResolver Instance` | modifier order (§ "Declarations… Output changes", row 4) |
| `Requests/GeneratedCodeRegressionTests.cs` ×3 | `new ExecutionRequestHandlerInfo("…", typeof(HealthController), …)` | `new global::Hardened.Requests.Runtime.Execution.ExecutionRequestHandlerInfo("…", typeof(global::TestApp.HealthController), …)` | B2 |
| `Shared/ApplicationRootEmitTests.cs` ×1 | `RootServiceProvider ?? throw new Exception` | `RootServiceProvider ?? throw new global::System.Exception` | B2 |
| `Function/FunctionHandlerProviderTests.cs` ×1 | `[DynamicDependency(nameof(FunctionHandlersDI))]` | `[global::System.Diagnostics.CodeAnalysis.DynamicDependency(nameof(FunctionHandlersDI))]` | B1 |
| `Hardened.OpenApi.SourceGenerator.Tests/GeneratorConfigurationTests.cs` ×4 | `[ExcludeFromCodeCoverage]` | `[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]` | B1 |
| `Hardened.OpenApi.SourceGenerator.Tests/GeneratedCodeCompilesTests.cs` ×4 | `[property: Required]`, `[property: StringLength(`, `[property: Range(`, `[property: Pattern(typeof(` | each prefixed `global::ValidationModules.Constraints.` | B1 |

The eight `Hardened.OpenApi.SourceGenerator.Tests` rows are **not** caused by the patch: they
are B1 alone, and they were invisible until the patch made those suites buildable again. The
five `Hardened.SourceGenerator.Tests` rows are caused by the patch, and are the point of it — a
type that is now a type gets written like one.

The three `Hardened.OpenApi.BuildTask.Tests` rows are a third category: they are neither, and
nothing about them is new — V1's `_knownTypes` table listed `double` but not `float`, so the one
primitive in that test whose keyword was missing came out as `System.Single`, and V1 wrote field
modifiers as `readonly static`. **Nobody had ever seen these two tests run against V2**, because
that assembly was one of the six the `CSharpAuthor.props` defect above stopped building at all.
Both are B6/§"Output changes" behaviours that were already documented as intended; the
assertions simply still recorded V1's answer. Verdict: `improvement` on both.

If any of these is rejected, the corresponding writer change has to be rejected with it, and
that file's generated code goes back to not compiling.

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
| 1 | `d7d3d2e` `v2/output-context` — unpatched consumers | core | 384/735, 384/735 | 265/468 | FAIL — the §1 interlock, measured |
| 1 | `d7d3d2e` `v2/output-context` — unpatched consumers | full | 384/735, 384/735 | 265/468 | FAIL — only 23 of 35 assemblies could run; 3,413 passed / 1,306 failed |
| 2 | `d7d3d2e` `v2/output-context` + `docs/consumer-patches/*` | core | 725/735, 725/735 | **468/468** | 10 `.verified.txt` snapshots left, all in §5 |
| 2 | `d7d3d2e` `v2/output-context` + `docs/consumer-patches/*` | full | 725/735, 725/735 | **468/468** | 35 of 35 assemblies, 6,166 passed / 20 failed — the same 10 snapshots × 2 TFMs. Zero compile errors in generated code. |
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

---

# 9. Migrating a real repository — three worked examples

This section is self-contained. It assumes only that you maintain a Roslyn source generator that
consumes CSharpAuthor and that you are moving it from 1.x to 2.0. Everything in it was measured on
2026-08-21 against fresh clones of three repositories, driven through their own solution files with
CSharpAuthor `feature/v2` at `4c7dc35` wired in as a local checkout; no number here was estimated.

| Repository | Commit | How it consumes CSharpAuthor |
|---|---|---|
| `ipjohnson/DependencyModules` | `642bac3` | `PackageReference CSharpAuthor 1.1.1010` + `PackageCSharpAuthorIncludeSource=true` in `DependencyModules.SourceGenerator` and `.Impl` |
| `ipjohnson/ValidationModules` | `5daa26c` | the same pattern in its own two generator projects, **and** it compiles the published `DependencyModules.SourceGenerator.Impl` 1.0.0 sources in beside them |
| `ipjohnson/Hardened.Framework` | `a90d24d` | `src/SourceGenerators/CSharpAuthor.props`: `PackageReference CSharpAuthor 1.2.0` in package mode, its own `<Compile Include>` glob in `UseLocalCSharpAuthor` mode |

## 9.1 Results

| | Assemblies | Passed | Failed |
|---|---|---|---|
| **DependencyModules** — 1.x baseline | 8 | 1,812 | 0 |
| DependencyModules — 2.0, unpatched | **4** | 770 | 704 |
| DependencyModules — 2.0, patched | 8 | 1,792 | **20** (all snapshot pins, §9.6) |
| **ValidationModules** — 1.x baseline | 8 | 1,092 | 0 |
| ValidationModules — 2.0, unpatched | 8 | 1,092 | **0** |
| **Hardened.Framework** — 1.x baseline | 27 | 4,787 | 0 |
| Hardened.Framework — 2.0, unpatched | **16** | 2,437 | 359 |
| Hardened.Framework — 2.0, patched | 27 | 4,787 | **0** |

Read the assembly column first. **A project that fails to build does not fail its tests — it stops
existing, and its tests silently leave the total.** Unpatched, DependencyModules loses four test
assemblies and 338 tests to a build error, and Hardened.Framework loses eleven and 1,991. A pass
count on its own would have shown a suite getting *smaller*, not redder.

## 9.2 Wiring a repository at a local 2.0 checkout, and proving the wiring took

`dotnet test <some.dll>` against a previously built assembly is a **false green**: it re-runs
whatever the last successful build produced, which is the published 1.x package, and reports a clean
pass. Always drive the solution.

For a repository that consumes the package's source inclusion (DependencyModules, ValidationModules),
point it at a checkout with no edit at all, from the command line:

```bash
dotnet test <solution> \
  -p:CustomAfterMicrosoftCommonTargets=<abs path>/local-csharpauthor.targets \
  -p:PackageCSharpAuthorIncludeSource=false \
  -p:LocalCSharpAuthorRoot=<abs path to the CSharpAuthor checkout> \
  -p:LocalCSharpAuthorProjects='|MyGenerator|MyGenerator.Impl|'
```

where the injected `.targets` adds the same glob the package's `build/CSharpAuthor.targets` adds,
against the checkout instead:

```xml
<Compile Include="$(LocalCSharpAuthorRoot)/CSharpAuthor/**/*.cs"
         Exclude="…/obj/**/*.cs;…/bin/**/*.cs;…/CSharpAuthor/Roslyn/**/*.cs"
         Visible="false"/>
```

`PackageCSharpAuthorIncludeSource=false` has to come from the command line, because a command-line
property is global and a `<PropertyGroup>` in the csproj cannot override it — which is what makes
this work without touching the consumer's tree. `Roslyn/**` is excluded because the package does not
ship it under `src/`; see §9.5.

Then **prove it**, before believing any number:

```bash
dotnet build <the generator csproj> -getItem:Compile   # expect N files under the checkout, 0 under ~/.nuget/packages/csharpauthor
strings <the built generator>.dll | grep CSharpAuthor.Profiles   # a 2.0-only namespace
```

Both checks were run for every measurement in this section.

## 9.3 The one change every consumer makes

Bump the package reference. That is the whole of ValidationModules' migration:

```diff
-<PackageReference Include="CSharpAuthor" Version="1.1.1010">
+<PackageReference Include="CSharpAuthor" Version="2.0.0">
```

DependencyModules needs it in two csproj files, ValidationModules in two, Hardened.Framework in one
(`src/SourceGenerators/CSharpAuthor.props`, from `1.2.0`).

It is in all three patches. **2.0.0 is not on nuget.org yet, so `dotnet restore` after applying one
of them fails with `NU1102: Unable to find package CSharpAuthor with version (>= 2.0.0)`.** To
exercise a patch before the release, apply it, put the version line back, and wire the build at a
checkout as in §9.2 — which is how every number in §9.1 was measured, since there is no other way to
measure a package that does not exist.

## 9.4 Shape A and shape B — the two ways generated code stops compiling

2.0 enforces one invariant: **a type reaches the output only through
`IOutputContext.Write(ITypeDefinition)`**, and the two `ITypeDefinition` overloads of
`AddImportNamespace`/`AddImportNamespaces` have left `IOutputContext`. (The `string` overloads stay,
and `IOutputComponent.AddUsingNamespace(string)` stays. They are the supported channel and both are
used below.) These two removed members are the *only* public API 1.1.1010 had that 2.0 does not.

The consequence: in `TypeOutputMode.Global` — the mode every generator here uses — 1.x wrote a type
reference fully qualified *and* emitted a `using` for its namespace as a side effect. The
qualification made the directive redundant, so nobody noticed it was there. 2.0 does not emit it.
Any generated code that was silently living off that stray `using` now fails, in exactly two shapes.

### Shape A — an extension method (CS1061, or CS0308 on a generic one)

`global::` cannot name an extension method. `services.AddSingleton<T>()` resolves only if
`Microsoft.Extensions.DependencyInjection` is imported, and in 1.x it was imported by accident,
because the file also mentioned `IServiceCollection` as a type.

```
error CS1061: 'IServiceCollection' does not contain a definition for 'AddSingleton' and no
accessible extension method 'AddSingleton' accepting a first argument of type 'IServiceCollection'
could be found (are you missing a using directive or an assembly reference?)
```

**Fix — one line per generated file**, on the `CSharpFileDefinition` (or on the individual component
whose call needs it):

```csharp
var csharpFile = new CSharpFileDefinition(ns);
csharpFile.AddUsingNamespace("Microsoft.Extensions.DependencyInjection");
```

This is the string channel, which 2.0 deliberately kept: naming a namespace is the only way to reach
an extension method, and no amount of type tracking can do it for you.

DependencyModules already had `AddUsingNamespace("Microsoft.Extensions.DependencyInjection.Extensions")`
for `TryAdd*`. It did not have the one for `Add*`, because that one arrived free. That is the whole
failure: 76 CS1061 from the first project that reached the compiler, and 512 measured with the
`DependencyFileWriter` line in place but the decorator and interceptor writers left alone — a
solution build stops at the first broken project, so the first count is never the whole cost.

### Shape B — a type name written as a raw string (CS0246, CS0103, CS0117)

```csharp
parameters.Add(CodeOutputComponent.Get("ServiceLifetime.Transient"));                 // 1.x
field.InitializeValue = new CodeOutputComponent($"new ExecutionRequestHandlerInfo(…)"); // 1.x
provider.Get.AddCode("RootServiceProvider ?? throw new Exception(\"…\");");             // 1.x
```

By the time text reaches the writer it is text: it cannot be qualified, aliased, or counted in the
using list, and it resolved only while some *other* part of the file happened to import the
namespace. **Fix — hand the type over instead of its name:**

```csharp
// a member reached off a type
CodeOutputComponent.Get(KnownTypes.ServiceLifetime, "Transient")

// a statement mixing text and types; each ITypeDefinition stays unrendered until serialization
CodeOutputComponent.FromParts(new object[] {
    "new ", KnownTypes.ExecutionRequestHandlerInfo, "(\"/health\", \"GET\", typeof(",
    handlerModel.ControllerType, "), \"Health\")"
})

// a substitution into a line of code
provider.Get.AddCode("RootServiceProvider ?? throw new {arg1}(\"…\");", typeof(Exception));
```

`CodeOutputComponent.AddType(ITypeDefinition)` still exists for callers written against 1.x, but it
can only offer the namespace, and only in `TypeOutputMode.ShortName`. Prefer the three forms above.

### Shape C — a string literal quoted twice (silent in 1.x, visible in 2.0)

1.x's `SyntaxHelpers.QuoteString` was `"\"" + s + "\""`. 2.0's escapes embedded quotes and
backslashes. Code that quoted a value and then handed it to something that quotes values again used
to produce `""a""` — wrong, but containing the substring a test looked for. It now produces
`"\"a\""`, and the test fails. The fix is to stop double-quoting; the second quoting was always the
bug. This cost DependencyModules one line
(`collectionSyntax.Add(SyntaxHelpers.QuoteString(s))` → `collectionSyntax.Add(s)`).

## 9.5 If you glob a CSharpAuthor checkout yourself: exclude `Roslyn/`

2.0 ships a second, opt-in source root written against `Microsoft.CodeAnalysis`, gated behind
`PackageCSharpAuthorIncludeRoslyn`. In the package it lives under `srcRoslyn/`, outside the `src/`
glob. **In a working tree it lives at `<root>/CSharpAuthor/Roslyn/`, inside it.** A recursive glob
that used to be equivalent to the package no longer is, and every project that consumes CSharpAuthor
without a Roslyn reference — MSBuild tasks, plain emitters — stops compiling:

```
CSharpAuthor/Roslyn/EmitProfileRoslynExtensions.cs(6,54): error CS0234: The type or namespace name
'CSharp' does not exist in the namespace 'Microsoft.CodeAnalysis'
```

Measured on Hardened.Framework: 720 of its 816 unpatched errors, across eight project/TFM
combinations, taking eleven test assemblies with them. The fix is one `Exclude` entry, plus a second
`<Compile>` item gated on `PackageCSharpAuthorIncludeRoslyn` so the opt-in still exists locally. See
`docs/consumer-patches/hardened-v2.patch`.

This is a local-checkout-mode concern only. A consumer that stays on the package is unaffected.

## 9.6 Snapshots move, and that is a diff to justify — never a re-baseline

Nothing in this exercise ran with `UPDATE_SNAPSHOTS=1` or `APPROVE_PUBLIC_API=1`, and no committed
`.verified.txt` or `.approved.txt` was edited.

**Hardened.Framework: 13 `.approved.txt`, none moved.** Its snapshots pin runtime assemblies, not
the generator's own surface, and its generated-output assertions live inline.

**ValidationModules: 18 `.verified.txt`, none moved.**

**DependencyModules: 17 `.verified.txt`, 10 moved** (each failing on both `net8.0` and `net10.0`,
which is the 20 in §9.1).

*Nine `ModuleGenerationSnapshotTests` snapshots.* Every changed line is one of three things:

```diff
-using System.Diagnostics.CodeAnalysis;          # a stray using, 9x — and three more per file
-using DependencyModules.Runtime.Helpers;        # in TestModule.Module.g.cs
-    [ExcludeFromCodeCoverage]
+    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
-                ServiceLifetime.Singleton
+                global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton
```

The removed directives are the ones nothing used. The qualified attribute is the *same* attribute:
in `TypeOutputMode.Global` a type reference is written qualified, and there is no way to get the
short spelling back except by writing the attribute name as a raw string — the thing shape B exists
to remove. The `ServiceLifetime` line is the shape B fix itself. The generated code still compiles
and still behaves identically: `SutProject.Tests` (135 × 2 TFMs) and `SutProject.NUnitTests`
(34 × 2) compile and execute this output and are green.

*One `PublicApiTests.SourceGeneratorApi` snapshot,* **+731 / −9 lines.** This snapshot does not pin
DependencyModules' API. It pins the public surface of the *assembly*, and because CSharpAuthor is
source-included, every `public` top-level type in CSharpAuthor is public in the consumer's assembly
too. So:

- **+731 lines / 47 new public types** — 32 from `CSharpAuthor.Profiles`, 12 from
  `CSharpAuthor.Expressions`, 3 from `CSharpAuthor`, plus `CSharpAuthor.Collections`. 2.0's
  `#if CSHARPAUTHOR_PUBLIC_SYNTAX` mechanism keeps the ~250 `Syntax` nodes internal under source
  inclusion; it is **not** applied to `Profiles`, `Expressions` or `Collections`, so those do leak.
  If that is not intended, the same `#if` is the fix, and it belongs in 2.0 rather than in a
  consumer patch.
- **−9 lines, of which only 2 are real removals**: `IOutputContext.AddImportNamespace(ITypeDefinition)`
  and `AddImportNamespaces(IEnumerable<ITypeDefinition>)`. The other seven are `MakeArray()`,
  `Equals` and `GetHashCode` moving onto `BaseTypeDefinition` as `MakeArray(int rank)` became the
  abstract member — source-compatible, and still present.

A consumer with a snapshot of this kind has to review and accept the new baseline once. It cannot be
patched away.

## 9.7 2.0 is missing API that 1.2.0 published

**`ClassKeyword.Union` and `ClassDefinition.AddUnionCase(ITypeDefinition)` — C# 15 union support —
exist in CSharpAuthor 1.2.0 on nuget.org and did not exist on the 2.0 branch.** Verified by
downloading `csharpauthor.1.2.0.nupkg` from `api.nuget.org` and reading its `src/`. The 2.0 branch
was cut before that release, and nothing in the repository's history contains `AddUnionCase`.

Hardened.Framework pins **1.2.0**, not 1.1.1010, and uses both, in
`src/SourceGenerators/Hardened.Idl.Emit/Emitters/UnionResponseEmitter.cs`:

```
UnionResponseEmitter.cs(179,45): error CS0117: 'ClassKeyword' does not contain a definition for 'Union'
UnionResponseEmitter.cs(182,22): error CS1061: 'ClassDefinition' does not contain a definition for 'AddUnionCase'
```

**There is no consumer-side fix.** `ClassDefinition` has no seam for a fifth keyword, and hand-rolling
the declaration means giving up the `ClassDefinition` the emitter returns to its caller. Shipping 2.0
without this regresses the library against what is already published.

It is a small, self-contained restoration — an enum member, a ~20-line `AddUnionCase`, a
`TerminateWithSemicolon` side effect on the setter, one keyword string, and one branch in
`WritePrimaryConstructorParameters` that writes the case types bare. In 2.0 that branch goes through
`Write(ITypeDefinition)` rather than 1.2.0's `AddImportNamespace`, which is invariant 1 applied to
the same code.

**This is now fixed on the branch, by `51386a0` "Bring 1.2.0's union support forward into V2".** That
commit adds the same members and additionally gates a union on `LanguageFeature.Unions`
(C# 15, category `Impossible`, no downlevel) — which is stricter than 1.2.0, where a union carried no
capability requirement at all. Measured here: it trips nothing in Hardened.Framework.
**Every Hardened.Framework number in §9.1 was measured against `feature/v2` at `4c7dc35`, with union
support present**; without it, `Hardened.Idl.Emit` does not compile at all and eleven test assemblies
are unreachable.

The lesson generalises past this one API: **the version a consumer pins is the version to diff
against.** Hardened.Framework pins 1.2.0; DependencyModules and ValidationModules pin 1.1.1010.
Checking 2.0 only against the older of the two would have missed this entirely, and it was found by
`error CS0117: 'ClassKeyword' does not contain a definition for 'Union'` on a repository nobody
expected to break in that way.

Other than this, a full name-level comparison of 1.2.0's public surface against 2.0 found nothing
else missing.

## 9.8 What each repository actually needed

Patches: `docs/consumer-patches/{dependencymodules,validationmodules,hardened}-v2.patch`. Each is a
`git diff` against the commit in the table above, carries a prose header `git apply` ignores, and
applies cleanly to a pristine clone (`git apply --check`, verified). Each also carries the §9.3
version bump, with the `NU1102` caveat that comes with it.

### DependencyModules — 5 source files (18 lines added, 7 changed), plus the version bump in 2 csproj

| File | Change |
|---|---|
| `DependencyFileWriter.cs` | 1 × shape A `AddUsingNamespace`; 6 × shape B `ServiceLifetime.X` |
| `DecoratorFileWriter.cs` | 1 × shape A |
| `InterceptorRegistrationWriter.cs` | 1 × shape A |
| `KnownTypes.cs` | the `ServiceLifetime` `ITypeDefinition` shape B needs |
| `Models/AttributeModel.cs` | 1 × shape C |
| `DependencyModules.SourceGenerator{,.Impl}.csproj` | the §9.3 version bump |

Every hunk was verified necessary by reverting it alone: without the decorator and interceptor
`AddUsingNamespace` lines the build produces 512 CS1061; without the `AttributeModel` line
`AttributeModelOutputTests.GetArguments_WritesStringArraysAsACollection` fails on `["\"a\"", …]`.

### ValidationModules — nothing but the version bump

Never tested against 2.0 before this run, and the one that needed the least. **8 assemblies, 1,092
passed, 0 failed, identical to its 1.x baseline, with no source change whatsoever.** The reason is
worth knowing, because it is the cheapest possible migration and it is cheap for a structural reason:

- **ValidationModules names no CSharpAuthor type anywhere.** Not one `using CSharpAuthor`, not one
  identifier, in `src/`, `tests/` or `integ-tests/`. Its emitters (`ValidatorEmitter`,
  `RegistrationEmitter`, `PredicateEmitter`) build output with a `StringBuilder` and write their own
  `using global::…` lines.
- It sets `PackageCSharpAuthorIncludeSource=true` **only** to satisfy the DependencyModules generator
  internals it imports (63 files, from the published `DependencyModules.SourceGenerator.Impl` 1.0.0),
  which do use CSharpAuthor.
- Those DM sources contain the unfixed 1.x writers — and they are **dead code here**. The DM Impl
  package ships no `[Generator]`; ValidationModules' only generator is its own
  `ValidationSourceGenerator`, and nothing in the repository calls `DependencyFileWriter`,
  `DecoratorFileWriter` or `InterceptorRegistrationWriter`. They are compiled and never run.
- Which also demonstrates that **2.0 is source-compatible with 1.x generator code**: those 63
  unmodified 1.x-era files compile against 2.0 with zero errors. What changes is the *output*, not
  the API.
- Its `PublicApiTests` pin `ValidationModules.Runtime` and `ValidationModules.AspNetCore` — assemblies
  that do not source-include CSharpAuthor — so it does not have DependencyModules' §9.6 problem.

Nothing about it was unique in a way that needed new technique. The one thing to know is the
ordering: when DependencyModules ships a 2.0-compatible `Impl` package, ValidationModules picks it
up on its next bump and still needs no change, because it never called into it.

### Hardened.Framework — 15 files, ~62 lines

| Group | Files | Change |
|---|---|---|
| Wiring | `CSharpAuthor.props` | §9.5: exclude `Roslyn/**`, add the `PackageCSharpAuthorIncludeRoslyn`-gated item; and the §9.3 version bump, here from `1.2.0` |
| Shape A | `SpecRoutingTableGenerator`, `ConfigurationEntryPointGenerator`, `ServiceProviderFileGenerator`, `FunctionIncrementalGenerator`, `Hardened.Validation`'s `RegistrationWriter` | 1 × `AddUsingNamespace` each (`AddSingleton`, `AddTransient`, `AddLogging`, `BuildServiceProvider`) |
| Shape A | `BindRequestParametersMethodGenerator` | `AddUsingNamespace` on the header/cookie `Get` call only — `PathTokens` and `QueryString` carry their own `Get`, headers and cookies reach an extension |
| Shape B | `HandlerInfoCodeGenerator` | `new ExecutionRequestHandlerInfo(…)` string → `CodeOutputComponent.FromParts` |
| Shape B | `ApplicationRootImplementation` | `throw new Exception` → `AddCode("… {arg1} …", typeof(Exception))` |
| Test harness | `ApplicationRootEmitTests` | the derived writer in the test needs the same shape A line as the real ones |
| Expectations | 6 test files, 13 assertions | see below |

All eight generator-side hunks were verified necessary: reverting four of them costs 209 failures
and three assemblies.

The 13 moved assertions are inline `Assert.Contains` on generated text, not snapshot files. They
divide into:

- **9 that are the same invariant-1 change as DependencyModules':** `[ExcludeFromCodeCoverage]` →
  `[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]`, `[DynamicDependency(…)]`,
  `[property: Required]` → `[property: global::ValidationModules.Constraints.Required]`,
  `new ExecutionRequestHandlerInfo(…, typeof(HealthController), …)` →
  fully qualified, `throw new Exception` → `throw new global::System.Exception`.
- **2 that are 2.0 fixing 1.x output:** `public readonly static X` → `public static readonly X`
  (the conventional order, which 1.x wrote backwards), and `global::System.Single` → `float`
  (1.x's keyword table had `double` but not `float`, so the one type whose keyword was missing came
  out under its reflection name).
- **1 that is a test harness change**, not an expectation: `ADerivedWriterAddsItsOwnConstructorLogicAndDomainMethods`
  compiles the code it generates, and the writer defined inside the test needs the shape A line.

## 9.9 Order of operations for a fourth repository

1. Bump the package reference; build the solution and **count assemblies, not just tests**.
2. If you glob a checkout rather than using the package, exclude `Roslyn/**` first — it will
   otherwise bury everything else (§9.5).
3. Fix CS1061/CS0308 with `AddUsingNamespace` per generated file (shape A).
4. Fix CS0246/CS0103/CS0117 by handing types over instead of naming them (shape B).
5. Search for pre-quoted strings handed to something that quotes (shape C).
6. Expect snapshots of generated output to lose stray `using` lines and gain `global::` on
   attributes. Review the diff; do not run the updater.
7. Expect a snapshot of your *own assembly's* public API to grow by CSharpAuthor's new public types,
   if you source-include (§9.6).
