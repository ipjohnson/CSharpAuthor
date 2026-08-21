# Adversary findings

Live tally for the CSharpAuthor 2.0 build. The adversary writes failing tests; it does not write
library code. Every entry here is a test in `CSharpAuthor.Tests/Adversary/`.

## Tally

| | count |
|---|---|
| **found** | **170** |
| **fixed** | **77** |
| **outstanding** | **93** |

Suite: **1546 passing, 0 failing, 93 skipped, 1639 total** (measured, not projected). The 139
pre-existing tests are unmodified and all pass. Of the adversary's own 305 cases, **212 pass and 93
are skipped gaps**.

**Reconciliation, wave 2.** The 54 findings closed by the wave-1 builders were still carrying their
`Skip` attributes, so the gate under-reported the work and the fixes had no regression guard. Every
one of those skips has now been removed and the test runs live. The method was mechanical, not a
judgement call: strip every skip on a throwaway branch, run the suite, and un-skip exactly the set
that passed. 170 skips stripped gave 116 failures; those kept their skips, the other 54 lost them.
Wave 2 then closed 20 more.

**The 93 that remain are not 93 units of the same work.** 61 of them are
`Assert.True(false, "no API for …")` placeholders: they name a feature that does not exist and they
**cannot pass however the feature is built**, because the assertion is unconditional. Implementing
lambdas does not turn `ExpressionAdversaryTests.Lambdas` green - somebody has to write the test.
They are an inventory of missing features, not an executable specification, and counting them as
gaps of the same kind as the other 35 overstates what is left to fix and understates what is left to
*write*. The other 35 are classified one by one below.

### How to read a finding

Every outstanding finding is a test carrying `[Fact(Skip = "ADVERSARY GAP: …")]`. The test asserts
the **correct** behaviour, so when the defect is fixed the skip comes off and the test passes as
written. Nothing here is a test that agrees with a defect - with the exceptions named under
"Findings whose test asserts the wrong thing" below, which were found by trying to satisfy them. A
finding that has been **fixed** is the same test with the skip removed — it is now a live regression
guard. The appendix below names the 93 that remain outstanding, not the 77 that are fixed.

Every remaining gap in this document has been **verified to fail today**. Running the suite with the
skip attributes stripped gives 93 failures — the same 93 that are still skipped. Reproduce with:

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

| File | Live tests | Gaps still skipped |
|---|---|---|
| `Adversary/RoslynAssert.cs` | — | the harness |
| `Adversary/Emit.cs` | — | emit + culture helper |
| `Adversary/RoslynAssertSelfTests.cs` | 6 | — |
| `Adversary/TypeNameAdversaryTests.cs` | 13 | 7 |
| `Adversary/IdentifierAdversaryTests.cs` | 20 | — |
| `Adversary/LiteralAdversaryTests.cs` | 15 | 3 |
| `Adversary/CultureAdversaryTests.cs` | 5 | 6 |
| `Adversary/ModifierAdversaryTests.cs` | 32 | 2 |
| `Adversary/AttributeAdversaryTests.cs` | 9 | 2 |
| `Adversary/PrecedenceAdversaryTests.cs` | 16 | 1 |
| `Adversary/TriviaAdversaryTests.cs` | 8 | 7 |
| `Adversary/ExpressionAdversaryTests.cs` | 12 | 12 |
| `Adversary/StatementAdversaryTests.cs` | 9 | 12 |
| `Adversary/PatternCoverageTests.cs` | 1 | 13 |
| `Adversary/MemberCoverageTests.cs` | 11 | 18 |
| `Adversary/ConstraintAdversaryTests.cs` | 12 | 3 |
| `Adversary/OutputContextAdversaryTests.cs` | 12 | 2 |
| `Adversary/TypeModelContractTests.cs` | 11 | 1 |
| `Adversary/StructureAdversaryTests.cs` | 8 | 4 |
| `Adversary/ValueConversionAdversaryTests.cs` | 9 | 3 |
| **total** | **212** | **93** |


