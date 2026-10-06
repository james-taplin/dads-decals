# Installation

## Requirements
- **Derail Valley** (desktop). VR placement isn't supported yet.
- **[Unity Mod Manager](https://www.nexusmods.com/site/mods/21)** (UMM).
- **[Mod Toolbar](https://github.com/imagitama/derail-valley-mod-toolbar)**: the Decals window is a Mod Toolbar panel, so Dad's Decals won't load without it.

## Installing
1. Download `DadsDecals-<version>.zip` from the [Releases page](https://github.com/james-taplin/dads-decals/releases).
2. Either:
   - open the UMM installer, choose Derail Valley, go to **Mods** and drag the zip in; or
   - unzip it into `Derail Valley/Mods/`, so you end up with `Derail Valley/Mods/DadsDecals/info.json`.
3. Start the game. In the UMM window (Ctrl+F10) **Dad's Decals** should show a green dot.

The zip contains:

```
DadsDecals/
  DadsDecals.dll        the mod
  info.json             UMM manifest
  Bundles/dadsdecals    the decal and text shaders
  Decals/               example images (add your own here)
  LICENSE
```

## Opening the panel
The panel only appears while the mouse cursor is free (the same as other Mod Toolbar panels). Free the cursor, then click **Decals** on the toolbar strip at the top of the screen. You can drag the window by its title and resize it from the bottom-right corner, and the toolbar remembers both.

## Updating
Install the new zip over the old one. Your layouts live in your save games, and templates, exports and your own images live in folders the update doesn't replace, so they're kept.

> Back up `Mods/DadsDecals/Decals/` first if you've edited any of the example images, because the update overwrites files with the same names.

## Uninstalling
Disable or remove the mod in UMM, or delete `Mods/DadsDecals/`. Saves stay loadable: the layout data sits under its own key in the save. Whether the game keeps that data if you save *while the mod is removed* hasn't been tested yet. Back up your save if you plan to reinstall later and want the decals back.

## Files the mod creates
| Path (inside `Mods/DadsDecals/`) | What |
|---|---|
| `Decals/` | Your images (PNG). Subfolders become categories. |
| `Templates/<livery>/` | Saved templates |
| `Exports/` | Layouts you've exported for sharing |
| `Import/` | Put shared layout folders here to import them |
| `Diagnostics/` | Reports from the Debug tab |
| `Settings.xml` | Your saved colours, last font and folded sections |
