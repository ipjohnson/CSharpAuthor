# API gaps

Constructs CSharpAuthor's hand-written facade cannot express.

> ## ⚠ This file went stale, and it is being corrected in place
>
> The inventory below was written against **2.0.0-preview1002**. `CSharpAuthor.Expressions` —
> `Ex`, `Pat`, `Raw` — closed a large part of it in **preview1003**, and this file was not updated.
>
> **The `Expressions` and `Patterns` sections were wrong in their entirety.** All 11 expression
> entries and 12 of the 13 pattern entries describe constructs that are emitted correctly today;
> each has been replaced below with the call that builds it and the output it produces, verified
> against preview1003.
>
> Five more entries have since been checked by running them. Two are **closed**
> (`Base Class Constraint Ordering`, `Volatile Fields`), one was **partly wrong**
> (`Operator Declarations` — reachable by an unobvious route), and two are **confirmed still open**
> with the failing output recorded (`Conversion Operators`, `Destructors`). Everything not marked
> that way has **not** been re-verified and should be treated as unreliable.
>
> If a construct here looks like something you need, **try it before believing this file**. Reach
> for `AddCode` on its authority and you will write a string where an object would have worked.
>
> The irony is not lost: the section immediately below explains how the previous version of this
> inventory went stale, and then the same thing happened to this one. The fix is to generate it —
> a test that attempts each construct and records the ones that actually fail, so a stale entry
> breaks the build instead of misleading a reader.

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


### Expressions — ✅ all 11 closed in preview1003

Every entry that was here is emitted correctly today. Verified output, `x` being `Ex.Id("x")`:

| Was listed as missing | Build it with | Emits |
|---|---|---|
| As Expression | `Ex.As(x, strT)` | `x as string` |
| Collection Expressions And Spreads | `Ex.Collection(Ex.Int(1), Ex.Int(2), Ex.Spread(Ex.Id("rest")))` | `[1, 2, ..rest]` |
| Conditional Expression | `Ex.Conditional(x, Ex.Int(1), Ex.Int(2))` | `x ? 1 : 2` |
| Interpolated Strings | `Ex.Interpolate("a=", x)` | `$"a={x}"` |
| Lambdas | `Ex.Lambda("v", Ex.Id("v").Dot("N"))` | `v => v.N` |
| Object Initializer With Named Members | `Ex.NewWithInitializer(ptT, null, Ex.Assign(Ex.Id("Bar"), Ex.Int(1)))` | `new Point { Bar = 1 }` |
| Ranges And Indices | `Ex.Id("a").Index(Ex.Range(Ex.Int(1), Ex.FromEnd(Ex.Int(1))))` | `a[1..^1]` |
| Stack Alloc | `Ex.StackAlloc(intT, Ex.Int(4))` | `stackalloc int[4]` |
| Switch Expressions | `Ex.SwitchInline(x, Ex.Arm(Pat.Null, Ex.Int(0)), Ex.Arm(Pat.Discard, Ex.Int(1)))` | `x switch { null => 0, _ => 1 }` |
| Tuples And Deconstruction | `Ex.Tuple(Ex.Int(1), Ex.Str("a"))` | `(1, "a")` |
| With Expressions | `Ex.With(x, Ex.Assign(Ex.Id("N"), Ex.Int(2)))` | `x with { N = 2 }` |

`Ex.Switch` gives the same switch expression laid out over multiple lines; `Ex.SwitchInline` keeps
it on one.

### Generic constraints

