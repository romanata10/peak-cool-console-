#!/usr/bin/env bash
set -euo pipefail
if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "Usage: $0 /path/to/PEAK [/path/to/profile/BepInEx/core]" >&2
  exit 1
fi
root="$(cd "$(dirname "$0")" && pwd)"
peak_dir="$(cd "$1" && pwd)"
bep_core="${2:-$peak_dir/BepInEx/core}"
bep_core="$(cd "$bep_core" && pwd)"
command -v dotnet >/dev/null || { echo 'dotnet SDK is required.' >&2; exit 1; }
command -v python3 >/dev/null || { echo 'python3 is required.' >&2; exit 1; }
dotnet build "$root/src/PeakCoolConsole.csproj" -c Release -p:PeakDir="$peak_dir" -p:BepInExCore="$bep_core"
python3 "$root/package/build_zip.py" "$root/src/bin/Release/netstandard2.1/PeakCoolConsole.dll" "$root/PeakCoolConsoleHelper-0.4.0.zip"
