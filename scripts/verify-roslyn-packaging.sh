#!/usr/bin/env bash
#
# Proves the Roslyn bridge ships the way the packaging constraint requires:
#
#   1. the library itself builds with no Roslyn reference
#   2. the package carries the bridge in its own source folder, and NOT in the folder that
#      PackageCSharpAuthorIncludeSource compiles into every consumer
#   3. a project that opts out gets the library and cannot see the bridge at all
#   4. a project that opts in gets the bridge and compiles it - against the Roslyn version the
#      real consumers pin (4.10.0), on netstandard2.0, at LangVersion 10, with
#      TreatWarningsAsErrors and EnforceExtendedAnalyzerRules on, which is the strictest
#      configuration either consumer builds under
#
# Usage: scripts/verify-roslyn-packaging.sh [path-to-checkout]

set -uo pipefail

ROOT="${1:-$(cd "$(dirname "$0")/.." && pwd)}"
WORK="$(mktemp -d)"
# A fixed probe version is a false pass waiting to happen: NuGet caches the extracted package
# under ~/.nuget/packages/csharpauthor/<version>, so a second run silently builds against the
# FIRST run's content. That is exactly how a real break here passed locally and only failed in
# CI, on a clean runner. The version is unique per run, and the extracted copy is removed after.
VERSION="99.0.0-bridge$$"
CONSUMER_ROSLYN_VERSION="4.10.0"

FAILURES=0

