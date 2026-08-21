#!/usr/bin/env bash
#
# Gate 9 (V2-HANDOFF.md §6/§10): run the §10 payload benchmark against an arbitrary
# CSharpAuthor checkout and print ms/file and KB/file.
#
#   scripts/run-benchmark.sh <path-to-csharpauthor-checkout> [<another-checkout> ...]
#
# Give it two or more checkouts and it measures them back to back, interleaved, in the same
# run - which is the only comparison that means anything, because absolute numbers depend on
# the machine and on what else it is doing. The first checkout listed is the baseline.
#
# It never writes to the checkouts it measures: each one's CSharpAuthor/ is copied into a
# staging directory and built there. The harness sources always come from THIS repository,
# so the payload is identical for every target.
#
# Non-interactive and idempotent: safe to run repeatedly, no network needed unless
# --with-roslyn pulls a package that is not already in the NuGet cache.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
HARNESS_SOURCE="${REPO_ROOT}/benchmarks/CSharpAuthor.Benchmark"
BASELINE_FILE="${REPO_ROOT}/benchmarks/baseline-v1.txt"

ITERATIONS=2000
WARMUP=1000
WARMUP_MS=1500
REPS=3
ROUNDS=3
SCENARIOS="tree,stringbuilder"
WITH_ROSLYN=0
KEEP=0
STRICT=0
WORKDIR=""
TARGETS=()

# The gate's bar, from V2-HANDOFF.md §6. Absolute, measured on someone else's machine.
HANDOFF_MS="0.0477"
HANDOFF_KB="78.4"
GATE_MS="0.048"
GATE_KB="78"

usage() {
    cat <<'USAGE'
usage: run-benchmark.sh [options] <checkout> [<checkout> ...]

  --iterations N     measured iterations per repetition (default 2000, the §10 figure)
  --warmup N         warmup iterations, discarded (default 1000)
  --warmup-ms N      warmup also runs at least this long, discarded (default 1500)
  --reps N           in-process repetitions per process (default 3)
  --rounds N         separate processes per target, interleaved (default 3)
  --scenarios LIST   tree,stringbuilder[,roslyn] (default tree,stringbuilder)
  --with-roslyn      build the Roslyn reference point and add it to the scenarios
  --workdir DIR      staging directory (default $TMPDIR/csharpauthor-benchmark)
  --keep             keep the staging directory
  --strict           exit non-zero when the gate verdict is FAIL
  -h, --help         this
USAGE
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --iterations) ITERATIONS="$2"; shift 2 ;;
        --warmup) WARMUP="$2"; shift 2 ;;
        --warmup-ms) WARMUP_MS="$2"; shift 2 ;;
        --reps) REPS="$2"; shift 2 ;;
        --rounds) ROUNDS="$2"; shift 2 ;;
        --scenarios) SCENARIOS="$2"; shift 2 ;;
        --with-roslyn) WITH_ROSLYN=1; shift ;;
        --workdir) WORKDIR="$2"; shift 2 ;;
        --keep) KEEP=1; shift ;;
        --strict) STRICT=1; shift ;;
        -h|--help) usage; exit 0 ;;
        -*) echo "unknown option: $1" >&2; usage >&2; exit 2 ;;
        *) TARGETS+=("$1"); shift ;;
    esac
done

