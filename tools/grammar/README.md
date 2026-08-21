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
