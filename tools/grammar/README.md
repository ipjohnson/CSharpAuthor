# The grammar generator

`CSharpAuthor/Syntax/Nodes.g.cs` is generated from Roslyn's `Syntax.xml`. **Never edit it.**

## Regenerate

```
python3 tools/grammar/gen_all.py
```

No arguments, no dependencies beyond the Python standard library. It rewrites
`CSharpAuthor/Syntax/Nodes.g.cs` in place and prints a summary to stderr. Then:

```
dotnet build CSharpAuthor/CSharpAuthor.csproj
dotnet test CSharpAuthor.Tests
```

## Coverage report

```
python3 tools/grammar/gen_all.py --report
```

Prints node coverage (V2-HANDOFF.md §9(a)) and writes nothing.

## Inputs

| File | What it is |
|---|---|
| `Syntax.xml` | Roslyn's grammar, copied verbatim from the Roslyn repository. Field order inside a `<Node>` **is** emit order - that is the whole trick. |
| `tokens.json` | `SyntaxKind` → text, harvested from `SyntaxFacts.GetText`. The XML names token kinds but never gives their spelling, so this supplies it. 217 kinds. |

To move to a newer C# version, replace `Syntax.xml` (and `tokens.json` if new kinds
appeared) and re-run. A new language version is a regeneration, not a rewrite.

## The rule that makes this work

**Generated code is never hand-edited** (V2-HANDOFF.md §8.3). If the output is wrong,
fix `gen_all.py` and regenerate. If a node needs behaviour the generator cannot express,
put it in a hand-written file beside the generated one - never inside it. The moment
`Nodes.g.cs` is edited by hand, regeneration stops being safe and the whole approach
fails.

Every fix so far has been expressible in the generator, and each one was expressed as a
**category** rule - keyed on a field's type, its position within the node, the node's base
chain, or a token kind. None is keyed on a node's name. That is deliberate: a rule keyed
on `IfStatementSyntax` says nothing about the node C# 15 adds, while "a semicolon that is
not the node's last token is a separator" still holds.

## The spacing policy

Sixteen rules. The grammar encodes token *order*; none of this is in `Syntax.xml` and none
of it ever will be. Rules 1-4, 6, 13-16 live in `SyntaxWriter.NeedsSpace` and its line
handling; 5, 7-12 are role and list-style assignments the generator makes, which the writer
then acts on.

| # | Rule |
|---|---|
| R1 | Two word-like tokens separate: `public static void M`, `int x`, `List<int> x`, `int[] x`, `int? x`. |
| R2 | Punctuation binds tight. `.` `::` `->` `?.` take no space either side; `,` `;` `)` `]` `>` take none before; `..` binds to an operand on both sides but a comma still holds it off. |
| R3 | `(` binds tight after an identifier, a closing bracket, or a function-like keyword (`typeof`, `nameof`, `sizeof`, `default`, `checked`, `unchecked`, `stackalloc`, `new`); it takes a space after any other keyword. `typeof(int)` against `if (x)`. |
| R4 | `[` binds tight after a name, a type keyword, a closing bracket or `new`; elsewhere it takes a space. `int[]`, `this[0]`, `new[] { 1 }` against `case [x]:`. |
| R5 | Angle brackets are always tight. They only ever appear as literal token fields in type and type-parameter lists - a comparison operator arrives as a caller-supplied operator instead, so the two can never be confused. |
| R6 | `?` is tight in a type node (`int?`), spaced in a ternary, tight in a null-conditional access (`a?.b`). The grammar tells the last two apart: only the ternary carries a matching colon in the same node. |
| R7 | Colons: spaced in base lists, constraint clauses, constructor initializers and ternaries; tight-before/space-after in `name:` colons and attribute targets; tight-before/newline-after in switch and statement labels. |
| R8 | A semicolon that is the node's last token ends the line. A mid-node semicolon ends the line *and* leaves a blank one when the node is a member or compilation unit (`namespace Acme;`), and is a plain separator otherwise (the two in `for (;;)`). |
| R9 | Braces are Allman. `{` in a statement, member or container node - or in any node whose braces enclose an unseparated list of nodes - opens a scope and breaks the line; `{` in an expression or pattern node stays inline with spaces. |
| R10 | A statement in a statement-typed slot on a statement or clause is an *embedded* statement: a block writes itself, anything else takes its own line at one extra indent. An `if` after `else` stays on the `else` line so a ladder does not march right. |
| R11 | Statement lists break between elements, or indent themselves when the containing node has no braces of its own. Member lists take a blank line between elements. Using and extern-alias lists break after each and leave a blank line after the block. Attribute lists break after each in member and statement position and stay inline elsewhere. Constraint clauses take one indented line each. |
| R12 | Separated lists join with `, ` - except enum members, which join with `,` and a line break. |
| R13 | Indentation is never counted in the writer. Every `{`/`}` goes through the context's `OpenScope`/`CloseScope`, and every line re-reads the context's `IndentString`. |
| R14 | A line break is requested, not written: "at least N breaks separate these two tokens". Requests collapse instead of stacking, a request with nothing after it is dropped, and the indent is written lazily by the first token on the line - so trailing whitespace and doubled blank lines are structurally impossible. |
| R15 | An identifier that collides with a reserved keyword is escaped (`@class`). Contextual keywords (`var`, `value`, `record`, `when`) are not - escaping those would be wrong. Type keywords and operator names are not identifiers and are never escaped. |
| R16 | A directive owns its line, takes no indentation, and binds `#` to its keyword. Everything inside an interpolated string abuts its neighbour, braces included. |