if [[ ${#TARGETS[@]} -eq 0 ]]; then
    usage >&2
    exit 2
fi

if [[ ${WITH_ROSLYN} -eq 1 && "${SCENARIOS}" != *roslyn* ]]; then
    SCENARIOS="${SCENARIOS},roslyn"
fi

if [[ -z "${WORKDIR}" ]]; then
    WORKDIR="${TMPDIR:-/tmp}/csharpauthor-benchmark"
fi

rm -rf "${WORKDIR}"
mkdir -p "${WORKDIR}"

cleanup() {
    if [[ ${KEEP} -eq 0 ]]; then
        rm -rf "${WORKDIR}"
    fi
}
trap cleanup EXIT

RESULTS="${WORKDIR}/results.tsv"
: > "${RESULTS}"

copy_tree() {
    # $1 source dir, $2 destination dir. Never touches the source.
    mkdir -p "$2"
    if command -v rsync >/dev/null 2>&1; then
        rsync -a --exclude '.git/' --exclude 'bin/' --exclude 'obj/' "$1/" "$2/"
    else
        (cd "$1" && tar -cf - --exclude .git --exclude bin --exclude obj .) | (cd "$2" && tar -xf -)
    fi
}

LABELS=()
STAGES=()
TFMS=()

for index in "${!TARGETS[@]}"; do
    target="${TARGETS[$index]}"

    if [[ ! -d "${target}" ]]; then
        echo "error: not a directory: ${target}" >&2
        exit 1
    fi

    target="$(cd "${target}" && pwd)"

    if [[ ! -f "${target}/CSharpAuthor/CSharpAuthor.csproj" ]]; then
        echo "error: ${target} does not look like a CSharpAuthor checkout " \
             "(no CSharpAuthor/CSharpAuthor.csproj)" >&2
        exit 1
    fi

    label="$(basename "${target}")"
    if [[ ${#TARGETS[@]} -gt 1 ]]; then
        label="$((index + 1))-${label}"
    fi

    stage="${WORKDIR}/${label}"
    mkdir -p "${stage}"

    # The whole checkout is copied, not just CSharpAuthor/: any root-level MSBuild file, and any
    # project the library comes to reference, then applies exactly as it would in place. The
    # original is only ever read.
    copy_tree "${target}" "${stage}"

    # The harness always comes from THIS repository, overwriting any copy the target carries -
    # that is what makes the payload identical across targets.
    rm -rf "${stage}/benchmarks/CSharpAuthor.Benchmark"
    copy_tree "${HARNESS_SOURCE}" "${stage}/benchmarks/CSharpAuthor.Benchmark"

    # Stop MSBuild walking above the staging directory looking for one.
    if [[ ! -f "${stage}/Directory.Build.props" ]]; then
        printf '<Project />\n' > "${stage}/Directory.Build.props"
    fi

    # If the library multi-targets, pin one TFM so every target is compared like for like.
    tfm=""
    if grep -q '<TargetFrameworks>' "${stage}/CSharpAuthor/CSharpAuthor.csproj"; then
        multi="$(sed -n 's/.*<TargetFrameworks>\(.*\)<\/TargetFrameworks>.*/\1/p' \
                 "${stage}/CSharpAuthor/CSharpAuthor.csproj" | head -1)"
        if [[ "${multi}" == *netstandard2.0* ]]; then
            tfm="netstandard2.0"
        else
            tfm="${multi%%;*}"
        fi
    fi

    build_args=(
        "${stage}/benchmarks/CSharpAuthor.Benchmark/CSharpAuthor.Benchmark.csproj"
        -c Release --nologo -v quiet
        "-p:CSharpAuthorProject=${stage}/CSharpAuthor/CSharpAuthor.csproj"
    )
    [[ -n "${tfm}" ]] && build_args+=("-p:CSharpAuthorTargetFramework=${tfm}")
    [[ "${SCENARIOS}" == *roslyn* ]] && build_args+=("-p:IncludeRoslynReference=true")

    echo "building ${label} ..." >&2
    if ! dotnet build "${build_args[@]}" > "${stage}/build.log" 2>&1; then
        echo "error: build failed for ${target}" >&2
        tail -40 "${stage}/build.log" >&2
        exit 1
    fi

    exe="${stage}/benchmarks/CSharpAuthor.Benchmark/bin/Release/net8.0/CSharpAuthor.Benchmark.dll"
    if [[ ! -f "${exe}" ]]; then
        echo "error: harness was not produced at ${exe}" >&2
        exit 1
    fi

    resolved_tfm="${tfm}"
    if [[ -z "${resolved_tfm}" ]]; then
        resolved_tfm="$(sed -n 's/.*<TargetFramework>\(.*\)<\/TargetFramework>.*/\1/p' \
                        "${stage}/CSharpAuthor/CSharpAuthor.csproj" | head -1)"
    fi

    LABELS+=("${label}")
    STAGES+=("${stage}")
    TFMS+=("${resolved_tfm}")
    eval "SOURCE_${index}=\"\${target}\""
    eval "EXE_${index}=\"\${exe}\""
done

echo >&2
echo "CSharpAuthor benchmark - V2-HANDOFF.md §10 payload"
echo "  payload     : 1 class, 25 init-only properties, a constructor assigning all 25,"
echo "                a method with 27 statements"
echo "  iterations  : ${ITERATIONS} measured per repetition"
echo "  warmup      : ${WARMUP} iterations / ${WARMUP_MS} ms, discarded"
echo "  repetitions : ${REPS} in-process x ${ROUNDS} processes = $((REPS * ROUNDS)) per target, interleaved"
echo "  config      : Release, workstation GC (server GC off), concurrent GC on"
echo "  machine     : $(uname -s) $(uname -r) $(uname -m), $(getconf _NPROCESSORS_ONLN) CPUs, load$(uptime | sed 's/.*load average[s]*:/:/')"
echo "  sdk         : $(dotnet --version)"
echo

for index in "${!LABELS[@]}"; do
    eval "source_dir=\"\${SOURCE_${index}}\""
    eval "exe=\"\${EXE_${index}}\""
    verify="$(dotnet "${exe}" --verify --label "${LABELS[$index]}" 2>/dev/null || true)"
    chars="$(echo "${verify}" | awk -F'\t' '$3=="tree" {print $4}')"
    hash="$(echo "${verify}" | awk -F'\t' '$3=="tree" {print $5}')"
    same="$(echo "${verify}" | awk -F'\t' '$3=="identical" {print $4}')"
    echo "  target ${LABELS[$index]}"
    echo "    path            : ${source_dir}"
    echo "    library tfm     : ${TFMS[$index]}"
    echo "    generated file  : ${chars} chars, hash ${hash} (byte-identical to the StringBuilder reference: ${same})"
done
echo

for ((round = 1; round <= ROUNDS; round++)); do
    for index in "${!LABELS[@]}"; do
        eval "exe=\"\${EXE_${index}}\""
        echo "round ${round}/${ROUNDS}: ${LABELS[$index]}" >&2
        dotnet "${exe}" \
            --label "${LABELS[$index]}" \
            --iterations "${ITERATIONS}" \
            --warmup "${WARMUP}" \
            --warmup-ms "${WARMUP_MS}" \
            --reps "${REPS}" \
            --scenarios "${SCENARIOS}" \
            --quiet >> "${RESULTS}"
    done
done

# RESULT<TAB>label<TAB>scenario<TAB>rep<TAB>iterations<TAB>median<TAB>trimmed<TAB>mean
#       <TAB>min<TAB>max<TAB>stddev<TAB>p95<TAB>wall<TAB>kb<TAB>chars<TAB>hash<TAB>gc
SUMMARY="${WORKDIR}/summary.tsv"

awk -F'\t' '
    $1 == "RESULT" {
        key = $2 SUBSEP $3
        if (!(key in n)) { order[++count] = key; label[key] = $2; scenario[key] = $3 }
        n[key]++
        med[key] += $6; trim[key] += $7; mean[key] += $8; kb[key] += $14
        if (!(key in lo) || $6 < lo[key]) lo[key] = $6
        if (!(key in hi) || $6 > hi[key]) hi[key] = $6
        if (!(key in kblo) || $14 < kblo[key]) kblo[key] = $14
        if (!(key in kbhi) || $14 > kbhi[key]) kbhi[key] = $14
    }
    END {
        for (i = 1; i <= count; i++) {
            key = order[i]
            m = med[key] / n[key]
            spread = (m > 0) ? (hi[key] - lo[key]) / m * 100 : 0
            printf "%s\t%s\t%d\t%.4f\t%.4f\t%.4f\t%.4f\t%.4f\t%.1f\t%.1f\t%.1f\t%.1f\n",
                label[key], scenario[key], n[key], m, trim[key] / n[key], mean[key] / n[key],
                lo[key], hi[key], spread, kb[key] / n[key], kblo[key], kbhi[key]
        }
    }
' "${RESULTS}" > "${SUMMARY}"

printf '%-22s %-14s %5s %10s %10s %10s %10s %10s %8s %10s\n' \
    target scenario runs "ms/file" trimmed mean "min" "max" "spread" "KB/file"
printf '%.0s-' {1..126}; echo

while IFS=$'\t' read -r label scenario runs median trimmed mean lo hi spread kbmean kblo kbhi; do
    printf '%-22s %-14s %5s %10s %10s %10s %10s %10s %7s%% %10s\n' \
        "${label}" "${scenario}" "${runs}" "${median}" "${trimmed}" "${mean}" "${lo}" "${hi}" "${spread}" "${kbmean}"
done < "${SUMMARY}"

echo
echo "ms/file is the median per-iteration time, averaged over the ${ROUNDS}x${REPS} repetitions;"
echo "min/max are the lowest and highest of those repetition medians, and spread is their range."
echo "The mean column is dominated by a few multi-millisecond outliers whenever the machine is"
echo "busy, so the median is the number to compare. KB/file is a GC.GetAllocatedBytesForCurrentThread()"
echo "delta per iteration and is essentially noise-free."
echo

if [[ "${SCENARIOS}" == *roslyn* ]]; then
    echo "NOTE: the roslyn reference point allocates ~40x what the tree does, so interleaving it"
    echo "widens the spread on every other row. Run the gate without --with-roslyn."
    echo
fi

base_ms="$(awk -F'\t' -v l="${LABELS[0]}" '$1==l && $2=="tree" {print $4}' "${SUMMARY}")"
base_kb="$(awk -F'\t' -v l="${LABELS[0]}" '$1==l && $2=="tree" {print $10}' "${SUMMARY}")"
base_spread="$(awk -F'\t' -v l="${LABELS[0]}" '$1==l && $2=="tree" {print $9}' "${SUMMARY}")"

verdict=0

echo "GATE 9 - perf: no worse than V1"
echo

if [[ ${#LABELS[@]} -eq 1 ]]; then
    echo "  ${LABELS[0]}: ${base_ms} ms/file, ${base_kb} KB/file (spread ${base_spread}%)"
    echo
    if [[ -f "${BASELINE_FILE}" ]]; then
        echo "  recorded V1 baseline on this machine (benchmarks/baseline-v1.txt):"
        sed 's/^/    /' "${BASELINE_FILE}"
        echo
    fi
    echo "  Only one checkout was given, so this is NOT a gate verdict. The bar is relative:"
    echo "  pass both a V1 and a V2 checkout to have them measured back to back in one run."
    echo "  The handoff's absolute figures (${HANDOFF_MS} ms / ${HANDOFF_KB} KB, bar ${GATE_MS} ms / ${GATE_KB} KB)"
    echo "  were measured on different hardware and do not transfer."
else
    for index in "${!LABELS[@]}"; do
        [[ ${index} -eq 0 ]] && continue

        label="${LABELS[$index]}"
        ms="$(awk -F'\t' -v l="${label}" '$1==l && $2=="tree" {print $4}' "${SUMMARY}")"
        kb="$(awk -F'\t' -v l="${label}" '$1==l && $2=="tree" {print $10}' "${SUMMARY}")"

        read -r ms_delta kb_delta status <<<"$(awk -v a="${base_ms}" -v b="${ms}" \
            -v c="${base_kb}" -v d="${kb}" 'BEGIN {
                md = (b - a) / a * 100
                kd = (d - c) / c * 100
                printf "%+.1f %+.1f %s", md, kd, (md <= 5.0 && kd <= 1.0) ? "PASS" : "FAIL"
            }')"

        echo "  ${label} vs ${LABELS[0]} (baseline):"
        echo "    ms/file : ${base_ms} -> ${ms}  (${ms_delta}%)"
        echo "    KB/file : ${base_kb} -> ${kb}  (${kb_delta}%)"
        echo "    verdict : ${status}   [pass = time within +5% and allocation within +1% of the baseline]"
        echo

        [[ "${status}" == "FAIL" ]] && verdict=3
    done

    echo "  The handoff's ${HANDOFF_MS} ms / ${HANDOFF_KB} KB were measured on other hardware; the numbers above"
    echo "  are this machine's, taken in one run under the same load, which is what the gate is."
fi

if [[ ${STRICT} -eq 1 ]]; then
    exit ${verdict}
fi

exit 0
