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
VERSION="99.0.0-bridge"
CONSUMER_ROSLYN_VERSION="4.10.0"

FAILURES=0

pass() { printf '  PASS  %s\n' "$1"; }
fail() { printf '  FAIL  %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

section() { printf '\n== %s\n' "$1"; }

cleanup() { rm -rf "$WORK"; }
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

LEAKED="$(grep -rl "Microsoft.CodeAnalysis\|CSharpAuthor.Roslyn" "$WORK/unpacked/src" 2>/dev/null || true)"

if [ -n "$LEAKED" ]; then
    fail "src/ contains Roslyn-dependent source: $LEAKED"
else
    pass "src/CSharpAuthor carries no Roslyn-dependent source"
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

public static class UsesTheBridge
{
    public static ITypeDefinition Convert(ITypeSymbol symbol) => symbol.GetTypeDefinition();

    public static IReadOnlyList<AttributeInstance> Attributes(ISymbol symbol) =>
        symbol.GetAttributeInstances();

    public static string Nested(ITypeSymbol symbol) =>
        symbol.GetTypeDefinition().GetShortName();

    public static bool NullableKind(ITypeSymbol symbol) =>
        symbol.GetTypeDefinition().IsNullableValueType();

    public static ITypeDefinition Jagged(ITypeDefinition element) =>
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