pass() { printf '  PASS  %s\n' "$1"; }
fail() { printf '  FAIL  %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

section() { printf '\n== %s\n' "$1"; }

cleanup() {
    rm -rf "$WORK"
    # The probe package extracts into the NuGet cache. Leaving it there is how a stale copy
    # silently satisfies the next run - which is exactly how a real break passed here locally
    # and only failed on a clean CI runner.
    [ -n "${VERSION:-}" ] && rm -rf "$HOME/.nuget/packages/csharpauthor/$VERSION" 2>/dev/null || true
}
trap cleanup EXIT

section "1. the library builds with no Roslyn reference"

if grep -qE "<PackageReference[^>]*Microsoft\.CodeAnalysis" "$ROOT/CSharpAuthor/CSharpAuthor.csproj"; then
    fail "CSharpAuthor.csproj declares a Microsoft.CodeAnalysis PackageReference"
else
    pass "CSharpAuthor.csproj declares no Roslyn PackageReference"
fi

# Direct or transitive - the resolved graph is what would be imposed on a consumer.
dotnet list "$ROOT/CSharpAuthor/CSharpAuthor.csproj" package --include-transitive \
    > "$WORK/library-packages.log" 2>&1

if grep -qi "CodeAnalysis" "$WORK/library-packages.log"; then
    fail "the library resolves a Roslyn package"
    grep -i "CodeAnalysis" "$WORK/library-packages.log"
else
    pass "the library's resolved package graph contains no Roslyn"
fi

if dotnet build "$ROOT/CSharpAuthor/CSharpAuthor.csproj" -c Release -v quiet --nologo > "$WORK/library-build.log" 2>&1; then
    pass "dotnet build CSharpAuthor.csproj (netstandard2.0)"
else
    fail "dotnet build CSharpAuthor.csproj"
    tail -20 "$WORK/library-build.log"
fi

section "2. the package puts the bridge in its own folder"

if dotnet pack "$ROOT/CSharpAuthor/CSharpAuthor.csproj" -c Release -o "$WORK/packages" \
    "-p:Version=$VERSION" -v quiet --nologo > "$WORK/pack.log" 2>&1; then
    pass "dotnet pack"
else
    fail "dotnet pack"
    tail -20 "$WORK/pack.log"
    exit 1
fi

mkdir -p "$WORK/unpacked"
unzip -o -q "$WORK/packages/CSharpAuthor.$VERSION.nupkg" -d "$WORK/unpacked"

# What counts as a leak is a *code* reference - a using directive, a qualified type name, an
# alias - not the words "Microsoft.CodeAnalysis" in a comment. Four shipped files
# (BaseTypeDefinition.cs, Profiles/LanguageVersion.cs, Profiles/EmitProfile.cs,
# Profiles/ProfileEmitter.cs) explain their relationship to Roslyn in <c>...</c> precisely
# because the bridge is a separate source folder, and a bare `grep -rl` reported all four and
# called the package broken. roslyn-code-refs.py blanks comments, string literals and char
# literals first, so what it matches on is code. Its negative control runs below: a check that
# never fails is not a check.
roslyn_code_refs() {
    # roslyn_code_refs <dir> - one `path:line:source` row per real code reference
    find "$1" -name '*.cs' -print0 2>/dev/null \
        | xargs -0 python3 "$ROOT/scripts/roslyn-code-refs.py" 2>/dev/null
}

if ! command -v python3 >/dev/null 2>&1; then
    fail "python3 not on PATH - scripts/roslyn-code-refs.py cannot run, so this check is unproven"
else
    LEAKED="$(roslyn_code_refs "$WORK/unpacked/src")"

    if [ -n "$LEAKED" ]; then
        fail "src/ contains Roslyn-dependent code"
        printf '%s\n' "$LEAKED" | sed "s|$WORK/unpacked/||; s|^|          |"
    else
        pass "src/CSharpAuthor carries no Roslyn-dependent code ($(find "$WORK/unpacked/src" -name '*.cs' | wc -l | tr -d ' ') files scanned)"
    fi

    # Negative control. The check above passes on a package that is fine, which proves nothing
    # on its own - a check that always passes reads the same. Plant an actual leak and a piece
    # of pure prose in a scratch tree, and require it to tell them apart.
    mkdir -p "$WORK/leak-probe"

    cat > "$WORK/leak-probe/PlantedLeak.cs" <<'EOF'
using Microsoft.CodeAnalysis;

namespace CSharpAuthor;

internal static class PlantedLeak
{
    internal static string Name(ISymbol symbol) => symbol.Name;
}
EOF

    cat > "$WORK/leak-probe/PureProse.cs" <<'EOF'
namespace CSharpAuthor;

/// <summary>
/// Mentions <c>Microsoft.CodeAnalysis</c> and <c>CSharpAuthor.Roslyn</c> in prose, the way the
/// four shipped files that explain the bridge do. This must not register as a dependency.
/// </summary>
internal static class PureProse
{
    // Microsoft.CodeAnalysis in a line comment, and "CSharpAuthor.Roslyn" in a string.
    internal const string Note = "Microsoft.CodeAnalysis";
}
EOF

    PLANTED="$(roslyn_code_refs "$WORK/leak-probe")"

    if printf '%s' "$PLANTED" | grep -q "PlantedLeak.cs"; then
        if printf '%s' "$PLANTED" | grep -q "PureProse.cs"; then
            fail "the check flags prose as a leak - the false positive is back"
            printf '%s\n' "$PLANTED" | sed "s|$WORK/||; s|^|          |"
        else
            pass "negative control: a planted Roslyn reference fails, pure prose does not"
        fi
    else
        fail "negative control: a planted Roslyn reference went UNDETECTED - the check is blind"
    fi
fi

BRIDGE_COUNT="$(find "$WORK/unpacked/srcRoslyn" -name '*.cs' 2>/dev/null | wc -l | tr -d ' ')"
SOURCE_COUNT="$(find "$WORK/unpacked/src" -name '*.cs' 2>/dev/null | wc -l | tr -d ' ')"

if [ "$BRIDGE_COUNT" -gt 0 ]; then
    pass "srcRoslyn/CSharpAuthor.Roslyn carries $BRIDGE_COUNT bridge files ($SOURCE_COUNT in src/CSharpAuthor)"
else
    fail "srcRoslyn is empty"
fi

if grep -q "PackageCSharpAuthorIncludeRoslyn" "$WORK/unpacked/build/CSharpAuthor.targets"; then
    pass "build/CSharpAuthor.targets carries the sibling gate"
else
    fail "build/CSharpAuthor.targets has no PackageCSharpAuthorIncludeRoslyn gate"
fi

cat > "$WORK/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="$WORK/packages" />
  </packageSources>
</configuration>
EOF

write_project() {
    local dir="$1"
    local include_roslyn="$2"

    mkdir -p "$dir"
    cp "$WORK/nuget.config" "$dir/nuget.config"

    cat > "$dir/Probe.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>10</LangVersion>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <!-- The four the consumers already suppress for the V1 sources they include. Anything the
         bridge adds beyond these fails this build. -->
    <NoWarn>\$(NoWarn);CS8603;CS8604;CS8765;CS8767;NU5128</NoWarn>
    <PackageCSharpAuthorIncludeSource>true</PackageCSharpAuthorIncludeSource>
    <PackageCSharpAuthorIncludeRoslyn>$include_roslyn</PackageCSharpAuthorIncludeRoslyn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CSharpAuthor" Version="$VERSION">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>build</IncludeAssets>
    </PackageReference>
  </ItemGroup>
EOF

    if [ "$include_roslyn" = "true" ]; then
        cat >> "$dir/Probe.csproj" <<EOF
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="$CONSUMER_ROSLYN_VERSION" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
EOF
    fi

    echo "</Project>" >> "$dir/Probe.csproj"
}

section "3. a project that opts out cannot see the bridge"

write_project "$WORK/optout" "false"

cat > "$WORK/optout/UsesTheLibrary.cs" <<'EOF'
using CSharpAuthor;

public static class UsesTheLibrary
{
    public static string Name() => TypeDefinition.Get("N", "Thing").GetShortName();
}
EOF

if dotnet build "$WORK/optout/Probe.csproj" -v quiet --nologo > "$WORK/optout-build.log" 2>&1; then
    pass "opt-out project builds against the source-included library"
else
    fail "opt-out project does not build"
    tail -20 "$WORK/optout-build.log"
fi

cat > "$WORK/optout/WantsTheBridge.cs" <<'EOF'
using CSharpAuthor.Roslyn;

public static class WantsTheBridge
{
    public static bool Probe() => typeof(SymbolTypeExtensions) != null;
}
EOF

if dotnet build "$WORK/optout/Probe.csproj" -v quiet --nologo > "$WORK/optout-negative.log" 2>&1; then
    fail "opt-out project compiled a reference to CSharpAuthor.Roslyn - the bridge leaked"
else
    if grep -q "CS0246\|CS0234" "$WORK/optout-negative.log"; then
        pass "opt-out project cannot name CSharpAuthor.Roslyn (namespace absent)"
    else
        fail "opt-out project failed for an unexpected reason"
        tail -20 "$WORK/optout-negative.log"
    fi
fi

rm "$WORK/optout/WantsTheBridge.cs"

section "4. a project that opts in gets the bridge"

write_project "$WORK/optin" "true"

cat > "$WORK/optin/UsesTheBridge.cs" <<'EOF'
using System.Collections.Generic;
using CSharpAuthor;
using CSharpAuthor.Roslyn;
using Microsoft.CodeAnalysis;

// The core namespace stays public in a source-including consumer - 1.x published it and hiding
// it now would be the breaking change - so a consumer may still expose ITypeDefinition from its
// own public API.
public static class UsesTheBridge
{
    public static ITypeDefinition Convert(ITypeSymbol symbol) => symbol.GetTypeDefinition();

    public static string Nested(ITypeSymbol symbol) =>
        symbol.GetTypeDefinition().GetShortName();

    public static bool NullableKind(ITypeSymbol symbol) =>
        symbol.GetTypeDefinition().IsNullableValueType();
}

// The bridge's own types are internal here, by design: CSHARPAUTHOR_PUBLIC_API is defined only
// when CSharpAuthor builds itself, so a consumer uses them freely without republishing them as
// part of its own surface. This half of the probe is what proves that is still usable - and
// making either class public would be CS0050, which is the contract working.
internal static class UsesTheBridgeInternally
{
    internal static IReadOnlyList<AttributeInstance> Attributes(ISymbol symbol) =>
        symbol.GetAttributeInstances();

    internal static ITypeDefinition Jagged(ITypeDefinition element) =>
        new ArrayTypeDefinition(new ArrayTypeDefinition(element, 2), 1);
}
EOF

if dotnet build "$WORK/optin/Probe.csproj" -v quiet --nologo > "$WORK/optin-build.log" 2>&1; then
    pass "opt-in project compiles the bridge (netstandard2.0, LangVersion 10, Roslyn $CONSUMER_ROSLYN_VERSION)"
    pass "no warning beyond the four V1 already needs (TreatWarningsAsErrors on)"
    pass "no RS1035 from EnforceExtendedAnalyzerRules"
else
    fail "opt-in project does not build"
    tail -40 "$WORK/optin-build.log"
fi

printf '\n'

if [ "$FAILURES" -eq 0 ]; then
    printf 'ALL PACKAGING CHECKS PASSED\n'
    exit 0
fi

printf '%s PACKAGING CHECK(S) FAILED\n' "$FAILURES"
exit 1
