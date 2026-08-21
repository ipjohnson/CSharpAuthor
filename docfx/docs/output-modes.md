---
title: Output modes
---

# Output modes

Every type reference in a file is spelled one of three ways, and one setting decides which for the
whole file.

```csharp
var output = new OutputContext(new OutputContextOptions
{
    TypeOutputMode = TypeOutputMode.Global,
});
```

| Mode | `List<string>` becomes | Usings |
|---|---|---|
| `ShortName` *(default)* | `List<string>` | derived from the types the file wrote |
| `FullName` | `System.Collections.Generic.List<string>` | none derived |
| `Global` | `global::System.Collections.Generic.List<string>` | none derived |

## Use `Global` in a source generator

**If you are generating code into somebody else's compilation, use
@CSharpAuthor.TypeOutputMode.Global.** It is not the library default, because the default has to
stay compatible with 1.x, but it is the right answer for a generator and it is not a close call.

The reason is that a generated file is compiled inside a project you do not control, and name
resolution in C# is context-sensitive. In `ShortName` mode a file that says `Task` means whatever
`Task` resolves to *in that project*, which depends on the user's usings, their own types, and
anything a different generator emitted into the same namespace. A user who declares
`namespace Acme.Inventory { class Task { } }` silently re-points your generated code at their
class.

`global::` cannot be captured. There is no declaration a user can add that changes what
`global::System.Threading.Tasks.Task` means.

The cost is that the file is more verbose, and that qualified output is slightly faster to produce
than short names (a qualifying mode can write each reference as it goes, where `ShortName` has to
wait until the file is finished to know what needs a `using` and what needs an alias).

Here is one tree in all three modes:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/OutputModeSamples.cs#three-modes)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/output-modes-three-modes.txt)]

## Derived usings

In `ShortName` mode the `using` block is **computed from the types the file wrote**, not from a
list you maintain. Writing `List<string>` is what puts `using System.Collections.Generic;` at the
top; nothing else does, and nothing needs to.

This is enforced structurally: a writer cannot add a namespace directly. Namespaces reach the
`using` block only by way of a type that was written, which is what makes a missing `using`
impossible rather than merely unlikely.

In `FullName` and `Global` modes nothing is derived, because nothing needs to be.

### Dropping the file's own namespace

A `using` naming the namespace the file already declares is noise. Tell the context which
namespace that is and it is left out:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/OutputModeSamples.cs#containing-namespace)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/output-modes-containing-namespace.txt)]

`GreetingService` resolved with no `using` because the file is already in `Acme.Services`;
`Acme.Diagnostics` was still derived for `Log`.

> [!NOTE]
> @CSharpAuthor.OutputContextOptions.ContainingNamespace is set on the *options*.
> @CSharpAuthor.Profiles.EmitProfile has a property of the same name that currently does not
> reach the writer — see [the known gap](emit-profiles.md#known-gap-three-profile-settings-do-not-reach-the-writer).

## Collision aliasing

Two types can want the same short name. In `ShortName` mode that would be `CS0104: ambiguous
reference`. Because names are decided after the whole file is known, the second one can be given
an alias instead:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/OutputModeSamples.cs#collision-aliasing)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/output-modes-collision-aliasing.txt)]

`AliasCollisions` defaults to `true`. Turning it off gets you the ambiguous file, which is
occasionally what you want when you are diffing against something else.

Aliasing has nothing to do in a qualifying mode: `global::Acme.Domain.Task` and
`global::System.Threading.Tasks.Task` were never ambiguous.

## Members reached off a type

This is the mistake worth learning before you make it.

A member reached off a type — `ServiceLifetime.Singleton`, `Task.CompletedTask`,
`StringComparer.Ordinal` — is very tempting to write as a string. A string tracks no namespace. It
derives no `using`, it is never qualified, and it is never aliased. In a file that qualifies
everything else, there is nothing left for it to resolve against:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/OutputModeSamples.cs#members-off-a-type)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/output-modes-members-off-a-type.txt)]

The first file does not compile. The second is the same statement with the type handed over
unrendered, so it qualifies with everything else.

@CSharpAuthor.CodeOutputComponent has the shapes for this:

| | |
|---|---|
| `CodeOutputComponent.Get(type, "Member")` | `Type.Member`, with `Type` unrendered |
| `CodeOutputComponent.FromParts(parts)` | a statement assembled from strings and unrendered types |
| `SyntaxHelpers.Property(type, "Member")` | a static property or field off a type |
| `SyntaxHelpers.Invoke(type, "Method", …)` | a static method call on a type |
| `SyntaxHelpers.TypeOf(type)` | `typeof(T)` |

Reaching for a raw string is still allowed, and it is still the right call for things that are not
types at all. Just know what it costs.

## Extension methods still need a `using`

There is exactly one thing `global::` cannot do, and it will catch you: **C# resolves extension
methods through `using` directives only.** There is no `global::Namespace.Method(x)` form. If your
generated code calls `services.AddSingleton(...)`, the namespace has to be in scope, in every
mode, and no amount of qualification substitutes for it.

Nothing in the tree can derive this for you either — the tree records that a method named
`AddSingleton` was invoked on something, not which static class it actually lives on. So name it:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/OutputModeSamples.cs#extension-usings)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/output-modes-extension-usings.txt)]

`AddUsingNamespace` adds a namespace *by name*, and by-name namespaces survive in a qualifying
mode. That is what @CSharpAuthor.OutputContextOptions.EmitExplicitUsings controls: leave it at its
default of `true` and an explicit `using` you asked for is emitted; set it to `false` and only
derived usings are, which in `Global` mode means none.

> This is the single most common thing to fix when moving a generator from 1.x to 2.0 — in 1.x a
> stray derived `using` in `Global` mode often happened to bring the namespace in. See
> [Migrating from 1.x](migrating-from-v1.md).

## Summary

- Generators want `Global`. Everything else is a preference.
- In `ShortName`, usings are derived from what you wrote — do not maintain a list.
- Write types as types, not as strings, or none of the above applies to them.
- Extension methods need `AddUsingNamespace`, in every mode.
