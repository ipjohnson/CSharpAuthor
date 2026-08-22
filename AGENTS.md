# Working on CSharpAuthor

CSharpAuthor turns a tree of definitions into formatted C#. It exists for **Roslyn source
generators**, where it is roughly **25× faster** than `SyntaxFactory` + `NormalizeWhitespace`
(0.019 ms vs 0.489 ms per file, measured).

Read this before changing anything. Everything below was learned the hard way — each trap here
caught a real contributor, usually silently.

---

## The one idea everything else follows from

**A type is not text until the file is serialized.**

`ITypeDefinition` stays unrendered until `WriteTypeName(builder, mode)` at the very end. That single
property is what lets one option flip a whole file between short names and `global::`, what makes
collision aliasing possible, and what makes a missing `using` *structurally* impossible — the
namespace is derived from what was written rather than declared on the side.

**Any change that turns a type into a string early is wrong**, however convenient it looks. That is
not a style preference; it is the defect class the 2.0 rewrite existed to eliminate.

---

## The defect class to fear

Not crashes. **Silent wrongness** — output that is wrong but does not throw, and that a test
asserting a substring will happily accept.

Real examples, all shipped at some point, all found by compiling the output rather than reading it:

| Emitted | Should have been |
|---|---|
| `"he said "hi""` | escaped |
| `1,5` on a de-DE machine | `1.5` |
| `protected` for `private protected` | `private protected` — **it widened access** |
| `Int32[,][]` for `typeof(int[,])` | `int[,]` |
| `string[]?` for an array of nullable strings | `string?[]` — a different type |
| `void M(string class)` | `void M(string @class)` |

If you are adding a feature, the question is not "does it work" but **"how would I know if it
silently didn't?"**

---

## Verifying your work

```bash
dotnet test CSharpAuthor.Tests                          # 1634 passed / 0 failed / 18 skipped
./scripts/run-consumer-tests.sh <checkout> --scope core  # the real oracle
./scripts/run-roundtrip.sh   <checkout> --corpus all     # 1,315 / 1,373 = 95.8%
./scripts/run-benchmark.sh   <v1-checkout> <v2-checkout> # both, in ONE invocation
python3 tools/grammar/gen_all.py --report                # node coverage
./scripts/verify-roslyn-packaging.sh                     # the packaging gate
```

### ⚠️ `dotnet test <some.dll>` against a consumer is a FALSE GREEN

Running a consumer's prebuilt test assembly directly returns a clean pass **while silently measuring
the published CSharpAuthor package instead of your checkout**. It has nearly been reported as a real
result more than once. `run-consumer-tests.sh` asserts via `-getItem:Compile` that your files are
genuinely in the compile set. **Always use the script.**

### The unit tests are not the oracle — the consumers are

Two real generators depend on this library, and both are cloned locally. Changes have passed all
the entire unit suite (1,559 tests at the time) while breaking three consumer projects
outright. `--scope core` runs the two gate
suites in ~15 s; `--scope full` runs all 35 assemblies and is the only thing that catches a project
that fails to *build*.

### Never re-baseline

`UPDATE_SNAPSHOTS=1` and `APPROVE_PUBLIC_API=1` exist. **Do not set them.** A changed snapshot is
either a bug or an improvement, and which one it is is a human's call. Record the diff in
`docs/migration-v1-v2.md` and keep going.

---

## Traps

**`AddCode("...", args)` — `{argN}` and `[argN]` are not the same.**
`{argN}` keeps the argument as an unrendered `ITypeDefinition`, so it qualifies and aliases with
everything else. `[argN]` is pasted as raw text. The distinction is invisible from the signature and
is the easiest thing here to get wrong.

**A `PropertyDefinition` named `this` is an indexer.** A magic string with no other signal. A
keyword-escaping fix once turned it into `@this` and broke every indexer in the library.

**`MakeNullable().MakeArray()` is `int?[]`; `MakeArray().MakeNullable()` is `int[]?`.** Different
types, silently. Use `MakeArrayOfNullable()` when you mean the former.

**`ITypeDefinition.ToString()` is not the C# name.** It keeps its 1.x shape because a consumer
asserts on it directly. Use `GetShortName()` or `WriteTypeName`.

