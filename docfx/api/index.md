---
title: API reference
---

# API reference

Generated from the library's XML documentation comments.

## Where to start

| Namespace | |
|---|---|
| @CSharpAuthor | The type model, the declaration facade and the output context. Almost everything you call is here. |
| @CSharpAuthor.Profiles | @CSharpAuthor.Profiles.EmitProfile, the language-version capability table, and the diagnostic channel. See [Emit profiles](../docs/emit-profiles.md). |
| @CSharpAuthor.Expressions | The node-typed expression layer: `Ex`, `Pat`, `Raw`. |
| @CSharpAuthor.Collections | @CSharpAuthor.Collections.EquatableArray`1, for incremental generator models that have to compare by value. |
| @CSharpAuthor.Syntax | The ~250 generated grammar nodes. `public` in `CSharpAuthor.dll`, `internal` when the library is source-included. |

The three types to read first are @CSharpAuthor.ITypeDefinition,
@CSharpAuthor.CSharpFileDefinition and @CSharpAuthor.OutputContext.

## Not here: `CSharpAuthor.Roslyn`

The Roslyn bridge is **not part of `CSharpAuthor.dll`**. The shipped assembly is `netstandard2.0`
with no reference to `Microsoft.CodeAnalysis`, which is the whole reason a generator can use this
library without dependency grief; the bridge is packed as a second source folder and compiled into
your assembly only when you ask for it.

Since it is never compiled into the library, there is no metadata here to generate pages from. Its
API is listed in
[Using it in a source generator](../docs/source-generators.md#the-roslyn-bridge).

> [!NOTE]
> Coverage of these pages is uneven: a minority of the public surface carries XML documentation
> today, and members without it appear here with a signature and no prose. The
> [conceptual documentation](../docs/getting-started.md) is the fuller description of how the
> pieces fit together.
