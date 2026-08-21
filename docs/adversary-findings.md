# Adversary findings

Live tally for the CSharpAuthor 2.0 build. The adversary writes failing tests; it does not write
library code. Every entry here is a test in `CSharpAuthor.Tests/Adversary/`.

## Tally

| | count |
|---|---|
| **found** | **170** |
| **fixed** | **0** |
| **outstanding** | **170** |

Suite: **274 passing, 0 failing, 170 skipped, 444 total.** The 139 pre-existing tests are
unmodified and all pass. 135 of the passing tests are new adversary regression guards.

### How to read a finding

Every finding is a test carrying `[Fact(Skip = "ADVERSARY GAP: …")]`. The test asserts the
**correct** behaviour, so when the defect is fixed the skip comes off and the test passes as
written. Nothing here is a test that agrees with a defect.

Every gap in this document has been **verified to fail today**. Running the suite with the skip
attributes stripped gives 170 failures and 135 passes — the same 170. Reproduce with:

```
perl -0pi -e 's/\[(Fact|Theory)\(Skip\s*=\s*"(?:[^"\\]|\\.)*"\)\]/[$1]/g' CSharpAuthor.Tests/Adversary/*.cs
dotnet test CSharpAuthor.Tests --filter "FullyQualifiedName~Adversary"
```

### Method

The assertion is **"the emitted string compiles"**, not "the emitted string equals". A string
assertion can be satisfied by agreeing with a defect; a compile assertion cannot.
`Adversary/RoslynAssert.cs` parses and compiles emitted output against the running framework's
reference assemblies and fails with the compiler's own diagnostics. `RoslynAssertSelfTests` proves
the instrument works before anything is measured with it.

Where both readings compile — `string[]?` versus `string?[]`, `private protected` versus
`protected` — a compile assertion cannot separate them. Two techniques cover most of that:
promoting a specific warning to an error (`CS8625` turns the nullable-array question into a compile
question), and asserting the compiler *rejects* something (a `readonly struct` with a mutable field
is `CS8340`, so the error appearing proves the modifier reached the output). Where neither works,
the test asserts a string and says so.

`Microsoft.CodeAnalysis.CSharp` 4.14.0 is referenced by the **test project only** —
`CSharpAuthor.csproj` takes no new dependency (handoff §3). 4.14 knows C# up to 13, so nothing newer
can be validated here, only asserted (handoff §4).

### Breakdown

| kind | count |
|---|---|
| New defect in an existing feature — not on the §7 list | **89** |
| Confirms a §7 defect, stated as a compile question | 21 |
| Feature does not exist (no API) | 60 |

---

## Ranked: wrong code that compiles

The worst class. No diagnostic anywhere, at generation or at consumption. The generated program is
simply not the one that was asked for.

