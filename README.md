# Verdant Media — V12 GitHub Pages

This repository is the shared remote content layer for Verdant.

Base URL:
https://sorakaito0.github.io/verdant-media/

Recommended structure:

verdant-media/
├── ads/
│   ├── posters/
│   └── ads.json
├── announcements/
│   ├── archive/
│   ├── announcements.json
│   └── current.txt
├── backdrops/
├── badges/
│   ├── icons/
│   └── badges.json
├── banners/
├── config/
│   ├── modules.json
│   ├── verdant.json
│   └── worlds.json
├── gallery/
│   ├── photos/
│   └── gallery.json
├── media/
│   ├── thumbnails/
│   ├── featured.json
│   └── radio.json
├── posters/
├── supporters/
│   ├── perks.json
│   └── supporters.json
├── themes/
│   ├── previews/
│   └── themes.json
├── updates/
│   ├── changelog.json
│   └── status.json
├── .nojekyll
├── index.html
├── MIGRATION.md
└── README.md

What changed:
- Keep: ads, announcements, backdrops, badges, banners, config, gallery, posters.
- Remove/archive: catalog/ (old Movies/TV experiment).
- New: supporters, themes, media, updates, badge icons, ad posters, gallery photos, announcement archive.

Important:
Everything here is public. Never store passwords, tokens, private keys, or secure staff credentials in this repo.
