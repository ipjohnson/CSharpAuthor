# V2 open questions

Defaults taken where the specification was silent, recorded per §8.4. Each keeps V1 source
compatible unless the handoff said otherwise.

## Roslyn bridge (`CSharpAuthor.Roslyn`)

### Decisions taken

1. **A type in the global namespace has no namespace.** Roslyn writes `global::GlobalThing`;
   the type model spells "no namespace" as the empty string, and an empty namespace cannot
   carry a `global::` prefix. `Global` mode therefore writes `GlobalThing`, which is valid C#
   but not maximally qualified — a generated file that declares its own `GlobalThing` would
   shadow it. Fixing this properly means letting `TypeDefinition` distinguish "global
   namespace" from "no namespace", which is the type model's call, not the bridge's.

2. **Structs, records and delegates convert to `TypeDefinitionEnum.ClassDefinition`.** The
   enum has three members and neither consumer has ever had more; both already do this.

3. **`System.Void` keeps the `("System", "Void")` identity** rather than becoming a keyword
   with an empty namespace like the other special types. It already renders as `void` in
   every mode, and changing the pair would stop it comparing equal to
   `TypeDefinition.Get(typeof(void))`.

4. **`System.IntPtr` and `System.UIntPtr` convert to `nint` and `nuint`.** §7 asks for it, and
   it is what Roslyn's own fully-qualified display produces on a runtime where the two are
   unified — the symbol carries no flag that would let the bridge tell `IntPtr` from `nint`
   there. A consumer emitting for C# 8, where `nint` does not exist, needs the profile to
   downlevel it; the bridge does not decide language versions.

5. **Namespaces are not `@`-escaped; type names are.** `INamespaceSymbol.ToDisplayString()` is
   unescaped and DependencyModules compares namespaces against that spelling, so escaping here
   would break the comparison. A namespace segment that is a keyword still needs escaping at
   the point the `using` is written, which is the output context's job.

6. **The bridge's types are public.** §3 marks *generated node* types internal when source
   included; the bridge is different — DependencyModules splits its generator across two
   assemblies and the converted types cross that boundary. Only a consumer that set
   `PackageCSharpAuthorIncludeRoslyn` sees them at all.

7. **`Nullable<T>` gets its own type; a nullable annotation does not.** `int?` converts to
   `NullableValueTypeDefinition`, which derives from `TypeDefinition` so it still compares
   equal to a hand-built `TypeDefinition.Get(typeof(int)).MakeNullable()` in both directions
   and hashes the same. `string?` stays a plain type with `IsNullable` set. The two are
   distinguishable through `IsNullableValueType()`, which is what an emitter needs before it
   can drop one `?` and keep the other.

8. **A plain `T[]` keeps the model's flattened array shape.** Only what the flag cannot express
   — rank above one, jagged, or an annotation on an array level — becomes an
   `ArrayTypeDefinition`. This keeps the common case comparing equal to what callers build by
   hand.

### Merging with the type model

The bridge's five type implementations already answer `ArrayRanks`, `ContainingType` and
`MakeArray(int)` — the members the type model is adding — in the terms the model asks them in,
so they satisfy the wider interface without an edit. Compiling the bridge folder against that
work produces exactly one error, and it is in `NullableValueTypeDefinition`: it derives from
`TypeDefinition`, and the no-argument `MakeArray()` it overrides stops being virtual there. The
two overloads collapse into `public override ITypeDefinition MakeArray(int rank)`. With that one
edit the two compile clean together, warnings-as-errors included — measured, not assumed.

Once the model carries ranks and a containing type of its own, `ArrayTypeDefinition` and
`NestedTypeDefinition` stop earning their place: the conversion can build the model's own type
directly and the bridge sheds two classes. That is a simplification, not a fix, and it belongs
after both are merged.

### Still open

- `ArrayTypeDefinition`, `TupleTypeDefinition`, `PointerTypeDefinition`,
  `FunctionPointerTypeDefinition` and `NestedTypeDefinition` have no Roslyn dependency. They
  live in the bridge because that is what produces them, but they are type-model types and
  would be more useful in `CSharpAuthor` proper, where a caller who is not a source generator
  could reach them.
- A pointer type has no legal home in a generated class: `ComponentModifier` has no `Unsafe`,
  so a field of type `int*` cannot be emitted even though the type converts correctly.

## Opting in

The bridge is a second source folder in the same package, not a second package. A consumer
adds one property to the project that already references CSharpAuthor:

```xml
<PropertyGroup>
  <PackageCSharpAuthorIncludeRoslyn>true</PackageCSharpAuthorIncludeRoslyn>
</PropertyGroup>
```

It implies `PackageCSharpAuthorIncludeSource` unless that is set explicitly, because the
package is normally referenced with `IncludeAssets="build"` and brings no assembly with it.
The project needs `Microsoft.CodeAnalysis.CSharp`, which a generator project already has.

`scripts/verify-roslyn-packaging.sh` checks both directions: that the package carries no
Roslyn-dependent source in the folder every consumer compiles, and that a project which opts
in compiles the bridge on netstandard2.0 at LangVersion 10 under `TreatWarningsAsErrors` and
`EnforceExtendedAnalyzerRules`.