**`CSharpAuthor.Profiles.LanguageVersion` vs Roslyn's `LanguageVersion`.** A generator imports both
namespaces, so `LanguageVersion` alone is CS0104. Worse: **a `using X = ...` alias cannot fix it**
inside a namespace nested under `CSharpAuthor`, because enclosing-namespace members outrank aliases.
Fully qualify, or import `CSharpAuthor.Profiles` deliberately.

**Pick `TypeOutputMode.Global` unless you have a reason not to.** It qualifies everything and emits
no derived usings, so there is nothing to get wrong — and it is now the faster path, since a
qualifying file skips the record entirely. `ShortName` buys shorter names at the cost of derived
usings and collision aliasing.

---

## Rules that are not negotiable

1. **Never hand-edit `CSharpAuthor/Syntax/Nodes.g.cs`.** It is generated from Roslyn's `Syntax.xml`
   by `tools/grammar/gen_all.py`. Fix the generator and regenerate; regeneration is verified
   byte-identical, and that check is the only thing keeping the approach safe. If a node needs
   behaviour the generator cannot express, add a `partial` in a separate file.
2. **If you change the generated node field walk, mirror it in `tools/roundtrip/gen_shipping.py`.**
   The round-trip harness deliberately refuses to compile when the two disagree, rather than
   reporting a skewed number. It has already caught a one-sided fix.
3. **No new NuGet package in `CSharpAuthor.csproj`.** The library is netstandard2.0 with zero
   dependencies so a generator can source-include it without dependency grief. The **test** project
   may reference Roslyn freely; the Roslyn bridge lives in `CSharpAuthor/Roslyn/`, excluded from the
   library build and gated on `PackageCSharpAuthorIncludeRoslyn`.
4. **Library code is C# 10 or lower.** `DependencyModules.SourceGenerator.Impl` pins `LangVersion 10`
   and compiles this library in from source. C# 11+ syntax passes the unit tests and fails the
   consumer build. Never raise `LangVersion` to make something compile.
5. **`EnforceExtendedAnalyzerRules` is on in consumers.** File I/O, `Environment`, and
   culture-dependent string APIs are hard **errors** there and invisible here. Every numeric
   `ToString` needs `CultureInfo.InvariantCulture`; every comparison needs an explicit
   `StringComparison`.
6. **Never modify an existing test to make a change pass.** If a test fails, the change is wrong —
   or the test is, in which case say so loudly rather than editing it. One original test currently
   asserts `public int Test[string index]`, which is not valid C#; it is documented, not quietly fixed.

---

## Where things are

| Path | What |
|---|---|
| `CSharpAuthor/` | the library. Ships as source; every `.cs` here is compiled into consumers |
| `CSharpAuthor/Syntax/` | generated grammar nodes, `internal` behind `CSHARPAUTHOR_PUBLIC_SYNTAX` |
| `CSharpAuthor/Roslyn/` | the `ITypeSymbol` bridge — excluded from the library build, opt-in |
| `CSharpAuthor/Profiles/` | `EmitProfile`, language versions, downlevelling, diagnostics |
| `tools/grammar/` | `gen_all.py` + `Syntax.xml` — the node generator |
| `tools/roundtrip/` | the importer and the 1,373-file fidelity harness |
| `docs/` | migration guide, open questions, adversary findings, [api-gaps.md](docs/api-gaps.md) |
| `CSharpAuthor.Tests/Adversary/` | 280 tests / 305 cases asserting emitted code **compiles**, not that it matches a string |

---

## Adding a language feature

A new C# version is a **regeneration**, not a rewrite:

```bash
# replace tools/grammar/Syntax.xml (and tokens.json if new token kinds appeared)
python3 tools/grammar/gen_all.py
python3 tools/grammar/gen_all.py --report
```

New node classes appear automatically. What is *not* automatic: a spacing rule if the construct has
genuinely novel punctuation (raw interpolated strings are the standing example — the brace count
must match the `$` count, which the grammar cannot encode), and a capability row in
`CSharpAuthor/Profiles/LanguageFeature.cs` saying whether the feature is Free, Polyfillable or
Impossible to downlevel. That last one is a judgement call and no tool will make it for you.

Watch for **contextual keywords**. `RecordDeclarationSyntax.Keyword` declares `<ContextualKind>`
rather than `<Kind>` — the only field in the grammar that does — and reading only `<Kind>` emitted
`publicrecordFoo(...)`, which re-parses as a constructor and destroys the rest of the file. Modern
C# adds contextual keywords constantly. Expect more.
