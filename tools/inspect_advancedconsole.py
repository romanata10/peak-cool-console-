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
           'SpawnCommands', 'ScoutMasterCommands', 'MapCommands', 'EnvironmentCommands'}
# Record signatures only: method bodies and the dependency itself are not redistributed.
pattern = re.compile(r'^\s*(?:public|internal)\s+(?:static\s+)?(?:class|(?:static\s+)?[\w<>\[\],.?]+)\s+\w+[^\n]*', re.M)
lines = [m.group(0).strip() for m in pattern.finditer(source)
         if any(re.search(r'\b' + re.escape(c) + r'\b', m.group(0)) for c in classes)
         or re.search(r'\b(?:DropCoconutOnPlayer|CallScoutmaster|StopScoutmaster|SpawnScoutmaster|'
                      r'TriggerBananaPeel|PassOutAllPlayers|UnPassOutAllPlayers|ForceWin|'
                      r'SyncGoToBeach|GoToPeak|SpeedPlayer|SwarmWithBees|TickPlayer|'
                      r'SpawnItemByName|ObjectSpawnJellyfish|ListItems)\s*\(', m.group(0))]
if not lines:
    raise RuntimeError('No matching declarations found in analysis')
out = pathlib.Path('research/advancedconsole-signatures.txt')
out.parent.mkdir(exist_ok=True)
out.write_text('AdvancedConsole 1.1.9 public declarations; Thunderstore analysis.\n' + '\n'.join(lines) + '\n')
