# Research notes (2026-10-05)

Evidence gathered before writing code. "Verified" means read from the game files or decompiled source; anything else is marked.

## Game engine
- **Unity 2019.4.40**: verified from `UnityPlayer.dll` file version `2019.4.40.16762411`.
- **Built-in render pipeline**: verified, because `Managed/` contains no `Unity.RenderPipelines.*` assemblies and does contain `Unity.Postprocessing.Runtime.dll` (the built-in-pipeline post-processing stack). So there's no URP/HDRP decal projector, and anything shader-based has to be a built-in-pipeline shader.
- Forward vs deferred camera: **unverified**. The in-game diagnostics button logs it.

## Dependency mods (installed in this game copy)
| Mod | Use for us | Notes |
|---|---|---|
| **DerailValleyModToolbar** 1.1.0 (Jared Williams, MIT) | **Yes, a hard dependency.** Our window is a Toolbar panel. | API: `ModToolbarAPI.Register(modEntry).AddPanelControl(label, iconPath, tooltip, Action<Rect> draw, title, w, h).Finish()` and `ModToolbarAPI.Unregister(modEntry)`. Panels draw only while the screen-space mouse is on (cursor mode), and the Toolbar saves window position and visibility itself. Repo: github.com/imagitama/derail-valley-mod-toolbar |
| CommsRadioAPI 1.0.3 (fauxnik) | Later, for VR/radio placement | Used by LocoOwnership. |
| LocoOwnership 1.6.1 (Cruzer) | No code dependency needed | Owned locos keep their `CarGUID`, so our per-GUID layouts just keep working. |
| DVModAPI / BetterModUI (piotrulos) | No | Settings-menu framework; we don't need it. |
| SkinManagerMod 4.4.1 | Compatibility only | Decals are separate renderers, so reskins shouldn't affect them (**unverified**). |

## Identifying a specific loco
From decompiled `Assembly-CSharp`:
- `TrainCar.ID` → `logicCar.ID` (e.g. `L-014`), and `TrainCar.CarGUID` → `logicCar.carGuid`.
- `CarSpawner.SpawnLoadedCar(prefab, carId, carGuid, ...)` restores **both** from the save, so the GUID is stable across save/load.
- When the game deletes and regenerates a loco (not owned, not held as a work train), a new car with a new GUID appears. This matches what the user has seen in game.
- LocoOwnership keys owned locos by `CarGUID` (`OwnedLocosManager`, `HasLocoGUIDAsKey`).
- **Decision:** key layouts by `CarGUID`, and store `ID` and livery only for display. When `CarSpawner.CarAboutToBeDeleted` fires for a loco that has decals, its layout is kept as an "orphan" so the player can re-apply it to the replacement.

## Where layouts are saved
- LocoOwnership stores its data in the save game: a prefix on `SaveGameManager.Save` calls `SaveGameManager.Instance.data.SetJObject("MOD_LOCOOWNERSHIP", ...)`, and a prefix on `CarsSaveManager.Load` reads it back.
- **Decision:** layouts go in the save game under `MOD_DADSDECALS`, because GUIDs only mean something inside one save. Decal PNGs stay in the user folder `Mods/DadsDecals/Decals/`. Export/import of a layout to a file (for sharing and templates) comes later.
- Car lifecycle events: `SingletonBehaviour<CarSpawner>.Instance.CarSpawned` / `.CarAboutToBeDeleted`, subscribed once `WorldStreamingInit.LoadingFinished` fires.

## How Conformal Decals (KSP) does it (analysis only, GPL-3 source, nothing copied)
Read from github.com/Mecripp/KSP-Conformal-Decals, a 2020 snapshot (the original author's repo isn't on GitHub any more). Licence: `LICENSE-SOURCE.md` is **GPL-3**, art is CC-BY-SA. We are **not** copying code or shaders, only noting the technique:

- **No CPU mesh clipping.** For every renderer the decal box overlaps (a simple bounds check), it **redraws that renderer's whole mesh** with `Graphics.DrawMesh` using a decal material.
- The vertex shader turns each vertex into "projector space" with a matrix (projector box → 0..1 cube) and uses that as the UV.
- The fragment shader **discards** any pixel outside the 0..1 cube or facing away from the projector, so only the part of the surface inside the box shows the decal. That's what makes it wrap.
- Optional: it borrows the target material's normal map so the decal picks up the surface detail.
- Alpha-blended, drawn at queue Geometry+100, plus forward-add lighting.

### What that means for us
| | GPU projection (CD-style) | CPU mesh clipping |
|---|---|---|
| Needs CPU-readable meshes | **No** | **Yes** (unknown whether DV's are) |
| Needs a custom shader (an AssetBundle built in Unity 2019.4.40) | **Yes** | No, if a stock transparent shader exists at runtime (unknown, DV ships a shader stripper) |
| Live preview while dragging | Free, just update a matrix | Rebuilds the mesh every time the decal moves |
| Per-frame cost | One extra draw of each overlapped renderer per decal | One small mesh per decal |
| Wrap-angle control | In the shader | While building the mesh |

**Next step:** run the in-game **diagnostics** button on a few locos. It logs mesh readability, the shaders each loco uses, which candidate stock shaders `Shader.Find` can see, and the camera rendering path. The answer picks the technique:
- Meshes readable + a stock transparent shader found → CPU clipping first (no Unity Editor needed).
- Otherwise → GPU projection with our own shader, which means installing Unity 2019.4.40 to build an AssetBundle.

## In-game diagnostics, round 1 (2026-10-05)
Run on L-041 (LocoDH4), L-006 (LocoDE2), L-084 (GN_M2, CCL), L-055 (em_h6f_main, CCL). Source: Player.log.

- **Camera: DeferredShading, HDR on**, for all four. Alpha-blended decals draw in the forward pass after the deferred pass, which is the same way Conformal Decals renders.
- **Shaders found by `Shader.Find`:** `Standard`, `Unlit/Transparent`, `Sprites/Default`. **Missing:** every `Legacy Shaders/Transparent/*` and `Unlit/Transparent Cutout`, so the game's shader stripper removed them. DV also has its own `TransparencyWithFog`, used on DH4/DE2 glass.
- **Mesh readability:** mixed. Readable vs total MeshFilters: DH4 61/163, DE2 55/193, GN_M2 134/418, H6F 62/108.
  - The **vanilla main bodies at LOD0 are readable**: `dh4_exterior_body` (24,754 verts) and DE2 `621_exterior` (24,951 verts). The DE2 LOD1–3 bodies are *not* readable, but we only need LOD0.
  - The CCL bodies weren't in the round-1 output, which only printed the first 40 meshes. Round 2 diagnostics now list every exterior LOD0 target to `Mods/DadsDecals/Diagnostics/<id>_<livery>.txt`.

**Provisional reading:** CPU mesh clipping + `Standard` (Fade mode) works on vanilla bodies with no Unity Editor. Whether it covers CCL locos depends on round 2. Whether Standard's transparent variants survived stripping is only provable by rendering one.
