# Migrating from CSharpAuthor 1.x to 2.0

Every behaviour change, with the mechanical fix.

<!-- Each build area appends its own section. Keep sections separate so they merge cleanly. -->

## Type model

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

- `ToString()`, `Equals` and `GetHashCode` include the container. Two nested types that differ only in
  their container used to compare **equal**; they no longer do. If you keyed a dictionary on a type
  definition and relied on that collision, you were relying on a bug.
- Rebuilding a type from its parts — `TypeDefinition.Get(t.TypeDefinitionEnum, t.Namespace, t.Name, t.IsArray)`
  — drops the container and the array ranks, exactly as it always dropped generic arguments.
  **Fix:** use the type definition you already have, or the new
  `TypeDefinition.Get(enumValue, ns, name, arrayRanks, isNullable, containingType)` overload.

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

### `ITypeDefinition` gained three members

`ContainingType`, `ArrayRanks` and `MakeArray(int rank)`. Callers are unaffected. **Anyone
implementing `ITypeDefinition` from outside the library must add the three members** —
`netstandard2.0` has no default interface members, so they could not be given bodies. Neither
consumer repository implements the interface; both build unchanged.
