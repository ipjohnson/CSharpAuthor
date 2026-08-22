# CSharpAuthor

Programmatically generate C# source. Built for **Roslyn source generators**, where it is about
**25× faster** than `SyntaxFactory` + `NormalizeWhitespace` — 0.019 ms against 0.489 ms per file,
measured on the same machine in the same run.

You build a tree of definitions; it emits formatted C#. Types stay unrendered until the whole file
is known, so namespaces are *derived* from what you actually wrote rather than declared on the side.

```csharp
var file = new CSharpFileDefinition("Sample.Generated");

var widget = file.AddClass("Widget");
widget.Modifiers = ComponentModifier.Public | ComponentModifier.Partial;

var name = widget.AddProperty(typeof(string), "Name");
name.Modifiers = ComponentModifier.Public;

var describe = widget.AddMethod("Describe");
describe.Modifiers = ComponentModifier.Public;
describe.SetReturnType(typeof(string));
describe.Return(SyntaxHelpers.QuoteString("widget"));

var items = widget.AddMethod("Items");
items.Modifiers = ComponentModifier.Public;
items.SetReturnType(TypeDefinition.IEnumerable(typeof(string)));
items.AddIndentedStatement("yield break");

var context = new OutputContext();

file.WriteOutput(context);

var outputString = context.Output();
```

```csharp
using System.Collections.Generic;

namespace Sample.Generated
{
    public partial class Widget
    {

        public string Name { get; set; }

        public string Describe()
        {
            return "widget";
        }

        public IEnumerable<string> Items()
        {
            yield break;
        }
    }
}
```

Note `SyntaxHelpers.QuoteString` on the return value, and the derived `using System.Collections.Generic;`
that nothing asked for. Both are explained below.

## Install

```
dotnet add package CSharpAuthor
```

CSharpAuthor ships as **source**, compiled into your project, so a source generator can use it
without taking on a dependency it would then have to redistribute. In a generator project:

```xml
<PackageReference Include="CSharpAuthor" Version="2.0.0">
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

## Writing statements: `AddCode`

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

- **[Migrating from 1.x](https://github.com/ipjohnson/CSharpAuthor/blob/main/docs/migration-v1-v2.md)**
- **[Known API gaps](https://github.com/ipjohnson/CSharpAuthor/blob/main/docs/api-gaps.md)** —
  constructs the hand-written facade has no entry point for, and what to do instead
- **[AGENTS.md](https://github.com/ipjohnson/CSharpAuthor/blob/main/AGENTS.md)** — conventions,
  invariants and traps, for humans and AI agents working on the library itself

## License

MIT. See [LICENSE](https://github.com/ipjohnson/CSharpAuthor/blob/main/LICENSE).
