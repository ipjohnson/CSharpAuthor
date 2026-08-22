# API gaps

Constructs CSharpAuthor's hand-written facade cannot express, as of 2.0.0-preview1002.


## Why this file exists

These gaps used to live in the test suite as 93 `[Fact(Skip = "ADVERSARY GAP: ...")]` placeholders
whose bodies were `Assert.True(false, ...)`. That shape cannot work: un-skipping one always fails,
whether or not the gap is still real, so nothing ever forced a placeholder to be revisited when its
feature shipped. By the time they were audited, 21 of them described features that existed —
`#region`, `#if`, `#line`, `const`, `new`, `file`, the `field` keyword, `Continue()`, `nameof`,
`required` and `static abstract` among them — and the suite still reported `1559 passed / 93
skipped` as though the 93 were a measurement.

The placeholders are gone. What they were pointing at is here, where it can be read without
running anything, and where being out of date is visible rather than silent.

## Scope

A gap here means **the hand-written facade has no entry point**. It does not necessarily mean the
construct cannot be emitted:

- the generated grammar tier under `CSharpAuthor/Syntax/` covers a great deal more, and
- anything at all can be written as text through `AddCode` / `AddIndentedStatement`, with the usual
  cost — no type tracking, so no derived `using` and no `TypeOutputMode` participation.

So each entry below is an ergonomics and type-safety gap, not an expressiveness one.


## Inventory (50)


### Expressions

| Construct | What is missing |
|---|---|
| `As Expression` | There is no 'as' emitter, nor a safe-cast pairing with 'is' |
| `Collection Expressions And Spreads` | There is no collection expression emitter, so [1, 2, ..rest] cannot be built. NewArrayStatement writes the new T[] { } form only. |
| `Conditional Expression` | There is no conditional (?:) emitter; LogicStatement takes one infix operator and a ternary needs two |
| `Interpolated Strings` | There is no interpolated string emitter, so $"{a}" cannot be built, and neither can the alignment/format clauses ({a,-10:N2}) or the escaping a nested quote needs |
| `Lambdas` | There is no lambda emitter. None of the four forms (x => e, (x) => e, (T x) => e, delegate { }) can be built, so any generator emitting a LINQ query, an event handler or a factory has to hand AddCode a string. |
| `Object Initializer With Named Members` | There is no object or collection initializer emitter that names members. NewStatement.AddInitValue writes bare values into { }, so 'new Foo { Bar = 1 }' can only be produced by passing the whole assignment as a preformatted string. |
| `Ranges And Indices` | There is no range or index emitter. IndexStatement writes x[i]; x[1..^1] and x[^1] have no component. |
| `Stack Alloc` | There is no stackalloc emitter |
| `Switch Expressions` | There is no switch expression emitter. SwitchBlockDefinition writes the statement form only, so 'x switch { ... }' cannot be produced. |
| `Tuples And Deconstruction` | A tuple type cannot be expressed. ValueTuple<int,string> can, but (int Count, string Name) cannot, so the element names are unreachable; nor is there a tuple literal or a deconstruction. |
| `With Expressions` | There is no 'with' expression emitter, so a record cannot be copied with a change, which is the reason records were used in the first place |

### Generic constraints

