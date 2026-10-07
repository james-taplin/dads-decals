# Changelog

All notable changes to Dad's Decals. Versions follow [semantic versioning](https://semver.org/); dates are YYYY-MM-DD.

## [0.3.2] - unreleased (in development on the `gui` branch)

### Added
- **Type exact values.** Every settings slider now has a box to type a number into, its unit, and a **Reset** button (hover it to see the default). Percentages for opacity, smoothness, metallic, grime, chipping and outline; degrees for rotation and wrap angle; 0–255 for colour sliders.
- **Two-column layout** when the window is 700 px or wider (drag its bottom-right corner): in Place, images or the text editor on the left and settings on the right; in Edit, the decal list and actions on the left and the selected decal's settings on the right. Each column scrolls on its own. Narrower windows keep one column.
- **Status line** under the tabs: what you're placing or editing, whether the mouse is over a car, the controls for that mode, and the car you're working on. It replaces the paragraphs of help text.
- **Performance settings** in the Debug tab: *Decal detail* (High / Medium / Low) skips decals that are only a few pixels big on screen, and *Max draw distance* skips far-away ones. The selected decal and the placement preview are never skipped.
- **Performance readout** in the Debug tab: decals drawn and skipped last frame, parts drawn, and Dad's Decals' CPU time per frame.

### Changed
- **Solid panel.** The window body is now opaque (Unity's default grey), so scenery no longer shows through the text.
- The **Aiming at** readout (with the preview's part count) moved from the Place tab to the Debug tab.
- **Decals stay on at a distance.** They used to be drawn only on each car's full-detail model, so they could vanish when a car switched to its low-detail model far away. They now draw on whichever detail level is showing.
- **Less work per frame.** Which parts each decal overlaps is now worked out once and remembered until the decal or the car changes, instead of being checked against every part every frame.
- **Faster on split locos.** Parts are matched using the triangles they actually draw. Locomotive Mesh Splitter's S060 pieces each carry the whole body's vertices, so every decal used to be drawn on all ~50 pieces.

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
