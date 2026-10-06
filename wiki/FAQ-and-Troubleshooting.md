# FAQ and troubleshooting

## The Decals button isn't on the toolbar
- Check **Mod Toolbar** is installed and enabled. Dad's Decals needs it.
- Open UMM (Ctrl+F10) and check **Dad's Decals** has a green dot. A red `!!!` means it failed to load: see *Reporting a bug* below.
- The toolbar and its panels only show while the **cursor is free**.

## No preview appears when I point at a car
Open the **Debug** tab (or watch the **Aiming at:** line in the Place tab):
- **"waiting: mouse is over the panel/toolbar"**: move the mouse off the window.
- **"waiting: cursor hidden"**: free the cursor.
- **"not over a car"**: the mouse ray isn't hitting the car's collision shape. Try pointing at the middle of the body.
- **Aiming at the car but "preview on 0 part(s)"**: try raising **Projection depth** (Size and placement section). Some custom locos have collision shapes that sit away from the visible body.

## A decal doesn't reach the surface, or appears on the inside
Adjust **Projection depth**: higher reaches further into the bodywork. If a decal shows through to an inner surface, lower it, or reduce the **Wrap angle**.

## Text looks blurry
Text resolution follows the decal's size. If small text looks soft up close, make the decal a bit larger, or use a bolder font.

## The camera zooms when I scroll the panel or resize with Shift+wheel
That's the game's exterior camera reacting to the mouse wheel. Mod Toolbar windows don't block it. Use the sliders instead of the wheel if it gets in the way.

## A decal slides around on the running gear
Decals attach to the car body or a bogie. Parts that animate inside a bogie (rods, valve gear) move under a fixed decal. Place decals on non-moving parts.

## My decals disappeared
- **Did the game save?** Decals are written with the save.
- **Was the loco regenerated?** Look under **Orphaned layouts** in the Layouts tab.
- **Did you rename or remove the PNG?** Put it back with the same name and press **Reload folder**.
- **Is "Link layout to paint" on?** The decals for another paint scheme are stored until that scheme is applied again.

## Do players or loco authors need anything special?
No. It works on vanilla and custom (CCL) cars as they are.

## Does it affect performance?
Each decal redraws the few parts of the car it touches, only in the camera you're looking through. A few dozen decals on a train should be unnoticeable. Text decals are rendered once and cached.

## Reporting a bug
Open an issue on [GitHub](https://github.com/james-taplin/dads-decals/issues) with:
- what you did and what you saw (a screenshot helps);
- your `Player.log` (`%USERPROFILE%\AppData\LocalLow\Altfuture\Derail Valley\Player.log`) or the UMM log;
- for a specific car, a **Run diagnostics** report from the Debug tab (saved in `Mods/DadsDecals/Diagnostics/`).
