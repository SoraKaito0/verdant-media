# Verdant Media

A separate GitHub Pages repository for Verdant's remotely hosted artwork and optional remote configuration.

## Recommended repository name

`verdant-media`

## Folder layout

```text
verdant-media/
├── posters/       Movie / TV poster artwork
├── backdrops/     Large detail-page artwork
├── banners/       Verdant event / featured banners
├── catalog/       Optional future catalogue JSON files
├── config/        Optional Verdant remote configuration
├── index.html     Simple status page
└── .nojekyll      Keeps GitHub Pages simple/static
```

## 1. Upload this folder to GitHub

Create a new public repository, ideally named:

`verdant-media`

Upload the **contents of this folder to the root of the repository**.

For example:

```text
https://github.com/YOUR_USERNAME/verdant-media
```

## 2. Enable GitHub Pages

In the repository:

**Settings → Pages → Build and deployment → Deploy from a branch**

Choose:

- Branch: `main`
- Folder: `/ (root)`

Save it.

Your site should then become available at approximately:

```text
https://YOUR_USERNAME.github.io/verdant-media/
```

## 3. Add a poster

Put an image in:

```text
posters/interstellar.jpg
```

The public image URL becomes:

```text
https://YOUR_USERNAME.github.io/verdant-media/posters/interstellar.jpg
```

That is the URL you can later store in Verdant's Movie Library.

## Recommended image sizes

For movie / TV posters:

- Aspect ratio: **2:3**
- Good size: **600 × 900**
- Format: JPG or WebP
- Keep each file reasonably compressed.

For large backdrops:

- Aspect ratio: **16:9**
- Good size: **1280 × 720**
- JPG or WebP

You do not need massive 4K images for a VRChat UI.

## File naming

Use simple lowercase names:

```text
posters/dune-part-two.jpg
posters/interstellar.jpg
posters/fallout-s1.jpg
```

Avoid spaces and unusual symbols.

## Important

This repository is intended for artwork/configuration you have permission to host.

Do not commit secret keys, passwords, private API tokens, or anything you do not want publicly accessible.

## Optional JSON files

The `catalog/` and `config/` folders contain starter JSON examples for Verdant's future Remote Config / remote catalogue features.

Verdant does **not need those JSON files yet** for basic remote poster hosting. You can start by simply adding images to `posters/`.
