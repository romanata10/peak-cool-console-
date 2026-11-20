#!/usr/bin/env python3
"""Extract public command signatures (not bodies) from Thunderstore's 1.1.9 analysis."""
import pathlib
import re
import urllib.request

url = ('https://ccdn.thunderstore.io/live/blob-storage/sha256/'
       'aa0e34c7a791a6ba2f4e91f85cd4c76f5a9f9155c4d66ec6e774d937ca5c0fae.sh_gEl2AGs.blob')
request = urllib.request.Request(url, headers={'User-Agent': 'PEAK-helper-build/0.3'})
with urllib.request.urlopen(request, timeout=60) as response:
    source = response.read(2_000_000).decode('utf-8')
classes = {'FunCommands', 'RPCCommands', 'PlayerEffectCommands', 'ServerAdmin',
           'SpawnCommands', 'ScoutMasterCommands', 'MapCommands', 'EnvironmentCommands', 'Teleport'}
# Keep API declarations only, not bodies or the dependency itself.
lines = []
current = None
for line in source.splitlines():
    m = re.match(r'^\s*(?:public|internal)\s+(?:static\s+)?class\s+(\w+)', line)
    if m:
        current = m.group(1)
        if current in classes:
            lines.append('\n### ' + current)
        continue
    if current in classes and re.match(r'^\s*public\s+static\s+.*\(', line):
        lines.append(line.strip())
if len(lines) < 10:
    raise RuntimeError('No matching declarations found in analysis')
out = pathlib.Path('research/advancedconsole-signatures.txt')
out.parent.mkdir(exist_ok=True)
out.write_text('AdvancedConsole 1.1.9 public declarations; Thunderstore analysis.\n' + '\n'.join(lines) + '\n')
