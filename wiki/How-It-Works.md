# How it works

Technical notes for the curious and for contributors. Design history is in [BRIEF.md](../BRIEF.md) and the research notes in [docs/RESEARCH.md](../docs/RESEARCH.md).

## Projection (why it works on any car)
A decal is an oriented box attached to the car body or a bogie. Each frame, for every exterior, full-detail part whose bounds overlap the box, the mod redraws that part's mesh with the decal shader (`Graphics.DrawMesh`, into the view camera only). The shader:
1. transforms each vertex into the box's space; that position becomes the image coordinate;
2. throws away pixels outside the box, and pixels facing away beyond the wrap angle;
3. lights the result with Unity's Standard lighting, then blends it over the paint.

Because it never reads the mesh on the CPU, it works on meshes that aren't marked readable, which is most custom-loco meshes. That's why loco authors don't need to change anything. The idea is the same one KSP's Conformal Decals uses; Dad's Decals' shader is written from scratch.

The shader sits in render queue 2499, the last opaque slot. Unity only gives shadows to queues up to 2500, and opaque queues draw before screen-space fog and before transparent effects like smoke. So decals get shadows and fog like the paint under them, and smoke passes in front of them.

## Aiming
Placement casts a ray from `PlayerManager.ActiveCamera` (the camera you're looking through, including the exterior camera) through the mouse position, against the car's collision shapes. Collision shapes can sit a little off the visible body, so the decal box starts slightly outside the hit point and runs inward by the projection depth.

## Text
Text decals use Unity's `TextMesh` with fonts created from installed Windows fonts. Each one is drawn once by an off-screen orthographic camera into a texture, using the mod's own text shader, then cached. The outline is 16 offset copies of the text in the outline colour. Resolution steps with the decal's size.

## Saving
Layouts are stored in the save game under the key `MOD_DADSDECALS`, keyed by each car's `CarGUID`. That ID survives save/load but changes when the game regenerates a car, hence orphaned layouts. Templates and exports are JSON files in the mod folder.

## Building from source
Requirements:
- the .NET SDK (8 or newer) and a Derail Valley install;
- for shader changes only: **Unity 2019.4.40f1**, the game's Unity version.

| Command | Does |
|---|---|
| `dotnet build -c Release` (in `src/DadsDecals`) | Builds the mod |
| `dotnet build -c Release -p:Deploy=true` | Builds and copies it into `<game>/Mods/DadsDecals` |
| `-p:DvInstallDir="C:\path\to\Derail Valley"` | Points the build at a different game install |
| `.\build-shaders.ps1` | Rebuilds the shader bundle from `unity/DadsDecalsShaders` |
| `.\test-shader.ps1 -Tag t- -Props "_Grime=0.7"` | Renders the shader offline under deferred and forward cameras (images in `unity/DadsDecalsShaders/TestRenders/`) |
| `.\tools\make-example-decals.ps1` | Regenerates the example images |
| `.\tools\package-release.ps1` | Builds and packages `dist/DadsDecals-<version>.zip` |

The prebuilt shader bundle is committed (`src/DadsDecals/Bundles/dadsdecals`), so you only need Unity to change the shaders.

## Code map
| File | What |
|---|---|
| `Main.cs` | UMM entry point, Toolbar registration |
| `DecalPanel.cs` | The window and its tabs |
| `Interaction.cs` | Mouse work: placing, selecting, dragging, wheel, eyedropper |
| `DecalRenderer.cs` | Per-car component that draws decals each frame |
| `DecalTargets.cs` | Which parts decals can go on; body/bogie anchors |
| `TextDecals.cs` | Text-to-texture rendering and tokens |
| `LayoutStore.cs` | Data model, save-game storage, undo |
| `Layouts.cs` | Paint link, templates, export/import |
| `DecalLibrary.cs` | Loading PNGs from the Decals folder |
| `Assets.cs` | Loading the shader bundle |
| `Patches.cs` | Save/load hooks and car spawn/delete events |
