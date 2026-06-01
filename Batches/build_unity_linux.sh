#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   ./Batches/build_unity_linux.sh [full|standard] [prod|dev] [unity_path]
# Example:
#   ./Batches/build_unity_linux.sh full dev "/opt/Unity/Hub/Editor/6000.0.58f2/Editor/Unity"

APP_EDITION_INPUT="${1:-full}"
APP_ENV_INPUT="${2:-prod}"
UNITY_EXE_INPUT="${3:-/opt/Unity/Hub/Editor/6000.0.58f2/Editor/Unity}"

if [[ "$APP_EDITION_INPUT" == "standard" ]]; then
  APP_EDITION="Standard"
else
  APP_EDITION="Full"
fi

if [[ "$APP_ENV_INPUT" == "dev" ]]; then
  APP_ENV="Dev"
else
  APP_ENV="Prod"
fi

if [[ "$APP_EDITION_INPUT" == "standard" ]]; then
  if [[ "$APP_ENV_INPUT" == "dev" ]]; then
    BIN_FOLDER="Bin_Standard_Dev_Linux"
  else
    BIN_FOLDER="Bin_Standard_Linux"
  fi
else
  if [[ "$APP_ENV_INPUT" == "dev" ]]; then
    BIN_FOLDER="Bin_Dev_Linux"
  else
    BIN_FOLDER="Bin_Linux"
  fi
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ_PATH="$(cd "$SCRIPT_DIR/../VMagicMirror" && pwd)"
SAVE_PATH="$(cd "$SCRIPT_DIR/.." && pwd)/$BIN_FOLDER"
UNITY_EXE="$UNITY_EXE_INPUT"

if [[ ! -x "$UNITY_EXE" ]]; then
  echo "Unity executable not found or not executable: $UNITY_EXE" >&2
  exit 1
fi

echo "$(date +%T) Build Unity Linux: setup script symbols"
"$UNITY_EXE" \
  -batchmode -nographics -quit \
  -projectPath "$PROJ_PATH" \
  -SavePath="$SAVE_PATH" \
  -Edition="$APP_EDITION" \
  -Env="$APP_ENV" \
  -Target=Linux \
  -executeMethod Baku.VMagicMirror.BuildHelper.DoPrepareScriptDefineSymbol

echo "$(date +%T) Sleep 5 sec to let symbols settle"
sleep 5

echo "$(date +%T) Build Unity Linux: run build"
"$UNITY_EXE" \
  -batchmode -nographics -quit \
  -projectPath "$PROJ_PATH" \
  -SavePath="$SAVE_PATH" \
  -Edition="$APP_EDITION" \
  -Env="$APP_ENV" \
  -Target=Linux \
  -executeMethod Baku.VMagicMirror.BuildHelper.DoBuild

echo "Build finished. Output folder: $SAVE_PATH"