| Construct | What is missing |
|---|---|
| `Allows Ref Struct` | 'allows ref struct' (C# 13) has no method on ConstraintDefinition, so a generic that accepts a ref struct argument cannot be declared |
| `Base Class Constraint Ordering` | A base-class constraint cannot be distinguished from an interface constraint. Implements() takes both, and C# requires the base class first, so the ordering rule the class documents as 'the caller's to keep' is unenforceable and produces CS0406 when a caller adds them in the order a symbol reports them. |
| `Interface Constraints` | An interface cannot be generic at all, so an interface's type parameters cannot be constrained either |

### Literals

| Construct | What is missing |
|---|---|
| `Raw String Literal Fence Length` | There is no raw-string literal emitter, so the fence-length rule (content ending in a quote needs a longer fence, else CS8998) has nowhere to live yet |

### Member declarations

| Construct | What is missing |
|---|---|
| `Conversion Operators` | Conversion operators. 'public static implicit operator int(Money m)' and the explicit form have no component. |
| `Destructors` | Destructors/finalizers. '~Host()' has no component; a ConstructorDefinition named ~Host would still write an access modifier, which a destructor may not have. |
| `Enum Member Literal Form` | An enum member cannot be given a negative or hex value in a controlled way; EnumValueDefinition writes Value.ToString(), so the literal form is whatever the CLR chose |
| `Extension Blocks And Members` | Extension blocks and extension members. An extension method is reachable via ParameterDefinition.This; the C# 14 'extension(T x) { }' block, and extension properties and indexers inside it, are not. |
| `Extern Members` | 'extern' has no ComponentModifier flag, so a DllImport declaration cannot be emitted |
| `Generic Delegate Constraints` | A delegate cannot be generic. DelegateDefinition inherits MethodDefinition's generic parameters but a caller cannot reach constraints on them in a delegate position. |
| `Generic Interfaces` | An interface cannot be generic. InterfaceDefinition has no AddGenericParameter and no constraint list, so 'interface IRepo<T> where T : class' cannot be declared. |
| `Interface Member Kinds` | An interface cannot declare an event, an indexer, a nested type, or a generic parameter. InterfaceDefinition holds only methods and properties. |
| `Operator Declarations` | Operator declarations. 'public static Money operator +(Money a, Money b)' cannot be written; MethodDefinition writes a name where the operator keyword and symbol go. |
| `Ref Returns` | Ref returns and ref locals. MethodDefinition writes the return type with no modifier position, so 'ref int M()' and 'ref readonly int M()' cannot be declared. |
| `Unsafe Members` | 'unsafe' has no ComponentModifier flag, so neither an unsafe member nor a pointer type can be declared |
| `Volatile Fields` | 'volatile' has no ComponentModifier flag |

### Patterns

| Construct | What is missing |
|---|---|
| `Constant Pattern` | Constant pattern. 'x is 0', 'x is null', 'x is "a"' have no component; the null test has to be written as an equality expression instead. |
| `Declaration Pattern` | Declaration pattern. 'x is Dog d' cannot be written: Is takes no designation, so the tested value cannot be captured and every use has to cast a second time. |
| `Discard Pattern` | Discard pattern. 'x is _' has no component, and neither does the discard arm of a switch expression. |
| `List Pattern` | List pattern. 'x is [1, 2, ..]' has no component. |
| `Not Null Pattern` | 'is not null' cannot be written as a pattern. This is the single most common pattern in generated code and there is no route to it. |
| `Parenthesised Pattern` | Parenthesised pattern. Without one, a combinator fix cannot control its own precedence: 'a or b and c' means 'a or (b and c)'. |
| `Pattern Combinators` | Pattern combinators. 'and', 'or' and 'not' have no node, so no pattern can be composed with another; SyntaxHelpers.And/Or build boolean expressions, which is a different grammar position. |
| `Pattern In A Case Label` | A pattern cannot appear in a case label. CaseBlockDefinition writes 'case <value>:' from an expression, so 'case Dog d when d.Age > 2:' cannot be produced. |
| `Positional Pattern` | Positional/recursive pattern. 'x is Point(0, var y)' cannot be written, so a deconstructible type cannot be matched. |
| `Property Pattern` | Property pattern. 'x is { Count: > 0 }' and nested designations like 'x is { Owner: { Name: var n } }' have no component. |
| `Relational Pattern` | Relational patterns. 'x is > 0', 'x is <= 10' cannot be written, so a range test cannot be expressed as a pattern at all. |
| `Slice Pattern` | Slice pattern. 'x is [first, .. var rest]' has no component. |
| `Var Pattern` | Var pattern. 'x is var v' has no component. |

### Statements

| Construct | What is missing |
|---|---|
| `Checked And Unchecked` | There is no checked or unchecked emitter, in statement or expression position |
| `Do While Statement` | There is no do/while emitter; WhileDefinition writes the pre-test form only |
| `For Each With An Explicit Element Type` | ForEachDefinition writes 'foreach(var x in ...)' with the type fixed as var, so a non-generic sequence cannot be iterated as its element type; and there is no await foreach |
| `Goto And Labels` | There is no goto emitter, no label emitter, and so no labelled break or continue either |
| `Local Functions` | There is no local function emitter |
| `Lock Statement` | There is no lock emitter |
| `Throw Expression And Rethrow` | There is no 'throw' expression (only ThrowNewExceptionStatement, which is a statement), and no rethrow: a bare 'throw;' inside a catch cannot be written except as a raw string |
| `Using Statement And Declaration` | There is no using statement or using declaration emitter, so generated code cannot dispose anything without writing the block by hand |
| `Yield Break` | YieldReturn exists but there is no 'yield break', so an iterator cannot terminate early |

### Trivia and directives

| Construct | What is missing |
|---|---|
| `Ordinary Comment` | There is no ordinary // or /* */ comment emitter; Comment on a component is always written as a /// documentation comment |
