# Verdant World System 15.4.10 — validation candidate

Based on the existing 15.4.9 VPM archive. Do not merge or publish until the Unity checks below pass.

Apply through Tools > Verdant Board > V15.4.10 > Apply Supporters + Clock + Version Fix.
Save the scene before applying so the installer can create a backup. The migration updates the iPad version, places the Main Board version beside PLAYERS, and copies its Image and graphic effects. The card is non-interactive and ignores parent layout groups. Inspect available header space in the actual world; no scene is included in this repository.

The distance migration sets recognized serialized boolean opt-outs to false only under the detected Main Board and iPad roots. It logs every field and warns when no supported flag is found. It does not disable whole controllers or change arbitrary distance numbers. Custom names, external controllers and Udon program variables are not covered. The actual world controller source is required to finish/verify distance behavior if diagnostics show no supported flag. A zero-match warning is a release blocker.

Runtime scripts and assemblies are retained byte for byte from 15.4.9, including supporter ownership, role authority and the shared clock. This does not establish that existing scripts compile in a particular SDK. The package includes only the current installer; older vpm-src installers are historical sources, not packaged. The bootstrap status now reports 15.4.10. The renamed installer retains its asset GUID.

## Completed static checks

Run `python tools/build-vpm.py` to rebuild and verify archive integrity, safe paths, manifest/index agreement, unique asset GUIDs, obsolete packaged installer references, and unchanged runtime/assembly bytes. Rebuilding produces the same archive bytes.

## Required Unity release gate (pending)

- Import/upgrade from 15.4.9 in Unity 2022.3 with the supported VRChat Worlds SDK; confirm no duplicate types, missing references or obsolete installer menu entries.
- Compile all UdonSharp programs; retain the console output. Static inspection is not a compiler run.
- Apply in a copy of the real world scene. Review the distance flag logs separately for Main Board and iPad. Inspect actual controller source/Udon variables and finish any unsupported migration before release.
- Verify the card sits beside top PLAYERS without overlap at supported canvas scales; compare corners, background, effects and typography. Verify iPad version reads 15.4.10.
- Walk/teleport well beyond the previous hide thresholds and return, on desktop and VR. Both surfaces must stay visible, and manual visibility/page controls must still work.
- Reapply the installer; confirm one version card, unchanged unrelated UI and no duplicated controllers.
- Verify supporter scrolling, refresh, tiers/roles and optional integrations. Verify 12/24-hour changes on both surfaces and clock agreement with two clients, including late join.

No Unity editor or world scene is available in the build environment. This candidate has not passed the release gate and must remain a draft PR. Production update feeds remain on 15.4.9.
