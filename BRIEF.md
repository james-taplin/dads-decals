# Dad's Decals — Project Brief

## One-liner
A lightweight Derail Valley mod that lets players stick decals (numbers, logos, stripes, weathering, anything PNG) onto locomotives and cars. Decals wrap around curved and angled surfaces the way KSP's Conformal Decals do, and each loco remembers its own layout.

## Goals
1. **Conformal decals.** A decal is projected onto the car's mesh and follows its shape (boiler curves, cab corners, tender sides) instead of floating as a flat card.
2. **Easy to use.** Point, place, tweak, done. No config-file editing for normal use.
3. **Per-loco layouts.** Every individual loco (e.g. `L-014`) keeps its own saved decals. Layouts reload automatically when the loco spawns, and you can save one as a template to reuse on others of the same type.
4. **Lightweight.** No noticeable FPS cost with a few dozen decals on a train. No hard dependencies beyond the mod loader.

## Non-goals (for v1)
- Painting or reskinning whole liveries (that's a skin manager's job).
- Multiplayer sync (maybe later).
- An in-game image editor. Users bring their own PNGs.

## How a player uses it
1. Drop PNGs into `Mods/DadsDecals/Decals/` (subfolders become categories).
2. Walk up to a loco and open the decal tool (hotkey; the comms radio could come later).
3. Pick a decal from a thumbnail palette. A ghost preview follows the crosshair and wraps onto whatever surface it's over.
4. Adjust **size, rotation, stretch, mirror, tint and opacity**, then click to place.
5. Select a placed decal to move, adjust, duplicate or delete it. Undo/redo.
6. The layout saves automatically for that loco. Optional: "Save as template" / "Apply template" per loco type.

## Technical approach (proposed)
- **Mod loader:** Unity Mod Manager (the standard for DV). C#, Harmony for patches.
- **Decal technique: mesh-clipping projection** (how Conformal Decals and most game decal systems work):
  - A decal is an oriented box (position, rotation, size) attached to the car.
  - When it's placed or changed, gather the triangles of the car's MeshRenderers that touch the box, clip them to the box, and build a new small mesh with UVs projected from the box's front face.
  - Push the mesh out slightly along the normals and use a transparent material with depth offset to avoid z-fighting.
  - Parent the result to the car's transform so it moves and sways with the loco. Static after creation, so the per-frame cost is near zero.
  - Cull back-facing and steep-angle triangles so a decal doesn't smear around a corner unless the user wants it to (a "wrap angle" slider).
  - *Why not a screen-space decal shader?* DV is believed to use Unity's built-in render pipeline (**unverified, check the game's Unity version and pipeline**), which has no native decal projector. Mesh decals work in any pipeline and look correct in VR.
- **Targeting:** raycast from camera to the car's colliders/meshes. Skip interior and LOD meshes as needed.
- **Persistence:** one JSON file per loco, keyed by the car's unique ID, plus templates keyed by car type:
  ```
  Mods/DadsDecals/Layouts/<carId>.json
  Mods/DadsDecals/Templates/<carType>/<name>.json
  ```
  Each decal entry stores: image path, the mesh/transform path it's attached to, local position/rotation/size, tint, opacity, wrap angle, mirror flags. A reference model is Zeibach's sounds mod, which saves per-car config; **check how it keys cars and when it applies config on spawn before copying the pattern.**
- **Hooking spawn:** patch car spawn/load so saved layouts are applied when a loco appears, and rebuilt when it despawns and respawns.

## UI
- Simple IMGUI window to start (fast to build, works in UMM). Can be swapped for something nicer later.
- Panels: palette (thumbnails + search), transform controls, placed-decal list for the current loco, template save/load.
- VR: v1 is desktop-first. Keep input behind an abstraction so VR controllers can be added.

## Performance budget
- Decal mesh generation happens only on place/edit/load, never per frame.
- Textures are loaded once and shared between decals. Downscale anything over 2048px.
- Target: under 1 ms per frame of extra cost with 50 decals in view.

## Milestones
1. **Skeleton:** UMM mod loads, settings window opens, PNGs load from the folder.
2. **Flat placement:** raycast + quad decal parented to the car (proves the attach/save path).
3. **Conformal:** mesh-clipping projection replaces the quad.
4. **Persistence:** per-loco JSON save/load, auto-apply on spawn.
5. **Editing UX:** select/move/rotate/scale/delete, undo, ghost preview.
6. **Templates:** save/apply per loco type.
7. **Polish:** thumbnails, categories, tint/opacity, wrap-angle slider, docs, Nexus release.

## Open questions
- Unity version and render pipeline DV currently ships with (affects shaders).
- Best stable per-car ID that survives save/load (car ID vs GUID).
- Should decals also work on custom cars (CCL) and skins from Skin Manager? Probably, if they're in the car's mesh hierarchy.
- Should layout data live in the mod folder (portable, shareable) or the save game (follows the save)? Proposed: mod folder, with an export/import button for sharing.
