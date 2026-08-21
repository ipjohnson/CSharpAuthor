# Migration V1 → V2

## Declarations, literals and statements

Owner: `declarations` builder. Branch `v2/declarations`.

**No existing test asserted buggy behaviour.** All 139 tests in `CSharpAuthor.Tests`
passed unmodified through every change in this area, and none was edited or
re-baselined. Where a change alters emitted output, it is listed below.

Three existing tests did reject an early version of the identifier escaping —
`IndexerPropertyTests` — and they were right: an indexer is a `PropertyDefinition`
named `this`, where `this` is the keyword and not a name, so it must not become
`@this`. The library was fixed, not the tests.

### Source-compatible additions

Nothing in the public surface was removed or renamed. New API:

| Added | Why |
|---|---|
| `ComponentModifier.ProtectedInternal`, `.PrivateProtected` | Names for the existing flag pairs. Callers already writing `Protected \| Internal` need no change. |
| `ComponentModifierExtensions` | `GetAccessibilityKeywords`, `GetAccessorAccessibilityKeywords`, `GetModifierKeywords`. |
| `LiteralFormatter` | One place that turns a value into C# literal text. |
| `CSharpIdentifier` | `Escape`, `EscapeReference`, `EscapeQualified`, `IsReservedKeyword`. |
| `MethodDefinition.OmitBody` | Declares the defining half of a `partial` method. There was no way to express one. |
| `ForDefinition` constructors, `Variable`, `Initializer`, `Condition`, `Increment` | The class existed but wrote nothing. |
| `BaseBlockDefinition.For(...)`, `.Continue()` | Nothing returned a `ForDefinition`; `Continue` had no equivalent to `Break`. |

### Output changes

Each of these changes generated text. The first five are the ones most likely to
appear in a consumer snapshot diff.

| # | Was | Now | Why |
|---|---|---|---|
| 1 | `1.5` for a `double` | `1.5d` | Without a suffix, `Get(1.0d)` and `Get(1)` both emit `1`, so the source type is lost. Where the emitted text is an argument, `1` binds `Foo(int)` and `1d` binds `Foo(double)` — a silently different overload. Consistent with `f`, `m`, `L`, `U`, `UL`. |
| 2 | `1.5` for a `float` | `1.5f` | `float f = 1.5;` is CS0664. |
| 3 | `1` for `long`/`uint`/`ulong` | `1L` / `1U` / `1UL` | A value above `int.MaxValue` did not fit the literal it was written as. |
| 4 | `readonly static` on a field | `static readonly` | The conventional order. |
| 5 | `a` for a `char` | `'a'` | `char c = a;` is CS0103. |
| 6 | `"he said "hi""` | `"he said \"hi\""` | Only affects values that contain something needing an escape. |
| 7 | `internal` for `Protected \| Internal` | `protected internal` | |
| 8 | `protected` for `Private \| Protected` | `private protected` | **Widened access.** See below. |
| 9 | `{ }` after an `abstract` method | `;` | CS0500. |
| 10 | `partial`, `readonly` dropped | emitted | |
| 11 | `override` for `Sealed \| Override` | `sealed override` | |
| 12 | a name that is a keyword | `@name` | Only affects names that are one of C#'s 77 reserved words. |
| 13 | property setter: `internal set` emitted nothing | `internal set` | Only `private` and `protected` were handled. |
| 14 | property with a bodied `init` accessor emitted `set` | `init` | `IsInit` was honoured only on the auto-property path. |

### The one to read twice

`Private | Protected` emitted `protected`. `private protected` is the most
restrictive accessibility C# has — a derived type **in this assembly**.
`protected` is reachable from a derived type in **any** assembly. Every member a
generator declared as `private protected` was published outside its own
assembly, and because the result compiled, nothing reported it.

If a consumer's snapshot changes from `protected` to `private protected`, that
snapshot was recording the defect.

### Not changed, deliberately

- **A `string` passed to `CodeOutputComponent.Get` is still emitted unquoted.**
  Throughout this library a string argument is a fragment of code —
  `AddCode("Foo()")`, `Get("Lifetime.Scoped")` — and quoting it would turn every
  one of them into text. `SyntaxHelpers.QuoteString` is how a caller asks for a
  string literal, and that now escapes.
