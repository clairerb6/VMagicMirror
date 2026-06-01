#!/usr/bin/env bash
set -euo pipefail

# Restore the minimum NuGet binaries required by com.cysharp.r3's Unity asmdef.
# This avoids relying on NuGetForUnity UI in headless/Linux environments.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_DIR="$ROOT_DIR/VMagicMirror"
OUT_DIR="$PROJECT_DIR/Assets/Plugins/R3"
TMP_DIR="$ROOT_DIR/.tmp/nuget-r3"

mkdir -p "$OUT_DIR" "$TMP_DIR"

fetch_nupkg() {
  local package_id="$1"
  local version="$2"
  local lower_id
  lower_id="$(echo "$package_id" | tr '[:upper:]' '[:lower:]')"
  local url="https://api.nuget.org/v3-flatcontainer/${lower_id}/${version}/${lower_id}.${version}.nupkg"
  local nupkg="$TMP_DIR/${lower_id}.${version}.nupkg"
  echo "Downloading ${package_id} ${version}..."
  curl -fsSL "$url" -o "$nupkg"
}

extract_first_match() {
  local nupkg="$1"
  local pattern="$2"
  local out_file="$3"
  local entry
  entry="$(unzip -Z1 "$nupkg" | rg -m1 "$pattern" || true)"
  if [[ -z "$entry" ]]; then
    echo "ERROR: Could not find '$pattern' in $(basename "$nupkg")" >&2
    return 1
  fi
  unzip -p "$nupkg" "$entry" > "$out_file"
}

extract_prefer_netstandard20() {
  local nupkg="$1"
  local exact_path="$2"
  local fallback_pattern="$3"
  local out_file="$4"

  if unzip -Z1 "$nupkg" | rg -q "^${exact_path}$"; then
    unzip -p "$nupkg" "$exact_path" > "$out_file"
    return 0
  fi

  extract_first_match "$nupkg" "$fallback_pattern" "$out_file"
}

fetch_nupkg "R3" "1.3.0"
fetch_nupkg "Microsoft.Bcl.TimeProvider" "8.0.0"
fetch_nupkg "Microsoft.Bcl.AsyncInterfaces" "6.0.0"

extract_prefer_netstandard20 "$TMP_DIR/r3.1.3.0.nupkg" \
  "lib/netstandard2.0/R3.dll" \
  'lib/(netstandard2\.1|net8\.0|net6\.0)/R3\.dll$' \
  "$OUT_DIR/R3.dll"

extract_prefer_netstandard20 "$TMP_DIR/microsoft.bcl.timeprovider.8.0.0.nupkg" \
  "lib/netstandard2.0/Microsoft.Bcl.TimeProvider.dll" \
  'lib/(net462|net8\.0)/Microsoft\.Bcl\.TimeProvider\.dll$' \
  "$OUT_DIR/Microsoft.Bcl.TimeProvider.dll"

extract_prefer_netstandard20 "$TMP_DIR/microsoft.bcl.asyncinterfaces.6.0.0.nupkg" \
  "lib/netstandard2.0/Microsoft.Bcl.AsyncInterfaces.dll" \
  'lib/(netstandard2\.1|net461)/Microsoft\.Bcl\.AsyncInterfaces\.dll$' \
  "$OUT_DIR/Microsoft.Bcl.AsyncInterfaces.dll"

echo "Restored binaries into: $OUT_DIR"
ls -lh "$OUT_DIR"/R3.dll "$OUT_DIR"/Microsoft.Bcl.TimeProvider.dll "$OUT_DIR"/Microsoft.Bcl.AsyncInterfaces.dll
