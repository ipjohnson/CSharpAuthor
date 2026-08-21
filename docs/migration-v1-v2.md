# Migrating V1 to V2

<!-- Sections are agent-scoped so that concurrent builders append rather than collide. -->

## expressions

### Breaking changes

None. The expression layer is additive: it introduces the namespace
`CSharpAuthor.Expressions` and does not add to, remove from or alter any type in the
`CSharpAuthor` namespace. Existing code compiles and emits byte-for-byte identical output.

### Snapshot diff — `DependencyModules.Tests.PublicApiTests.SourceGeneratorApi`

**Not re-baselined.** Recorded here per rule 8.1; approving it is the human's call.

CSharpAuthor is source-compiled into `DependencyModules.SourceGenerator`, so every public
type it declares appears in that assembly's approved public-API snapshot. The new
expression layer is public by design — it is the ergonomic front door a generator author
uses — and therefore shows up.

Measured against
`tests/DependencyModules.Tests/Snapshots/PublicApiTests.SourceGeneratorApi.verified.txt`:

| | |
|---|---|
| Lines added | 296 |
| Lines removed | **0** |
| Lines changed | **0** |

Every added line is inside `namespace CSharpAuthor.Expressions`. Nothing existing moved.
The diff is a pure addition of new API surface, not a change to old API surface, and no
behaviour of any V1 type is affected.

Consumer results with the layer in place:

| Suite | Result |
|---|---|
| `DependencyModules.Tests` (net8.0) | 734 passed, 1 failed — the API snapshot above |
| `DependencyModules.Tests` (net10.0) | 734 passed, 1 failed — the same test |
| `Hardened.SourceGenerator.Tests` (net8.0) | 468 passed, 0 failed |

If the intent is that a source-included CSharpAuthor should not widen the consumer's public
surface at all, the mechanical fix is to compile the layer as `internal` under the
`PackageCSharpAuthorIncludeSource` condition. Internal types are still fully usable by the
consumer that source-includes them; they simply stop appearing in its public API. That
choice affects every public type V1 already ships, not just this layer, so it is left open
rather than taken unilaterally.

### New API worth knowing about

- `Ex` — expressions with automatic, precedence-correct parenthesisation. A bare `string`
  converts to an *identifier*; literals are explicit (`Ex.Str`, `Ex.Int`, `Ex.Char`).
- `Pat` — patterns, including the `and`/`or`/`not` combinators and their own precedence.
- `Raw` — the escape hatch, which holds `ITypeDefinition` references unrendered, exposes
  them through `Raw.TypeReferences`, and infers its own precedence.
- `CSharpText` — invariant-culture numeric literals, escaped string and character
  literals, and keyword-escaped identifiers. Available directly for writers that are not
  yet using `Ex`.

`CSharpText` also supersedes `SyntaxHelpers.QuoteString`, which does not escape its input
and so emits `"he said "hi""` for a string containing quotes. `QuoteString` is left alone
here because it is another agent's territory; it is reported as a live defect.