| # | Category | Repro | Emits | Should emit |
|---|---|---|---|---|
| 1 | Type names | `TypeDefinition.Get(typeof(string)).MakeNullable().MakeArray()` | `string[]?` | `string?[]` |
| 2 | Precedence | `Property(StaticCast(Dog, "animal"), "Breed")` | `(Dog)animal.Breed` | `((Dog)animal).Breed` |
| 3 | Expressions | `Is(x, IEnumerable<int>)` | `x is IEnumerable` | `x is IEnumerable<int>` |
| 4 | Culture | `AddAttribute(MeasureAttribute, 1.5d)` on de-DE | `[Measure(1,5)]` — two arguments | `[Measure(1.5)]` |
| 5 | Literals | `QuoteString(@"C:\temp\new")` | `"C:\temp\new"` — a tab and a newline | `"C:\\temp\\new"` |
| 6 | Modifiers | `Modifiers = Private \| Protected` | `protected` — **widens access** (§7) | `private protected` |
| 7 | Modifiers | `AddProperty(int, "P")` with `Abstract` | `public int P { get; set; }` | `public abstract int P { get; set; }` |
| 8 | Type names | `TypeDefinition.Get(typeof(OuterG<int>.InnerG<string>))` | `InnerG<int,string>` | `OuterG<int>.InnerG<string>` |
| 9 | Attributes | `AddAttribute(ValidateAttribute<int>)` | `[Validate]` | `[Validate<int>]` |
| 10 | Precedence | `Property(Await(GetAsync()), "Length")` | `await GetAsync().Length` | `(await GetAsync()).Length` |
| 11 | Modifiers | `Modifiers = Protected \| Internal` | `internal` (§7) | `protected internal` |
| 12 | Modifiers | `Modifiers = Sealed \| Override` on a method | `override` — leaves the member overridable (§7) | `sealed override` |
| 13 | Modifiers | `TypeKeyword = Struct, Modifiers = Readonly` | `struct` — immutability gone (§7) | `readonly struct` |
| 14 | Trivia | `classDefinition.EnableNullable()` | closes `#nullable disable` — off for the rest of the file | `#nullable restore` |
| 15 | Output context | `Options.NewLine = "\r\n"` | mixed CRLF/LF; on Windows the `"\n"` default is silently CRLF | the configured newline everywhere |
| 16 | Expressions | `Is(x, Foo)` in `TypeOutputMode.Global` | `x is Foo` | `x is global::Ns.Foo` |
| 17 | Attributes | `AddAttribute(MyAttribute)` in `Global` mode | `[My]` | `[global::Probe.My]` |
| 18 | Type model | `plain.CompareTo(generic)` is `0`, reverse is `-1` | asymmetric `IComparable`; sorts are undefined | symmetric |
| 19 | Type model | `int.GetHashCode() == int[].GetHashCode()` | guaranteed collision — `ToString()` omits `IsArray` | distinct hashes |
| 20 | Output context | `AddCode("new {arg1}()", type)` in `Global` mode | `new Thing()` + a stray `using` (§7) | `new global::Ns.Thing()` |
| 21 | Output context | a type in the file's own namespace | `using Probe;` above `namespace Probe` | no import |
| 22 | Value conversion | `CodeOutputComponent.Get(Lifetime.Singleton)` | `Singleton` — no type, no namespace | `Lifetime.Singleton` |
| 23 | Value conversion | `CodeOutputComponent.Get(new[,]{{1,2},{3,4}})` | `new int[] { 1, 2, 3, 4 }` — flattened | `new int[,] { { 1, 2 }, { 3, 4 } }` |
| 24 | Trivia | `file.AddLeadingTrait("// <auto-generated/>")` | the marker lands **below** the usings | first line of the file |
| 25 | Trivia | `new CSharpFileDefinition(ns) { Comment = … }` | nothing — silently dropped | the comment |
| 26 | Trivia | `new NamespaceDefinition(ns) { Comment = … }` | nothing — silently dropped | the comment |

`#22` is the §1 defect at its source. The handoff traces a stray
`using Microsoft.Extensions.DependencyInjection` in a Global-mode file to
`CodeOutputComponent.Get("ServiceLifetime.Transient")` — a raw string tracking no namespace. The
caller does not have to reach for a string to get there: handing over the enum value itself produces
the same bare member name. The root is the last line of `DefaultComponent`, `value.ToString()`, which
answers "how does this look to a person" when the question is "what is the C# for this".

`#24` matters more than it looks. The `<auto-generated/>` marker has to be line one — analyzers,
StyleCop and the IDE all read line one to decide whether to skip a file — and
`GenerateUsingStatements` inserts the usings at index 0 *after* everything else has been written, so
anything attached as a leading trait is pushed below them.

`#15` is the one that reproduces only on the platform nobody generating this ran it on:
`WriteLine(text)` and `WriteIndentedLine` call `StringBuilder.AppendLine`, which appends
`Environment.NewLine` and ignores `Options.NewLine`. On Linux and macOS the two agree, so it is
invisible.

---

## Ranked: broken code the compiler rejects

Loud, but at the consumer's build rather than at generation, and often far from the generator that
caused it.

