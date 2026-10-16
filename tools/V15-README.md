# Verdant V15.0 RC1 - Final Media + Stability Pass

This is a one-click Unity editor finalizer for the current Verdant System project.

## Install
Copy `VerdantV15ReleaseFinalizer.cs` into:
`Assets/VerdantBoard/Editor/`

Wait for Unity to compile, stay out of Play Mode, then run:

`Tools > Verdant Board > V15 Release Candidate > Run Final Media + Stability Pass`

## What it repairs
- Reconnects Verdant media adapters to the shared video controller.
- Ensures the video player has one shared 1280x720 RenderTexture.
- Forces world/tablet video display RawImages to white with no tint material.
- Excludes video surfaces from Verdant theme recolouring.
- Repairs YouTube search thumbnails so they display at original brightness.
- Removes thumbnail references from VerdantThemeFollower arrays.
- Reduces obvious thumbnail dim/tint overlays.
- Disables known obsolete legacy video wrappers.
- Retires old `Brothers.png` string references.
- Stabilizes visible Arcade CanvasGroups.
- Updates visible Verdant version labels to V15.0 RC1.
- Creates a backup copy of the open scene before changing it.

## Test order
1. Clear the Unity Console.
2. Run the finalizer.
3. Enter ClientSim and check Home, Media, YouTube Search, Arcade and Settings.
4. Search YouTube and confirm thumbnails are bright/not theme-tinted.
5. Load a normal YouTube URL and test Play/Pause/Seek/Stop/Retry/Loop/Volume.
6. Change Verdant UI theme and confirm the video picture and thumbnails do not change colour.
7. Run VRChat Build & Test for the final verification.

The finalizer deliberately avoids moving supporter props, cats, or scene layout objects.
