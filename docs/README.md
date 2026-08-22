# What is in this folder

Two different kinds of document live here, and only one of them belongs on the documentation site.

## User documentation

| | |
|---|---|
| [`../README.md`](../README.md) | The manual. Install, the type model, building statements with `Ex`/`Pat`, text templates, and language-version targeting. |
| [`migration-v1-v2.md`](migration-v1-v2.md) | Every breaking change from 1.x to 2.0 with its mechanical fix, every changed snapshot with a justification, and the patches applied to the two production consumers. |
| [`api-gaps.md`](api-gaps.md) | Constructs with no first-class entry point, and what to do instead. **See the staleness warning at the top of that file.** |

There is no documentation site. A DocFX site was built for 2.0 and then dropped before release
(`635f90c` added it, `f06cce6` removed it), so `../docfx` and `.github/workflows/docs.yml` do not
exist. Until that decision is revisited, the README above is the whole user manual — anything a
user needs belongs there, not here.

## The 2.0 release record

These are the working papers of the 2.0 build. They are for whoever reviews or maintains 2.0, not
for somebody learning the library.

| | |
|---|---|
| [`v2-pr-summary.md`](v2-pr-summary.md) | The pull request body: every gate with its measured number, and what was not finished. |
| [`adversary-findings.md`](adversary-findings.md) | The adversary ledger — every case found, fixed or outstanding. |
| [`v2-open-questions.md`](v2-open-questions.md) | Every default taken where the specification was silent, and why. Referenced from source comments and from two test files. |
| [`consumer-patches/`](consumer-patches) | The patches applied to `DependencyModules` and `Hardened.Framework` to run their suites against 2.0. |

They stay at these paths because roughly twenty references point at them — from
`migration-v1-v2.md`, from `V2-HANDOFF.md`, from `CSharpAuthor/ITypeDefinition.cs`, and from two
test files that are not editable under the 2.0 build rules. Moving them would break those
references to gain nothing the site does not already get from an explicit content list.
