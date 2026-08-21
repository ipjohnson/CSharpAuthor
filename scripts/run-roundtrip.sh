#!/usr/bin/env bash
#
# V2-HANDOFF.md 9(b): round-trip fidelity against a corpus of real C#.
#
#   scripts/run-roundtrip.sh <path-to-csharpauthor-checkout> [--corpus own|dm|hardened|all]
#
#     source file -> Roslyn parse -> import to CSharpAuthor tree -> emit -> Roslyn parse
#                  -> compare the two trees for structural equivalence
#
# Prints "files round-tripping / files attempted" plus a histogram of the node kinds that
# failed, split into three buckets that are never conflated:
#
#     (a) the importer could not build a tree for a node kind
#     (b) a tree was built but the emitted text does not re-parse
#     (c) the re-parsed tree differs structurally from the original
#
# Non-interactive and idempotent. It never writes to the checkout it measures, not even an
# obj/ directory: CSharpAuthor/ and proto/ are copied into a staging directory and built
# there, the importer is regenerated into THIS repository, and the corpus is only ever read.
#
# The measurement is capped by the parser: Microsoft.CodeAnalysis.CSharp 4.14.0 knows
# language versions only up to C# 13, so nothing above C# 13 is validated here.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
TOOL_DIR="${REPO_ROOT}/tools/roundtrip"
PROBE_DIR="${TOOL_DIR}/RoslynProbe"
HARNESS_DIR="${TOOL_DIR}/CSharpAuthor.RoundTrip"
GEN_DIR="${HARNESS_DIR}/Generated"

CORPUS="own"
LAYER="proto,rt"
TYPES="model"
RT_SPACING="gen_all"
OUT=""
EMIT_DIR=""
DUMP_FIRST=0
CONSUMERS=""
REGEN=1
KEEP=0
WORKDIR=""
TARGET=""

usage() {
    cat <<'USAGE'
usage: run-roundtrip.sh <path-to-csharpauthor-checkout> [options]

  --corpus SET       own | dm | hardened | all   (default own)
                     own      the checkout's own CSharpAuthor/**/*.cs
                     dm       consumers/DependencyModules
                     hardened consumers/Hardened.Framework
  --layer LIST       proto | rt | proto,rt   (default proto,rt)
                     proto  the node layer as committed - THE HEADLINE NUMBER
                     rt     a complete layer generated from the same Syntax.xml with
                            absent optional tokens representable - the diagnostic ceiling
  --types MODE       model | verbatim   (default model)
                     model     TypeSyntax must fit ITypeDefinition; what it cannot hold fails
                     verbatim  carry the type's text through, to separate emitter failures
                               from type-model failures
  --rt-spacing P     gen_all | identifier-aware   (default gen_all)
                     applies to the rt layer only; quantifies what fixing the spacing rule
                     is worth
  --consumers DIR    where DependencyModules/ and Hardened.Framework/ live
                     (default: nearest `consumers` directory walking up from the checkout)
  --emit-dir DIR     write what the emitter produced, one file per source file
  --dump-first N     print the first N failing files with their reasons
  --out FILE         also write the report to FILE
  --no-regen         skip regenerating the importer (faster; only safe if Syntax.xml,
                     nodes.json and the node layer have not changed)
  --workdir DIR      staging directory (default $TMPDIR/csharpauthor-roundtrip)
  --keep             keep the staging directory after the run
  -h, --help         this
USAGE
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --corpus)      CORPUS="$2"; shift 2 ;;
        --layer)       LAYER="$2"; shift 2 ;;
        --types)       TYPES="$2"; shift 2 ;;
        --rt-spacing)  RT_SPACING="$2"; shift 2 ;;
        --consumers)   CONSUMERS="$2"; shift 2 ;;
        --emit-dir)    EMIT_DIR="$2"; shift 2 ;;
        --dump-first)  DUMP_FIRST="$2"; shift 2 ;;
        --out)         OUT="$2"; shift 2 ;;
        --no-regen)    REGEN=0; shift ;;
        --workdir)     WORKDIR="$2"; shift 2 ;;
        --keep)        KEEP=1; shift ;;
        -h|--help)     usage; exit 0 ;;
        -*)            echo "unknown option: $1" >&2; usage; exit 2 ;;
        *)             TARGET="$1"; shift ;;
    esac
done

if [[ -z "${TARGET}" ]]; then
    echo "error: no CSharpAuthor checkout given" >&2
    usage
    exit 2
fi

TARGET="$(cd "${TARGET}" && pwd)"

if [[ ! -f "${TARGET}/CSharpAuthor/CSharpAuthor.csproj" ]]; then
    echo "error: ${TARGET} is not a CSharpAuthor checkout (no CSharpAuthor/CSharpAuthor.csproj)" >&2
    exit 2
fi

echo "checkout under measurement : ${TARGET}"

