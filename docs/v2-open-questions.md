# V2 open questions

> Defaults taken under §8.4 — "when the spec is silent, choose the option that keeps V1 source
> compatible". Sections below are contributed by the `output-context` builder; other builders add
> their own.

## Output context

### 1. "`Global` mode emits no usings" — does that include the ones the caller asked for?

**Taken:** no. A namespace derived from a type is not emitted in a qualifying mode; a namespace asked
for by name (`AddUsingNamespace`, `AddImportNamespace(string)`) still is, controlled by
`OutputContextOptions.EmitExplicitUsings`, default `true`.

**Why:** an extension method is only reachable through a `using`; `global::` cannot name one. Both
consumers depend on this — `DependencyFileWriter` asks for
`Microsoft.Extensions.DependencyInjection.Extensions` by name so `TryAddSingleton` resolves, and
`Hardened.SourceGenerator` does the same in a dozen places. Dropping those would break files that a
purely derived model has no way to fix. The stray directive the handoff identifies is the *derived*
one, and that is gone unconditionally.

**If the human disagrees:** set `EmitExplicitUsings = false` in the two generators' options, and the
mode emits nothing at all.

### 2. Should the file's own namespace be dropped from the using list?

**Taken:** only when asked. `OutputContextOptions.ContainingNamespace` is `null` by default, so the
V1 output — which imports the file's own namespace if a type in it is written — is unchanged.

**Why:** `NamespaceDefinition` knows the name and could set it automatically, but a redundant
directive is harmless and a dropped one that someone was relying on is not. Turning it on is one
line at each call site.

### 3. Which side of a collision keeps the plain name?

**Taken:** the one written first, unless a type with no namespace is in the group, in which case that
one keeps it (a keyword type or a generic parameter names itself and cannot be aliased). If the
losing namespace has to stay imported because something else in it is still written plainly, *both*
sides are aliased.

**Why:** deterministic — it depends only on write order, which depends only on the tree — and it
keeps the common case reading naturally. The alternative, aliasing every contender always, is
uglier for no gain when the losing namespace can simply be dropped.

### 4. What does an alias get called?

**Taken:** the last segment of the namespace, then the last two, and so on until it is unique, with
the short name appended: `Second.Model` → `SecondModel`; `Company.Web.Models.Widget` → `ModelsWidget`.
Falls back to `NameAlias`, `NameAlias2`, … if the namespace runs out.

**Why:** it is what a person writing the alias by hand would pick, and it is stable across runs.
Not specified anywhere; if a house style wants something else, `MakeAlias` is the one place.

### 5. Colliding generics

**Taken:** written with their namespace in front (`First.Box<int>`) rather than aliased.

**Why:** a `using` alias names a closed type, so aliasing `Box<T>` would have to pick one closing and
would then be wrong everywhere else. Qualifying is correct in every case. Both sides are qualified,
not just one, because leaving one bare with both namespaces imported is still CS0104.

### 6. Perf against gate 9 (≤ 0.048 ms and ≤ 78 KB per file)

**Not measured.** There is no benchmark project in this repository, and the handoff does not say
where the §10 payload lives. The segment list is a `List<Segment>` of a readonly struct — one array,
no object per write — and the no-collision path calls `ITypeDefinition.WriteTypeName` straight into
the output builder, exactly as V1 did. Allocation is one array of ~32-byte structs plus V1's
`StringBuilder`, so a regression is possible and is **unverified either way**. Whoever owns gate 9
should measure this before the PR claims it.

### 7. Ordering of the `using` list

**Taken:** ordinal.

**Why:** V1 sorted with `List<string>.Sort()`, which is culture-aware, so the order of a generated
file could depend on the culture the generator ran under — the same defect class as §7's
culture-dependent numbers. For every namespace either consumer actually emits, ordinal and
culture-aware agree, so this is a determinism fix with no observed output change. A namespace pair
that differs only in punctuation (`A.B` against `AB`) could order differently than under V1.

### 8. `CSharpIdentifier` is copied, not shared

`CSharpAuthor/CSharpIdentifier.cs` is the `declarations` builder's file, copied byte-identical into
this branch so it compiles standalone — the `using` directives need the same escaping the namespace
declaration gets, and writing a second escaper would be worse. The two copies are the same file and
merge cleanly; if `declarations` changes it, theirs wins.
