---
title: Working on these docs
---

# Working on these docs

## No sample on this site is typed into a page

Every C# block is a `#region` of a file in `docfx/samples`, pulled in by DocFX's code-include
syntax. Every "produces" block is that program's recorded output. So a sample that stops compiling
breaks a build, and a sample whose output changes fails a check. Neither can reach the site
quietly.

```
docfx/samples/
  CSharpAuthor.Docs.Samples/            getting started, type model, output modes, emit profiles
    expected/                           the recorded output of each
  CSharpAuthor.Docs.Samples.Generator/  a working IIncrementalGenerator
    expected/                           what it generates
  CSharpAuthor.Docs.Samples.Generator.Runner/
                                        drives that generator over a real compilation
```

The generator's project also proves the packaging model: it compiles CSharpAuthor's source into
itself with the same two globs `build/CSharpAuthor.targets` uses, so if source inclusion breaks,
this project stops building.

## Building the site

```bash
dotnet tool restore
dotnet docfx docfx/docfx.json
```

The output is `docfx/_site`. To serve it locally with live reload:

```bash
dotnet docfx docfx/docfx.json --serve
```

The API reference is generated from the library's XML documentation file, which
`CSharpAuthor.csproj` produces because `GenerateDocumentationFile` is on. DocFX builds the project
itself during the `metadata` phase, so no separate build step is needed.

## Checking the samples

```bash
dotnet run --project docfx/samples/CSharpAuthor.Docs.Samples
dotnet run --project docfx/samples/CSharpAuthor.Docs.Samples.Generator.Runner
```

Both print one line per sample and exit non-zero on any mismatch. The generator runner additionally
compiles what the generator produced and fails if it does not.

To re-record after an intentional change:

```bash
dotnet run --project docfx/samples/CSharpAuthor.Docs.Samples -- --update
dotnet run --project docfx/samples/CSharpAuthor.Docs.Samples.Generator.Runner -- --update
```

Read the diff before committing a re-recording. A changed recording is either a fix or a
regression, and the recording itself cannot tell you which.

## Adding a sample

1. Write a method in one of the sample projects that returns the string to show, and wrap the part
   you want on the page in `#region name` / `#endregion`.
2. Register it in that project's `Program.cs` with a stable name.
3. Run with `--update` to record it.
4. Reference both halves from the markdown:

   ```markdown
   [!code-csharp[](../samples/CSharpAuthor.Docs.Samples/YourFile.cs#name)]

   [!code-csharp[](../samples/CSharpAuthor.Docs.Samples/expected/your-name.txt)]
   ```

## What is on the site, and what is not

`docs/` in the repository holds both user documentation and the 2.0 release record. Only the first
belongs on a site people read to learn the library, so `docfx.json` names its content explicitly
rather than globbing the repository.

| File | On the site |
|---|---|
| `docs/migration-v1-v2.md` | **yes** — linked from [Migrating from 1.x](migrating-from-v1.md) |
| `docs/adversary-findings.md` | no — the 2.0 adversary ledger |
| `docs/v2-open-questions.md` | no — decisions taken during the 2.0 build, for the maintainer |
| `docs/v2-pr-summary.md` | no — the 2.0 pull request body |
| `docs/consumer-patches/` | no — patches applied to consumer repositories during the 2.0 build |

See `docs/README.md` in the repository for the same split, stated where those files live.

## Publishing

`.github/workflows/docs.yml` builds the site and publishes it to GitHub Pages on every push to
`main`. It runs the sample checks first, so a broken sample fails the deployment rather than
shipping.

Pull requests build the site but do not publish, which is what catches a broken code-include
reference before it is merged.