# ---------------------------------------------------------------------------
# 0. Stage the checkout. Building a ProjectReference inside it would leave obj/ and bin/
#    behind, and the checkout may belong to someone else. Copy what is compiled, read the
#    corpus in place.
# ---------------------------------------------------------------------------
WORKDIR="${WORKDIR:-${TMPDIR:-/tmp}/csharpauthor-roundtrip}"
STAGE="${WORKDIR}/stage"
rm -rf "${STAGE}"
mkdir -p "${STAGE}"
for d in CSharpAuthor proto; do
    [[ -d "${TARGET}/${d}" ]] || continue
    (cd "${TARGET}" && tar cf - --exclude=obj --exclude=bin --exclude=.git "${d}") \
        | (cd "${STAGE}" && tar xf -)
done
[[ -f "${STAGE}/CSharpAuthor/CSharpAuthor.csproj" ]] || {
    echo "error: staging ${TARGET} failed" >&2; exit 1
}
echo "staged for building        : ${STAGE}"

# ---------------------------------------------------------------------------
# 1. Ask the referenced Roslyn what it actually knows. proto/grammar/Syntax.xml runs ahead
#    of the parser package, so some grammar nodes and fields cannot appear in a parsed tree.
#    Measuring that rather than assuming it is what keeps the C# 13 ceiling honest.
# ---------------------------------------------------------------------------
if [[ ${REGEN} -eq 1 || ! -f "${GEN_DIR}/roslyn-nodes.txt" ]]; then
    mkdir -p "${GEN_DIR}"
    echo -n "roslyn probe               : "
    dotnet run -c Release --project "${PROBE_DIR}" -- "${GEN_DIR}/roslyn-nodes.txt" \
        | tail -1
fi

# ---------------------------------------------------------------------------
# 2. Generate the importer - the same field walk gen_all.py uses, inverted.
# ---------------------------------------------------------------------------
if [[ ${REGEN} -eq 1 ]]; then
    echo "regenerating importer      : python3 tools/roundtrip/gen_roundtrip.py --repo ${STAGE} \\"
    echo "                               --roslyn-nodes ${GEN_DIR}/roslyn-nodes.txt --rt-spacing ${RT_SPACING}"
    python3 "${TOOL_DIR}/gen_roundtrip.py" \
        --repo "${STAGE}" \
        --roslyn-nodes "${GEN_DIR}/roslyn-nodes.txt" \
        --rt-spacing "${RT_SPACING}" 2>&1 | sed 's/^/  /'
fi

# ---------------------------------------------------------------------------
# 3. Build against that checkout. If the node layer has been promoted out of
#    proto/grammar/ into the library, it arrives through the ProjectReference and must not
#    be compiled a second time.
# ---------------------------------------------------------------------------
BUILD_ARGS=(-c Release --nologo "-p:CSharpAuthorRepo=${STAGE}")
if [[ ! -f "${STAGE}/proto/grammar/Nodes.cs" ]]; then
    echo "node layer                 : not in proto/grammar - taking it from the library reference"
    BUILD_ARGS+=("-p:ProtoNodesFile=none")
fi

echo "building harness           : dotnet build ${HARNESS_DIR}"
dotnet build "${HARNESS_DIR}" "${BUILD_ARGS[@]}" > /tmp/roundtrip-build.$$ 2>&1 || {
    echo "BUILD FAILED - the importer no longer matches the node layer it measures:" >&2
    grep -E "error" /tmp/roundtrip-build.$$ | head -30 >&2
    rm -f /tmp/roundtrip-build.$$
    exit 1
}
rm -f /tmp/roundtrip-build.$$

# ---------------------------------------------------------------------------
# 4. Run.
# ---------------------------------------------------------------------------
RUN_ARGS=(--repo "${TARGET}" --corpus "${CORPUS}" --layer "${LAYER}" --types "${TYPES}")
[[ -n "${CONSUMERS}" ]] && RUN_ARGS+=(--consumers "${CONSUMERS}")
[[ -n "${EMIT_DIR}"  ]] && RUN_ARGS+=(--emit-dir "${EMIT_DIR}")
[[ -n "${OUT}"       ]] && RUN_ARGS+=(--out "${OUT}")
[[ "${DUMP_FIRST}" != "0" ]] && RUN_ARGS+=(--dump-first "${DUMP_FIRST}")

echo
set +e
dotnet run -c Release --project "${HARNESS_DIR}" --no-build "-p:CSharpAuthorRepo=${STAGE}" \
    $( [[ ! -f "${STAGE}/proto/grammar/Nodes.cs" ]] && echo "-p:ProtoNodesFile=none" ) \
    -- "${RUN_ARGS[@]}"
STATUS=$?
set -e

[[ ${KEEP} -eq 1 ]] || rm -rf "${STAGE}"

# Exit 0 only when every attempted file round-tripped. A partial pass is a real number, not
# a green light, so it must not look like one to a caller that checks the exit code.
exit ${STATUS}
