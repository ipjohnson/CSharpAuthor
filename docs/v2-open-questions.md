# V2 open questions

Defaults taken under V2-HANDOFF.md §8.4, where the spec was silent. Each took the
option that keeps V1 source-compatible, and each is cheap to reverse.

## Declarations, literals and statements

Owner: `declarations` builder.

### 1. `double` literals carry a `d` suffix

`1.5` became `1.5d`. A bare `1.0` for a `double` emits `1`, which is
indistinguishable from `Get(1)` — so the source type is lost, and where the text
lands in an argument position, `1` binds `Foo(int)` while `1d` binds
`Foo(double)`. Suffixing keeps the literal denoting the type it came from, and
matches `f`, `m`, `L`, `U`, `UL`.

Reversible: drop the suffix in `LiteralFormatter.FormatDouble`. **This is the
change most likely to show up in a consumer snapshot diff.**

### 2. Float and double use the `"R"` format

`"R"` is shortest-round-trippable on .NET Core. On .NET Framework — which is
where a source generator runs inside Visual Studio — `"R"` has a known precision
bug for which the documented workaround is `G9`/`G17`. `G17` was not chosen
because it prints `0.1` as `0.10000000000000001`, which is worse for every
ordinary value.

If a Framework-hosted generator is ever shown to emit a wrong double, switch to
`G9`/`G17`.

### 3. `partial` alone does not remove a method body

Marking a method `partial` still writes its body. The defining half — the one
that ends at `;` — is asked for with the new `MethodDefinition.OmitBody`.

The alternative, inferring "no statements means defining declaration", would
silently change what an existing caller emits, and a partial *implementation*
with an empty body is equally legal, so the inference has no right answer.

### 4. `for(` matches the existing house style

The library emits `while(` and `foreach(` with no space, so `for(` was written
to match rather than introducing a third convention. C# convention is `for (`.

All three should change together when the formatting pass lands (§4
`EmitProfile`), not one at a time.

### 5. Only reserved words are `@`-escaped

C#'s contextual keywords — `value`, `var`, `record`, `async`, `where`, `nint`,
`required`, `init` — are legal identifiers as they stand. Escaping them would
add noise to the output for no gain. Only the 77 reserved words are escaped.

### 6. Reference sites leave `this`, `base`, `null`, `true`, `false`, `default` alone

These arrive at `InstanceDefinition` as expressions rather than as names, and
`@this.Foo()` is not `this.Foo()`. Declaration sites escape them, because a
parameter genuinely named `this` does need the prefix.

The risk in the other direction is a caller who really did name a field `default`
and refers to it through a bare `InstanceDefinition`; that reference will not be
escaped. Naming a field after one of these six and reaching it without a
qualifier is rare enough to prefer not breaking expressions.

### 7. Non-finite floats emit their named form

There is no C# literal for NaN or infinity, so `float.NaN`,
`float.PositiveInfinity` and the `double` equivalents are emitted as member
accesses. This is the only place the formatter emits something that is not a
literal.

### 8. The incidental helpers are `internal`, not `public`

`LiteralFormatter`, `CSharpIdentifier` and `ComponentModifierExtensions` were
written `public` and are now `internal`. None of them is something §7 asks for:
§7 mandates the *behaviour* (escaped strings, invariant numbers, suffixed
literals, `@`-prefixed keywords), and that reaches callers through API that was
already public — `CodeOutputComponent.Get`, `SyntaxHelpers.QuoteString`, and the
writers themselves.

Making them public would have committed the project to supporting three helper
surfaces forever in exchange for nothing. The consumers **source-include** this
library (§3), so they can still reach every one of them; only the compiled
package's public surface shrinks. §3 already sets this precedent for the
generated grammar nodes: *"mark generated node types `internal` when
source-included so they don't leak into consumer API surface."*

`CSharpAuthor.Tests` reaches them through an `InternalsVisibleTo` declared as an
**MSBuild item**, not a source attribute, so the attribute is generated into
`obj/` and is not one of the `.cs` files a consumer compiles in.

Reversible: `internal` → `public` is not a breaking change, so any of these can
be promoted later if a consumer turns out to want it.

### 9. `MethodDefinition.OmitBody` stays public

It is the only way to express the *defining* half of a `partial` method — the
one that ends at `;`. §7 requires `partial` on methods to work, and emitting the
keyword alone does not achieve that: two implementing halves is CS0111. So this
is mandated in substance even though §7 does not name it.

It is the one member in the public-API diff that is a judgement call rather than
a literal §7 line item.

### 10. `MethodDefinition.IsBodyless` is `private`, not `protected virtual`

Written `protected virtual` out of habit. Nothing overrides it, and `private` →
`protected` is a non-breaking change later while the reverse is not, so it
starts private and stays off the public surface. `OmitBody` already gives
callers the control.

### 11. `AddCode` placeholder matching is ordinal

`AddCode` located its `{argN}` and `[argN]` placeholders with
`StringComparison.CurrentCulture`. Finding a fixed placeholder is an exact-text
question, so it is now `Ordinal`.

This also matters for a reason invisible to gate 1: both consumers build with
`EnforceExtendedAnalyzerRules=true`, which makes culture-dependent APIs hard
errors, and the library is compiled *into* them from source.
