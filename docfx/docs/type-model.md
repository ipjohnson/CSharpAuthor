---
title: The type model
---

# The type model

@CSharpAuthor.ITypeDefinition is the central idea in the library. Across the two production
consumers, `ITypeDefinition` and its implementations account for more type references than
everything else in the API combined.

It is worth understanding what it is *not*. It is not a string, and it is not a rendered name. It
is a record of what a type is:

| | |
|---|---|
| `Namespace`, `Name` | where it lives and what it is called |
| `ContainingType` | the outer type, when it is nested — so `Outer.Inner` stays `Outer.Inner` |
| `TypeArguments` | the closing types of a generic, each an `ITypeDefinition` in its own right |
| `ArrayRanks` | outermost first, so `int[,][]` is `[2, 1]` and `int[][,]` is `[1, 2]` |
| `NullableAnnotations` | one per level, so `string?[]` and `string[]?` are different types |
| `KnownNamespaces` | every namespace a file must have in scope to use this type |

The one method that turns any of it into characters is

```csharp
void WriteTypeName(StringBuilder builder, TypeOutputMode typeOutputMode = TypeOutputMode.ShortName);
```

and nothing calls it while you are building a tree.

## Why nothing is rendered until the end

When you write a type into an output context, the context does not append its name. It **records
the reference**. Names, `using` directives, aliases and formatting are all decided at `Output()`,
when the whole file is known.

That ordering is what makes several things possible at once:

- **Derived usings.** The file's `using` block is computed from the types the file actually
  wrote. A missing `using` is not a bug you can have; nothing else adds them.
- **Collision aliasing.** Two types with the same short name are only *known* to collide once
  both have been written. At the end, one of them gets an alias.
- **One tree, several files.** Qualification mode, indentation, brace style and line endings can
  all still change after the last node has been written.

Concretely:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/TypeModelSamples.cs#one-tree-two-renderings)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/type-model-one-tree-two-renderings.txt)]

The tree was built once. `List<string>` became `List<string>` with a `using` in one file and
`global::System.Collections.Generic.List<string>` with no usings in the other, and nothing in the
building code knew which was going to happen.

> [!IMPORTANT]
> The corollary is the rule that matters most in practice: **anything committed to text early
> cannot participate in any of this.** A type written as the string `"ServiceLifetime.Singleton"`
> tracks no namespace, so it derives no `using`, gets no alias, and is not qualified in a
> qualifying mode. [Output modes](output-modes.md#members-reached-off-a-type) shows what that
> looks like, and what to write instead.

## Getting hold of a type

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/TypeModelSamples.cs#constructing)]

Rendered in `ShortName` and then in `Global`:

[!code-text[](../samples/CSharpAuthor.Docs.Samples/expected/type-model-constructing.txt)]

Note that `typeof(int)` came back as the keyword `int`, and that qualifying a closed generic
qualified its arguments too, on their own terms.

In a source generator you rarely construct these by hand — you have an `ITypeSymbol` from the
semantic model, and the [Roslyn bridge](source-generators.md#the-roslyn-bridge) converts it:

```csharp
ITypeDefinition type = propertySymbol.Type.GetTypeDefinition();
```

That conversion carries array ranks, tuple element names, nested containers, pointer and function
pointer shapes, and the difference between `Nullable<T>` and an annotated reference type.

## Shapes are applied, not spelled

`MakeArray`, `MakeArray(rank)` and `MakeNullable` return a new type; the original is unchanged.
Order matters, and the model keeps it straight:

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/TypeModelSamples.cs#shapes)]

[!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/type-model-shapes.txt)]

The last two are the ones string concatenation always gets wrong.
`stringType.MakeArray().MakeNullable()` is *an array, which may be null*; `MakeArrayOfNullable()`
is *an array of strings, each of which may be null*. They are different types, and C# spells them
differently.

## Comparison and caching

@CSharpAuthor.ITypeDefinition implements `IComparable<ITypeDefinition>` and value equality, so
type definitions can be dictionary keys and set members.

That matters for incremental generators. A model that reaches Roslyn's caching layer must compare
by value, or the cache never hits. C# records do **not** do this for collection members — a
`record R(ImmutableList<X> Items)` with identical contents returns `Equals == false`, because the
list compares by reference. @CSharpAuthor.Collections.EquatableArray`1 is in the library for that
reason:

```csharp
internal readonly struct DescribeModel
{
    public ITypeDefinition Type { get; }
    public EquatableArray<DescribedProperty> Properties { get; }
}
```

[The generator sample](source-generators.md) uses it.

## What to read next

- [Output modes](output-modes.md) — the mode that `WriteTypeName` takes, and what each one does to
  a whole file.
- [API reference for `ITypeDefinition`](xref:CSharpAuthor.ITypeDefinition).
