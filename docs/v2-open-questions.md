# V2 open questions — defaults taken where the spec was silent

Each entry records a decision made under handoff rule 8.4: the spec did not say, so the
option that keeps V1 source-compatible was taken and written down rather than asked about.

<!-- Sections are agent-scoped so that concurrent builders append rather than collide. -->

## expressions

### 1. The expression layer lives in `CSharpAuthor.Expressions`

§2 names three namespaces — `CSharpAuthor`, `CSharpAuthor.Syntax`, `CSharpAuthor.Roslyn` —
and does not say which one holds the expression combinators.

Taken: a new `CSharpAuthor.Expressions`. The root namespace already contains V1 type names
(`NewStatement`, `TypeStatement`) that a combinator layer would collide with, and
`CSharpAuthor.Syntax` belongs to the generated grammar, whose node names include
`BinaryExpression`, `SwitchExpression` and so on. V1 source compatibility is exact:
nothing in the root namespace was added, removed or changed.

### 2. Role interfaces are `IExpressionNode` / `IStatementNode` / `IPatternNode`

Invariant 4 says `Raw` implements `IExpression`, `IStatement` and `IPattern`. The generated
grammar declares interfaces with exactly those names in `CSharpAuthor.Syntax`, and two
identically named interfaces in two imported namespaces make every call site ambiguous.

Taken: distinct names here, and every public type in the layer is `partial`. Attaching the
generated interfaces at integration is a one-line file per type and needs no edit to the
expression sources. See the integration note in the agent report.

### 3. Parentheses are minimal, not defensive

The spec asks that emitted text re-parse to the same tree. It does not say whether to add
brackets that are merely reassuring.

Taken: minimal. A bracket appears exactly where dropping it would change the tree.
`a ?? b ?? c` and `a ? b : c ? d : e` are emitted bare, because `??` and `?:` are
right-associative and those nestings are the ones the language already gives for free.
`Ex.Paren` is available where an author wants grouping for a reader rather than a parser,
and explicit brackets are always preserved.

### 4. `Raw` infers its precedence, and defaults to bracketing when it cannot

V1's `CodeOutputComponent` treats every fragment as an atom. That is wrong for
`Get("a + b")` used as an operand, and wrong silently.

Taken: `Raw` reads the shape of its own text. A member chain, a call, a literal or a
keyword primary is `Primary`; a `?.` chain is `NullChain`; a prefix operator is `Unary`;
anything with a token left over is `Lowest`, which brackets. This diverges from V1's
assumption deliberately, and in the safe direction — the failure mode becomes a redundant
pair of brackets instead of a reassociated expression. Two carve-outs keep V1 behaviour:
an opaque `IOutputComponent` part is still treated as an atom, because its text cannot be
inspected, and `Raw.At` lets an author asserts a precedence outright.

### 5. `-(-a)` rather than `- -a`

Both are valid C#, and `--a` is neither. Taken: brackets. A space is load-bearing
punctuation that a later reformat can eat; a bracket is not.

The hazard is per-operator, so `!` and `~` do not bracket a unary operand — `!!a` and
`~-a` are emitted bare, since neither can re-lex.

### 6. Switch expressions render one arm per line by default

`Ex.Switch` writes multi-line at the surrounding indent, which is how the construct is
normally read. `Ex.SwitchInline` writes one line, which is what an assertion in a test
usually wants.

### 7. Argument modifiers and `throw` are never bracketed

`f((out x))` and `a ?? (throw new T())` are both compile errors — the second is CS8115,
verified. Nodes for `out`/`ref`/`in`/named arguments and for `throw` expressions carry a
flag that suppresses bracketing entirely, so an operand rule cannot make them invalid.
