# CSharpAuthor

Programmatically generate C# source. Built for **Roslyn source generators**, where it is about
**25× faster** than `SyntaxFactory` + `NormalizeWhitespace` — 0.019 ms against 0.489 ms per file,
measured on the same machine in the same run.

You build a tree of definitions and expressions; it emits formatted C#. Nothing is a string until
serialization — types stay unrendered, so namespaces are *derived* from what you actually wrote, and
expressions stay structured, so precedence and escaping are handled for you.

```csharp
using CSharpAuthor;
using CSharpAuthor.Expressions;

var file = new CSharpFileDefinition("Sample.Generated");

var widget = file.AddClass("Widget");
widget.Modifiers = ComponentModifier.Public | ComponentModifier.Partial;

var name = widget.AddProperty(typeof(string), "Name");
name.Modifiers = ComponentModifier.Public;

var describe = widget.AddMethod("Describe");
describe.Modifiers = ComponentModifier.Public;
describe.SetReturnType(typeof(string));
describe.Return(Ex.Interpolate("widget ", Ex.Id("Name")));

var rank = widget.AddMethod("Rank");
rank.Modifiers = ComponentModifier.Public;
rank.SetReturnType(typeof(int));
rank.Return(Ex.Switch(Ex.Id("Name"),
    Ex.Arm(Pat.Null, Ex.Int(0)),
    Ex.Arm(Pat.Declaration(TypeDefinition.Get(typeof(string)), "s"),
           Ex.Id("s").Dot("Length").Is(Pat.GreaterThan(Ex.Int(8))),
           Ex.Int(2)),
    Ex.Arm(Pat.Discard, Ex.Int(1))));

var context = new OutputContext();

file.WriteOutput(context);

var outputString = context.Output();
```

```csharp
namespace Sample.Generated
{
    public partial class Widget
    {

        public string Name { get; set; }

        public string Describe()
        {
            return $"widget {Name}";
        }

        public int Rank()
        {
            return Name switch
            {
                null => 0,
                string s when s.Length is > 8 => 2,
                _ => 1
            };
        }
    }
}
```

Nothing in that input was a fragment of C# text. `Ex.Interpolate` decided where the `$"…{…}"` holes
go; `Ex.Switch` laid out the arms and indented them; `Pat` built the guard. That is the library
working as intended, and it is what the rest of this page is about.

## Install

```
dotnet add package CSharpAuthor --prerelease
```

`--prerelease` is required while 2.0 is in preview. Without it you get **1.2.0**, the previous
major — and silently, because the sample above compiles there too. Drop the flag once 2.0.0 ships.

CSharpAuthor ships as **source**, compiled into your project, so a source generator can use it
without taking on a dependency it would then have to redistribute. In a generator project:

```xml
<PackageReference Include="CSharpAuthor" Version="2.0.0-preview1003">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>build</IncludeAssets>
</PackageReference>
```

```xml
<PropertyGroup>
  <PackageCSharpAuthorIncludeSource>true</PackageCSharpAuthorIncludeSource>
</PropertyGroup>
```

`IncludeAssets="build"` is what keeps the package's assembly out of your reference list, so the
source compiled into your project is the only copy of each type. If you leave it off, the package
drops the reference itself — the two cannot both apply — so a plain `PackageReference` works too.

An optional Roslyn bridge — `ITypeSymbol` → `ITypeDefinition`, and attribute reading — ships in the
same package behind `PackageCSharpAuthorIncludeRoslyn=true`. Your generator already references
Roslyn, so it costs nothing extra. It implies `PackageCSharpAuthorIncludeSource`.

## Why the type model matters

`ITypeDefinition` is not a string. It stays unrendered until serialization, which is what lets one
option flip an entire file between short names and `global::`, resolve same-name collisions with an
alias, and make a missing `using` structurally impossible.

```csharp
var options = new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global };
var context = new OutputContext(options);
```

`Global` qualifies every reference and emits no derived usings — **recommended for a generator**, and
the faster path, because nothing you emit can collide with anything in the consumer's file. `ShortName`
gives you shorter names, derived `using` directives and automatic collision aliasing.

