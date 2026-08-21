# What is in this folder

Two different kinds of document live here, and only one of them belongs on the documentation site.

## User documentation

| | |
|---|---|
| [`migration-v1-v2.md`](migration-v1-v2.md) | Every breaking change from 1.x to 2.0 with its mechanical fix, every changed snapshot with a justification, and the patches applied to the two production consumers. **Linked from the site**, at `docs/migrating-from-v1.html`. |

The rest of the user-facing documentation is not here — it is the DocFX site under
[`../docfx`](../docfx), which is published to GitHub Pages from `main` by
[`.github/workflows/docs.yml`](../.github/workflows/docs.yml).

## The 2.0 release record

These are the working papers of the 2.0 build. They are for whoever reviews or maintains 2.0, not
for somebody learning the library, and **`docfx/docfx.json` does not include them** — its content
list names files explicitly rather than globbing this folder, so they cannot reach the site by
accident.

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
