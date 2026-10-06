# Changelog

All notable changes to Dad's Decals. Versions follow [semantic versioning](https://semver.org/); dates are YYYY-MM-DD.

## [0.3.0] - 2026-10-06 (first public pre-release)

### Fixed
- **Decals on mirrored parts.** No preview or decals appeared on locos with mirrored parts, such as the S060 with Locomotive Mesh Splitter, which mirrors its split body pieces. Decals now draw on both sides of a surface and rely on the facing check to skip the back.
- **Paused while the window is away.** Placing and editing kept working in the world after the Decals window was closed or the mouse was locked, and the selected decal kept pulsing. Everything now pauses until the window is back on screen; your mode and selection are kept.
- The installed `info.json` could report an old version number.

### Docs
- README and wiki now have screenshots of every tab: placing images and text, the placing preview, the settings sections, the colour picker, editing and layouts.
- New showcase shots, including a fully decorated S060.
- New FAQ entries: the "paused" aim message, the S060 fix, and the small white dot in the centre of the screen. That dot is the DV People mod's passenger-seat aim dot (turn off *Show Passenger Seat Hover Text* in its settings), not Dad's Decals.

## [0.2.0] - 2026-10-06 (feature build, not published)

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
