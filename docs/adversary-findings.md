# Adversary findings

Live tally for the CSharpAuthor 2.0 build. The adversary writes failing tests; it does not write
library code. Every entry here is a test in `CSharpAuthor.Tests/Adversary/`.

## Tally

| | count |
|---|---|
| **found** | **170** |
| **fixed** | **54** |
| **outstanding** | **116** |

Suite: **1122 passing, 0 failing, 116 skipped, 1238 total** (measured, not projected). The 139
pre-existing tests are unmodified and all pass. Of the adversary's own 305 cases, **189 pass and
116 are skipped gaps**.

**Reconciliation, wave 2.** The 54 findings closed by the wave-1 builders were still carrying their
`Skip` attributes, so the gate under-reported the work and the fixes had no regression guard. Every
one of those skips has now been removed and the test runs live. The method was mechanical, not a
judgement call: strip every skip on a throwaway branch, run the suite, and un-skip exactly the set
that passed. 170 skips stripped → 116 failures; those 116 keep their skips, the other 54 lost them.

### How to read a finding

Every outstanding finding is a test carrying `[Fact(Skip = "ADVERSARY GAP: …")]`. The test asserts
the **correct** behaviour, so when the defect is fixed the skip comes off and the test passes as
written. Nothing here is a test that agrees with a defect. A finding that has been **fixed** is the
same test with the skip removed — it is now a live regression guard, and the appendix names all 54.

Every remaining gap in this document has been **verified to fail today**. Running the suite with the
skip attributes stripped gives 116 failures — the same 116 that are still skipped. Reproduce with:

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
| `Adversary/IdentifierAdversaryTests.cs` | 18 | 2 |
| `Adversary/LiteralAdversaryTests.cs` | 14 | 4 |
| `Adversary/CultureAdversaryTests.cs` | 5 | 6 |
| `Adversary/ModifierAdversaryTests.cs` | 29 | 5 |
| `Adversary/AttributeAdversaryTests.cs` | 9 | 2 |
| `Adversary/PrecedenceAdversaryTests.cs` | 11 | 6 |
| `Adversary/TriviaAdversaryTests.cs` | 5 | 10 |
| `Adversary/ExpressionAdversaryTests.cs` | 12 | 12 |
| `Adversary/StatementAdversaryTests.cs` | 9 | 12 |
| `Adversary/PatternCoverageTests.cs` | 1 | 13 |
| `Adversary/MemberCoverageTests.cs` | 11 | 18 |
| `Adversary/ConstraintAdversaryTests.cs` | 12 | 3 |
| `Adversary/OutputContextAdversaryTests.cs` | 12 | 2 |
| `Adversary/TypeModelContractTests.cs` | 8 | 4 |
| `Adversary/StructureAdversaryTests.cs` | 8 | 4 |
| `Adversary/ValueConversionAdversaryTests.cs` | 6 | 6 |
| **total** | **189** | **116** |

---

## Appendix: the ledger, reconciled

Generated mechanically from a run of the suite, not by hand. `fixed` = the skip has been
removed and the test passes as written; `outstanding` = the skip is still on and the test still
fails without it.

### Fixed by wave 1, un-skipped in wave 2 (54)

**AttributeAdversaryTests.cs** — 2

- `AttributeInGlobalMode`
- `GenericAttribute`

**CultureAdversaryTests.cs** — 3

- `AWholeFileIsIdenticalAcrossCultures`
- `AddCodeRawArgument`
- `FieldInitializer`

**ExpressionAdversaryTests.cs** — 5

- `IsImportsItsTypeArgumentsNamespaces`
- `IsInGlobalMode`
- `IsWithAGenericType`
- `IsWithAGenericTypeThatHasANonGenericTwin`
- `IsWithAnArrayType`

**IdentifierAdversaryTests.cs** — 8

- `ClassNamedEvent`
- `EnumValueNamedDefault`
- `FieldNamedLock`
- `InterfaceNamedInterface`
- `MethodNamedIf`
- `NamespaceSegmentNamedNamespace`
- `ParameterNamedClass`
- `PropertyNamedString`

**LiteralAdversaryTests.cs** — 11

- `AddCodeStringArgumentContainingAQuote`
- `CharLiteral`
- `CharLiteralThatIsAQuote`
- `DecimalLiteral`
- `FloatLiteral`
- `NonFiniteDoubleLiterals`
- `StringArrayElementsContainingQuotes`
- `StringContainingANewline`
- `StringContainingAQuote`
- `StringContainingBackslashes`
- `StringContainingNul`

**ModifierAdversaryTests.cs** — 9

- `AbstractAndSealedOnAClass`
- `AbstractMethodHasNoBody`
- `AbstractPropertyHasNoBody`
- `PrivateProtectedOnAMethod`
- `ProtectedInternalOnAClass`
- `ReadonlyMethodOnAStruct`
- `ReadonlyStructIsActuallyReadonly`
- `SealedOverrideKeepsBoth`
- `StaticReadonlyFieldModifierOrder`

**OutputContextAdversaryTests.cs** — 4

- `AddCodeDefersItsTypes`
- `FilesOwnNamespaceIsNotImported`
- `GlobalModeEmitsNoUsings`
- `NewLineOptionIsHonouredEverywhere`

**StatementAdversaryTests.cs** — 1

- `ForLoopWritesItsBody`

**StructureAdversaryTests.cs** — 1

- `ClosingAnUnopenedScope`

**TypeModelContractTests.cs** — 2

- `ArrayAndElementTypesHashDifferently`
- `EquatableArrayExists`

**TypeNameAdversaryTests.cs** — 8

