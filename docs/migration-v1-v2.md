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
