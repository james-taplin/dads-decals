# FAQ and troubleshooting

## The Decals button isn't on the toolbar
- Check **Mod Toolbar** is installed and enabled. Dad's Decals needs it.
- Open UMM (Ctrl+F10) and check **Dad's Decals** has a green dot. A red `!!!` means it failed to load: see *Reporting a bug* below.
- The toolbar and its panels only show while the **cursor is free**.

## No preview appears when I point at a car
Open the **Debug** tab (or watch the **Aiming at:** line in the Place tab):
- **"waiting: mouse is over the panel/toolbar"**: move the mouse off the window.
- **"waiting: cursor hidden"**: free the cursor.
- **"paused: panel closed or mouse not free"**: Dad's Decals pauses while the Decals window is off screen. Free the cursor and open the window.
- **"not over a car"**: the mouse ray isn't hitting the car's collision shape. Try pointing at the middle of the body.
- **Aiming at the car but "preview on 0 part(s)"**: try raising **Projection depth** (Size and placement section). Some custom locos have collision shapes that sit away from the visible body.

## A decal doesn't reach the surface, or appears on the inside
Adjust **Projection depth**: higher reaches further into the bodywork. If a decal shows through to an inner surface, lower it, or reduce the **Wrap angle**.

## Parts of letters are cut off
Fixed in 0.3.1. Older versions cut off descenders (g, j, p, q, y) and the flourishes of script fonts, both in the panel preview and on the car. Text is now cropped to the actual ink, so every font fits. Existing text decals are redrawn automatically; they may come out a touch bigger or smaller than before.

## Overlapping decals swap places as I move the camera
Fixed in 0.3.1 with layers: see [Editing decals](Editing-Decals.md#layers-which-decal-is-on-top).

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

## There's a small white dot in the middle of my screen
That's not Dad's Decals: it's the passenger-seat aim dot from the **DV People** mod. Turn off **Show Passenger Seat Hover Text** in its UMM settings (Ctrl+F10) to hide it.

## The preview doesn't show on parts of the S060 (or other locos with mirrored parts)
Fixed in 0.3.0. Locomotive Mesh Splitter mirrors the S060's body pieces, and older versions of Dad's Decals couldn't draw on mirrored parts.

## Do players or loco authors need anything special?
No. It works on vanilla and custom (CCL) cars as they are.

## Does it affect performance?
Each decal redraws the few parts of the car it touches, only in the camera you're looking through. A few dozen decals on a train should be unnoticeable. Text decals are rendered once and cached.

## Reporting a bug
Open an issue on [GitHub](https://github.com/james-taplin/dads-decals/issues) with:
- what you did and what you saw (a screenshot helps);
- your `Player.log` (`%USERPROFILE%\AppData\LocalLow\Altfuture\Derail Valley\Player.log`) or the UMM log;
- for a specific car, a **Run diagnostics** report from the Debug tab (saved in `Mods/DadsDecals/Diagnostics/`).
