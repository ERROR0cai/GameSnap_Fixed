# Changelog

All notable changes to GameSnap are documented here.  
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.3.0] — 2026-05-29

### Added
- Screenshot counter in game right-click menu — shows total file count and opens the game folder on click
- Emulator screenshot support (disabled by default) — auto-detects RetroArch, PCSX2, Dolphin, DuckStation, PPSSPP and Cemu; custom folders configurable for unlisted emulators

### Fixed
- Playnite crash on startup caused by unhandled exceptions in plugin initialization — all critical methods now have defensive try/catch
- Settings loss on restart — BeginEdit now notifies all bound fields correctly on reload; EndEdit shows an error dialog if save fails
- New settings fields (CustomEmulatorFolders) being reset after plugin update — added to merge logic in LoadSettings

---

## [1.2.0] — 2026-05-28

### Added
- **Review window** — new UI inside Playnite to manually assign unmatched screenshots to games (`Extensions → GameSnap → Review unmatched screenshots`)
  - Image preview panel
  - Game search with real-time filter
  - Assign, Skip, and Delete actions
  - Auto-learns alias after manual assignment
- **Tooltips** on every setting in the Settings screen — hover over any option to see a description, usage tip, or warning

### Fixed
- `Open log` and `Open dictionary` menu items not opening Notepad
- `Watcher stopped` appearing twice in the log on settings save
- Settings not persisting between sessions — now reloads from disk on every Settings open
- Settings being reset after plugin update — new fields now merge with saved data instead of overwriting

### Improved
- Plugin startup is now fully asynchronous — Playnite initialization is no longer blocked
- File events run off the main thread — no UI freezes when screenshots arrive
- Window fallback now uses two-stage logic:
  - **Rule C:** only activates if the file prefix is already known in the dictionary
  - **Rule D:** only activates during an active game session (between `OnGameStarted` and `OnGameStopped`)
- Window blacklist expanded to block Explorer, browsers, email clients, terminals, and Playnite itself
- Automatic pattern blocking for titles containing "e X mais guias", "Explorador de Arquivos", email addresses, OneDrive, etc.

---

## [1.1.0] — 2026-05-25

### Added
- **Notifications** — Playnite toast when screenshots are organized (e.g. "Organized 3 screenshot(s): Elden Ring (2) | Celeste (1)")
- **Unmatched folder** — move unrecognized screenshots to `_Unmatched` instead of leaving them in the source folder (disabled by default)
- **Multiple source folders** — monitor more than one capture folder simultaneously
- **Rename pattern** — customize the output filename using tokens: `{game}`, `{date}`, `{time}`, `{datetime}`, `{original}`
- **Automatic backup** — copy organized screenshots to a second destination after moving (disabled by default)
- **Steam screenshot support** — monitors Steam's `userdata` folder and organizes screenshots using your Playnite library to resolve AppIDs (disabled by default)
- **Screenshots Utilities Local Provider integration** — automatically registers GameSnap's destination folder in the Local Provider `config.json` so screenshots appear in fullscreen viewers like Aniki Remake (disabled by default)
- **Auto-create game folders** — creates a subfolder for each game when it first starts in Playnite (disabled by default)

---

## [1.0.0] — 2026-05-19

### Initial release

- Real-time file watcher on the source folder (no polling delay)
- Three-stage detection: Dictionary → Playnite → Active window fallback
- Auto-learning: aliases saved to `dictionary.txt` when Playnite detection is used
- Videos moved to `GameFolder\Videos\` subfolder
- Backup polling loop as safety net alongside the file watcher
- `Open log` and `Open dictionary` shortcuts in the GameSnap menu
- `Organize screenshots now` available from the main menu and right-click on any game
- Settings screen with Browse buttons for all folder paths
- Works with Xbox Game Bar, ShareX, and any capture tool with a configurable output folder
