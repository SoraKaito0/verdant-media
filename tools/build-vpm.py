#!/usr/bin/env python3
"""Build the 15.4.10 candidate from the existing 15.4.9 VPM package."""
import json
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'vpm/releases/com.sorakaito.verdant-15.4.9.zip'
TARGET = ROOT / 'vpm/releases/com.sorakaito.verdant-15.4.10.zip'
with ZipFile(BASE) as archive:
    files = {name: archive.read(name) for name in archive.namelist()}
old = 'Editor/VerdantVpm1549Installer.cs'
new = 'Editor/VerdantVpm15410Installer.cs'
files[new + '.meta'] = files.pop(old + '.meta') # Preserve the script GUID on upgrade.
del files[old]
files[new] = (ROOT / 'vpm-src/VerdantVpm15410Installer.cs').read_bytes()
files['EditorBootstrap/VerdantVpmBootstrapMenu.cs'] = (ROOT / 'vpm-src/VerdantVpmBootstrapMenu.cs').read_bytes()
files['package.json'] = (ROOT / 'vpm/package.json').read_bytes()
files['README.md'] = (ROOT / 'vpm/VALIDATION-15.4.10.md').read_bytes()
with ZipFile(TARGET, 'w', compression=ZIP_DEFLATED) as archive:
    for name, content in sorted(files.items()):
        info = ZipInfo(name, date_time=(2026, 10, 29, 0, 0, 0))
        info.compress_type = ZIP_DEFLATED
        info.external_attr = 0o100644 << 16
        archive.writestr(info, content)

with ZipFile(BASE) as baseline, ZipFile(TARGET) as candidate:
    assert candidate.testzip() is None
    manifest = json.loads(candidate.read('package.json'))
    assert manifest['version'] == '15.4.10'
    index = json.loads((ROOT / 'index.json').read_text())
    assert index['packages'][manifest['name']]['versions']['15.4.10'] == manifest
    # All runtime behavior, assembly references and asset GUIDs stay byte-identical.
    for name in baseline.namelist():
        if name.startswith('Runtime/') or name.endswith('.asmdef') or name.endswith('.asmdef.meta'):
            assert candidate.read(name) == baseline.read(name), name
    assert old not in candidate.namelist()
    assert candidate.read(new + '.meta') == baseline.read(old + '.meta')
    guids = []
    for name in candidate.namelist():
        assert not name.startswith('/') and '..' not in Path(name).parts
        if name.endswith('.meta'):
            guids.extend(line for line in candidate.read(name).decode().splitlines() if line.startswith('guid:'))
        if name.endswith('.cs'):
            text = candidate.read(name).decode()
            assert 'VerdantVpm1549Installer' not in text, name
    assert len(guids) == len(set(guids)), 'Duplicate GUID'
print('PASS: archive integrity, manifest/index agreement, paths, GUIDs, obsolete installer references, unchanged runtime/assemblies')
print('NOT VERIFIED: Unity/UdonSharp compilation, scene migration, actual distance-hiding fields, visual layout, multiplayer clocks')
