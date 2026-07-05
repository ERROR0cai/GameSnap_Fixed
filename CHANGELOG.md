# Changelog

All notable changes to GameSnap are documented here.  
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.4.4] — 2026-07-05

### Added
- **Emulator folder scanning now consults `dictionary.txt`** before matching against
  the Playnite library or existing folders. Many arcade/Neo Geo ROMs save native
  screenshots using their internal short name (`mslug`, `garou`, `matrim`...) instead
  of the display title, which doesn't fuzzy-match Playnite's game names — these can
  now be mapped with a normal dictionary alias, same as the ShareX flow. No
  auto-learning here (unlike the ShareX/Playnite path); aliases must be added manually.

---

## [1.4.3] — 2026-07-05

### Fixed
- **Emulator folder scanning created folders named after cores/systems** —
  `ResolveGameName` trusted any subfolder name as the game name, even when it
  didn't match anything in the Playnite library. RetroBat organizes native
  screenshots into subfolders named after the **system/core** (e.g. `pcsx2`,
  `duckstation`, `screenshots`), not the game, which created bogus folders
  named after those cores. The subfolder name is now only used when it
  actually matches a game in the Playnite library; otherwise resolution falls
  through to filename-based parsing (the reliable `RomName-Date-...`
  convention RetroArch itself uses).

---

## [1.4.2] — 2026-07-05

### Fixed
- **Emulator folder scanning had no effect** — `EmulatorService` was never instantiated
  or assigned to `OrganizerService.EmulatorService` in `GameSnapPlugin.InitServices`,
  so `OrganizeEmulators()` always returned immediately regardless of settings. Now
  wired up the same way as `SteamService`, gated by **Enable emulator screenshot
  support**.

### Documentation
- Updated README: folder-scanning emulator support is now functional and documented
  as a complementary automatic path alongside Emulator process prefixes, for emulators
  where the native in-emulator screenshot function is used instead of ShareX.

---

## [1.4.1] — 2026-07-05

### Added
- **Emulator process prefixes** — new setting (Settings → Detection) listing filename
  prefixes (default: `retroarch, pcsx2, dolphin, rpcs3, cemu, ppsspp, mgba, duckstation`)
  that skip dictionary and active-window detection entirely and use only the game
  currently reported as running by Playnite. Fixes core-only emulators (RetroArch under
  RetroBat, etc.) where the window title/process name only shows the core, not the ROM —
  which previously caused the dictionary to learn the first ROM detected and reuse that
  folder for every subsequent ROM using the same core.

### Documentation
- Clarified in README that the folder-scanning **Emulator support** feature
  (Settings → Emulators) is currently not wired into the plugin and has no effect even
  when enabled; recommended using Emulator process prefixes instead.
- Added troubleshooting entry for the "every emulator screenshot lands in the same
  folder" symptom.

---

## [1.4.0] — 2026-06-08

### Added
- **Fullscreen Review window for gamepad** — new "Review unmatched
  screenshots (Fullscreen / Gamepad)" menu item opens a black fullscreen
  window designed for TV/couch use; Xbox controller support via Windows
  XInput mapping (A = Assign, B = Close, Start = Skip, D-pad = Navigate)

- **ScreenshotsVisualizer auto-refresh toggle** — new option in Settings under
  a dedicated ScreenshotsVisualizer section (disabled by default); when enabled,
  GameSnap notifies ScreenshotsVisualizer to rescan each affected game after
  organizing; has no effect if ScreenshotsVisualizer is not installed

### Changed
- GitHub releases now publish as **drafts** — allows testing before making a
  release public to other users
- README updated with a dedicated ScreenshotsVisualizer section, including the
  suggested `GlobalScreenshootsPath` configuration tip (`{DestinationBase}\{Name}`)

### Removed
- **Screenshot Utilities Local Provider integration** — removed from settings
  and codebase; ScreenshotsVisualizer covers this use case more effectively

  ---

## [1.3.3] — 2026-06-07

