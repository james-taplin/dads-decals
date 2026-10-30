# Changelog

All notable changes to Dad's Decals. Versions follow [semantic versioning](https://semver.org/); dates are YYYY-MM-DD.

## [0.5.1-beta] - 2026-10-30 (multiplayer beta)

### Added
- **Everyone can place and edit decals in multiplayer**, guests included. Each player's changes go to the host when they let go of the mouse, and the host sends the result to everyone. The host's game is the referee: if two people change the same car at the same moment, the later change is turned down, that player's car goes back to the agreed version, and their panel says why.
- **Pictures are shared.** A PNG that other players don't have is sent to them automatically, in small pieces checked against its fingerprint (up to 2 MB per picture). Received pictures are cached in `Decals/_Multiplayer` and kept out of the palette.
- **Host settings in UMM** (Ctrl+F10 → Dad's Decals): **Guests can place and edit decals** (untick for view only), and **Block** a player by picking a guest or typing a name. Blocked players can't place or edit, and everything they placed is removed.
- **Who placed it:** new decals remember the player who placed them (used for blocking).
- Updates from other players no longer drop your selection, and they wait until you finish a drag.

### Changed
- Multiplayer protocol 2: players on 0.5.0 alphas can still join, but their decals stay on their own screen until they update.
- UMM shows **Dad's Decals (multiplayer beta)**.

## [0.5.0-alpha.2] - 2026-10-29 (multiplayer alpha)

First public multiplayer alpha, built on 0.4.2. (0.5.0-alpha.1 was a private tester build.)

First step of multiplayer support for [Derail Valley Multiplayer](https://github.com/AMacro/dv-multiplayer) (0.1.16+). Single player is unchanged.

### Added
- **Players with and without Dad's Decals can join each other's games.** Dad's Decals now tells Multiplayer it's a client-side mod (`"MultiplayerCompatibility": "Client"`), so it's no longer required on both sides.
- **Guests see the host's decals.** When a guest with Dad's Decals 0.5+ joins, the host sends every car's layout, then sends changes live as the host places and edits. Guests can't place or edit yet; that comes in a later test build. Images a guest doesn't have are listed in the multiplayer log (image transfer comes in alpha.2).
- **Polite note for old versions.** A guest running Dad's Decals before 0.5 can still join a 0.5 host, and gets a whispered message that their version isn't supported in multiplayer.
- **Local-only mode.** If the host has no Dad's Decals (or an incompatible version), a guest's decals stay on their own screen, and the panel says so.
- **Multiplayer log** in the Debug tab, with a button that saves it to a file for bug reports.
- Multiplayer code lives in a separate `DadsDecals.Multiplayer.dll`, loaded only when the Multiplayer mod is installed.
- **Create debug zip** in the Debug tab: one click collects the game log (and the previous one), the UMM log, Dad's Decals' diagnostics and multiplayer log, settings, and a summary of versions and installed mods into `Mods/DadsDecals/Diagnostics/debug-*.zip`. **Open folder** shows it.
- **Tester guide** (`MULTIPLAYER-TESTING.md`, in the zip and in `docs/`): what to try, and which logs and files to send after a session or a problem.

### Changed
- Save format v3: every decal gets a stable ID and a "placed by" name. Older saves load fine and get new IDs.
## [0.4.2] - 2026-10-29

### Fixed
- **A stream of NullReferenceExceptions** (`Mesh.get_subMeshCount` in `DecalRenderer.Draw`), seen after leaving the caboose. The game can destroy a part's mesh while the part itself stays, and Dad's Decals kept trying to draw a decal on the missing mesh every frame. Such parts are now skipped, and the car's parts are re-read on the next frame.
- **"Getting control n's position in a group with only n controls"** errors from the Decal toolbox, when selecting a decal or starting to place made it change what it shows mid-frame. It now waits for the next frame instead.

## [0.4.1] - 2026-10-18

### Fixed
- **Decals cut off or missing behind invisible geometry** (from 0.3.3). The "stop at the first surface" check also counted parts that weren't being drawn: a car's simplified low-detail models, the low-detail cab while the full interior is loaded, and see-through cutout parts. Where one of those sat in front of the real surface, the decal behind it was hidden. On the S282's tender rear it only appeared on the ladder; in cabs it worked in some spots and not others. The check now uses only the solid, full-detail parts you can actually see.
- **Aiming in cabs stopped on invisible boxes.** The aim stopped at any collider, including the grab boxes around levers and handles, teleport spots and the car's big outer collision hull, so a decal could land on an invisible box in front of the wall. Aiming now goes through those to the real surface behind them (up to 1.5 m), and only uses them when there's nothing else.
- Decals are no longer drawn on cutout or see-through parts (ladders, grilles), where they ignored the holes, or on shadow-only meshes, which are invisible.
- The Debug tab's *Aiming at* line now shows the collider's layer.

## [0.4.0] - 2026-10-16

### Added
- **Decals in the cab.** Place decals on cab interior walls, ceilings, backheads and dashes. Point at them from inside the cab with the panel open, as you would outside. They come back when you walk away and return (DV loads the interior only when you're near), and they're saved with the car like any other decal.
- Cab decals also show on the low-detail cab you see through the windows from outside.
- Levers, valves, switches, doors and gauges are left out, so a decal doesn't stay behind while the part moves through it.
- Diagnostics report the cab interior: whether it's loaded and how many parts can take decals.

### Fixed
- **Decals reappeared floating in front of the loco with the high beam on** (seen on the DH4: a number on the hood showed again about half a metre out, by the railing). The headlight beams are cone-shaped meshes that switch on with the lights, and decals near a lamp were drawn on them too. Headlight beams, lamp glare and other effect meshes are no longer decal targets.

## [0.3.3] - 2026-10-11

### Fixed
- **Decals also landed on the inside of the cab.** A decal is projected through a box that reaches *Projection depth* into the car, and it painted every surface in that box facing its way, including the inside of the cab's far wall, which faces the decal just like the outside it was placed on. Each decal now stops at the first surface it meets: when it's placed or moved, the mod takes a small depth picture of the parts along the decal's direction, and anything more than 15 cm behind that first surface is left alone. The 15 cm lets a decal carry on behind a handrail or pipe instead of leaving a gap. Curved boilers and mirrored parts (S060) are unaffected.
- **Decals could be seen from behind the surface they're on.** Since 0.3.0 decals are drawn on both sides of a surface (for Locomotive Mesh Splitter's mirrored S060 pieces), so from inside a cab one could show on the back of the outer wall. A decal is now hidden whenever the camera is behind its surface.

## [0.3.2] - 2026-10-11

First stable release. A tidier window layout, plus fixes found while testing it.

### Added
- **Type exact values.** Every settings slider now has a box to type a number into, its unit, and a **Reset** button (hover it to see the default). Percentages for opacity, smoothness, metallic, grime, chipping and outline; degrees for rotation and wrap angle; 0–255 for colour sliders.
- **Decal toolbox.** Size, colour, finish and weathering now live in a second, smaller window that pops out beside the main one (toolbar button **DT**; the main window is now **DD**). It opens by itself when you start placing or select a decal, and closes when you stop; **Open toolbox** in the Place and Edit tabs opens it by hand. For a selected decal it also holds **Attached to** (body or bogie), the layer buttons, **Duplicate**, **Mirror**/**Unlink** and **Delete**, at the top where they're easy to find. Drag and resize it anywhere; Mod Toolbar remembers where. Closing the main window closes it too.
- **Status line** under the tabs: what you're placing or editing, whether the mouse is over a car, the controls for that mode, and the car you're working on. It replaces the paragraphs of help text.
- **Performance settings** in the Debug tab: *Decal detail* (High / Medium / Low) skips decals that are only a few pixels big on screen, and *Max draw distance* skips far-away ones. The selected decal and the placement preview are never skipped.
- **Performance readout** in the Debug tab: decals drawn and skipped last frame, parts drawn, and Dad's Decals' CPU time per frame.

### Changed
- **Solid panel.** The window body is now opaque (Unity's default grey), so scenery no longer shows through the text.
- The **Aiming at** readout (with the preview's part count) moved from the Place tab to the Debug tab.
- **Decals stay on at a distance.** They used to be drawn only on each car's full-detail model, so they could vanish when a car switched to its low-detail model far away. They now draw on whichever detail level is showing.
- **Less work per frame.** Which parts each decal overlaps is now worked out once and remembered until the decal or the car changes, instead of being checked against every part every frame.
- **Faster on split locos.** Parts are matched using the triangles they actually draw. Locomotive Mesh Splitter's S060 pieces each carry the whole body's vertices, so every decal used to be drawn on all ~50 pieces.

### Fixed
- **Edit lists the tender's decals too.** A steam loco and its tender are separate cars, and the Edit list only showed whichever one you last pointed at, so text on the tender and images on the cab never appeared together. It now lists the car's decals and those on any car coupled to it, each under its own heading.
- **Colour swatches did nothing** until you'd picked from the screen: the hex box wrote its old value back over the click whenever any text box had focus.
- **Button labels were cut off** at both ends: fixed-width buttons were too narrow for the game's font. They now fit their text.
- **Toolbox too narrow**: the Grime and Chipping rows added a sideways scrollbar at the toolbox's default width. **New grime pattern** and **New chip pattern** now sit on their own row under the sliders, and the toolbox opens 460 px wide.

## [0.3.1] - 2026-10-06 (pre-release)

### Added
- **Layers.** Choose which decal shows on top where decals overlap: **To back**, **Backward**, **Forward** and **To front** in the Edit tab, with the selected decal's layer shown. New decals go on top, mirrored pairs move together, and undo restores the old order. The decal list now shows the top layer first.

### Fixed
- **Overlapping decals swapped places** depending on the camera angle, because they all shared one render slot and the game sorted them by distance. Each layer now draws in its own slot (up to 49 per car), and every slot still gets shadows and fog.
- **Text was clipped**: descenders (g, j, q…) and script-font flourishes were cut off, in the panel preview and on the car. The text image was sized from Unity's reported text bounds, which sit too high and miss overhangs. Text now renders on a generous canvas and is cropped to the actual ink.

## [0.3.0] - 2026-10-06 (pre-release)

### Fixed
- **Decals on mirrored parts.** No preview or decals appeared on locos with mirrored parts, such as the S060 with Locomotive Mesh Splitter, which mirrors its split body pieces. Decals now draw on both sides of a surface and rely on the facing check to skip the back.
- **Paused while the window is away.** Placing and editing kept working in the world after the Decals window was closed or the mouse was locked, and the selected decal kept pulsing. Everything now pauses until the window is back on screen; your mode and selection are kept.
- The installed `info.json` could report an old version number.

### Docs
- README and wiki now have screenshots of every tab: placing images and text, the placing preview, the settings sections, the colour picker, editing and layouts.
- New showcase shots, including a fully decorated S060.
- New FAQ entries: the "paused" aim message, the S060 fix, and the small white dot in the centre of the screen. That dot is the DV People mod's passenger-seat aim dot (turn off *Show Passenger Seat Hover Text* in its settings), not Dad's Decals.

## [0.2.0] - 2026-10-06 (first pre-release)

### Added
- **Decal placement** on any loco, tender or wagon, vanilla or custom (CCL), with a live preview that follows the mouse. It aims through the camera you're looking through, so the exterior camera works.
- **Conformal projection**: decals wrap around curved and angled bodywork, work on meshes that aren't CPU-readable, and are drawn only on the part they're attached to (body or bogie).
- **Edit tab**: click a decal to select it (it pulses), drag to move, Ctrl+wheel to rotate, Shift+wheel to resize, Delete to remove. Also duplicate, attach to body/bogie, and undo/redo (50 steps per car).
- **Keep level** (projects straight onto the side, top or end of the car) and **15° rotation snapping**.
- **Mirror to the other side**, with linked twins that follow edits until unlinked.
- **Text decals** using any font installed in Windows: outline, bold/italic, alignment. Resolution follows the decal's size, so large text stays sharp.
- **Auto road number tokens**: `{n}`, `{num}`, `{id}`, `{type}`.
- **Colour picker**: hex field, RGB sliders, presets, saved swatches, and pick-from-screen.
- **Finish presets** (matte paint, gloss vinyl, metal plate) with smoothness, metallic and glow.
- **Weathering**: procedural grime (strength, re-roll, editable colour) and chipping (strength, re-roll).
- **Layouts**: saved per car in the save game; templates per livery (shared across saves); copy from another car; export/import with images; optional layout per paint scheme (covers Skin Manager skins); orphaned layouts kept when the game deletes a car.
- **Collapsible panel sections**, remembered between sessions.
- **51 example decals**: warning stripes, OSHA-style placards, voltage labels, fuel and fluid labels, access and service markings, and DVRT-style logos.
- **Debug tab** with an aim readout and per-car diagnostics.

### Fixed during pre-release testing
- Decals filled whole parts with flat white (a shader UV bug).
- Placement missed the loco in the exterior camera, because it aimed from the player's head.
- The decal box missed the paint on locos whose collision shapes sit away from the visible body.
- Decals showed through shadows and drew on top of fog. They're now in the last opaque render slot.
- The mod crashed while loading at the main menu.
