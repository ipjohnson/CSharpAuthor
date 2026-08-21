# V2 open questions

Defaults taken where the handoff was silent, each with the reasoning, for a human to confirm or
overturn. Every one of these took the option that keeps V1 source-compatible.

<!-- Each build area appends its own section. Keep sections separate so they merge cleanly. -->

## Type model

### `nint`/`nuint` need a language-version gate that only the profile can apply

§7 lists `nint`→`IntPtr` as a missing-keyword defect, so `typeof(IntPtr)` now writes `nint`.
Unlike `float`, `char` and `sbyte` — C# 1 keywords, safe everywhere — `nint` and `nuint` need
**C# 9** in the consuming code, and reflection cannot distinguish `nint` from `IntPtr` to let the
caller choose.

**Taken:** always write the keyword, as §7 asks.
**For the `profiles` agent:** this is a capability-gated keyword. `EmitProfile.Target < CSharp9`
should select `IntPtr`/`UIntPtr`. The choice belongs in the writer, not the tree — the type model
holds one value for the type either way.

### `EquatableArray<T>` lives in `CSharpAuthor.Collections`, not `CSharpAuthor`

§7 says the type belongs "beside `ITypeDefinition`", which reads as the `CSharpAuthor` namespace. It is
in `CSharpAuthor.Collections` instead.

CSharpAuthor is *source-compiled into* its consumers, and `Hardened.Framework`'s generators already
source-include an `EquatableArray<T>` of their own (`ValidationModules.SourceGenerator.Impl`, used in
`HandlerValidationFrontEnd.cs`). Nothing breaks today because no file there imports both namespaces —
but in the bare `CSharpAuthor` namespace the two would be one `using CSharpAuthor;` away from CS0104
in a repo that includes both, and the point of adding the type is to let those generators *delete*
their hand-written comparers.

**Taken:** a sub-namespace. Consumers add `using CSharpAuthor.Collections;` where they want it, and
can adopt it file by file while their own version still exists. If the human prefers it in
`CSharpAuthor`, the move is one line plus a `using` in each consumer that adopts it.

### Nullability sits on the array, not on the element

`ITypeDefinition` carries one `IsNullable` flag, and it is written after the array ranks, so
`Get(typeof(int)).MakeNullable().MakeArray()` writes `int[]?` — a nullable array of `int` — not
`int?[]`, an array of nullable `int`. The two are different types. `MakeArray().MakeNullable()` also
writes `int[]?`, which is right, so the flag is not wrong so much as unable to express one of the two
readings.

**Taken:** V1 behaviour preserved exactly — nullability always applies to the outermost array. Fixing
it means a nullability marker per array rank plus one for the element, which changes `IsNullable`'s
meaning for every caller. Not in the §7 defect list, and no consumer writes `int?[]` today.

### Interface additions over base-class-only extension

`ContainingType`, `ArrayRanks` and `MakeArray(int rank)` went on `ITypeDefinition`, which breaks
outside implementors of the interface (`netstandard2.0` has no default interface members).

**Taken:** put them on the interface. Everything in the library and in both consumers passes types
around as `ITypeDefinition`, so members reachable only through `BaseTypeDefinition` would be
unreachable at every call site that matters — the bridge could build a nested type but nothing
downstream could read it. Verified: neither `DependencyModules` nor `Hardened.Framework` implements
`ITypeDefinition`; both only construct through `TypeDefinition.Get` and `new GenericTypeDefinition`,
whose existing signatures are untouched.

### `ToString()` on a type definition keeps its 1.x shape

`$"{Namespace}.{Name}"` hashes `int` and `int[]` — and `Ns.Outer.Inner` and `Ns.Other.Inner` — to the
same value, because `GetHashCode` was `ToString().GetHashCode()`. The first attempt made `ToString()`
the fully qualified C# name; **`Hardened.SourceGenerator.Tests` caught it**.
`HardenedMethodDefinition` builds its own `ToString()` and its cache key out of the return type's, and
asserts the result is `"System.Void Configure()"` — where C# says `void`.

**Taken:** `ToString()` reverted to the 1.x shape exactly, and hashing moved to a private key that is
the fully qualified C# name with containers, generic arguments and array shape in it. Equal values
always agree on either form, so the equality contract holds under both; the private key just stops
every newly distinguishable type landing in one bucket. `WriteTypeName` remains the only thing that
produces C#.

This is worth a human's attention: **`ToString()` on a type definition is public API that a consumer
asserts on**, so it is not a debugger convenience and cannot be improved silently.
