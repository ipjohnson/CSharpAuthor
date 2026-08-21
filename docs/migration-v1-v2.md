# Migration, V1 → V2

> Sections below are contributed by the `output-context` builder. Other builders add their own;
> merge rather than replace.

## Output context — what changed

### Source compatibility

`IOutputContext` is unchanged. No member was added, removed or re-signatured, so every existing
implementation and every one of the 198+ call sites in `DependencyModules` and `Hardened.Framework`
compiles untouched.

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

A **generic** attribute now writes its type arguments — `[Marker<int>]` where V1 wrote `[Marker]` and
silently dropped them.

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

### Snapshot diffs to expect in the consumers

Not yet run against the consumer suites from this branch — the migrator owns that gate. Predicted,
from reading the writers:

| Snapshot | Predicted diff | Why |
|---|---|---|
| Any `Global`-mode file | `using Microsoft.Extensions.DependencyInjection;` and `using System.Diagnostics.CodeAnalysis;` disappear | #1 |
| `ModuleGenerationSnapshotTests.RegistrationTypeVariants` | `ServiceLifetime.Singleton` becomes bare *unless* the six call sites are changed | #2 — **fix the call sites** |
| Any `Global`-mode file with attributes | `[ExcludeFromCodeCoverage]` becomes `[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]` | #4 |
| `Global`-mode files using `AddCode` with `{argN}` | the substituted type becomes qualified | #3 |

Every one of these is the defect list being satisfied, not a regression. None is a reason to
re-baseline without reading the diff.
