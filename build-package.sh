#!/usr/bin/env bash
set -euo pipefail
if [ "$#" -ne 1 ]; then echo "Usage: $0 /path/to/PEAK" >&2; exit 1; fi
root="$(cd "$(dirname "$0")" && pwd)"
peak_dir="$(cd "$1" && pwd)"
dotnet build "$root/src/PeakCoolConsole.csproj" -c Release -p:PeakDir="$peak_dir"
python3 "$root/package/build_zip.py" "$root/src/bin/Release/netstandard2.1/PeakCoolConsole.dll" "$root/PeakCoolConsoleHelper-0.3.0.zip"
