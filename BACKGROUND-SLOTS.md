# Verdant remote background slots (V12.3.1)

VRChat does not allow Udon to create a `VRCUrl` from a downloaded string at runtime. Verdant therefore pre-declares 40 safe image URLs in the world. GitHub decides which slots are visible, their names, and which supporter tier is required.

## Add a new background without re-uploading the world

1. Pick an unused slot from `00` to `39`.
2. Upload the full PNG as `assets/backgrounds/slots/slot-XX.png`. Keep it at or below 2048x2048.
3. Upload a small JPG thumbnail as `assets/backgrounds/slots/previews/slot-XX.jpg`.
4. Add an entry to `config/backgrounds.json` with the same numeric `slot`.

Example:

```json
{
  "id": "new-background",
  "name": "New Background",
  "requiredTier": 0,
  "slot": 9,
  "path": "slots/slot-09.png",
  "thumbnailPath": "slots/previews/slot-09.jpg"
}
```

`requiredTier`: 0 = Free, 1 = Basic, 2 = Supporter, 3 = VIP, 4 = Elite, 5 = Guardian.

Do not change the slot filenames/URLs in the world. Replacing an image in the same slot and changing its manifest metadata works without a Unity/world rebuild.