`ShortName` is the default, so a generator has to opt in to the mode it wants.

## Building statements

Statements are built the same way types are: out of objects that stay unrendered until the end.
`Ex` builds expressions, `Pat` builds patterns. Both live in `CSharpAuthor.Expressions`.

```csharp
using CSharpAuthor.Expressions;
```

### Expressions — `Ex`

`Ex.Id` is an identifier, `Ex.Str` a string literal, and the difference matters:

```csharp
Ex.Id("Name")       // Name        — an identifier, keyword-escaped
Ex.Str("Name")      // "Name"      — a string literal, with escaping
Ex.Int(42)          // 42
Ex.Value(1.5m)      // 1.5M        — the suffix, because `decimal d = 1.5;` is CS0664
```

Identifiers are escaped for you, which is the whole reason to build them as objects:

```csharp
Ex.Id("class")                        // @class
Ex.On(targetsType, "new")             // AttributeTargets.@new
```

Member access, calls and null-conditionals chain:

```csharp
Ex.Id("sb").Call("Append", Ex.Str("hi"))          // sb.Append("hi")
Ex.Id("sb").NullCall("Clear")                     // sb?.Clear()
Ex.New(TypeDefinition.Get(typeof(StringBuilder))) // new StringBuilder()
Ex.Id("items").Call("Select", Ex.Lambda("x", Ex.Id("x").Dot("Name")))
                                                  // items.Select(x => x.Name)
```

**Precedence is handled, so you never parenthesise by hand.** `Ex` knows where each operator sits
and brackets only what needs it:

```csharp
Ex.Multiply(Ex.Add(Ex.Int(1), Ex.Int(2)), Ex.Int(3))   // (1 + 2) * 3
Ex.Add(Ex.Int(1), Ex.Multiply(Ex.Int(2), Ex.Int(3)))   //  1 + 2 * 3
```

One trap worth knowing: `&` and `|` on `Ex` are the **short-circuiting** operators, because that is
what generated code usually wants. For a `[Flags]` combination you want `Ex.BitOr`, not `|`:

```csharp
Ex.On(t, "Class") | Ex.On(t, "Struct")             // AttributeTargets.Class || …   ⚠ CS0019
Ex.BitOr(Ex.On(t, "Class"), Ex.On(t, "Struct"))    // AttributeTargets.Class | …
```

### Patterns — `Pat`

Every pattern form C# has, including the combinators. Patterns are used through `Ex.Is`, or as
switch arms:

```csharp
Ex.Id("value").Is(Pat.NotNull())                      // value is not null
Ex.Id("value").Is(Pat.Declaration(stringType, "s"))   // value is string s
Ex.Id("value").Is(Pat.GreaterThan(Ex.Int(8)))         // value is > 8

Ex.Id("value").Is(Pat.Or(Pat.Constant(Ex.Int(1)),
                         Pat.Constant(Ex.Int(2))))    // value is 1 or 2
```

`Pat.Null`, `Pat.Discard`, `Pat.Type`, `Pat.Var`, `Pat.VarTuple`, `Pat.Relational`, `Pat.Not`,
`Pat.And` and property patterns via `Pat.Prop` cover the rest.

### Putting them in a method

An `Ex` goes into a method body through the statement API — there is no template step:

```csharp
method.Assign(Ex.New(sbType)).ToVar("sb");        // var sb = new StringBuilder();
method.AddIndentedStatement(Ex.Id("sb").Call("Append", Ex.Str("hi")));
method.Return(Ex.Id("sb").Call("ToString"));      // return sb.ToString();

var block = method.If(Ex.Id("sb").Dot("Length").Is(Pat.GreaterThan(Ex.Int(0))));
block.Return(null);                               // if (sb.Length is > 0) { return; }
```

## Text templates: `AddCode`

`AddCode` is the escape hatch for C# you would rather write as text than build. Prefer `Ex` — it
escapes identifiers, tracks types and handles precedence, none of which a template can do.

An `Ex` can be substituted into a template like any other value, so the two mix:

```csharp
method.AddCode("var sum = {arg1};", Ex.Add(Ex.Int(42), Ex.Int(1)));   // var sum = 42 + 1;
```