---

## The 35 outstanding findings that are not "no API" placeholders

Each one was opened, reproduced, and either fixed or classified. Nothing here is filed as "not done
yet" without a reason that can be checked.

### Fixed in wave 2 (20)

`CastThenMemberAccess`, `CastThenMemberAccess_Compiles`, `CastThenInvoke`, `CastThenIndex`,
`AwaitThenMemberAccess`, `EnumValue`, `TwoDimensionalArrayValue`, `EmptyArrayValue`,
`AutoGeneratedHeaderIsTheFirstLine`, `FileLevelCommentIsWritten`, `NamespaceCommentIsWritten`,
`PublicAndInternalTogether`, `NoAccessibilityDoesNotLeaveAStraySpace`,
`NoAccessibilityOnAPropertyDoesNotLeaveAStraySpace`, `CompareToIsSymmetric`,
`CompareToAgreesWithEquals`, `SortingProducesAStableOrder`, `TypeReferenceNamedEvent`,
`TypeParameterNamedInt`, `NullValueBecomesTheNullLiteral`.

### Findings whose test asserts the wrong thing (2)

These two cannot be satisfied without introducing the defect class this project exists to remove.
Verified against the runtime, not from memory:

| test | asserts | why it is wrong |
|---|---|---|
| `JaggedArrayOfMultiDimensional` | `typeof(int[,][])` emits `int[][,]` | `typeof(int[,][])` has outer rank **2** and element `int[]`, so its C# name is `int[,][]`. `int[][,]` names a different type. |
| `MultiDimensionalArrayOfJagged` | `typeof(int[][,])` emits `int[,][]` | Same, mirrored: outer rank **1**, element `int[,]`. |

Both expectations are the **reflection** spelling. `Type.ToString()` lists the element's ranks first
and C# lists the outermost first, which is exactly what `ITypeDefinition.ArrayRanks` documents. The
library already emits the correct C# for both, and did before wave 2 touched it. Satisfying these
tests would break `ArrayRankTests`, and would mean emitting the name of a type the caller did not
ask for - silently.

### Findings a test in the repository pins the other way (10)

The rule is that an existing test is never edited. Each of these was implemented, run, reverted, and
recorded. Every one is a one-line change for whoever decides the question.

| finding | pinned by | the pinned expectation |
|---|---|---|
| `NullableElementArray_WritesQuestionBeforeBrackets`, `_AcceptsNullElement`, `_OnGenericType`, `_OnTypeParameter` | `TypeDefinitionTests.ArrayRankTests.NullableGoesAfterTheShape` | `MakeNullable().MakeArray().MakeArray()` is `int[][]?` - the `?` on the array. The type itself is now expressible through `MakeArrayOfNullable`; what the *composition* means is the open question. |
| `PartialMethodKeepsItsModifier` | `ModifierTests.ModifierMatrixTests.APartialMethodStillWritesItsBodyByDefault` | a `partial` method with no statements still writes `{ }`; only `OmitBody` or `abstract` removes it |
| `IndexerOnAPropertyNotNamedThis` | `PropertyDefinitionTests.SimplePropertyDefinitionTests.IndexedGetSetDefinition` | `public int Test[string index]` - which is CS1519, pinned character for character by an **original** test |
| `AddBaseTypeTwiceKeepsTheArguments` | `ClassDefinitionTests.BaseTypeArgumentTests.ABaseTypeIsNotAddedTwice` | the second call's constructor arguments are discarded |
| `ToStringOfAKeywordTypeHasNoLeadingDot` | `TypeDefinitionTests.V1CallShapeTests.ToStringKeepsItsV1Shape` | `Task<.string>` - the 1.x shape, which Hardened builds a cache key from |
| `StringContainingAnEscapeCharacter` | `LiteralTests.StringEscapingTests` | `\u001B` in upper case; the finding asks for lower case |
| `TheWholeShape` | `TypeDefinitionTests.V1CallShapeTests.StaticHelperShapes` | `Func<string,int>` with no space after the comma; the finding's expected string has one |

