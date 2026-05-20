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

1. Checks the **dictionary** for a known alias → moves to the matching game folder
2. Falls back to **Playnite's currently running game** → moves and learns the alias for next time
3. Falls back to the **active window title** → moves and logs the detection
4. If nothing matches → **logs and ignores** so you can review and rename later

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

Any other tool that lets you set an output folder will work the same way.

### Step 2 — Configure GameSnap

In Playnite, go to **Add-ons → GameSnap → Settings** and fill in:

| Setting | Description |
|---|---|
| Source folder | The drop folder where all screenshots land |
| Destination base | The parent folder that contains your per-game subfolders |
| Use Playnite detection | Recommended — identifies the game while it's running |
| Use active window fallback | Secondary detection when no game is active in Playnite |
| Polling interval | Backup scan in seconds (in addition to the real-time file watcher) |
| Auto-create game folders | Automatically creates a subfolder when a new game is played (disabled by default) |

### Step 3 — Create your game folders

Inside the destination base, create one subfolder per game:

```
Captures\
├── Cyberpunk 2077\
├── Elden Ring\
├── Celeste\
└── ...
```

GameSnap will match screenshots to these folders automatically.

> **Tip:** Enable **Auto-create game folders** in Settings to have GameSnap create these folders automatically whenever you start a game for the first time. This replaces the need for a separate plugin like ScreenshotsVisualizer just for folder creation.

---

## Dictionary

The dictionary lets you map any filename prefix or alias to a game folder. It lives at:

```
%AppData%\Playnite\ExtensionsData\<plugin-id>\dictionary.txt
```

You can open it directly from **Add-ons → GameSnap → Settings → Open dictionary.txt**.

Format:

```
[Cyberpunk 2077]
cyberpunk
cp2077
Cyberpunk2077

[Elden Ring]
eldenring
ELDEN RING
```

GameSnap **learns automatically** — when Playnite detection identifies a game, the alias is saved to the dictionary so future screenshots are matched instantly without Playnite needing to be running.

---

## Menu reference

| Menu item | Where | What it does |
|---|---|---|
| Organize screenshots now | Main menu → GameSnap | Manually triggers a full scan of the source folder |
| Open log | Main menu → GameSnap | Opens the log file in Notepad |
| Open dictionary | Main menu → GameSnap | Opens dictionary.txt in Notepad |
| Organize screenshots now | Right-click a game | Same as above, scoped to context |

---

## Troubleshooting

**Screenshots are not being moved**  
→ Check that the source and destination folders are correctly set in Settings.  
→ Open the log (**Main menu → GameSnap → Open log**) to see what happened.

**Wrong game folder was chosen**  
→ Move the file manually and add the correct alias to `dictionary.txt`.

**A game has no folder yet**  
→ Create the subfolder inside the destination base. The next scan will pick it up.  
→ Or enable **Auto-create game folders** in Settings so GameSnap handles this automatically.

**ShareX is saving files with timestamps only, no game name**  
→ In ShareX, set the name pattern to `%t %y-%mo-%d_%h-%mi-%s` so the window title is included.

**Where is the log?**  
→ **Main menu → GameSnap → Open log**, or navigate to:  
`%AppData%\Playnite\ExtensionsData\<plugin-id>\gamesnap.log`

---

## Migrating from the script version

If you were using the PowerShell script version posted on Reddit, remove these from Playnite's Scripts tab:

- **Game Started** → the `playnite_current_game.txt` writer
- **Game Finished** → the `Remove-Item` cleanup
- **App Scripts → On start** → the `wscript.exe LaunchWatcher.vbs` line
- **App Scripts → On shutdown** → the `Stop-Process` block

GameSnap handles all of this natively.

---

## Contributing

Pull requests are welcome! Ideas for improvement:

- **Steam screenshot support** — detect and move screenshots from Steam's own folder
- **Notification on move** — optional Playnite toast when files are organized
- **Per-game source folders** — support multiple drop folders for different tools

---

## License

MIT — see [LICENSE.txt](LICENSE.txt)

---

## Support

If GameSnap saved you some time, a coffee is always appreciated! ☕

[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-☕-yellow)](https://buymeacoffee.com/TokamiGankei)
