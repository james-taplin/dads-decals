# Changelog

All notable changes to Dad's Decals. Versions follow [semantic versioning](https://semver.org/); dates are YYYY-MM-DD.

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
- A stray white dot appeared on screen and over menus, because decals were drawn into every camera. They now draw only into the view camera.
- The mod crashed while loading at the main menu.