### Fixed
- **Settings amnesia (root cause)** — plugin GUID was a placeholder (`a1b2c3d4-...`) that collided with another plugin's data folder; replaced with a real unique GUID (`1826881c-4e6e-4ed3-ac6c-8605f953daf4`); config.json is now written to the correct location
- **Settings pattern rewritten** — adopted the ScreenshotsVisualizer/Ludusavi pattern exactly: `GameSnapSettings` is a pure data DTO extending `ObservableObject`; `GameSnapSettingsViewModel` implements `ISettings`, uses `Serialization.GetClone()` for `BeginEdit` snapshot, and `RelayCommand<object>` for commands
- **Emulator paths not persisting** — `EmulatorProfiles` ObservableCollection in the ViewModel is now synced to/from the DTO `List<T>` on `EndEdit`/`CancelEdit`; the XAML `ItemsControl` binds to `{Binding EmulatorProfiles}` on the ViewModel directly
- **Duplicate entries in config.json** — all list properties in `GameSnapSettings` now initialize as empty; defaults are applied by the ViewModel constructor only when lists are empty after deserialization, preventing Newtonsoft from appending items on top of existing ones
- **Crash on Save (circular reference)** — `Settings => this` property caused `JsonSerializationException`; resolved by separating the ISettings object from the data DTO
- **Browse button broken in Emulators tab** — `EmulatorsView.xaml.cs` was casting `DataContext` to `SettingsViewModel` instead of `GameSnapSettingsViewModel`
- **Duplicate `EmulatorsView.xaml.cs`** — phantom file in project root removed
- **Build failures** — removed stale `using Newtonsoft.Json`, duplicate `using System.Collections.ObjectModel`, and `SettingsViewModel` references that no longer exist

### Changed
- `GetSettings()` now returns `PluginSettings` (the ViewModel singleton), never `new SettingsViewModel()` — prevents state loss on settings reopen
- `SettingsViewModel.cs` reduced to an empty stub; all logic lives in `Settings.cs`
- `EmulatorProfile` computed properties (`IsCustom`, `DisplayPath`, `StatusText`, `StatusColor`, `ResolvedPath`) marked with `[IgnoreDataMember]` to prevent serialization of non-persistent data

---

## [1.3.2] — 2026-05-31

### Fixed
- Settings loss on restart — root cause identified: computed properties in `EmulatorProfile` (`IsCustom`, `DisplayPath`, `StatusText`, `StatusColor`, `ResolvedPath`) were being serialized to `config.json` and causing a version conflict when Playnite's internal Newtonsoft.Json tried to deserialize them; fixed by replacing `[JsonIgnore]` with empty setters, making deserialization always succeed regardless of Newtonsoft version
- Removed separate `Newtonsoft.Json` package reference that was conflicting with Playnite's bundled version

---

## [1.3.1] — 2026-05-30

### Fixed
- Settings loss on restart caused by JSON serialization failure — computed properties in `EmulatorProfile` (`StatusText`, `DisplayPath`, `StatusColor`, etc.) were being serialized and failing on deserialization, causing the catch block to return empty defaults; fixed with `[JsonIgnore]` on all computed properties
- Added `Settings saved` and `Settings loaded` entries to the log for easier debugging

---

## [1.3.0] — 2026-05-30

### Added
- Screenshot counter in game right-click menu — shows total file count and opens the game folder on click
- Emulator screenshot support (disabled by default) — dedicated **Emulators** tab in Settings with per-emulator toggles, auto-detection status and custom path override
- Built-in support for: RetroArch, PCSX2, Dolphin, RPCS3, Cemu, PPSSPP, mGBA, DuckStation
- Custom emulator button — add any unlisted emulator with its screenshot folder
- ROM name cleaning — strips region tags like `(USA)`, `(Rev 1)`, `[!]` before matching to Playnite library

### Fixed
- Playnite crash on startup — all critical methods wrapped in try/catch; plugin now falls back to defaults instead of crashing
- Settings loss on restart — race condition between async startup and settings loader resolved
- New settings fields now merge correctly with saved data on plugin update

---

## [1.2.0] — 2026-05-28

### Added
- **Review window** — new UI inside Playnite to manually assign unmatched screenshots to games (`Extensions → GameSnap → Review unmatched screenshots`)
  - Image preview panel
  - Game search with real-time filter
  - Assign, Skip, and Delete actions
  - Auto-learns alias after manual assignment
- **Tooltips** on every setting in the Settings screen — hover over any option to see a description, usage tip, or warning

---

## [1.1.0] — 2026-05-23

### Added
- Steam screenshot support — automatically detects Steam userdata folders and maps AppIDs to game names
- Local Provider integration — registers destination folder with Screenshot Utilities Local Provider
- Backup system — optional automatic backup before moving files
- Rename pattern tokens — `{game}`, `{date}`, `{time}`, `{ext}`
- Multiple source folders support

### Fixed
- Window title fallback detection improved — better filtering with blacklist
- Dictionary learning — aliases now saved automatically after successful window-title match

---

## [1.0.0] — 2026-05-20

### Added
- Initial release
- Real-time file watcher on source folder
- Playnite game detection (currently running game)
- Active window title fallback detection
- Dictionary-based alias matching
- `_Unmatched` folder for unrecognized screenshots
- Toast notifications
- Auto-create game folders on game start
- Settings UI with source/destination folder configuration
