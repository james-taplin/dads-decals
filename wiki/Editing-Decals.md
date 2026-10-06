# Editing decals

![The Edit tab with a decal selected](../docs/images/gui-edit.png)

Open the **Edit** tab to work on decals you've already placed.

## Selecting
- **Click a decal on the car** to select it. If decals overlap, the smallest one under the cursor wins.
- Or click it in the **Decals on this car** list. Mirrored pairs are marked *(mirrored pair)*.
- The selected decal **pulses blue** on the car. Its mirrored twin pulses too.
- **Right-click** in the world to deselect.

## Moving and adjusting
| Input | Does |
|---|---|
| **Drag** a selected decal | Moves it over the surface, keeping its size and rotation |
| **Ctrl + wheel** | Rotate |
| **Shift + wheel** | Resize |
| **Delete** key | Delete it (and its mirrored twin) |

**Keep level (when moving)** and **Snap 15°** work as in the Place tab. Every setting in the panel's settings sections (size, rotation, colour, finish, weathering, and the text for text decals) applies **live** to the selected decal.

## Buttons
- **Body / Front bogie / Rear bogie**: which part the decal is attached to. Changing it keeps the decal where it is in the world; it just moves with a different part from then on.
- **Duplicate**: makes a copy just below the original and selects it, ready to drag into place.
- **Mirror to other side**: adds a linked mirror copy on the opposite side.
- **Unlink mirror**: breaks the link, so the two copies can be edited separately.
- **Delete**: removes the decal (and its twin if linked).

## Mirrored pairs
A pair made with **Mirror to other side** stays linked:
- moving, resizing, rotating or restyling either one updates the other;
- deleting one deletes both;
- **Unlink mirror** makes them independent.

The mirror is across the centre line of the part the decal is attached to. A pair on the body mirrors left to right across the car.

## Undo and redo
**Undo** and **Redo** at the top of the Edit tab cover placing, moving, editing, deleting, mirroring, and applying templates or copies. A whole slider drag counts as one step. Each car keeps 50 steps for the current session; history isn't saved with the game.

**Clear all** removes every decal from the car, after a confirmation. It can be undone.
