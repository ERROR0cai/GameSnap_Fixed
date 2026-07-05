# GameSnap

A [Playnite](https://playnite.link/) plugin that **automatically organizes your game screenshots** into per-game folders — works with Xbox Game Bar, ShareX, and any other capture tool.

---

## Screenshots

![Right Click Menu](screenshots/01_Right_Click.png)
![Settings](screenshots/02_Settings.png)
![Organized Folders](screenshots/03_Folders.png)
![Screenshots of Games](screenshots/04_Screenshots_of_games.png)

---

## How it works

GameSnap watches a single "drop folder" where all your screenshots land, regardless of which tool captured them. When a new file appears, it:

1. Checks if the filename prefix matches a known **emulator process** (e.g. `retroarch`) → skips the dictionary and window detection entirely and uses only the game currently running in Playnite (see [Emulator process prefixes](#emulator-process-prefixes) below)
2. Checks the **dictionary** for a known alias → moves to the matching game folder
3. Falls back to **Playnite's currently running game** → moves and learns the alias for next time
4. Falls back to the **active window title** → moves and logs the detection
5. If nothing matches → moves to `_Unmatched` folder for manual review

Everything runs natively inside Playnite — no background `.ps1` scripts, no `.vbs` launchers, no manual setup in the Scripts tab.

---

## Requirements

- Windows 10 or 11
- [Playnite](https://playnite.link/) 9 or later
- .NET Framework 4.8 (included in Windows 10/11)
- A capture tool that saves to a configurable folder (Xbox Game Bar, ShareX, etc.)

---

## Installation

### Option A — Direct download (recommended)

1. Download the latest `.pext` file from the [Releases](https://github.com/TokamiGankei/GameSnap/releases) page
2. Double-click the `.pext` file — Playnite will install it automatically
3. Restart Playnite

### Option B — Manual

1. Download and extract the release `.zip`
2. Copy the folder to:  
   `%AppData%\Playnite\Extensions\GameSnap`
3. Restart Playnite

---

## Setup

### Step 1 — Configure your capture tool

Point your capture tool's output to a single folder, for example `C:\Captures\`.

**Xbox Game Bar:** Settings → Gaming → Captures → Change where clips are saved

**ShareX:** Task Settings → File naming → Override Screenshots Folder per hotkey

### Step 2 — Configure GameSnap

In Playnite, go to **Add-ons → GameSnap → Settings** and fill in:

| Setting | Description |
|---|---|
| Source folder | The drop folder where all screenshots land |
| Destination base | The parent folder that contains your per-game subfolders |
| Use Playnite detection | Recommended — identifies the game while it's running |
| Use active window fallback | Secondary detection when no game is active in Playnite |
| Emulator process prefixes | Filename prefixes (e.g. `retroarch`) that bypass dictionary/window detection and use only the active Playnite game — see [Emulator process prefixes](#emulator-process-prefixes) |
| Auto-create game folders | Automatically creates a subfolder when a new game is played |

### Step 3 — Create your game folders

Inside the destination base, create one subfolder per game:

```
ScreenShots\
├── Cyberpunk 2077\
├── Elden Ring\
├── Celeste\
└── ...
```

> **Tip:** Enable **Auto-create game folders** in Settings to have GameSnap create these automatically whenever you start a game for the first time.

---

## Works great with ScreenshotsVisualizer

GameSnap pairs naturally with [ScreenshotsVisualizer](https://github.com/Lacro59/playnite-screenshotsvisualizer-plugin). They serve different purposes:

- **GameSnap** organizes screenshots into per-game folders automatically
- **ScreenshotsVisualizer** displays and browses those screenshots inside Playnite

When both are installed, GameSnap automatically notifies ScreenshotsVisualizer to refresh whenever a screenshot is moved — so your screenshots appear instantly in the viewer without any manual refresh.

### Suggested ScreenshotsVisualizer configuration

In ScreenshotsVisualizer settings, set the **Global screenshots path** to:

```
H:\Your\ScreenShots\Destination\{Name}
```

Replace `H:\Your\ScreenShots\Destination\` with your GameSnap **Destination base** folder. The `{Name}` token is resolved automatically by ScreenshotsVisualizer to the game name, matching the subfolders GameSnap creates.

This single setting covers your entire library without configuring each game individually.

---

## Emulator process prefixes

Some emulators (RetroArch running under RetroBat is the main case) only expose the **core name** in their window title and process name — not the ROM/game name. `RetroArch Snes9x 1.63 ...` looks the same whether you're playing *Demon's Crest* or *Super Mario World*.

If you let GameSnap's normal dictionary/window detection handle these screenshots, it will learn the **first** game it sees for that prefix (e.g. `retroarch → Demon's Crest`) and then keep reusing that same folder for every future ROM — even though the dictionary was doing exactly what it's designed to do.

**Emulator process prefixes** fixes this: any filename that starts with a configured prefix skips the dictionary and window-title detection entirely and is placed using **only** the game Playnite currently reports as running. If no game is active in Playnite, the file goes to `_Unmatched` instead of guessing.

Configure this in **Settings → Detection → Emulator process prefixes** (comma-separated). Defaults: `retroarch, pcsx2, dolphin, rpcs3, cemu, ppsspp, mgba, duckstation`.

> **Requirement:** this only works while the game is actually running through Playnite (so it knows which game is active). It does **not** require ShareX/Xbox Game Bar to see the ROM name — Playnite is the source of truth for these prefixes.

---

## Emulator support (folder scanning)

Settings → Emulators lets you point GameSnap directly at each emulator's own screenshot folder (e.g. `%AppData%\RetroArch\screenshots`, or wherever RetroBat redirects it) as an **automatic, second capture path** alongside the drop-folder approach above — the same idea as ScreenshotsVisualizer.

This is a good fit for emulators like RetroArch, where the emulator's own screenshot function already embeds the ROM name in the filename (`RomName-Date-Numbers.png`), so no detection guesswork is needed — GameSnap just reads the folder, matches the filename to your Playnite library, and moves it. It also avoids any capture-tool latency, since it reads what the emulator itself saved rather than relying on an external hotkey capture.

You can use this together with [Emulator process prefixes](#emulator-process-prefixes): folder scanning for emulators where you use the native in-emulator screenshot function, and the prefix bypass for anything still captured through ShareX/Xbox Game Bar.

Supported out of the box: RetroArch, PCSX2, Dolphin, RPCS3, Cemu, PPSSPP, mGBA, DuckStation. Custom emulators can be added with the **+ Add emulator** button. Each entry needs its checkbox enabled and either an auto-detected path or a custom one.

**ROM internal names:** many arcade/Neo Geo ROMs save screenshots using their internal short name (`mslug`, `garou`, `kof98`...) instead of the full display title, which won't fuzzy-match your Playnite library. Add an alias to the same `dictionary.txt` you already use for PC games:
```
[Metal Slug X - Super Vehicle-001]
mslug

[Garou: Mark of the Wolves]
garou
```
The dictionary is checked before folder/library matching, for both ShareX-captured and natively-captured emulator screenshots.

---

## Dictionary

The dictionary lets you map any filename prefix or alias to a game folder. Open it from **Add-ons → GameSnap → Open dictionary**.

Format:

```
[Cyberpunk 2077]
cyberpunk
cp2077

[Elden Ring]
eldenring
ELDEN RING
```

GameSnap **learns automatically** — when Playnite detection identifies a game, the alias is saved so future screenshots are matched instantly.

---

## Menu reference

| Menu item | Where | What it does |
|---|---|---|
| Organize screenshots now | Main menu → GameSnap | Manually triggers a full scan of the source folder |
| Open log | Main menu → GameSnap | Opens the log file in Notepad |
| Open dictionary | Main menu → GameSnap | Opens dictionary.txt in Notepad |
| Review unmatched screenshots | Main menu → GameSnap | Opens the review window for unmatched files |
| Organize screenshots now | Right-click a game | Same as above, scoped to context |

---

## Troubleshooting

**Screenshots are not being moved**  
→ Check that source and destination folders are correctly set.  
→ Open the log (**Main menu → GameSnap → Open log**) to see what happened.

**Wrong game folder was chosen**  
→ Move the file manually and add the correct alias to the dictionary.

**Every screenshot from an emulator lands in the same game folder, regardless of which ROM is running**  
→ The dictionary learned a `prefix → game` alias from the first ROM detected (e.g. `retroarch → Demon's Crest`) and is now reusing it for everything. Remove that alias from the dictionary (**Open dictionary**), then add the emulator's process prefix to **Emulator process prefixes** in Settings so this can't happen again.

**A game has no folder yet**  
→ Create the subfolder inside the destination base, or enable **Auto-create game folders**.

**ShareX is saving files with timestamps only**  
→ In ShareX, set the name pattern to `%t %y-%mo-%d_%h-%mi-%s` so the window title is included.

---

## License

MIT — see [LICENSE.txt](LICENSE.txt)

---

## Support

If GameSnap saved you some time, a coffee is always appreciated! ☕

[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-☕-yellow)](https://buymeacoffee.com/TokamiGankei)
