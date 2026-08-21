---
title: CSharpAuthor
---

# CSharpAuthor

A library for programmatically generating C# source. You build a tree of definitions; it emits
formatted, compilable C#.

It exists for **Roslyn source generators**. A generator runs inside the compiler, on every
keystroke, for every project that references it, so how fast it produces a file and how much it
allocates doing so are not academic questions.

## Why not `SyntaxFactory`

Roslyn can build C# too — `SyntaxFactory` plus `NormalizeWhitespace()`. On the same payload
(one class, 25 init-only properties, a constructor assigning all of them, a method with 27
statements), 2,000 iterations in Release:

| | per file |
|---|---|
| CSharpAuthor | **0.019 ms** |
| `SyntaxFactory` + `NormalizeWhitespace` | 0.489 ms |

**About 25× faster**, and it allocates several times less.

The ergonomics differ as much as the numbers. `SyntaxFactory` makes you name every token and
every separator; CSharpAuthor makes you name the class, the method and the statement.

## The idea worth knowing before anything else

A type is not text until the file is serialized.

`ITypeDefinition` records a namespace, a name, an array shape and a nullable annotation, and
renders none of it until the whole file has been written. That is why **one option** flips a file
between `List<string>` with a `using` at the top and
`global::System.Collections.Generic.List<string>` with no usings at all — and why two types that
share a short name can be given an alias, rather than colliding.

[The type model](docs/type-model.md) is the page to read second.

## Hello, world

[!code-csharp[](samples/CSharpAuthor.Docs.Samples/GettingStarted.cs#smallest)]

produces

[!code-csharp[](samples/CSharpAuthor.Docs.Samples/expected/getting-started-smallest.txt)]

## Where to go

- **[Getting started](docs/getting-started.md)** — install it, and the smallest thing that works.
- **[The type model](docs/type-model.md)** — `ITypeDefinition`, deferred rendering, and what it buys.
- **[Output modes](docs/output-modes.md)** — `Global` vs `ShortName`, derived usings, collision aliasing.
- **[Emit profiles](docs/emit-profiles.md)** — targeting a language version, downlevelling, diagnostics.
- **[Using it in a source generator](docs/source-generators.md)** — the packaging model and the Roslyn bridge.
- **[Migrating from 1.x](docs/migrating-from-v1.md)**.
- **[API reference](api/index.md)** — generated from the library's XML documentation.

> [!NOTE]
> Every C# block on this site is a region of a file in
> [`docfx/samples`](https://github.com/ipjohnson/CSharpAuthor/tree/main/docfx/samples), and every
> "produces" block is that program's recorded output. Neither is typed into the page by hand.
> See [Working on these docs](docs/contributing-to-the-docs.md).
