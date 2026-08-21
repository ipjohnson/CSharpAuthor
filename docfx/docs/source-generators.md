---
title: Using it in a source generator
---

# Using it in a source generator

This is what the library is for. A Roslyn source generator has two constraints that shape how
CSharpAuthor ships, and both are worth understanding before you wire it up.

## The packaging model

**A generator cannot take a normal dependency.** An analyzer is loaded by the compiler with no
probing path of its own, so a `CSharpAuthor.dll` sitting next to your analyzer is not a reference —
it is a `FileNotFoundException` at generator initialization, after which the build carries on
emitting nothing until the missing generated types surface as unrelated-looking `CS0246`s
somewhere else.

So CSharpAuthor ships **as source**, in one package. Set a property and the package compiles its
own `.cs` files into your assembly:

```xml
<PropertyGroup>
  <PackageCSharpAuthorIncludeSource>true</PackageCSharpAuthorIncludeSource>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="CSharpAuthor" Version="2.0.0" PrivateAssets="all" IncludeAssets="build" />
</ItemGroup>
```

`IncludeAssets="build"` is the point: the package contributes its MSBuild targets and no assembly.
Here is the whole of what those targets do:

[!code-xml[](../../CSharpAuthor/Package/CSharpAuthor.targets)]

Cost of source-including the library, including the ~250 generated grammar nodes, as measured
during 2.0 development: **5,565 → 11,034 lines, build 0.28 s → 0.40 s, assembly 75 KB → 203 KB.**

Two consequences to know about:

- **The grammar nodes do not leak into your public API.** The ~250 types under
  `CSharpAuthor.Syntax` are guarded by `#if CSHARPAUTHOR_PUBLIC_SYNTAX`, and only
  `CSharpAuthor.csproj` defines that symbol. Compiled into your assembly without it, they take
  C#'s default accessibility for a top-level type, which is `internal`. **Do not define that
  symbol in your project.**
- **The rest of the library *is* public in your assembly.** `ClassDefinition`, `TypeDefinition`
  and friends are declared `public`, and source inclusion does not change that. If your generator
  is packable and you care about its surface, that is something to look at.

### There is no second package

The Roslyn bridge — everything that converts between Roslyn's symbols and CSharpAuthor's type
model — is written against `Microsoft.CodeAnalysis`, and the shipped `CSharpAuthor.dll` is
`netstandard2.0` with **no** Roslyn reference at all. A `netstandard2.0` library that took a Roslyn
dependency would impose it on everyone, including build tasks that have no compiler in sight.

Rather than split the package, the bridge ships as a **second source folder in the same package**,
behind a sibling property:

```xml
<PropertyGroup>
  <PackageCSharpAuthorIncludeSource>true</PackageCSharpAuthorIncludeSource>
  <PackageCSharpAuthorIncludeRoslyn>true</PackageCSharpAuthorIncludeRoslyn>
</PropertyGroup>
```

A generator project already references Roslyn, or it could not be a generator, so this adds no
package to anything. Setting `IncludeRoslyn` without `IncludeSource` turns `IncludeSource` on, because
the bridge extends `EmitProfile` as a `partial` and has to be compiled beside it.

> [!WARNING]
> If you wire a **local checkout** rather than the package — with a `<Compile Include>` glob of
> your own — exclude `CSharpAuthor/Roslyn/**`, or you will compile Roslyn-dependent source into
> every project that uses the glob, including build tasks that do not reference Roslyn. That
> mistake took three projects and six test assemblies down during 2.0 development. The sample
> project below shows the glob with the exclusion in place.

## The Roslyn bridge

With `PackageCSharpAuthorIncludeRoslyn=true` you get `CSharpAuthor.Roslyn`:

