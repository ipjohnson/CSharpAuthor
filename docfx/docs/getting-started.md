---
title: Getting started
---

# Getting started

## Install

```xml
<PackageReference Include="CSharpAuthor" Version="2.0.0" />
```

If you are writing a **Roslyn source generator**, that is not the reference you want — a generator
is loaded by the compiler with no probing path of its own, so a `CSharpAuthor.dll` sitting next to
your analyzer is a `FileNotFoundException` at initialization rather than a reference. The package
ships its own source for exactly that case:

```xml
<PropertyGroup>
  <PackageCSharpAuthorIncludeSource>true</PackageCSharpAuthorIncludeSource>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="CSharpAuthor" Version="2.0.0" PrivateAssets="all" IncludeAssets="build" />
</ItemGroup>
```

[Using it in a source generator](source-generators.md) covers why, and what the second property
(`PackageCSharpAuthorIncludeRoslyn`) adds.

## The smallest thing that works

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/GettingStarted.cs#smallest)]

`Output()` returns a string:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/getting-started-smallest.txt)]

Three things happened that you did not ask for. The namespace was opened and closed, the class
body was indented, and `Greet` was given `public` and `void`. You describe the shape; the writer
handles the punctuation.

## Something with state in it

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/GettingStarted.cs#greeter)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/getting-started-greeter.txt)]

Worth noticing:

- **`TypeDefinition.Get(typeof(string))` came out as `string`, not `String`.** The type model
  knows the C# keyword for every predefined type. See [the type model](type-model.md).
- **`constructor.Assign(nameParameter).To(name.Instance)`** is the assignment shape:
  `Assign` takes the value, and the returned object takes the destination — `.To(...)` for an
  existing target, `.ToVar(...)` to declare a `var`, `.ToLocal(type, ...)` to declare a typed local.
- **`name.Instance`**, not the string `"_name"`. A field, property or parameter hands you an
  @CSharpAuthor.InstanceDefinition that can be assigned to, invoked on, or indexed. Reaching for
  strings works, and costs you what [output modes](output-modes.md) explains.

## The three pieces

Everything on this site is built out of three things.

| | |
|---|---|
| **A tree** | @CSharpAuthor.CSharpFileDefinition, and the `Add…` methods hanging off it. Version-agnostic and rendering-agnostic: the same tree can produce several different files. |
| **A type model** | @CSharpAuthor.ITypeDefinition. Records what a type *is* and renders it only at the end. |
| **An output context** | @CSharpAuthor.OutputContext, or @CSharpAuthor.Profiles.ProfiledOutputContext when you want a language version and a diagnostic channel. This is where every decision about *how* to spell things lives. |

The split is the whole design. Nothing about qualification, indentation, brace style or language
version is stored on the tree, so rendering the same tree under different settings is the normal
case rather than a trick.

## Next

- [The type model](type-model.md) — why types stay unrendered, and what that buys.
- [Output modes](output-modes.md) — `Global` vs `ShortName`, and which one a generator wants.
- [Emit profiles](emit-profiles.md) — targeting a language version.
- [Using it in a source generator](source-generators.md) — packaging, and the Roslyn bridge.
