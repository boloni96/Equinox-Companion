"""Verify a prepared release before updating its install feeds in one commit."""
import base64
import hashlib
import io
import json
from pathlib import Path
import re
import shutil
import zipfile

root = Path(__file__).resolve().parents[1]
stage = root / '.release-staging'
manifest = json.loads((stage / 'manifest.json').read_text())
version = manifest['version']
assert re.fullmatch(r'\d+\.\d+\.\d+\.\d+', version), 'Invalid version'
assert f'<Version>{version}</Version>' in (root / 'src/EquinoxCompanion.csproj').read_text()
names = manifest['chunks']
assert names == [f'part-{i:03d}.b64' for i in range(len(names))]
payload = b''.join(base64.b64decode((stage / n).read_bytes(), validate=True) for n in names)
assert len(payload) == manifest['bytes'], 'Wrong package length'
assert hashlib.sha256(payload).hexdigest() == manifest['sha256'], 'Package checksum mismatch'
feed_bytes = (stage / 'repo.json').read_bytes()
feed = json.loads(feed_bytes)
assert len(feed) == 1 and feed[0]['AssemblyVersion'] == version
base = 'https://raw.githubusercontent.com/boloni96/Equinox-Companion/main/'
for key in ('DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting'):
    assert feed[0][key] == base + f'dist/v{version}/EquinoxCompanion.zip'
assert feed[0]['IconUrl'] == base + f'dist/v{version}/icon.png'
with zipfile.ZipFile(io.BytesIO(payload)) as archive:
    assert archive.testzip() is None
    compiled = json.loads(archive.read('EquinoxCompanion.json'))
    assert compiled['AssemblyVersion'] == version
    assert compiled['InternalName'] == 'EquinoxCompanion'
    assert compiled['DalamudApiLevel'] == 15
    assert archive.read('EquinoxCompanion.dll').startswith(b'MZ')
    icon = archive.read('icon.png')
    assert icon == (root / 'src/icon.png').read_bytes()
    assert 'garden-art/assets/centers/stone-emblem.png' in archive.namelist()
destination = root / 'dist' / f'v{version}'
assert not destination.exists(), 'Do not overwrite an existing version'
destination.mkdir(parents=True)
(destination / 'EquinoxCompanion.zip').write_bytes(payload)
(destination / 'icon.png').write_bytes(icon)
(destination / 'repo.json').write_bytes(feed_bytes)
for name in ('repo.json', 'test-repo.json', 'testrepo.json'):
    (root / name).write_bytes(feed_bytes)
shutil.rmtree(stage)
print(f'Verified {version}: {manifest["sha256"]}')
