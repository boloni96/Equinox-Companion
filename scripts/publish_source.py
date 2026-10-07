"""Publish the already-tested package with a single non-forced ref update."""
import base64
import hashlib
import json
import os
from pathlib import Path
import urllib.request

root = Path(__file__).resolve().parents[1]
repo = os.environ['GITHUB_REPOSITORY']
head = os.environ['GITHUB_SHA']
token = os.environ['GH_TOKEN']

def api(path, body=None, method=None):
    request = urllib.request.Request(
        f'https://api.github.com/repos/{repo}/{path}',
        data=None if body is None else json.dumps(body).encode(),
        headers={'Authorization': f'Bearer {token}', 'Accept': 'application/vnd.github+json',
                 'Content-Type': 'application/json', 'User-Agent': 'Equinox-release'},
        method=method)
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.load(response)

feed = json.loads((root / 'repo.json').read_text())
version = feed[0]['AssemblyVersion']
directory = Path('dist') / ('v' + version)
if api('git/ref/heads/main')['object']['sha'] != head:
    raise RuntimeError('Main changed; refusing to overwrite it')
base = api('git/commits/' + head)
entries = []
for path in [directory / 'EquinoxCompanion.zip', directory / 'icon.png', directory / 'repo.json', Path('repo.json')]:
    content = (root / path).read_bytes()
    blob = api('git/blobs', {'encoding': 'base64', 'content': base64.b64encode(content).decode()})
    expected = hashlib.sha1(b'blob ' + str(len(content)).encode() + b'\0' + content).hexdigest()
    if blob['sha'] != expected:
        raise RuntimeError('Uploaded blob mismatch: ' + str(path))
    entries.append({'path': path.as_posix(), 'mode': '100644', 'type': 'blob', 'sha': blob['sha']})
tree = api('git/trees', {'base_tree': base['tree']['sha'], 'tree': entries})
commit = api('git/commits', {'tree': tree['sha'], 'parents': [head],
                           'message': f'Publish verified Companion {version} and feed'})
# Non-forced update rejects any concurrent divergent main commit.
api('git/refs/heads/main', {'sha': commit['sha'], 'force': False}, 'PATCH')
if api('git/ref/heads/main')['object']['sha'] != commit['sha']:
    raise RuntimeError('Published head verification failed')
print('Published', version, commit['sha'])
print('Package SHA256', hashlib.sha256((root / directory / 'EquinoxCompanion.zip').read_bytes()).hexdigest())
