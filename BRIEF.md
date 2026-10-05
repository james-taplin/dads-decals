# Dad's Decals — Project Brief

## One-liner
A lightweight Derail Valley mod that lets players stick decals (numbers, logos, stripes, weathering, anything PNG) onto locomotives and cars. Decals wrap around curved and angled surfaces the way KSP's Conformal Decals do, and each loco remembers its own layout.

## Goals
1. **Conformal decals.** A decal is projected onto the car's mesh and follows its shape (boiler curves, cab corners, tender sides) instead of floating as a flat card.
2. **Easy to use.** Point, place, tweak, done, all from an in-game window. No config-file editing for normal use.
3. **Per-loco layouts.** Every individual loco keeps its own saved decals. Layouts reapply automatically when the loco spawns. Templates for reusing a layout come after that.
4. **Lightweight.** No noticeable FPS cost with a few dozen decals on a train. Reuse existing community mods instead of reinventing them.

## Non-goals (for v1)
- Painting or reskinning whole liveries (that's Skin Manager's job).
- Multiplayer sync (maybe later).
- An in-game image editor. Users bring their own PNGs.

## How a player uses it
1. Drop PNGs into the user folder `Mods/DadsDecals/Decals/` (subfolders become categories).
2. Get on a loco, bring up the cursor, and open **Decals** from the **Mod Toolbar**.
3. Pick a decal from the thumbnail palette. A ghost preview follows the mouse and wraps onto whatever surface it's over.
4. Adjust **size, rotation, stretch, mirror, tint, opacity and wrap angle**, then click to place.
5. Select a placed decal to move, adjust, duplicate or delete it. Undo/redo.
6. The layout saves with the game for that specific loco.
7. If the game deletes and regenerates a loco, its old layout shows up under "Orphaned layouts" so it can be applied to the new one.

## Decisions so far (evidence in [docs/RESEARCH.md](docs/RESEARCH.md))
- **Platform:** Unity 2019.4.40, built-in render pipeline, Unity Mod Manager + Harmony, .NET Framework 4.8.
- **UI:** a panel in **DerailValleyModToolbar** (MIT, hard dependency). It's an IMGUI window, and the Toolbar remembers its position and size.
- **Per-loco identity:** `TrainCar.CarGUID`. It survives save/load and is what LocoOwnership uses too. It changes only when the game regenerates the loco, which is why orphaned layouts exist.
- **Storage:** the save game (`MOD_DADSDECALS` key, same pattern as LocoOwnership), because GUIDs only mean something inside one save. Export/import to a file comes later for sharing.
- **Not like Zeibach's sounds mod:** that works per loco *type*. We go per *individual loco* first, with per-type templates later.

## Decal technique: GPU projection (decided 2026-10-05)
**Requirement:** decals must work on any loco, including mod/CCL locos, **without loco authors having to do anything**. Round-1 diagnostics showed that mesh Read/Write is often off (e.g. DE2 LODs, roughly 2/3 of all meshes), and only the loco author can change that. So CPU mesh clipping is out.

We use **GPU projection**, the same technique Conformal Decals uses. Our shader is written from scratch, not copied (theirs is GPL-3).
- A decal is an oriented box attached to a transform on the car.
- For each LOD0 exterior renderer whose bounds overlap the box, we redraw its mesh (`Graphics.DrawMesh`, or a `CommandBuffer`) with our decal material and a projector matrix.
- The shader turns each vertex into box space, uses that as the UV, and discards pixels outside the box or facing away beyond the wrap angle.
- This works on **any** mesh, readable or not. Live preview just means updating a matrix.
- **The only dependency is ours:** the shader is compiled once in Unity 2019.4.40 into an AssetBundle that ships inside the mod. Players and loco authors need nothing extra.
## Performance budget
- Textures are loaded once and shared. Downscale anything over 2048px.
- No per-frame allocation. Decal geometry/matrices update only on place/edit/spawn.
- Target: under 1 ms per frame of extra cost with 50 decals in view.

## Milestones
1. ✅ **Skeleton:** UMM mod loads, Toolbar panel, PNG palette, save/load plumbing, diagnostics.
2. **Shader:** install Unity 2019.4.40, write the projector shader, build the AssetBundle, load it in the mod.
3. **Placement:** mouse raycast onto the loco, ghost preview, click to place, attached to the right transform.
4. **Conformal rendering:** projector drawing on overlapped LOD0 renderers; check deferred/HDR lighting and fog match.
5. **Persistence live:** layouts reapply on `CarSpawned`; orphan re-apply UI.
6. **Editing UX:** select/move/rotate/scale/delete, undo.
7. **Templates + export/import.**
8. **Polish:** tint/opacity/wrap slider, downscaling, docs, Nexus release.

## Open questions
- Skin Manager reskins: expected fine (we draw separately), check in game.
- Interior decals (cab) as well as exterior? Exterior first.
- VR: placement via CommsRadioAPI later.