| Construct | What is missing |
|---|---|
| `Allows Ref Struct` | 'allows ref struct' (C# 13) has no method on ConstraintDefinition, so a generic that accepts a ref struct argument cannot be declared |
| ~~`Base Class Constraint Ordering`~~ | **Closed.** `Implements()` still takes both, but the writer now orders them - the type model knows which is an interface, so a base class is emitted first whatever order it was added in. Pinned by `BaseClassConstraintIsWrittenBeforeInterfaces`. |
| `Interface Constraints` | An interface cannot be generic at all, so an interface's type parameters cannot be constrained either |

### Literals

| Construct | What is missing |
|---|---|
| `Raw String Literal Fence Length` | There is no raw-string literal emitter, so the fence-length rule (content ending in a quote needs a longer fence, else CS8998) has nowhere to live yet |

### Member declarations

| Construct | What is missing |
|---|---|
| `Conversion Operators` | **Confirmed still open.** The `operator +` trick does not extend to these: a conversion operator declares no return type, and `MethodDefinition` always writes one, so `AddMethod("implicit operator int")` emits `public static void implicit operator int(Money m)`. |
| `Destructors` | **Confirmed still open.** Same cause as conversion operators - a destructor declares no return type. `AddMethod("~Host")` with `NoAccessibility` emits `&nbsp;void ~Host()`, with the stray leading space the suppressed modifier leaves behind. |
| `Enum Member Literal Form` | An enum member cannot be given a negative or hex value in a controlled way; EnumValueDefinition writes Value.ToString(), so the literal form is whatever the CLR chose |
| `Extension Blocks And Members` | Extension blocks and extension members. An extension method is reachable via ParameterDefinition.This; the C# 14 'extension(T x) { }' block, and extension properties and indexers inside it, are not. |
| `Extern Members` | 'extern' has no ComponentModifier flag, so a DllImport declaration cannot be emitted |
| `Generic Delegate Constraints` | A delegate cannot be generic. DelegateDefinition inherits MethodDefinition's generic parameters but a caller cannot reach constraints on them in a delegate position. |
| `Generic Interfaces` | An interface cannot be generic. InterfaceDefinition has no AddGenericParameter and no constraint list, so 'interface IRepo<T> where T : class' cannot be declared. |
| `Interface Member Kinds` | An interface cannot declare an event, an indexer, a nested type, or a generic parameter. InterfaceDefinition holds only methods and properties. |
| `Operator Declarations` | **Partly wrong.** A binary operator *can* be written, because `MethodDefinition` writes its name where the operator keyword and symbol go - so `AddMethod("operator +")` emits `public static Money operator +(Money a, Money b)`, verified. It is a trick rather than an API, and nothing validates the name, but the construct is reachable. |
| `Ref Returns` | Ref returns and ref locals. MethodDefinition writes the return type with no modifier position, so 'ref int M()' and 'ref readonly int M()' cannot be declared. |
| `Unsafe Members` | 'unsafe' has no ComponentModifier flag, so neither an unsafe member nor a pointer type can be declared |
| ~~`Volatile Fields`~~ | **Closed.** `ComponentModifier.Volatile`. |

### Patterns — ✅ 12 of 13 closed in preview1003

`Pat` implements every pattern form C# has. Verified output, `x` being `Ex.Id("x")`:

| Was listed as missing | Build it with | Emits |
|---|---|---|
| Constant Pattern | `x.Is(Pat.Constant(Ex.Int(0)))` | `x is 0` |
| Declaration Pattern | `x.Is(Pat.Declaration(strT, "d"))` | `x is string d` |
| Discard Pattern | `x.Is(Pat.Discard)` | `x is _` |
| List Pattern | `x.Is(Pat.List(Pat.Constant(Ex.Int(1)), Pat.Slice()))` | `x is [1, ..]` |
| Not Null Pattern | `x.Is(Pat.NotNull())` | `x is not null` |
| Parenthesised Pattern | `Pat.Parenthesized(…)` | `x is 1 or (not null and _)` |
| Pattern Combinators | `x.Is(Pat.And(Pat.NotNull(), Pat.GreaterThan(Ex.Int(2))))` | `x is not null and > 2` |
| Positional Pattern | `x.Is(Pat.Positional(ptT, Pat.Constant(Ex.Int(0)), Pat.Var("y")))` | `x is Point(0, var y)` |
| Property Pattern | `x.Is(Pat.Property(null, new[]{ Pat.Prop("Count", Pat.GreaterThan(Ex.Int(0))) }))` | `x is { Count: > 0 }` |
| Relational Pattern | `x.Is(Pat.LessThanOrEqual(Ex.Int(10)))` | `x is <= 10` |
| Slice Pattern | `x.Is(Pat.List(Pat.Var("first"), Pat.Slice(Pat.Var("rest"))))` | `x is [var first, .. var rest]` |
| Var Pattern | `x.Is(Pat.Var("v"))` | `x is var v` |

`Pat.Recursive` combines a positional and a property pattern with an optional designation.

**Still open:**

| Construct | What is missing |
|---|---|
| `Pattern In A Case Label` | A pattern cannot appear in a case label. `CaseBlockDefinition` writes `case <value>:` from an expression, so `case Dog d when d.Age > 2:` cannot be produced. `Ex.Switch` covers the *expression* form with guards; the statement form does not take a `Pat`. |

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