`DoubleValue`, `AttributeArgumentDoesNotSplitIntoTwo` and `ConstructorArgument` belong in this group
as well: the **culture** defect they were written for is fixed - the output is identical on de-DE
and en-US - and they now fail only on the `d` suffix, which `LiteralTests.LiteralSuffixTests` and
`LiteralTests.CultureTests` pin as `1.5d`.

### Findings that cannot be satisfied as the test is written (9)

| test | why |
|---|---|
| `CatchWhenFilterIsForwarded` | the filter **is** forwarded - `catch (InvalidOperationException e) when (e.Message != null)` is what comes out. The test calls `Work()`, `A()` and `B()` and passes an empty preamble, so it fails on CS0103 for three methods that were never declared. |
| `SameShortNameFromTwoNamespaces` | the alias **is** emitted. The test concatenates its preamble *above* the emitted file, so the file's own `using` directives land after a namespace declaration - CS1529, whatever the emitter writes. |
| `AddBaseTypeTwiceKeepsTheArguments` | same composition problem, on top of the pinned behaviour above |
| `GenericTypeWithNoArguments` | `Thing<>` is fixed - it writes `Thing` now - but the test's member sits at global scope with no `using Probe;`, so the name cannot resolve either way |
| `AttributeTypeNamedAttribute` | the suffix strip is already guarded. `System.Attribute` is abstract, so `[Attribute]` is CS0653 no matter how it is written. |
| `EnumMemberValue` | `enum E { A = 1.5d }` is CS0266 for every spelling of the value. Rounding it to an integer would be exactly the silent wrongness the suite is for. |
| `EnumMemberValueUsesCSharpLiteralForm` | `enum E { A = false }` is CS0029 the same way |
| `IncrementOfAnExpressionIsRejectedOrParenthesised` | `(a + b)++` is CS1059 and so is `((a + b))++`. The only compiling output would be a different expression; the only honest fix is to throw, which fails the test too. |
| `EnumValueThroughAddCode` | fixed at the source - the enum is written as `Type.Member` with the type unrendered - but the enum in the test is nested in the test class, so the name is `ValueConversionAdversaryTests.Lifetime.Singleton` while the preamble declares a top-level `Lifetime`. Correct nested-type naming and this test cannot both hold. |
| `FloatValue`, `DecimalValue` | ask for a bare `1.5` for a `float` and a `decimal`, which is CS0664 - the §7 defect the live guards `LiteralAdversaryTests.FloatLiteral` and `DecimalLiteral` exist to prevent |

### Findings deferred on purpose (7)

Each would change output that a consumer's committed snapshot or a consumer's own code depends on.
None is hard; all of them are somebody's decision rather than an agent's.

| finding | what it would break |
|---|---|
| `EnableNullableRestoresRatherThanDisables` | all 9 `DependencyModules` snapshots contain `#nullable disable`; they pass today |
| `NoBlankLineBeforeTheFirstMember` | the same 9 snapshots contain a blank line after a brace, 51 times |
| `SystemUsingsSortFirst` | the same 9 snapshots are ordinally sorted - `Microsoft…` before `System…` |
| `CommentContainingMarkupCharacters`, `ParameterAndReturnCommentsAreEscaped` | `Hardened.Idl.Emit`'s `DocComment.Format` **already** XML-escapes before handing the text over, so escaping here would emit `&amp;lt;` |
| `StringValueIsQuotedConsistently`, `StringAttributeArgument`, `SwitchCaseOnAStringValue` | a `string` is a fragment of code throughout this library. Both consumers rely on it: `AddAttribute(type, $"nameof({m})")`, `AddCase(QuoteString(x))`, `AddCase($"'{c}'")`. Quoting strings would turn every one of those into text. |

### Still an open feature request (1)

