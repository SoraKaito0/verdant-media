#!/usr/bin/env python3
"""Build Verdant VPM 15.4.11 using the approved 15.4.10 package as baseline."""
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED
import json
root = Path(__file__).resolve().parents[1]
old_zip = root / "vpm/releases/com.sorakaito.verdant-15.4.10.zip"
new_zip = root / "vpm/releases/com.sorakaito.verdant-15.4.11.zip"
with ZipFile(old_zip) as source:
    files = {name: source.read(name) for name in source.namelist()}
old = "Editor/VerdantVpm15410Installer.cs"
new = "Editor/VerdantVpm15411Installer.cs"
assert old in files and old + ".meta" in files
files[new] = (root / "vpm-src/VerdantVpm15411Installer.cs").read_bytes()
files[new + ".meta"] = files.pop(old + ".meta")
del files[old]
files["EditorBootstrap/VerdantVpmBootstrapMenu.cs"] = (root / "vpm-src/VerdantVpmBootstrapMenu.cs").read_bytes()
files["package.json"] = (root / "vpm/package.json").read_bytes()
files["README.md"] = b"Verdant 15.4.11 - Run Tools > Verdant Board > V15.4.11 > Apply Supporters + Clock + Version Fix in a backed-up Unity project.\nUnity/UdonSharp testing is still required.\n"
with ZipFile(new_zip, "w", compression=ZIP_DEFLATED) as out:
    for name,data in sorted(files.items()):
        info=ZipInfo(name,date_time=(2026,10,29,0,0,0))
        info.compress_type=ZIP_DEFLATED
        info.external_attr=0o100644<<16
        out.writestr(info,data)
with ZipFile(new_zip) as z:
    assert z.testzip() is None
    assert json.loads(z.read("package.json"))["version"] == "15.4.11"
    assert new in z.namelist() and old not in z.namelist()
    assert z.read(new+".meta") == files[new+".meta"]
    guids=[]
    for name in z.namelist():
        assert not name.startswith("/") and ".." not in Path(name).parts
        if name.endswith(".meta"):
            guids += [x for x in z.read(name).decode().splitlines() if x.startswith("guid:")]
    assert len(guids)==len(set(guids))
index = json.loads((root/"index.json").read_text())
assert index["packages"]["com.sorakaito.verdant"]["versions"]["15.4.11"] == json.loads((root/"vpm/package.json").read_text())
print("PASS: VPM ZIP, manifest and index, GUIDs, installer replacement")
print("Unity/UdonSharp and scene runtime checks are not possible in GitHub Actions.")
