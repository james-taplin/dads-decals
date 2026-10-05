# Dad's Decals

A lightweight Derail Valley mod for placing surface-conforming decals on locomotives and cars, with layouts saved per loco.

Status: early skeleton. See [BRIEF.md](BRIEF.md) and [docs/RESEARCH.md](docs/RESEARCH.md).

## Requirements
- [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)
- [Mod Toolbar](https://github.com/imagitama/derail-valley-mod-toolbar)

## Building
Needs the .NET SDK (8+) and a Derail Valley install.

```
cd src/DadsDecals
dotnet build -c Release -p:Deploy=true
```

`Deploy=true` copies the mod to `<game>/Mods/DadsDecals`. If your game is elsewhere, add `-p:DvInstallDir="C:\path\to\Derail Valley"`.

## Using
Put PNGs in `Mods/DadsDecals/Decals/` (subfolders become categories), then open **Decals** from the Mod Toolbar in game.