- **Modifier combinations are not validated.** `abstract sealed` on a method is
  CS0238 and gets written anyway, consistent with the rest of the library, which
  writes what it is handed. A modifier the compiler rejects is better than one
  dropped in silence.
- **An indexer's name is not escaped.** `this[int index]`, not `@this[...]`.

### Verification

Beyond the unit tests, the emitted output was compiled by the real C# compiler:
a file exercising every fix above was generated **under `de-DE`**, compiled
clean on `net8.0`, and then executed to confirm every literal equals the value
it was generated from — including a string containing `"`, `\`, CRLF, tab, NUL,
a control character, and a surrogate pair.

### Culture-dependent APIs (RS1035)

Both consumers build with `EnforceExtendedAnalyzerRules=true`, and this library
is compiled **into** them from source, so a culture-dependent API here is a hard
error in their build and invisible to `dotnet test CSharpAuthor.Tests`. The whole
library was swept, not only the numeric emission sites:

| Site | Was | Now |
|---|---|---|
| `BaseBlockDefinition.AddCode` | `IndexOf(placeholder, StringComparison.CurrentCulture)` ×2 | `StringComparison.Ordinal` |
| `BaseBlockDefinition.AddCode` | `"{arg" + (index + 1)` ×2 — `int.ToString()` under the current culture | `.ToString(CultureInfo.InvariantCulture)` |
| `MethodDefinition.GetUniqueVariable` | `prefix + VariableCount++` | `.ToString(CultureInfo.InvariantCulture)` |
| `AttributeDefinition.WriteBody` | `EndsWith(string)` — the culture-sensitive overload | `EndsWith(string, StringComparison.Ordinal)` |

Locating a fixed placeholder is an exact-text question, so ordinal is also the
correct answer on the merits, not only for the analyzer.

Clean already, and confirmed by sweep: no `ToLower`/`ToUpper`, no `Parse`, no
`Convert`, no `string.Format`, no file I/O, no `Environment`. `string.Compare`
was already explicit `StringComparison.Ordinal` everywhere. The `IndexOf(char)`
overloads are ordinal by definition and were left alone.

netstandard2.0 was respected throughout: surrogate pairs are handled manually
with `char.IsHighSurrogate`/`IsLowSurrogate` (there is no `Rune`), and nothing
uses `string.Contains(char)` or `HashCode`.

### Public API surface

`DependencyModules`' `PublicApiTests` snapshot legitimately changes, because §7
requires new public API. After demoting every incidental helper to `internal`,
the diff is exactly:

```
BaseBlockDefinition:  + Continue()
                      + For(IOutputComponent?, IOutputComponent?, IOutputComponent?)
                      + For(string, object, object)
ComponentModifier:    + ProtectedInternal = 1026
                      + PrivateProtected  = 6
ForDefinition:        + two constructors
                      + Condition, Increment, Initializer, Variable
MethodDefinition:     + OmitBody
```

Every line is a §7 line item except `MethodDefinition.OmitBody`, which is the
only way to declare the defining half of a `partial` method and so is mandated in
substance — see `docs/v2-open-questions.md` §9.

Demoted to `internal` and therefore **absent** from the diff: `LiteralFormatter`,
`CSharpIdentifier`, `ComponentModifierExtensions` (whole classes), and
`MethodDefinition.IsBodyless` (now `private`). That removed 25 of the 38 added
lines.

**This snapshot has not been re-baselined and must not be** (rule 8.1). Adding
public API is exactly the kind of change that is the human's call, so
`DependencyModules` sits at **733/735** — the two failures are the same single
`PublicApiTests` test, counted once per target framework. It cannot reach 735/735
without someone approving the addition above.

### Proof that `private protected` no longer widens access

A string assertion can be satisfied by a coincidence, so this was checked against
the compiler as a differential. Two types were generated that differ **only** in
that modifier, and one separate assembly was compiled against each, deriving from
the type and touching a method, a field and a property:

| Generated as | Cross-assembly derived access |
|---|---|
| `protected` | **compiles** — correct, it is reachable from a derived type in any assembly |
| `private protected` | **fails** — the members do not resolve at all |

Same generator, same consumer source, one modifier different, opposite results.
Had the emitter still widened `private protected` to `protected`, both would have
compiled.
