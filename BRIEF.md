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

## Decal technique: decided by in-game diagnostics
There are two options (full comparison in RESEARCH.md):
- **CPU mesh clipping:** copy the loco's triangles inside the decal box into a small mesh. This needs readable meshes and a stock transparent shader.
- **GPU projection (the Conformal Decals technique):** redraw the overlapped renderers with a projector shader that discards pixels outside the box. This works on non-readable meshes and makes live preview easy, but needs our own shader built in Unity 2019.4.40.

The skeleton mod has a **Run diagnostics** button that logs the facts that decide this.

## Performance budget
- Textures are loaded once and shared. Downscale anything over 2048px.
- No per-frame allocation. Decal geometry/matrices update only on place/edit/spawn.
- Target: under 1 ms per frame of extra cost with 50 decals in view.

## Milestones
1. ✅ **Skeleton:** UMM mod loads, Toolbar panel, PNG palette, save/load plumbing, diagnostics.
2. **Pick technique** from diagnostics on several locos (vanilla + CCL).
3. **Placement:** mouse raycast onto the loco, ghost preview, click to place, attached to the right transform.
4. **Conformal rendering** with the chosen technique.
5. **Persistence live:** layouts reapply on `CarSpawned`; orphan re-apply UI.
6. **Editing UX:** select/move/rotate/scale/delete, undo.
7. **Templates + export/import.**
8. **Polish:** tint/opacity/wrap slider, downscaling, docs, Nexus release.

## Open questions
- Do CCL locos and Skin Manager reskins work out of the box? Expected yes, to be checked in game.
- Interior decals (cab) as well as exterior? Exterior first.
- VR: placement via CommsRadioAPI later.
