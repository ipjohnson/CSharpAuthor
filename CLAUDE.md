# CLAUDE.md

**The full brief is [AGENTS.md](AGENTS.md). Read it before changing anything.**

It is not boilerplate — every trap in it caught a real contributor this week, usually silently.

The four things worth carrying even if you read nothing else:

1. **A type is not text until the file is serialized.** `ITypeDefinition` stays unrendered until
   `WriteTypeName` at the very end. That is what lets one option flip a file between short names and
   `global::`, and what makes a missing `using` structurally impossible. Any change that turns a type
   into a string early is wrong, however convenient it looks.

2. **The defect class is silent wrongness, not crashes.** Output that is wrong but does not throw,
   and that a substring assertion happily accepts — `protected` emitted for `private protected`
   (widening access), `string[]?` for `string?[]`, `1,5` on a de-DE machine. When adding anything,
   ask "how would I know if it silently didn't work?"

3. **`dotnet test <some.dll>` against a consumer is a FALSE GREEN.** It returns a clean pass while
   measuring the *published* CSharpAuthor package instead of your checkout. Always use
   `./scripts/run-consumer-tests.sh <checkout> --scope core`, which asserts your files are really in
   the compile set. And the unit tests are not the oracle — the two consumer repos are. Changes have
   passed the entire unit suite (1,559 tests at the time) while breaking three consumer projects
   outright.

4. **Never re-baseline a snapshot and never edit an existing test to make a change pass.**
   `UPDATE_SNAPSHOTS=1` and `APPROVE_PUBLIC_API=1` exist; do not set them. A changed snapshot is
   either a bug or an improvement, and which one is a human's call.

   This protects tests that *assert* something. It never protected the 93 `ADVERSARY GAP`
   placeholders, whose bodies were `Assert.True(false, ...)`: un-skipping one failed whether or not
   the gap was still real, so nothing forced a revisit when a feature shipped, and 24 of them
   turned out to describe features that existed. They are gone - the surviving gaps are in
   [docs/api-gaps.md](docs/api-gaps.md), and the 18 tests still skipped all assert something real.
   Changing a test because a human decided the behaviour it pins is wrong is a different act from
   bending one to hide a regression; say which you are doing.

Verification, all of it:

```bash
dotnet test CSharpAuthor.Tests                            # 1634 passed / 0 failed / 18 skipped
./scripts/run-consumer-tests.sh <checkout> --scope core   # the real oracle
./scripts/run-roundtrip.sh   <checkout> --corpus all      # 1,315 / 1,373 = 95.8%
./scripts/run-benchmark.sh   <v1> <v2>                    # both checkouts, ONE invocation
./scripts/verify-roslyn-packaging.sh
python3 tools/grammar/gen_all.py --report
```
