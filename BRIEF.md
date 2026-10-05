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
2. ✅ **Shader:** projector shader in an AssetBundle built with Unity 2019.4.40. It can be checked offline with `ShaderTestRender`.
3. ✅ **Placement:** mouse raycast through `PlayerManager.ActiveCamera` (so the exterior camera works), ghost preview, click to place. Works on any car.
4. ✅ **Conformal rendering:** confirmed in game on the CCL Big Boy and its tender (2026-10-05).
5. ✅ **Persistence:** layouts saved in the save game, reapplied on spawn, orphans kept.
6. **v0.2: the feature set below (groups 1–4).**
7. **Later (group 5):** VR placement via CommsRadioAPI, distance culling for long trains, multiplayer sync.

## v0.2 feature plan (agreed 2026-10-06)

### Panel layout
The panel gets tabs: **Place | Edit | Layouts | Debug**. Shared controls (size, rotation, colour, finish, weathering) live in one "decal settings" block. **Place** edits the next decal, and **Edit** edits the selected one.

### Group 1: placing and editing
- **Edit placed decals.** In the Edit tab, clicking a decal on the car selects it. It's picked by testing the hit point against each decal's box; the smallest box wins. A list of the car's decals also selects. The selected decal pulses (shader `_Highlight`).
  - Drag with the left mouse to move it over the surface. It keeps its size and rotation.
  - Every setting in the settings block applies live. Buttons: Delete (or the Delete key), Duplicate, Move to placing.
  - Undo/redo covers place, edit, move, delete and mirror. It keeps a snapshot stack per car, up to 50 steps.
- **Mouse wheel while placing or dragging:** Ctrl+wheel rotates, Shift+wheel resizes. If the game's own camera also reacts to the wheel, the sliders remain. That needs checking in game.
- **Keep level** (on by default): the image's horizontal axis stays parallel to the car's horizontal plane. Projection also snaps to the nearest car axis (side, top, front or back), so numbers on a curved boiler are stencilled straight on. With it off, the decal projects along the surface normal. There's also a rotation snap toggle with 15° steps.
- **Mirror to the other side:** a toggle when placing, plus a "Mirror" button in Edit. The copy is reflected across the car's centre line (local X = 0). The maths reflects with M = diag(-1,1,1), so the quaternion becomes (x, −y, −z, w). Text still reads correctly from the other side.
  - Mirrored pairs are **linked** (`PairId`): editing one updates its twin until you unlink them.
- **Moving parts:** a decal is anchored to the part it was placed on. That's the car body, or the bogie / articulated engine unit if you clicked one (`TrainCar.Bogies`). It's only drawn on renderers under that anchor, so it doesn't slide when the bogie swings. Anything animated inside that part (rods) may still slide, so avoid placing on those.

### Group 2: text without PNGs
- **Text decals:** in the Place tab, choose "Image" or "Text". Text settings: text, font, size, colour, outline colour and width, letter spacing, and alignment.
- **How it renders:** legacy `TextMesh` with `Font.CreateDynamicFontFromOSFont`, so **any font installed in Windows** can be used. It draws once into a RenderTexture with an off-screen orthographic camera and our own `DadsDecals/TextRender` shader, so it doesn't depend on any game shaders surviving. The result is copied to a `Texture2D` (with mipmaps) and cached by its settings.
  - **Outline:** the text is drawn in the outline colour at 8 offsets, then the main colour on top.
- **Auto road numbers:** tokens in the text are replaced per car:
  - `{id}` → `L-042`
  - `{num}` → `042`
  - `{n}` → `42` (no leading zeros)
  - `{type}` → livery name

  So one template can number a whole fleet. Text with tokens is rendered per car.

### Group 3: looking like real paint
- **Colour picker:** RGB/HSV sliders, a hex field and a swatch row. Swatches are presets plus your own saved colours, kept in mod settings. There's also **Pick from screen**: an eyedropper that samples the pixel under the mouse at the end of the frame. That colour includes the game's lighting, so treat it as a starting point.
- **Finish:** smoothness and metallic sliders, with presets **Matte paint** (0.15/0), **Gloss vinyl** (0.75/0) and **Metal plate** (0.55/0.9).
- **Weathering:**
  - **Grime:** takes the dirt from the livery under the decal. The shader samples the target material's albedo at the mesh's own UVs, compares it with a blurred mip of the same texture, and darkens the decal by that ratio. Streaks and grime on the livery show through.
  - **Chipping:** procedural noise in decal space erodes the edges and paint, with a per-decal seed.
- **Glow:** emission strength, for number boards and plates that should read at night.

### Group 4: reusing layouts
- **Templates:** "Save as template" writes `Mods/DadsDecals/Templates/<livery id>/<name>.json`. These are files, not save-game data, so they work across saves. "Apply" adds or replaces a template's decals on the current car. Templates for the car's livery are listed first, then all others.
- **Copy from another car:** lists cars in the world that have layouts, plus orphans, and copies the decals over.
- **Export / import:** export writes `Mods/DadsDecals/Exports/<name>/layout.json` plus copies of the PNGs it uses. Import copies the PNGs into `Decals/Imported/<name>/` and remaps the image keys. A folder is all someone needs to share.
- **Layouts per livery:** an optional per-car toggle, "Link layout to paint". The current decals are stored per paint theme, read from `TrainCar.PaintExterior.CurrentTheme.AssetName`. That covers vanilla themes and Skin Manager skins, because Skin Manager themes extend the game's `PaintTheme`. When the paint changes (`TrainCarPaint.OnThemeChanged`), the decals swap to that theme's set. No Skin Manager dependency.

### Data format v2
`DecalPlacement` gains these fields:
- `Kind` (image or text) and the text fields
- `Anchor` (path to the anchor transform; empty means the car body)
- `Smoothness`, `Metallic`, `Grime`, `Chipping`, `ChipSeed`, `Glow`
- `PairId`

`LocoLayout` gains `LinkToPaint` and `ByTheme`. v1 saves load with sensible defaults.

### Build order
1. Data v2 + shader v2 (finish, grime, chipping, glow, highlight), checked offline with `ShaderTestRender`.
2. Panel tabs + shared settings block + colour picker + finish/weathering controls.
3. Editing: select, drag, live edit, delete/duplicate, undo/redo, keep level, snap, wheel, anchors.
4. Mirror + linked pairs.
5. Text decals + tokens.
6. Templates, copy-from, export/import, paint-linked layouts.

Each step is built, committed, and installed when the game is closed.

## Open questions
- Does the game's exterior camera also zoom on the mouse wheel? If so, Ctrl/Shift+wheel may need different modifiers.
- Interior (cab) decals: exterior first.