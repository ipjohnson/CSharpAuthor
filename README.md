# CSharpAuthor

Programmatically generate C# source. Built for **Roslyn source generators**, where it is about
**25× faster** than `SyntaxFactory` + `NormalizeWhitespace` — 0.019 ms against 0.489 ms per file,
measured on the same machine in the same run.

You build a tree of definitions; it emits formatted C#. Types stay unrendered until the whole file
is known, so namespaces are *derived* from what you actually wrote rather than declared on the side.

```csharp
var file = new CSharpFileDefinition("TestNamespace");

var classDefinition = file.AddClass("TestClass");

var method = classDefinition.AddMethod("SomeMethod");

classDefinition.AddUsingNamespace("SomeNamespace");

var outputContext = new OutputContext();

file.WriteOutput(outputContext);

var outputString = outputContext.Output();
```

```csharp
using SomeNamespace;

namespace TestNamespace
{
    public class TestClass
    {

        public void SomeMethod()
        {
        }
    }
}
```

## Install

```
dotnet add package CSharpAuthor
```

CSharpAuthor ships as **source**, compiled into your project, so a source generator can use it
without taking on a dependency it would then have to redistribute. Set
`PackageCSharpAuthorIncludeSource=true` in a generator project.

An optional Roslyn bridge — `ITypeSymbol` → `ITypeDefinition`, and attribute reading — ships in the
same package behind `PackageCSharpAuthorIncludeRoslyn=true`. Your generator already references
Roslyn, so it costs nothing extra.

## Why the type model matters

`ITypeDefinition` is not a string. It stays unrendered until serialization, which is what lets one
option flip an entire file between short names and `global::`, resolve same-name collisions with an
alias, and make a missing `using` structurally impossible.

```csharp
var options = new OutputContextOptions { TypeOutputMode = TypeOutputMode.Global };
```

`Global` qualifies every reference and emits no derived usings — the recommended default for a
generator, and the faster path. `ShortName` gives you shorter names, derived `using` directives and
automatic collision aliasing.

## Targeting a language version

The tree is version-agnostic; the writer decides how to render it. An `EmitProfile` says what the
target may use, and features downlevel where an equivalent exists — a collection expression becomes
`new[] { … }` below C# 12, silently and correctly. Where no equivalent exists, you get a
**diagnostic rather than wrong output**.

## Documentation

- **[Migrating from 1.x](docs/migration-v1-v2.md)**
- **[AGENTS.md](AGENTS.md)** — conventions, invariants and traps, for humans and AI agents working
  on the library itself

## License

MIT. See [LICENSE](LICENSE).