`AddCode` takes a template plus positional values. **`N` is 1-based**, and the brackets decide what
happens to the value:

| Spelling | What it does |
|---|---|
| `{argN}` | Substitutes a **value**. A type stays a type — qualified per `TypeOutputMode`, aliased on collision, and its namespace is added to the file's usings. |
| `[argN]` | Substitutes **text**, on the spot. Nothing is tracked, so no `using` is derived. |

Measured behaviour, for the same value through each spelling:

| Value | `{argN}` | `[argN]` |
|---|---|---|
| `"hello"` | `hello` | `hello` |
| `SyntaxHelpers.QuoteString("hello")` | `"hello"` | `"hello"` |
| `42` | `42` | `42` |
| `42L` | `42L` | `42L` |
| `1.5d` | `1.5d` | `1.5d` |
| `1.5m` | `1.5m` | `1.5m` |
| `true` | `true` | `true` |
| `'a'` | `'a'` | `'a'` |
| `null` | `null` | `null` |
| `typeof(StringBuilder)` | `StringBuilder`, and `using System.Text;` is added | `System.Text.StringBuilder`, no using ⚠ |
| `DayOfWeek.Monday` | `DayOfWeek.Monday` | `Monday` ⚠ |

**The two spellings agree on every scalar.** They differ only on a type and on an `enum` — and on
both of those, `[argN]` is the wrong answer. A type substituted as text is frozen at its identity
string: it ignores `TypeOutputMode`, so it reads the same in a file that qualifies everything, and
no `using` is derived because nothing recorded that a type was mentioned. A bare `Monday` is
`CS0103` unless something of that name is in scope, in which case it compiles and means something
else.

So: use `{argN}`. Reach for `[argN]` only for text that is genuinely text — an identifier, an
operator, a fragment of a name you are building up — where the value is a `string` and the two are
equivalent anyway.

### A string is code, not a literal

```csharp
method.AddCode("var s = {arg1};", "hello");                          // var s = hello;
method.AddCode("var s = {arg1};", SyntaxHelpers.QuoteString("hello")); // var s = "hello";
```

This is the rule everywhere in the library, not just here: a `string` you hand to CSharpAuthor is a
fragment of C#. That is what lets you write `AddAttribute(type, $"nameof({member})")` or
`Return("_field.Value")` and have it mean what it says. When you want a string *literal*, ask for one
with `SyntaxHelpers.QuoteString`.

### Placeholder mismatches are silent

A placeholder with no value, and a value with no placeholder, are both ignored — the count is never
checked, so the mistake reaches your generated file as text:

```csharp
method.AddCode("var v = {arg0};", "hello");       // var v = {arg0};   0-based is wrong
method.AddCode("var v = {arg2};", "hello");       // var v = {arg2};   no second value
method.AddCode("var v = {arg1} + {arg1};", "a");  // var v = a + a;    every occurrence is filled
```

## Targeting a language version

The tree is version-agnostic; the writer decides how to render it. An `EmitProfile` says what the
target may use, and features downlevel where an equivalent exists — a collection expression becomes
`new[] { … }` below C# 12. Where no equivalent exists, you get a **diagnostic rather than wrong
output**: `CSA1001` arrives both as a structured diagnostic and as an inline `#error` at the
declaration that caused it, naming the feature, the version it needs and the version you targeted.

## Documentation

This page is the manual — there is no documentation site. Beyond it:

- **[Migrating from 1.x](https://github.com/ipjohnson/CSharpAuthor/blob/main/docs/migration-v1-v2.md)**
- **[Known API gaps](https://github.com/ipjohnson/CSharpAuthor/blob/main/docs/api-gaps.md)** —
  constructs with no first-class entry point. Written against preview1002 and **partly stale**: the
  expression and pattern sections have been corrected, the rest has not. Try a construct before
  believing it is missing.
- **[AGENTS.md](https://github.com/ipjohnson/CSharpAuthor/blob/main/AGENTS.md)** — conventions,
  invariants and traps, for humans and AI agents working on the library itself

## License

MIT. See [LICENSE](https://github.com/ipjohnson/CSharpAuthor/blob/main/LICENSE).
