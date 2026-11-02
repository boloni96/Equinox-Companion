"""Ensure the importer preserves every source JSON byte, not just known fields."""
import hashlib
import importlib.util
import json
import sys
import tempfile
import zipfile
from pathlib import Path
root, pack = map(Path, sys.argv[1:])
with zipfile.ZipFile(pack) as z:
    paths = sorted((root/'QuestPaths').rglob('*.json'))
    manifest = json.loads(z.read('manifest.json'))
    assert manifest['jsonFiles'] == len(paths) == 4392
    assert manifest['routeCount'] == 4388
    for path in paths:
        assert z.read(path.relative_to(root).as_posix()) == path.read_bytes(), path
    assert z.read('LICENSE') == (root/'LICENSE').read_bytes()
    assert manifest['interactionCounts']['Duty'] > 0
    assert manifest['interactionCounts']['SinglePlayerDuty'] > 0
    assert all(not r['executable'] for r in manifest['routes'])
spec = importlib.util.spec_from_file_location('importer', Path(__file__).parents[1]/'scripts/import_quest_routes.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
with tempfile.TemporaryDirectory() as temp:
    rebuilt = Path(temp)/'again.zip'
    module.build(root, rebuilt)
    assert hashlib.sha256(rebuilt.read_bytes()).digest() == hashlib.sha256(pack.read_bytes()).digest()
print('All source bytes, authors, licence and fields preserved; deterministic pack verified.')