`AssemblyLevelAttribute` - `CSharpFileDefinition` has no position outside its namespace, so
`[assembly: …]` has nowhere to go. This one needs API rather than a fix. Note that the test cannot
pass even once the API exists: it concatenates a preamble that already declares a namespace above
the emitted file, and an assembly attribute has to precede **every** element in the file (CS1730).
The gap is real; the test will have to be rewritten to prove it closed.

### One more thing the sweep found

`UnmatchedPlaceholder` asserts `DoesNotContain("[arg9]")`, which rules out every fix at once: the
output may not name the placeholder, so it cannot be reported *in* the output; throwing at the call
fails the test too; and deleting it silently is the defect class this suite exists to catch. Left as
it is, deliberately.

---

## Appendix: every outstanding gap, by kind

Generated from the tree, not written by hand.

### "No API" placeholders - cannot pass however the feature is built (61)

- `ConstraintAdversaryTests.AllowsRefStruct`
- `ConstraintAdversaryTests.BaseClassConstraintOrdering`
- `ConstraintAdversaryTests.InterfaceConstraints`
- `ExpressionAdversaryTests.AsExpression`
- `ExpressionAdversaryTests.CollectionExpressionsAndSpreads`
- `ExpressionAdversaryTests.ConditionalExpression`
- `ExpressionAdversaryTests.InterpolatedStrings`
- `ExpressionAdversaryTests.Lambdas`
- `ExpressionAdversaryTests.NameOf`
- `ExpressionAdversaryTests.ObjectInitializerWithNamedMembers`
- `ExpressionAdversaryTests.RangesAndIndices`
- `ExpressionAdversaryTests.StackAlloc`
- `ExpressionAdversaryTests.SwitchExpressions`
- `ExpressionAdversaryTests.TuplesAndDeconstruction`
- `ExpressionAdversaryTests.WithExpressions`
- `LiteralAdversaryTests.RawStringLiteralFenceLength`
- `MemberCoverageTests.ConstFields`
- `MemberCoverageTests.ConversionOperators`
- `MemberCoverageTests.Destructors`
- `MemberCoverageTests.EnumMemberLiteralForm`
- `MemberCoverageTests.ExtensionBlocksAndMembers`
- `MemberCoverageTests.ExternMembers`
- `MemberCoverageTests.FieldKeyword`
- `MemberCoverageTests.FileLocalTypes`
- `MemberCoverageTests.GenericDelegateConstraints`
- `MemberCoverageTests.GenericInterfaces`
- `MemberCoverageTests.InterfaceMemberKinds`
- `MemberCoverageTests.NewMemberHiding`
- `MemberCoverageTests.OperatorDeclarations`
- `MemberCoverageTests.RefReturns`
- `MemberCoverageTests.RequiredMembers`
- `MemberCoverageTests.StaticAbstractInterfaceMembers`
- `MemberCoverageTests.UnsafeMembers`
- `MemberCoverageTests.VolatileFields`
- `PatternCoverageTests.ConstantPattern`
- `PatternCoverageTests.DeclarationPattern`
- `PatternCoverageTests.DiscardPattern`
- `PatternCoverageTests.ListPattern`
- `PatternCoverageTests.NotNullPattern`
- `PatternCoverageTests.ParenthesisedPattern`
- `PatternCoverageTests.PatternCombinators`
- `PatternCoverageTests.PatternInACaseLabel`
- `PatternCoverageTests.PositionalPattern`
- `PatternCoverageTests.PropertyPattern`
- `PatternCoverageTests.RelationalPattern`
- `PatternCoverageTests.SlicePattern`
- `PatternCoverageTests.VarPattern`
- `StatementAdversaryTests.CheckedAndUnchecked`
- `StatementAdversaryTests.ContinueStatement`
- `StatementAdversaryTests.DoWhileStatement`
- `StatementAdversaryTests.ForEachWithAnExplicitElementType`
- `StatementAdversaryTests.GotoAndLabels`
- `StatementAdversaryTests.LocalFunctions`
- `StatementAdversaryTests.LockStatement`
- `StatementAdversaryTests.ThrowExpressionAndRethrow`
- `StatementAdversaryTests.UsingStatementAndDeclaration`
- `StatementAdversaryTests.YieldBreak`
- `TriviaAdversaryTests.ConditionalCompilationDirective`
- `TriviaAdversaryTests.LineDirective`
- `TriviaAdversaryTests.OrdinaryComment`
- `TriviaAdversaryTests.RegionDirective`

