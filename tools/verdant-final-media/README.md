# Verdant V15.0.0 — Final Media Repair

This is a non-destructive editor-side repair pass for the current Verdant scene.

## What it fixes

- Repairs missing Verdant shared-video references where they can be resolved safely.
- Restores/creates the video render target used by the existing player.
- Forces actual video display RawImages to render neutral white instead of inheriting theme tint.
- Detects YouTube search thumbnail/preview graphics and removes dark UI tint.
- Reduces thumbnail shade/dim overlays.
- Removes video/thumbnails from VerdantThemeFollower arrays so later theme refreshes do not darken them again.
- Creates a scene backup before changing anything.
- Writes a final audit report to Assets/VerdantBoard/Generated/Verdant_Final_Media_Audit.txt.

## Use

1. Import the Unity package.
2. Leave Play Mode and wait for Unity/UdonSharp to finish compiling.
3. Open the scene containing Verdant.
4. Run:
   **Tools > Verdant Board > Final Polish > Repair Media + YouTube**
5. Save and use **VRChat Build & Test** to test YouTube playback.

## Important VRChat behaviour

AVPro does not play in ordinary Unity editor Play Mode. YouTube watch URLs also need to be tested in the VRChat client. VRChat globally rate-limits new video URL loads and remote image downloads, so search thumbnails can queue instead of all arriving at once.