## What lives where

| File | Generated? | What it holds |
|---|---|---|
| `CSharpAuthor/Syntax/Nodes.g.cs` | **yes** | 250 node classes, 44 interfaces. Token order only. |
| `CSharpAuthor/Syntax/SyntaxWriter.cs` | no | The spacing, line-breaking and blank-line policy. The genuinely hand-written part. |
| `CSharpAuthor/Syntax/TokenRole.cs` | no | The vocabulary the generator and the writer share. |
| `CSharpAuthor/Syntax/TypeRef.cs` | no | The deferral point: a type slot holds an unrendered `ITypeDefinition` or a type node. |
| `CSharpAuthor/Syntax/SyntaxNode.cs` | no | The base class, so 250 copies of an empty `AddUsingNamespace` do not ship. |
| `CSharpAuthor/Syntax/Raw.cs` | no | The escape hatch, and the literal-quoting helpers. |

## Visibility

The generated types carry:

```csharp
#if CSHARPAUTHOR_PUBLIC_SYNTAX
public
#endif
sealed class ClassDeclaration : SyntaxNode, ITypeDeclaration
```

Only `CSharpAuthor.csproj` defines `CSHARPAUTHOR_PUBLIC_SYNTAX`. A binary reference to
`CSharpAuthor.dll` therefore gets a usable public grammar API, while every form of source
inclusion - the package's own `build/CSharpAuthor.targets`, or a consumer that simply
globs these `.cs` files - leaves them at C#'s default accessibility for a top-level type,
which is internal (V2-HANDOFF.md §3).

The polarity is the point. Defining the symbol only in the assembly build means a
source-including host that has never heard of it still gets the safe answer. The opposite
arrangement was tried first and leaked 250 types into DependencyModules' public API
snapshot, because the host wires the source in with its own `<Compile Include>` glob and
never sees the package targets.

## Language version

The emitted source must compile as **C# 10 or lower** on **netstandard2.0**, under
`EnforceExtendedAnalyzerRules=true`. `CSharpAuthor.csproj` pins `LangVersion 10`, and both
consumers source-compile this file into generator projects that pin 10 and 11.

So: no collection expressions, no primary constructors, no `required`, no raw string
literals, no `field` keyword; and nothing from `System.IO`, `System.Environment`, or a
culture-sensitive overload - RS1035 is an error in the consumer build and is invisible to
`dotnet test CSharpAuthor.Tests`. The generated file calls nothing but `SyntaxWriter`, so
it has no surface for that; the hand-written files use `CultureInfo.InvariantCulture`
explicitly.

Raising `LangVersion` to make generation compile would pass this repository's tests and
break the consumers silently. Do not.