| # | Category | Repro | Emits | Error |
|---|---|---|---|---|
| 22 | Literals | `CodeOutputComponent.Get(double.PositiveInfinity)` | `= ∞` | CS1056 |
| 23 | Literals | `CodeOutputComponent.Get(double.NaN)` | `= NaN` | CS0103 |
| 24 | Literals | `CodeOutputComponent.Get(1.5m)` on a `decimal` field | `= 1.5` | CS0664 |
| 25 | Literals | `CodeOutputComponent.Get(1.5f)` on a `float` field (§7) | `= 1.5` | CS0664 |
| 26 | Literals | `CodeOutputComponent.Get('a')` (§7) | `= a` | CS0103 |
| 27 | Literals | `CodeOutputComponent.Get(null)` as an initializer | `= ;` | CS1525 |
| 28 | Literals | `QuoteString("he said \"hi\"")` (§7) | `"he said "hi""` | CS1002 |
| 29 | Literals | `QuoteString("a\nb")` | a literal newline in the literal | CS1010 |
| 30 | Literals | `QuoteString("a\0b")` | a raw U+0000 in the source | — (invisible) |
| 31 | Statements | `switch.AddCase("abc")` | `case abc:` | CS0103 |
| 32 | Structure | `AddBaseType(Pet)` then `AddBaseType(Pet, Id)` | `: Pet;` — arguments dropped | CS7036 |
| 33 | Structure | `EnumDefinition.AddValue("A", false)` | `A = False` | CS0103 |
| 34 | Type names | `TypeDefinition.Get(typeof(List<int>[]))` | ``List`1[][]`` | CS1002 |
| 35 | Type names | `TypeDefinition.Get(typeof(int[,]))` (§7) | `Int32[,][]` | CS0246 |
| 36 | Type names | `TypeDefinition.Get(typeof(int[,][]))` | `Int32[][,][]` | CS0246 |
| 37 | Type names | `TypeDefinition.Get(typeof(int[][,]))` | `Int32[,][][]` | CS0246 |
| 38 | Type names | `.MakeArray().MakeArray()` (§7) | `int[]` — a rank lost | — (silent) |
| 39 | Type names | `TypeDefinition.Get(typeof(Outer.Inner))` (§7) | `Inner` | CS0246 |
| 40 | Identifiers | a class named `event` | `public class event` | CS1001 |
| 41 | Identifiers | a field named `lock` | `private int lock;` | CS1001 |
| 42 | Identifiers | a property named `string` | `public int string { get; set; }` | CS1001 |
| 43 | Identifiers | a method named `if` | `public void if()` | CS1001 |
| 44 | Identifiers | an enum member named `default` | `default,` | CS1001 |
| 45 | Identifiers | an interface named `interface` | `public interface interface` | CS1001 |
| 46 | Identifiers | a type parameter named `int` | `class Box<int>` | CS1001 |
| 47 | Identifiers | namespace `My.namespace.Thing` | written whole, unescaped | CS1001 |
| 48 | Identifiers | a parameter named `class` (§7) | `void M(string class)` | CS1001 |
| 49 | Modifiers | `Abstract` on a method (§7) | `abstract void M() { }` | CS0500 |
| 50 | Modifiers | `Partial` on a method (§7) | dropped — two declarations collide | CS0111 |
| 51 | Modifiers | `IndexType` on a property not named `this` | `public string Item[int index]` | CS1519 |
| 52 | Attributes | an attribute type named exactly `Attribute` | `[]` | CS1001 |
| 53 | Attributes | `[assembly:]` via `file.AddComponent` | written inside the namespace | CS1730 |
| 54 | Output context | two `Thing` types from two namespaces (§7) | both imported, both short | CS0104 |
| 55 | Statements | `Catch(type, name, when)` (§7) | the filter is dropped | CS0160 |
| 56 | Statements | `new ForDefinition()` with a body (§7) | nothing at all | — (silent) |
| 57 | Precedence | `Increment(Add(a, b))` | `(a + b)++` | CS1059 |
| 58 | Structure | `GenericTypeDefinition` with no type arguments | `Thing<>` | CS1031 |
| 59 | Culture | any non-integer number on de-DE (§7 + 7 further sites) | `1,5` | CS1002 |
| 60 | Trivia | `Comment = "Use List<int> when a & b"` | malformed XML | CS1570 |
| 61 | Trivia | `<param>` / `<returns>` text with `<` or `&` | malformed XML | CS1570 |
| 62 | Value conversion | `AddCode("var x = {arg1};", Lifetime.Singleton)` | `var x = Singleton;` | CS0103 |
| 63 | Value conversion | `CodeOutputComponent.Get(new int[0])` | `new int[]` | CS1586 |
| 64 | Value conversion | `AddAttribute(type, "hello")` | `[My(hello)]` | CS0103 |
| 65 | Value conversion | `AddCode("X([arg1],[arg9]);", 1, 99)` | `[arg9]` reaches the file as text | CS1002 |

`#30` deserves a note: a NUL in a string literal is legal C#, so nothing rejects it. It goes into
the file as a raw zero byte, which no editor and no diff will show.

---

## Cosmetic: compiles, and appears in every snapshot

These matter because §6 gate 6 counts snapshot diffs, and because a consuming repository's formatter
will rewrite them on the first save.

| # | Category | Emits | Should emit |
|---|---|---|---|
| 66 | Modifiers | `     int f;` — `NoAccessibility` leaves a stray space | `    int f;` |
| 67 | Modifiers | `public readonly static int f` | `public static readonly int f` |
| 68 | Structure | a blank line before the **first** member of every type | none |
| 69 | Output context | `using Aaa; using System.Text; using Zzz;` | `System` namespaces first |
| 70 | Type model | `TypeDefinition.Get(typeof(int)).ToString()` is `.int` | `int` |
| 71 | Structure | `CloseScope()` past zero throws `ArgumentOutOfRangeException: count '-4'` | a diagnosis of the unbalanced scope |

---

## Feature does not exist (60)

Not defects. Nothing emits the wrong thing because nothing emits anything. Each is a skipped test
naming the API that would be needed, so the shape of the work can be read off the file rather than
off this table.

