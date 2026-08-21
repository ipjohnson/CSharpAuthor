#!/usr/bin/env bash
#
# run-consumer-tests.sh - run the CSharpAuthor consumer suites against a local checkout.
#
#   ./scripts/run-consumer-tests.sh <path-to-CSharpAuthor-checkout> [options]
#
# The two consumer repositories are the real oracle for CSharpAuthor 2.0 (V2-HANDOFF.md
# section 6). This points both of them at a checkout of your choice and reports the counts.
#
#   DependencyModules        gate 4   tests/DependencyModules.Tests           (net8.0 + net10.0)
#   Hardened.Framework       gate 5   Hardened.SourceGenerator.Tests          (net8.0)
#
# ---------------------------------------------------------------------------------------
# RULE 8.2 - never commit to the consumer repositories.
#
# This script goes further and never *writes* to them either: both consumers are wired to
# the local checkout entirely through MSBuild properties passed on the command line, so
# `git status` in each scratch clone stays empty and there is nothing to accidentally
# commit. It checks that invariant before and after every run and fails if it is violated.
#
# The only thing it writes outside the log directory is each consumer's own obj/ and bin/,
# which are gitignored build output.
#
# RULE 8.1 - never re-baseline a .verified.txt snapshot.
#
# UPDATE_SNAPSHOTS is explicitly unset for the whole run, so a snapshot test can only ever
# report a diff, never absorb one. When DependencyModules' snapshot tests fail they write
# the actual output to bin/.../Snapshots/*.received.txt; this script harvests those into
# the log directory so the diff can be justified in docs/migration-v1-v2.md.
# ---------------------------------------------------------------------------------------
#
# How each consumer is pointed at the checkout:
#
#   Hardened.Framework already ships the wiring in src/SourceGenerators/CSharpAuthor.props.
#   Setting CSharpAuthorRoot selects the checkout; UseLocalCSharpAuthor=true is passed as
#   well so a bad path is a build error rather than a silent fall back to the published
#   1.1.1010 package (which would produce a meaningless green).
#
#   DependencyModules has no such switch: its two generator projects carry a hard
#   PackageReference plus PackageCSharpAuthorIncludeSource=true. Global properties from the
#   command line cannot be overridden by a project body, so
#   /p:PackageCSharpAuthorIncludeSource=false disables the package's own source inclusion,
#   and scripts/local-csharpauthor.targets - injected via CustomAfterMicrosoftCommonTargets
#   - adds the same Compile glob against the checkout instead. That targets file errors out
#   rather than falling back, for the same reason.
#
# Both reproduce what the package's build/CSharpAuthor.targets does: compile
# <root>/CSharpAuthor/**/*.cs (minus obj/ and bin/) straight into the generator assembly.
#
# Exit code: 0 only if every suite that was asked for ran and reported zero failures.

set -uo pipefail

# ---------------------------------------------------------------------------- args & paths

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INJECT_TARGETS="$SCRIPT_DIR/local-csharpauthor.targets"

CSA_ROOT=""
CONSUMERS_DIR="${CONSUMERS_DIR:-}"
LOG_DIR="${LOG_DIR:-}"
ONLY="both"
SCOPE="core"
QUIET=0

