# V2 open questions

Defaults taken under V2-HANDOFF.md §8.4 — *when the spec is silent, choose the option
that keeps V1 source-compatible, record it, and do not stop to ask.*

One section per agent. When this file conflicts on merge, the resolution is to keep
both sides' sections; nothing here depends on anything above it.

---

## grammar

### 1. Accessor lists use block braces, so an auto-property spans four lines

**Chosen:** `AccessorListSyntax` gets Allman block braces like any other node whose braces
enclose an unseparated list of nodes.

```csharp
public int Count
{
    get;
    set;
}
```

**Why:** the alternative — inline braces, giving the familiar `public int Count { get; set; }`
— is better for auto-properties and clearly worse for an accessor with a body, which then
reads as

```csharp
public int Count { get
{
    return _count;
}
}
```

Deciding per-instance needs to know whether any accessor has a body *before* the opening
brace is written, and the writer streams into `IOutputContext` without buffering, so it
cannot look ahead and cannot retract a newline it has already emitted. Block braces are
always valid and never ugly; inline braces are sometimes prettier and sometimes bad.

**Nothing regresses today:** V1's `PropertyDefinition` facade still emits `{ get; set; }`
and that is what both consumers use. All nine DependencyModules generator-output snapshots
are byte-identical.

**To choose differently:** this is `EmitProfile` territory (§4) — a formatting preference,
not a capability. Either add a brace-style option that the writer consults, or give the
writer a bounded lookahead: a marker interface the generator assigns structurally to any
node that has both a body field and a semicolon field, exposing "am I body-less", plus a
`BraceGroup` emission for the shape `{ OpenBrace, SyntaxList<T>, CloseBrace }`. The
generator can express both; neither was worth destabilising a green consumer run for.

### 2. A property initializer after an accessor list lands on its own line

**Chosen:** accepted.

```csharp
public string Name
{
    get;
    set;
}
= "";
```

**Why:** same root cause as (1). `IOutputContext.CloseScope()` writes the indent, the
brace and a newline as one operation, so anything following a block brace necessarily
starts a new line. Using `CloseScope` is deliberate — V2-HANDOFF.md asks for indentation
through the context's scope markers rather than a depth counter in the writer, and a
segment-based context needs those markers to restyle output later.

Valid C#, and verified to compile.

**To choose differently:** make `CloseBrace` request a pending line break instead of
emitting one, and let the containing list style supply the break. That works for
statements, members and catch clauses, but changes `}` + `else` from Allman to K&R, so it
needs the brace-style option from (1) landing first.

### 3. Brace style is Allman, and not configurable yet

**Chosen:** Allman, unconditionally, matching V1's existing output.

**Why:** `OutputContextOptions` has no `BraceStyle` today, and `EmitProfile` (§4) is the
profiles agent's slice. The writer funnels every block brace through two methods, so the
switch point is one place when the option arrives.

### 4. `CSharpAuthor.Syntax.Attribute` collides with `System.Attribute`

**Chosen:** keep the grammar's own name. `AttributeSyntax` → `Attribute`, as with every
other node.

**Why:** it is the only collision — checked against every common `System` type, 1 of 250
class names. Renaming one node to dodge it would make the mapping from `Syntax.xml` to
class name conditional, which is exactly the kind of special case that stops a
regeneration being mechanical. A caller who has `using System;` in scope writes
`using Attr = CSharpAuthor.Syntax.Attribute;`, and there is a test covering it.

**To choose differently:** keep the `Syntax` suffix on every generated class name
(`AttributeSyntax`, `ClassDeclarationSyntax`, …). Collision-free and familiar to anyone who
knows Roslyn, at the cost of verbosity everywhere.

### 5. `SimpleNameSyntax` / `IdentifierNameSyntax` slots take a `TypeRef`

**Chosen:** every type-shaped slot in the grammar — `TypeSyntax`, `NameSyntax`,
`SimpleNameSyntax`, `IdentifierNameSyntax`, `ArrayTypeSyntax` — takes a `TypeRef`, which
holds either an unrendered `ITypeDefinition` or a type node.

**Why:** it makes the deferral point uniform, and `TypeRef` converts implicitly from
`string`, so `new MemberAccessExpression(x, ".", "Length")` reads naturally. The oddity is
that a *member* name is not a type; it travels through
`IOutputContext.Write(ITypeDefinition)` with an empty namespace, which is a no-op for
namespace derivation.

**To choose differently:** split the slot kinds — `TypeRef` for `TypeSyntax`/`NameSyntax`,
a plain `ISimpleName?` node reference for the rest. More correct, more verbose at every
call site.

### 6. A trailing line break at the very end of output is dropped

**Chosen:** a line break with nothing after it never materialises, so a fragment never
carries trailing whitespace. A file that ends in `}` still ends with the newline that
`CloseScope` wrote, so `CompilationUnit` output does end in a newline in practice; a file
ending in a top-level statement does not.

**Why:** it makes trailing whitespace structurally impossible rather than merely unlikely,
and it keeps exact-text assertions honest.

### 7. `UnsafeExpressionSyntax` is emitted but no shipping compiler parses it

**Chosen:** emit it. It is a concrete `<Node>` in the grammar like any other; the generator
does not judge.

**Why:** filtering on `ExperimentalUrl` would mean the node set silently changes shape when
a feature ships. `--report` names it instead.
