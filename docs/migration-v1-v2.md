# Migrating from CSharpAuthor 1.x to 2.0

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
