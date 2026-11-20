#!/usr/bin/env python3
import pathlib
import sys
import zipfile

root = pathlib.Path(__file__).resolve().parent
if len(sys.argv) != 3:
    raise SystemExit('Usage: build_zip.py plugin.dll output.zip')
dll, output = map(pathlib.Path, sys.argv[1:])
if not dll.is_file() or dll.stat().st_size == 0:
    raise SystemExit(f'Built plugin DLL missing or empty: {dll}')
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in ('manifest.json', 'README.md', 'icon.png'):
        archive.write(root / name, name)
    archive.write(dll, 'plugins/PeakCoolConsole.dll')
print(f'Created {output}')
