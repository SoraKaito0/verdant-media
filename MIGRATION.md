# Migration Guide

Current folders you already have:
- ads
- announcements
- backdrops
- badges
- banners
- catalog
- config
- gallery
- posters

Recommended:
1. Copy the contents of this V12 folder into your existing `verdant-media` repository.
2. Let matching folders merge.
3. Keep your current image files.
4. Review existing JSON before overwriting if you've customised it.
5. Delete `catalog/` only after checking you no longer need the old movie data.

New folders:
- supporters/
- themes/
- media/
- updates/
- badges/icons/
- ads/posters/
- gallery/photos/
- announcements/archive/

Backward compatibility:
Top-level posters/, backdrops/, and banners/ are intentionally kept.
