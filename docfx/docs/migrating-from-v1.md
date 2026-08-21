---
title: Migrating from 1.x
---

# Migrating from 1.x

The complete migration document lives in the repository, and it is the authority — every breaking
change with its mechanical fix, every changed snapshot with a justification, and the two patches
that were applied to the production consumers:

> ### **[docs/migration-v1-v2.md](https://github.com/ipjohnson/CSharpAuthor/blob/main/docs/migration-v1-v2.md)**

This page is a map of it, not a copy.

## Start here if you have a generator on 1.x

Most of what breaks is one of two shapes, and both come from the same fix.

In 1.x, seven writers called `AddImportNamespace` unconditionally, bypassing the qualification
mode. In `TypeOutputMode.Global` — a mode whose entire point is that it emits no usings — this left
stray `using` directives in the output. A lot of generated code was quietly resolving through them.

2.0 makes that structurally impossible: `AddImportNamespace(ITypeDefinition)` has left
@CSharpAuthor.IOutputContext, so a writer physically cannot add a namespace. Namespaces are derived
only from types that were actually written.

The stray usings are gone, and anything that was leaning on them stops compiling.

### Shape A — extension methods, `CS1061`

`global::` cannot name an extension method; C# resolves those through `using` directives only. Any
call to `services.AddSingleton(...)`, `builder.UseX(...)` and so on needs its namespace named
explicitly:

```csharp
method.AddUsingNamespace("Microsoft.Extensions.DependencyInjection");
```

Ten such lines fixed roughly 450 of the two consumers' combined failures. This is by far the most
likely thing you need. [Output modes](output-modes.md#extension-methods-still-need-a-using) has
the detail.

### Shape B — type names written as raw strings, `CS0246` / `CS0103`

A member reached off a type, written as a string — `"ServiceLifetime.Transient"` — tracks no
namespace, so nothing derives a `using` for it and nothing qualifies it. It only ever worked
because of one of those stray usings.

The fix is to hand over the type:

```csharp
CodeOutputComponent.Get(lifetimeType, "Transient")
```

[Members reached off a type](output-modes.md#members-reached-off-a-type) shows both sides.

## The other things to check

| | |
|---|---|
| **`using CSharpAuthor.Profiles;`** | Every 2.0 profile type is in `CSharpAuthor.Profiles`, not `CSharpAuthor` — `EmitProfile`, `EmitSession`, `EmitDiagnostic`, `EmitResult`, `ProfileEmitter`, `LanguageVersion`, `LanguageFeature` and the downlevel statements. Two of those names collide with Roslyn's. No 1.x code is affected; every one of them is new. |
| **`BaseTypeDefinition.MakeArray()` is no longer virtual** | If you subclass the type model and `override MakeArray()`, that is now `CS0506`. Callers bind exactly as before. |
| **`Output()` with no profile is unchanged** | It means `EmitProfile.V1Compatible`, not `EmitProfile.Default` — so block namespaces, no polyfills, no downlevel comments, byte for byte what 1.x produced. |
| **Some types were demoted to `internal`** | `LiteralFormatter`, `CSharpIdentifier`, `ComponentModifierExtensions`, `MethodDefinition.IsBodyless`. |
| **String literals and numbers are now escaped and culture-invariant** | 1.x emitted `"he said "hi""` and, on a `de-DE` machine, `1,5`. If you were working around either, stop. |

## Verifying your own migration

The two production consumers were migrated as part of 2.0, and both patches are in the repository
next to the migration document. They are short, and reading them is the fastest way to see what a
real migration looks like:

- [`docs/consumer-patches/dependencymodules-v2.patch`](https://github.com/ipjohnson/CSharpAuthor/blob/main/docs/consumer-patches/dependencymodules-v2.patch)
- [`docs/consumer-patches/hardened-v2.patch`](https://github.com/ipjohnson/CSharpAuthor/blob/main/docs/consumer-patches/hardened-v2.patch)