- `ArrayOfConstructedGeneric`
- `DeeplyNestedType_KeepsItsContainers`
- `GenericNestedInGeneric`
- `GenericNestedInGeneric_NamesATypeThatExists`
- `MakeArrayTwice`
- `MultiDimensionalArray`
- `NestedType_InGlobalMode`
- `NestedType_KeepsItsContainer`

### Outstanding (116)

**AttributeAdversaryTests.cs** — 2

- `AssemblyLevelAttribute`
- `AttributeTypeNamedAttribute`

**ConstraintAdversaryTests.cs** — 3

- `AllowsRefStruct`
- `BaseClassConstraintOrdering`
- `InterfaceConstraints`

**CultureAdversaryTests.cs** — 6

- `AttributeArgumentDoesNotSplitIntoTwo`
- `ConstructorArgument`
- `DecimalValue`
- `DoubleValue`
- `EnumMemberValue`
- `FloatValue`

**ExpressionAdversaryTests.cs** — 12

- `AsExpression`
- `CollectionExpressionsAndSpreads`
- `ConditionalExpression`
- `InterpolatedStrings`
- `Lambdas`
- `NameOf`
- `ObjectInitializerWithNamedMembers`
- `RangesAndIndices`
- `StackAlloc`
- `SwitchExpressions`
- `TuplesAndDeconstruction`
- `WithExpressions`

**IdentifierAdversaryTests.cs** — 2

- `TypeParameterNamedInt`
- `TypeReferenceNamedEvent`

**LiteralAdversaryTests.cs** — 4

- `NullValueBecomesTheNullLiteral`
- `RawStringLiteralFenceLength`
- `StringContainingAnEscapeCharacter`
- `StringValueIsQuotedConsistently`

**MemberCoverageTests.cs** — 18

- `ConstFields`
- `ConversionOperators`
- `Destructors`
- `EnumMemberLiteralForm`
- `ExtensionBlocksAndMembers`
- `ExternMembers`
- `FieldKeyword`
- `FileLocalTypes`
- `GenericDelegateConstraints`
- `GenericInterfaces`
- `InterfaceMemberKinds`
- `NewMemberHiding`
- `OperatorDeclarations`
- `RefReturns`
- `RequiredMembers`
- `StaticAbstractInterfaceMembers`
- `UnsafeMembers`
- `VolatileFields`

**ModifierAdversaryTests.cs** — 5

- `IndexerOnAPropertyNotNamedThis`
- `NoAccessibilityDoesNotLeaveAStraySpace`
- `NoAccessibilityOnAPropertyDoesNotLeaveAStraySpace`
- `PartialMethodKeepsItsModifier`
- `PublicAndInternalTogether`

**OutputContextAdversaryTests.cs** — 2

- `SameShortNameFromTwoNamespaces`
- `SystemUsingsSortFirst`

**PatternCoverageTests.cs** — 13

- `ConstantPattern`
- `DeclarationPattern`
- `DiscardPattern`
- `ListPattern`
- `NotNullPattern`
- `ParenthesisedPattern`
- `PatternCombinators`
- `PatternInACaseLabel`
- `PositionalPattern`
- `PropertyPattern`
- `RelationalPattern`
- `SlicePattern`
- `VarPattern`

**PrecedenceAdversaryTests.cs** — 6

- `AwaitThenMemberAccess`
- `CastThenIndex`
- `CastThenInvoke`
- `CastThenMemberAccess`
- `CastThenMemberAccess_Compiles`
- `IncrementOfAnExpressionIsRejectedOrParenthesised`

**StatementAdversaryTests.cs** — 12

- `CatchWhenFilterIsForwarded`
- `CheckedAndUnchecked`
- `ContinueStatement`
- `DoWhileStatement`
- `ForEachWithAnExplicitElementType`
- `GotoAndLabels`
- `LocalFunctions`
- `LockStatement`
- `SwitchCaseOnAStringValue`
- `ThrowExpressionAndRethrow`
- `UsingStatementAndDeclaration`
- `YieldBreak`

**StructureAdversaryTests.cs** — 4

- `AddBaseTypeTwiceKeepsTheArguments`
- `EnumMemberValueUsesCSharpLiteralForm`
- `GenericTypeWithNoArguments`
- `NoBlankLineBeforeTheFirstMember`

**TriviaAdversaryTests.cs** — 10

- `AutoGeneratedHeaderIsTheFirstLine`
- `CommentContainingMarkupCharacters`
- `ConditionalCompilationDirective`
- `EnableNullableRestoresRatherThanDisables`
- `FileLevelCommentIsWritten`
- `LineDirective`
- `NamespaceCommentIsWritten`
- `OrdinaryComment`
- `ParameterAndReturnCommentsAreEscaped`
- `RegionDirective`

**TypeModelContractTests.cs** — 4

- `CompareToAgreesWithEquals`
- `CompareToIsSymmetric`
- `SortingProducesAStableOrder`
- `ToStringOfAKeywordTypeHasNoLeadingDot`

**TypeNameAdversaryTests.cs** — 7

- `JaggedArrayOfMultiDimensional`
- `MultiDimensionalArrayOfJagged`
- `NullableElementArray_AcceptsNullElement`
- `NullableElementArray_OnGenericType`
- `NullableElementArray_OnTypeParameter`
- `NullableElementArray_WritesQuestionBeforeBrackets`
- `TheWholeShape`

**ValueConversionAdversaryTests.cs** — 6

- `EmptyArrayValue`
- `EnumValue`
- `EnumValueThroughAddCode`
- `StringAttributeArgument`
- `TwoDimensionalArrayValue`
- `UnmatchedPlaceholder`