usage() {
    cat <<'USAGE'
usage: run-consumer-tests.sh <path-to-CSharpAuthor-checkout> [options]

  --consumers DIR   directory holding the DependencyModules and Hardened.Framework scratch
                    clones. Default: $CONSUMERS_DIR, else the nearest "consumers" directory
                    found by walking up from this script (works from any clone under
                    scratchpad/work/* as well as from scratchpad/v2).
  --log-dir DIR     where to write build/test logs and harvested .received.txt snapshots.
                    Default: $LOG_DIR, else <consumers>/../logs.
  --only WHICH      dm | hardened | both            (default: both)
  --scope WHICH     core | full                     (default: core)
                      core - just the two gate projects (gates 4 and 5). ~15 s.
                      full - every test project in both solutions, which additionally
                             compiles generated code end to end. ~35 s.
  -q, --quiet       suppress the streamed dotnet output; the summary still prints.
  -h, --help        this text.

Examples:
  ./scripts/run-consumer-tests.sh ../../v2
  ./scripts/run-consumer-tests.sh /abs/path/to/CSharpAuthor --only dm
  ./scripts/run-consumer-tests.sh /abs/path/to/CSharpAuthor --scope full -q
USAGE
}

while [ $# -gt 0 ]; do
    case "$1" in
        --consumers) CONSUMERS_DIR="$2"; shift 2 ;;
        --log-dir)   LOG_DIR="$2";       shift 2 ;;
        --only)      ONLY="$2";          shift 2 ;;
        --scope)     SCOPE="$2";         shift 2 ;;
        -q|--quiet)  QUIET=1;            shift ;;
        -h|--help)   usage; exit 0 ;;
        -*)          echo "unknown option: $1" >&2; usage >&2; exit 2 ;;
        *)           if [ -n "$CSA_ROOT" ]; then echo "unexpected argument: $1" >&2; exit 2; fi
                     CSA_ROOT="$1"; shift ;;
    esac
done

if [ -z "$CSA_ROOT" ]; then usage >&2; exit 2; fi
case "$ONLY"  in dm|hardened|both) ;; *) echo "--only must be dm|hardened|both" >&2; exit 2 ;; esac
case "$SCOPE" in core|full)        ;; *) echo "--scope must be core|full" >&2;      exit 2 ;; esac

if [ ! -d "$CSA_ROOT" ]; then echo "no such directory: $CSA_ROOT" >&2; exit 2; fi
CSA_ROOT="$(cd "$CSA_ROOT" && pwd)"

if [ ! -f "$CSA_ROOT/CSharpAuthor/CSharpAuthor.csproj" ]; then
    echo "not a CSharpAuthor checkout: $CSA_ROOT/CSharpAuthor/CSharpAuthor.csproj does not exist" >&2
    exit 2
fi
if [ ! -f "$INJECT_TARGETS" ]; then
    echo "missing $INJECT_TARGETS - it ships beside this script" >&2
    exit 2
fi

# Walk up rather than hard-coding a depth: this script is copied between clones, and it has
# to resolve the same scratchpad/consumers from scratchpad/work/<agent>/scripts and from
# scratchpad/v2/scripts alike.
if [ -z "$CONSUMERS_DIR" ]; then
    _probe="$SCRIPT_DIR"
    while [ "$_probe" != "/" ]; do
        if [ -d "$_probe/consumers/DependencyModules" ] && [ -d "$_probe/consumers/Hardened.Framework" ]; then
            CONSUMERS_DIR="$_probe/consumers"; break
        fi
        _probe="$(dirname "$_probe")"
    done
fi
if [ -z "$CONSUMERS_DIR" ]; then
    echo "could not find a consumers/ directory holding DependencyModules and Hardened.Framework" >&2
    echo "above $SCRIPT_DIR - pass --consumers DIR or set \$CONSUMERS_DIR" >&2
    exit 2
fi
if [ ! -d "$CONSUMERS_DIR" ]; then echo "no consumers directory: $CONSUMERS_DIR" >&2; exit 2; fi
CONSUMERS_DIR="$(cd "$CONSUMERS_DIR" && pwd)"

DM_DIR="$CONSUMERS_DIR/DependencyModules"
HF_DIR="$CONSUMERS_DIR/Hardened.Framework"

if [ -z "$LOG_DIR" ]; then LOG_DIR="$CONSUMERS_DIR/../logs"; fi
mkdir -p "$LOG_DIR" || { echo "cannot create log dir $LOG_DIR" >&2; exit 2; }
LOG_DIR="$(cd "$LOG_DIR" && pwd)"
RUN_ID="$(date +%Y%m%d-%H%M%S)"

# 8.1: a snapshot test must never be able to rewrite its own baseline.
#   UPDATE_SNAPSHOTS   - DependencyModules/tests/DependencyModules.Tests/Infrastructure/Snapshot.cs
#   APPROVE_PUBLIC_API - Hardened.Framework/src/PublicApi/Hardened.PublicApi.Tests/PublicApiSurfaceTests.cs
# Both rewrite committed baselines in the source tree when set. They are unset here rather
# than merely "not set" so an exported value in the calling shell cannot leak in.
unset UPDATE_SNAPSHOTS
unset APPROVE_PUBLIC_API
# Non-interactive: never prompt for feed credentials, never open a browser.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_INTERACTIVE=false

# ---------------------------------------------------------------------------- reporting

RESULT_LINES=()
FAILURE_COUNT=0
SUITES_RUN=0

note()  { printf '%s\n' "$*"; }
fail()  { FAILURE_COUNT=$((FAILURE_COUNT + 1)); }
record() { RESULT_LINES+=("$1"); }

run_dotnet() {
    # run_dotnet <logfile> <args...>   - stream unless --quiet, always keep the log
    local log="$1"; shift
    if [ "$QUIET" -eq 1 ]; then
        "$@" > "$log" 2>&1
    else
        "$@" 2>&1 | tee "$log"
        return "${PIPESTATUS[0]}"
    fi
}

# Turns the `Passed! - Failed: 0, Passed: 735, ...` / `Failed! - ...` lines from a test log
# into one aligned result row each, and counts failures. Handles multi-TFM runs, where one
# log carries one line per target framework.
summarize_test_log() {
    local label="$1" log="$2" exit_code="$3"
    local found=0

    while IFS= read -r line; do
        found=1
        local failed passed skipped total tfm
        failed=$( printf '%s' "$line" | sed -n 's/.*Failed:[[:space:]]*\([0-9]*\).*/\1/p')
        passed=$( printf '%s' "$line" | sed -n 's/.*Passed:[[:space:]]*\([0-9]*\).*/\1/p')
        skipped=$(printf '%s' "$line" | sed -n 's/.*Skipped:[[:space:]]*\([0-9]*\).*/\1/p')
        total=$(  printf '%s' "$line" | sed -n 's/.*Total:[[:space:]]*\([0-9]*\).*/\1/p')
        tfm=$(    printf '%s' "$line" | sed -n 's/.*(\([^)]*\))[[:space:]]*$/\1/p')
        [ -n "$tfm" ] || tfm="-"

        local dll status
        dll=$(printf '%s' "$line" | sed -n 's/.*- \([A-Za-z0-9._]*\.dll\).*/\1/p')
        [ -n "$dll" ] || dll="$label"

        if [ "${failed:-1}" = "0" ]; then status="PASS"; else status="FAIL"; fail; fi
        record "$(printf '  %-46s %-9s %6s passed %5s failed %5s skipped  %s' \
                  "${dll%.dll}" "$tfm" "${passed:-?}" "${failed:-?}" "${skipped:-?}" "$status")"
        SUITES_RUN=$((SUITES_RUN + 1))
    done < <(grep -E '^(Passed!|Failed!)' "$log" 2>/dev/null)

    # A partial build failure is the hole this check exists to close. When a solution-wide run
    # loses an assembly to a compile error, the assemblies that DID build still print their
    # summaries, so `found` is non-zero and the missing one leaves no trace in the totals. That is
    # exactly how a V2 change that broke three Hardened build-task projects - and six test
    # assemblies with them - stayed invisible for a whole run. Compile errors are now surfaced
    # whether or not anything managed to report.
    if [ "$found" -gt 0 ] && grep -qE ': error [A-Z]+[0-9]+' "$log" 2>/dev/null; then
        local codes
        codes="$(grep -oE ': error [A-Z]+[0-9]+' "$log" | sed 's/: error //' | sort | uniq -c \
                 | sort -rn | head -4 | awk '{printf "%sx%s ", $1, $2}')"
        record "$(printf '  %-46s %-9s  BUILD ERRORS ALONGSIDE RESULTS - assemblies may be missing: %s see %s' \
                  "$label" "-" "$codes" "${log/#$LOG_DIR\//}")"
        fail
    fi

    if [ "$found" -eq 0 ]; then
        # No test summary at all: the build broke, or the run never got that far.
        local why="no test results in log"
        if grep -qE ': error [A-Z]+[0-9]+' "$log" 2>/dev/null; then
            why="BUILD FAILED - $(grep -oE ': error [A-Z]+[0-9]+' "$log" | sort -u | tr '\n' ' ' | sed 's/: error //g')"
        fi
        record "$(printf '  %-46s %-9s  %s (exit %s) see %s' "$label" "-" "$why" "$exit_code" "${log/#$LOG_DIR\//}")"
        fail
        SUITES_RUN=$((SUITES_RUN + 1))
    fi
}

# 8.2 guard. A dirty tracked file in a consumer clone is a bug in this script.
assert_clean() {
    local repo="$1" when="$2" dirty
    dirty="$(git -C "$repo" status --porcelain 2>/dev/null)"
    if [ -n "$dirty" ]; then
        note ""
        note "!! $(basename "$repo") has local modifications $when - RULE 8.2 says these clones"
        note "!! are read-only. Nothing was committed, but investigate before trusting the run:"
        printf '%s\n' "$dirty" | sed 's/^/!!   /'
        note ""
        return 1
    fi
    return 0
}

# Proves the compiler is actually being handed the checkout's sources. Without this a
# mis-wired run reports a beautiful green against the published 1.1.1010 package.
assert_wired() {
    local label="$1" proj="$2"; shift 2
    local n
    n=$(dotnet msbuild "$proj" -getItem:Compile -nologo "$@" 2>/dev/null \
        | grep -c "$CSA_ROOT/CSharpAuthor/")
    if [ "${n:-0}" -lt 1 ]; then
        note "!! $label is NOT compiling $CSA_ROOT/CSharpAuthor - refusing to report a result."
        record "$(printf '  %-46s %-9s  NOT WIRED to %s' "$label" "-" "$CSA_ROOT")"
        fail
        SUITES_RUN=$((SUITES_RUN + 1))
        return 1
    fi
    note "   wired: $label <- $CSA_ROOT/CSharpAuthor"
    return 0
}

# 8.1 aid: DependencyModules' snapshot tests write the actual output beside the expected
# one in bin/ when they fail. Copy those out so the diff survives the next build.
harvest_received() {
    local repo="$1" dest="$LOG_DIR/received-$RUN_ID"
    local any=0 f
    while IFS= read -r f; do
        mkdir -p "$dest"
        cp "$f" "$dest/" 2>/dev/null && any=1
    done < <(find "$repo" -path '*/bin/*' -name '*.received.txt' 2>/dev/null)
    if [ "$any" -eq 1 ]; then
        note ""
        note "   SNAPSHOT DIFFS harvested to $dest"
        note "   Do NOT re-baseline (rule 8.1). Diff each against"
        note "   $repo/tests/DependencyModules.Tests/Snapshots/ and justify it in"
        note "   docs/migration-v1-v2.md."
    fi
}

# ---------------------------------------------------------------------------- consumers

DM_PROPS=()
dm_props() {
    # Discover which projects consume CSharpAuthor rather than hard-coding two names, so a
    # third generator project in DependencyModules is picked up without editing this file.
    local names name
    names="|"
    while IFS= read -r proj; do
        name="$(basename "$proj" .csproj)"
        case "$names" in *"|$name|"*) ;; *) names="$names$name|" ;; esac
    done < <(grep -rl --include='*.csproj' 'Include="CSharpAuthor"' "$DM_DIR/src" 2>/dev/null)

    if [ "$names" = "|" ]; then
        note "!! found no project in $DM_DIR/src referencing the CSharpAuthor package."
        return 1
    fi

    DM_PROPS=(
        "/p:PackageCSharpAuthorIncludeSource=false"
        "/p:CustomAfterMicrosoftCommonTargets=$INJECT_TARGETS"
        "/p:LocalCSharpAuthorRoot=$CSA_ROOT"
        "/p:LocalCSharpAuthorProjects=$names"
    )
    return 0
}

HF_PROPS=("/p:UseLocalCSharpAuthor=true" "/p:CSharpAuthorRoot=$CSA_ROOT")

run_dm() {
    local target log
    if [ "$SCOPE" = "full" ]; then
        target="$DM_DIR/DependencyModules.sln"
    else
        target="$DM_DIR/tests/DependencyModules.Tests/DependencyModules.Tests.csproj"
    fi
    log="$LOG_DIR/dm-$RUN_ID.log"

    note ""
    note "== DependencyModules ($SCOPE) =="
    assert_clean "$DM_DIR" "before the run" || true
    dm_props || { fail; SUITES_RUN=$((SUITES_RUN + 1)); return; }

    assert_wired "DependencyModules.SourceGenerator" \
        "$DM_DIR/src/DependencyModules.SourceGenerator/DependencyModules.SourceGenerator.csproj" \
        "${DM_PROPS[@]}" || return

    run_dotnet "$log" dotnet test "$target" --nologo -v q "${DM_PROPS[@]}"
    local rc=$?
    summarize_test_log "DependencyModules" "$log" "$rc"
    harvest_received "$DM_DIR"
    assert_clean "$DM_DIR" "after the run" || true
}

run_hf() {
    local target log
    if [ "$SCOPE" = "full" ]; then
        target="$HF_DIR/src/Hardened.Framework.sln"
    else
        target="$HF_DIR/src/SourceGenerators/Hardened.SourceGenerator.Tests/Hardened.SourceGenerator.Tests.csproj"
    fi
    log="$LOG_DIR/hf-$RUN_ID.log"

    note ""
    note "== Hardened.Framework ($SCOPE) =="
    assert_clean "$HF_DIR" "before the run" || true

    assert_wired "Hardened.SourceGenerator" \
        "$HF_DIR/src/SourceGenerators/Hardened.SourceGenerator/Hardened.SourceGenerator.csproj" \
        "${HF_PROPS[@]}" || return

    run_dotnet "$log" dotnet test "$target" --nologo -v q "${HF_PROPS[@]}"
    local rc=$?
    summarize_test_log "Hardened.Framework" "$log" "$rc"
    assert_clean "$HF_DIR" "after the run" || true
}

# ---------------------------------------------------------------------------- go

note "CSharpAuthor : $CSA_ROOT"
if git -C "$CSA_ROOT" rev-parse --short HEAD >/dev/null 2>&1; then
    note "               $(git -C "$CSA_ROOT" rev-parse --short HEAD) on $(git -C "$CSA_ROOT" rev-parse --abbrev-ref HEAD)$(
        [ -n "$(git -C "$CSA_ROOT" status --porcelain)" ] && printf ' (dirty)')"
