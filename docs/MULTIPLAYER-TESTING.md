# Dad's Decals multiplayer beta (0.5.1-beta)

Thanks for testing! This is a **beta** of multiplayer support for [Derail Valley Multiplayer](https://github.com/AMacro/dv-multiplayer). Expect rough edges, and please don't use it on a save you'd hate to lose. Back the save up first.

## What this build does
- **Everyone can play together.** Players with and without Dad's Decals can join each other's games.
- **Everyone with Dad's Decals can place and edit decals.** That includes guests, and changes show up for everyone within a second or so.
  - The host's game is the referee. If two people change the same car at the same moment, one change is turned down, and the panel says so. Just have another go.
  - New decals remember who placed them.
- **Pictures are shared.** If you use a PNG the others don't have, it's sent to them automatically (up to 2 MB per picture). Received pictures are kept in `Mods\DadsDecals\Decals\_Multiplayer` and don't clutter your palette.
- **The host is in charge.** The host's settings are in UMM (Ctrl+F10) → **Dad's Decals** → the settings button:
  - **Guests can place and edit decals.** Untick this to make it view only.
  - **Block** a player. They can't place or edit any more, and everything they placed is removed. You can pick from the guests in the game or type a name.
- **Older versions:** players on Dad's Decals before 0.5 can still join, and get a polite chat message saying their version isn't supported in multiplayer. Players on 0.5.0 alphas can join, but their decals stay on their own screen until they update.

## Install
Everyone who wants to see and place decals needs:
1. [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)
2. [Mod Toolbar](https://github.com/imagitama/derail-valley-mod-toolbar)
3. [Derail Valley Multiplayer](https://github.com/AMacro/dv-multiplayer/releases) **0.1.16.0-Beta** (this build is made for that version)
4. **Dad's Decals 0.5.1-beta** (`DadsDecals-0.5.1-beta-MULTIPLAYER-BETA.zip`): install with UMM like any mod, replacing any older Dad's Decals.

To check it's installed: open UMM (Ctrl+F10). **Dad's Decals (multiplayer beta)** should show version **0.5.1** with a green dot.

## Things to try
You don't have to do all of these. Anything you notice is useful.

- [ ] **Host:** put a few decals on a loco (example images and some text), then have someone join. Do they see them, in the right places?
- [ ] **Guest:** place, move, resize, recolour and delete decals. Does the host (and everyone else) see it? Roughly how long does it take?
- [ ] **Everyone:** two people edit the same car at once. Does it sort itself out, and does everyone end up seeing the same thing?
- [ ] **Custom pictures:** someone uses a PNG the others don't have. Does it turn up for everyone?
- [ ] **Host:** untick **Guests can place and edit decals**, then tick it again. Do guests get told, and does it work?
- [ ] **Host:** block a guest, then unblock them. Their decals should vanish when blocked, and they should be able to place again after.
- [ ] **Undo**, **Clear all**, **templates**, **Copy from another car in this save**, layers and mirror, as host and as guest.
- [ ] **Cab decals** (inside a cab), as host and as guest.
- [ ] **Guest:** leave and rejoin. Are the decals still right?
- [ ] **Guest joins late,** after lots of decorated cars exist (10+ is great).
- [ ] **The host saves, quits and reloads.** Is everything guests placed still there?
- [ ] **A player without Dad's Decals joins.** They should join normally and just not see decals.
- [ ] **Single player still works as normal** for everyone.

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

Post it in the Dad's Decals bugs section on Nexus Mods, or attach it to an issue at <https://github.com/james-taplin/dads-decals/issues> (put "multiplayer" in the title).

> **Privacy:** `Player.log` can include your Steam name, your Windows user name (in folder paths) and other players' names. That's fine to send to James privately. If you post it in a public GitHub issue, have a quick look first and remove anything you'd rather not share.