| | |
|---|---|
| `ITypeSymbol.GetTypeDefinition()` | a symbol becomes an @CSharpAuthor.ITypeDefinition, carrying array ranks, tuple element names, nested containers, pointers, and the difference between `Nullable<T>` and an annotated reference type |
| `SyntaxNode.GetTypeDefinition(semanticModel)` | the same, from a syntax node, honouring `NullableTypeSyntax` |
| `symbol.GetAttributeInstances()` | bound attribute data as `AttributeInstance`, with typed constructor and named arguments |
| `symbol.FindAttribute(type)` / `HasAttribute(type)` | matched with or without the `Attribute` suffix |
| `EmitProfileRoslynExtensions.ForGeneration(optionsProvider, parseOptions)` | an @CSharpAuthor.Profiles.EmitProfile from the host's `.editorconfig` **and** its `LangVersion` |
| `emitDiagnostic.ToDiagnostic(location)` | a CSharpAuthor diagnostic as a `Microsoft.CodeAnalysis.Diagnostic` |

The one to wire up first is `ForGeneration`. It is what makes generated code match the host
project's formatting *and* respect its language version, instead of guessing at both:

```csharp
var profiles = context.AnalyzerConfigOptionsProvider
    .Combine(context.ParseOptionsProvider)
    .Select(static (pair, _) => EmitProfileRoslynExtensions.ForGeneration(pair.Left, pair.Right));
```

`LatestSupported()` reports what the *referenced* Roslyn knows, which for
`Microsoft.CodeAnalysis.CSharp` 4.14.0 is C# 13, regardless of which SDK is installed.

## A complete generator

For every partial class marked `[Describe]`, this emits a `Describe()` method naming each public
property and its declared type.

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples.Generator/DescribeGenerator.cs#generator)]

Given this in the host project:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples.Generator.Runner/Program.cs#host-source)]

it produces the marker attribute:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples.Generator/expected/source-generators-DescribeAttribute.txt)]

and the partial:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples.Generator/expected/source-generators-Widget.Describe.txt)]

`int?` came through as `int?` and not `Nullable<Int32>`, `IReadOnlyList<string>` qualified its
container without qualifying `string`, and the whole file is `global::`-qualified — none of which
the generator's own code had to arrange, because it handed over `ITypeDefinition`s rather than
strings.

> The generated file is checked at build time: the sample runner compiles what the generator
> produced and fails if there are any errors. See
> [Working on these docs](contributing-to-the-docs.md).

### The project that builds it

[!code-xml[](../../docfx/samples/CSharpAuthor.Docs.Samples.Generator/CSharpAuthor.Docs.Samples.Generator.csproj)]

This repository's own sample cannot use `PackageCSharpAuthorIncludeSource`, because it builds
against the working tree rather than a restored package. It reproduces exactly what that property
does instead: the same two globs, against the checkout. Your project sets the two properties and
gets the same result.

## Practical notes

**Use `TypeOutputMode.Global`.** A generated file is compiled inside a project you do not control,
and `global::` is the only spelling a user cannot accidentally capture. See
[Output modes](output-modes.md#use-global-in-a-source-generator).

**Extension methods still need `AddUsingNamespace`.** `global::` cannot name one. See
[Extension methods still need a `using`](output-modes.md#extension-methods-still-need-a-using).

**Keep the model comparable by value.** Roslyn's incremental caching compares your model with
`Equals`, and a record holding an `ImmutableArray` compares by reference — identical contents,
`Equals == false`, cache never hits. @CSharpAuthor.Collections.EquatableArray`1 is in the library
for this.

**Prefer `EmitErrorDirective` over `Throw`.** An impossible feature should become a diagnostic you
report against the user's compilation, not an exception that takes their build down. See
[Emit profiles](emit-profiles.md#impossible-features-are-errors).

**If you generate an XML documentation file, suppress CS1591 in your own project.** Source
inclusion means CSharpAuthor's `.cs` files are compiled by *your* compilation, under *your*
settings, and most of the library's public surface is not yet documented. `NoWarn` in the
library's own csproj does not travel with the source. If your generator project sets
`GenerateDocumentationFile=true` — and especially if it also sets `TreatWarningsAsErrors` — add:

```xml
<NoWarn>$(NoWarn);CS1591</NoWarn>
```

Neither of CSharpAuthor's own consumer suites hits that combination today, so this is a hazard to
know about rather than one you are likely to have already hit.
