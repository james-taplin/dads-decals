# Dad's Decals multiplayer test (0.5.0-alpha.1)

Thanks for testing! This is an **early test build** of multiplayer support for [Derail Valley Multiplayer](https://github.com/AMacro/dv-multiplayer). Expect rough edges, and please don't use it on a save you'd hate to lose. Back the save up first.

## What this build does
- **Everyone can play together.** Players with and without Dad's Decals can join each other's games.
- **Guests see the host's decals.** When you join, the host's decals appear on their cars. When the host places, moves or changes a decal, everyone with Dad's Decals sees it within a second or so.
- **Guests can only look in this build.** Placing and editing as a guest comes in a later test build. The panel tells you this.
- **Not yet: custom pictures.** Guests only see the host's image decals if they have the same picture files. Everyone has the example decals that come with the mod, and text decals always work. Custom PNGs the host added are listed as "missing" in the multiplayer log. Sending pictures between players is the next build.
- **Older versions:** if you join with Dad's Decals older than 0.5, you can still join. You'll get a whispered chat message saying your version isn't supported in multiplayer.

## Install
Everyone who wants to see decals needs:
1. [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)
2. [Mod Toolbar](https://github.com/imagitama/derail-valley-mod-toolbar)
3. [Derail Valley Multiplayer](https://github.com/AMacro/dv-multiplayer/releases) **0.1.16.0-Beta** (this build is made for that version)
4. **Dad's Decals 0.5.0-alpha.1** (`DadsDecals-0.5.0-alpha.1.zip`): install with UMM like any mod, replacing any older Dad's Decals.

To check it's installed: open UMM (Ctrl+F10). **Dad's Decals** should show version **0.5.0** with a green dot.

## Things to try
You don't have to do all of these. Anything you notice is useful.

- [ ] **Host:** put a few decals on a loco (example images and some text), then have someone join. Do they see them, in the right places?
- [ ] **Host:** move, resize, recolour and delete decals while guests watch. Does it update for them? Roughly how long does it take?
- [ ] **Host:** use **Undo**, **Clear all**, apply a **template**, and **Copy from another car in this save**. Does each one reach the guests?
- [ ] **Host:** put decals inside a cab (0.4 feature). Do guests see them when they get in?
- [ ] **Guest:** leave and rejoin. Are the decals still right?
- [ ] **Guest joins late,** after lots of decorated cars exist (10+ is great).
- [ ] **The host deletes or despawns a decorated car** while a guest is near it.
- [ ] **A player without Dad's Decals joins.** They should join normally and just not see decals.
- [ ] **A player with an older Dad's Decals (0.3.x or 0.4.x) joins.** They should get in, and see a polite chat message.
- [ ] **Host without Dad's Decals:** a guest with it should see "decals you place stay on your screen only" in the panel.
- [ ] **Single player still works as normal** for everyone (place, edit, undo, layers, save and reload).

## After a session, or as soon as something goes wrong

### The easy way: one button (game still running)
1. Free the cursor and open Dad's Decals (**DD** on the Mod Toolbar).
2. **Optional:** if one car's decals look wrong, point at that car first. Its diagnostics are added too.
3. Go to the **Debug** tab and click **Create debug zip**.
4. Click **Open folder** to find it: `Mods\DadsDecals\Diagnostics\debug-<date>-<time>.zip`.

The zip collects everything James needs in one file:
- the game log and the previous session's log;
- the UMM log;
- Dad's Decals' multiplayer log and diagnostics;
- your Dad's Decals settings;
- a summary of your versions and installed mods.

Everyone with Dad's Decals should make one, host and guests alike. Do it once at the end of a session, and again right after anything odd happens.

### If the game crashed or you've already quit
The game **overwrites its log the next time it starts**, so grab these before relaunching:

| File | Where |
|---|---|
| `Player.log` | `%USERPROFILE%\AppData\LocalLow\Altfuture\Derail Valley\Player.log` |
| `Player-prev.log` | Same folder: the session before the last one. Send this one if you already restarted. |
| UMM log | `<Derail Valley folder>\DerailValley_Data\Managed\UnityModManager\Log.txt` |
| Dad's Decals files | `<Derail Valley folder>\Mods\DadsDecals\Diagnostics\` (any recent `.txt` or `.zip`) |

Tip: paste `%USERPROFILE%\AppData\LocalLow\Altfuture\Derail Valley` into the Explorer address bar to open the folder.

`<Derail Valley folder>` is usually `C:\Program Files (x86)\Steam\steamapps\common\Derail Valley`. In Steam: right-click Derail Valley → **Manage** → **Browse local files**.

### Send it
Send the debug zip (or the files above, zipped), plus screenshots or a short clip if something looked wrong. With it, write a few lines:
- Were you the **host or a guest**?
- **Who was in the game**, and which of them had Dad's Decals (and which version)?
- **Steam or direct-IP** connection?
- **What happened**, and **roughly what time**? The logs are timestamped.
- **What you expected** to happen.

Send it to James, or attach it to an issue at <https://github.com/james-taplin/dads-decals/issues> (put "multiplayer" in the title).

> **Privacy:** `Player.log` can include your Steam name, your Windows user name (in folder paths) and other players' names. That's fine to send to James privately. If you post it in a public GitHub issue, have a quick look first and remove anything you'd rather not share.