fi
note "consumers    : $CONSUMERS_DIR"
note "logs         : $LOG_DIR"
note "scope        : $SCOPE   only: $ONLY"

START=$(date +%s)
if [ "$ONLY" = "dm" ] || [ "$ONLY" = "both" ]; then
    if [ -d "$DM_DIR" ]; then run_dm; else note "!! missing $DM_DIR"; fail; fi
fi
if [ "$ONLY" = "hardened" ] || [ "$ONLY" = "both" ]; then
    if [ -d "$HF_DIR" ]; then run_hf; else note "!! missing $HF_DIR"; fail; fi
fi
ELAPSED=$(( $(date +%s) - START ))

note ""
note "======================================================================================"
note "CONSUMER SUITES vs $CSA_ROOT"
note "======================================================================================"
for line in "${RESULT_LINES[@]:-}"; do [ -n "$line" ] && note "$line"; done
note "--------------------------------------------------------------------------------------"
if [ "$FAILURE_COUNT" -eq 0 ] && [ "$SUITES_RUN" -gt 0 ]; then
    note "RESULT: PASS  ($SUITES_RUN test assemblies, 0 failing)  ${ELAPSED}s"
    exit 0
fi
note "RESULT: FAIL  ($FAILURE_COUNT of $SUITES_RUN test assemblies failing or not run)  ${ELAPSED}s"
note "Logs: $LOG_DIR/*-$RUN_ID.log"
exit 1