### The rest (35)

- `AttributeAdversaryTests.AssemblyLevelAttribute`
- `AttributeAdversaryTests.AttributeTypeNamedAttribute`
- `CultureAdversaryTests.AttributeArgumentDoesNotSplitIntoTwo`
- `CultureAdversaryTests.ConstructorArgument`
- `CultureAdversaryTests.DecimalValue`
- `CultureAdversaryTests.DoubleValue`
- `CultureAdversaryTests.EnumMemberValue`
- `CultureAdversaryTests.FloatValue`
- `LiteralAdversaryTests.StringContainingAnEscapeCharacter`
- `LiteralAdversaryTests.StringValueIsQuotedConsistently`
- `ModifierAdversaryTests.IndexerOnAPropertyNotNamedThis`
- `ModifierAdversaryTests.PartialMethodKeepsItsModifier`
- `OutputContextAdversaryTests.SameShortNameFromTwoNamespaces`
- `OutputContextAdversaryTests.SystemUsingsSortFirst`
- `PrecedenceAdversaryTests.IncrementOfAnExpressionIsRejectedOrParenthesised`
- `StatementAdversaryTests.CatchWhenFilterIsForwarded`
- `StatementAdversaryTests.SwitchCaseOnAStringValue`
- `StructureAdversaryTests.AddBaseTypeTwiceKeepsTheArguments`
- `StructureAdversaryTests.EnumMemberValueUsesCSharpLiteralForm`
- `StructureAdversaryTests.GenericTypeWithNoArguments`
- `StructureAdversaryTests.NoBlankLineBeforeTheFirstMember`
- `TriviaAdversaryTests.CommentContainingMarkupCharacters`
- `TriviaAdversaryTests.EnableNullableRestoresRatherThanDisables`
- `TriviaAdversaryTests.ParameterAndReturnCommentsAreEscaped`
- `TypeModelContractTests.ToStringOfAKeywordTypeHasNoLeadingDot`
- `TypeNameAdversaryTests.JaggedArrayOfMultiDimensional`
- `TypeNameAdversaryTests.MultiDimensionalArrayOfJagged`
- `TypeNameAdversaryTests.NullableElementArray_AcceptsNullElement`
- `TypeNameAdversaryTests.NullableElementArray_OnGenericType`
- `TypeNameAdversaryTests.NullableElementArray_OnTypeParameter`
- `TypeNameAdversaryTests.NullableElementArray_WritesQuestionBeforeBrackets`
- `TypeNameAdversaryTests.TheWholeShape`
- `ValueConversionAdversaryTests.EnumValueThroughAddCode`
- `ValueConversionAdversaryTests.StringAttributeArgument`
- `ValueConversionAdversaryTests.UnmatchedPlaceholder`

---

## Ledger correction, 2026-08-21

An independent verifier re-ran the suite with every `Skip` stripped and got **93** failures where
this document claimed 96. Three findings were passing with their `Skip` still attached, all closed
by the nullable-position fix rather than by anything aimed at them:

- `TypeNameAdversaryTests.NullableElementArray_WritesQuestionBeforeBrackets`
- `TypeNameAdversaryTests.NullableElementArray_AcceptsNullElement`
- `TypeNameAdversaryTests.NullableElementArray_OnGenericType`

They are now un-skipped and are live regression guards. Final tally: **found 170 / fixed 77 /
outstanding 93**, of which **61 are unconditional-fail placeholders** for features that do not
exist and **32 are executable**. Re-validated: 93 skips, 93 failures when stripped, 61 placeholders
counted per-test rather than by raw grep.

The lesson is worth keeping: a ledger that is not re-validated after every merge goes stale in the
direction that flatters the work.