**Patterns — 13.** `PatternCoverageTests`. This is the 0% in §1, itemised: declaration, constant,
relational, `and`/`or`/`not`, `is not null`, property (with nested designations), positional, list,
slice, `var`, discard, parenthesised, and patterns in a `case` label with a guard. The library has
exactly one thing in this area — `Is(component, type)`, a bare type pattern with no designation, and
it is wrong for a generic or an array (`#3`). `is not null` is the most common pattern in generated
code and there is no route to it.

**Members and modifiers — 18.** `MemberCoverageTests`. Operators, conversion operators, destructors,
`const`, `required`, the `field` keyword, extension blocks and extension members, `volatile`,
`unsafe` (and pointer types), `extern`, member-hiding `new`, `file` accessibility, static
abstract/virtual interface members, `ref` returns. Also: `InterfaceDefinition` holds only methods and
properties — no events, indexers, nested types **or type parameters**, so `interface IRepo<T>` cannot
be declared at all.

**Expressions — 12.** `ExpressionAdversaryTests`. Lambdas (all four forms), switch expressions,
interpolated strings (including alignment and format clauses), tuple types and literals and
deconstruction, `with`, ranges and from-end indices, collection expressions and spreads, `stackalloc`,
`nameof`, the conditional operator, `as`, and named-member object initializers.

**Statements — 8.** `StatementAdversaryTests`. `do`/`while`, `using` statement and declaration,
`lock`, `goto` and labels (so no labelled `break`/`continue`), `yield break`, local functions,
`checked`/`unchecked`, throw expressions and bare rethrow.

**Directives and comments — 4.** `TriviaAdversaryTests`. `#region`, `#if`/`#else`/`#endif`, `#line`,
and ordinary `//` comments — `Comment` is always written as a `///` documentation comment.

**Constraints — 3.** `ConstraintAdversaryTests`. `allows ref struct` (C# 13); a base-class constraint
that can be distinguished from an interface constraint, so the ordering rule C# requires can be
enforced rather than documented; constraints on an interface's type parameters, which do not exist.

**Literals — 1.** Raw string literals, including the fence-length rule (content ending in a quote
needs a longer fence, else CS8998).

**Type model — 1.** `EquatableArray<T>`, which §7 requires.

---

## Two notes for the builders

**§5's constraint example is not legal C#.** The brief asks for
`where T : struct, IComparable<T>, new()`. `struct` already guarantees a parameterless constructor,
so combining it with `new()` is CS0451. `ConstraintDefinition` throws rather than emit it, which is
correct. `ConstraintAdversaryTests.StructAndNewIsRejected` pins that down, unskipped, so a later
change cannot quietly start emitting it.

**An indexer is declared by naming the property `this`.** Three existing tests rely on it, and
`ModifierAdversaryTests.IndexerNamedThisMustNotBeEscaped` guards it, unskipped. Whoever fixes the
keyword-identifier escaping (`#40`–`#48`) will turn `this` into `@this` and break every indexer in
the library unless the escaper knows about this one name. The guard is deliberately placed where
that work will trip over it.

---

## Files

| File | Guards | Gaps |
|---|---|---|
| `Adversary/RoslynAssert.cs` | — | the harness |
| `Adversary/Emit.cs` | — | emit + culture helper |
| `Adversary/RoslynAssertSelfTests.cs` | 6 | — |
| `Adversary/TypeNameAdversaryTests.cs` | 5 | 15 |
| `Adversary/IdentifierAdversaryTests.cs` | 10 | 10 |
| `Adversary/LiteralAdversaryTests.cs` | 3 | 15 |
| `Adversary/CultureAdversaryTests.cs` | 2 | 9 |
| `Adversary/ModifierAdversaryTests.cs` | 20 | 14 |
| `Adversary/AttributeAdversaryTests.cs` | 7 | 4 |
| `Adversary/PrecedenceAdversaryTests.cs` | 11 | 6 |
| `Adversary/TriviaAdversaryTests.cs` | 5 | 10 |
| `Adversary/ExpressionAdversaryTests.cs` | 7 | 17 |
| `Adversary/StatementAdversaryTests.cs` | 8 | 13 |
| `Adversary/PatternCoverageTests.cs` | 1 | 13 |
| `Adversary/MemberCoverageTests.cs` | 11 | 18 |
| `Adversary/ConstraintAdversaryTests.cs` | 12 | 3 |
| `Adversary/OutputContextAdversaryTests.cs` | 8 | 6 |
| `Adversary/TypeModelContractTests.cs` | 6 | 6 |
| `Adversary/StructureAdversaryTests.cs` | 7 | 5 |
| `Adversary/ValueConversionAdversaryTests.cs` | 6 | 6 |
| **total** | **135** | **170** |
