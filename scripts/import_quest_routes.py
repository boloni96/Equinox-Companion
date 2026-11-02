"""Build a lossless, deterministic quest-data pack from a pinned source checkout.

This imports data only. An imported step is never a claim of executor support.
"""
import argparse
import hashlib
import json
import re
import zipfile
from collections import Counter
from pathlib import Path

SOURCE = 'https://github.com/PunishXIV/Questionable'
REVISION = 'f737a0865627bc6fa70fb7024ab403b3e4881736'

def build(root, destination):
    root, destination = Path(root), Path(destination)
    license_bytes = (root / 'LICENSE').read_bytes()
    if b'GNU AFFERO GENERAL PUBLIC LICENSE' not in license_bytes:
        raise ValueError('Source licence missing or changed')
    rows, files, kinds = [], {}, Counter()
    # Include every JSON, including custom quest identifiers and schema metadata.
    for path in sorted((root / 'QuestPaths').rglob('*.json')):
        relative = path.relative_to(root).as_posix()
        raw = path.read_bytes()
        data = json.loads(raw.decode('utf-8-sig'))
        files[relative] = raw
        if not isinstance(data, dict) or 'QuestSequence' not in data:
            continue
        sequences = data['QuestSequence']
        if not isinstance(sequences, list):
            raise ValueError(f'Invalid sequences: {relative}')
        route_kinds = Counter()
        for sequence in sequences:
            for step in sequence.get('Steps', []):
                route_kinds[step.get('InteractionType', '<missing>')] += 1
        kinds.update(route_kinds)
        rows.append(dict(path=relative, key=path.stem.split('_', 1)[0],
                         sha256=hashlib.sha256(raw).hexdigest(),
                         interactions=dict(sorted(route_kinds.items())),
                         executable=False))
    if not rows:
        raise ValueError('No quest definitions found')
    manifest = dict(format=1, source=SOURCE, revision=REVISION,
                    license='AGPL-3.0', routes=rows,
                    interactionCounts=dict(sorted(kinds.items())),
                    jsonFiles=len(files), routeCount=len(rows))
    enum_path = root/'Questionable.Model/Common/EAetheryteLocation.cs'
    identifiers = dict((name, int(value)) for name, value in re.findall(r'(\w+)\s*=\s*(\d+)\b', enum_path.read_text(encoding='utf-8-sig')))
    travel = {}
    for relative in ['Questionable.Model/Common/Converter/AetheryteConverter.cs', 'Questionable.Model/Questing/Converter/AethernetShardConverter.cs']:
        source = (root/relative).read_text(encoding='utf-8-sig')
        for name, label in re.findall(r'EAetheryteLocation\.(\w+),\s*"([^"]+)"', source):
            if name not in identifiers:
                raise ValueError('Unresolved aetheryte mapping: '+name)
            travel[label] = identifiers[name]
        files['SourceMappings/'+Path(relative).name] = (root/relative).read_bytes()
    files['SourceMappings/EAetheryteLocation.cs'] = enum_path.read_bytes()
    files['travel-map.json'] = json.dumps(travel, sort_keys=True).encode()
    emote_enum = root/'Questionable.Model/Questing/EEmote.cs'
    emote_converter = root/'Questionable.Model/Questing/Converter/EmoteConverter.cs'
    emote_ids = dict((name, int(value)) for name, value in re.findall(r'(\w+)\s*=\s*(\d+)\b', emote_enum.read_text(encoding='utf-8-sig')))
    emotes = {label: emote_ids[name] for name, label in re.findall(r'EEmote\.(\w+),\s*"([^"]+)"', emote_converter.read_text(encoding='utf-8-sig'))}
    files['emote-map.json'] = json.dumps(emotes, sort_keys=True).encode()
    files['SourceMappings/EEmote.cs'] = emote_enum.read_bytes()
    files['SourceMappings/EmoteConverter.cs'] = emote_converter.read_bytes()

    files['LICENSE'] = license_bytes
    files['manifest.json'] = json.dumps(manifest, indent=2, ensure_ascii=False).encode()
    files['NOTICE.txt'] = (f'Unmodified Questionable quest data from {SOURCE}\n'
                           f'Commit: {REVISION}\nLicence: AGPL-3.0; see LICENSE.\n'
                           'Original author fields and all source JSON bytes are preserved.\n'
                           'This pack contains data, not the Questionable plugin.\n'
                           'Import coverage is not execution coverage.\n').encode()
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
        for name, raw in sorted(files.items()):
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, raw)
    return manifest

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source')
    parser.add_argument('output')
    args = parser.parse_args()
    manifest = build(args.source, args.output)
    print(f"Imported {manifest['routeCount']} routes / {manifest['jsonFiles']} JSON files; no steps enabled automatically.")
