using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameSnapPlugin
{
    public class EmulatorScreenshot
    {
        public string FilePath   { get; set; } = "";
        public string GameName   { get; set; } = "";
        public string Emulator   { get; set; } = "";
    }

    public class EmulatorService
    {
        private readonly IPlayniteAPI   _playniteApi;
        private readonly GameSnapLogger _logger;
        private readonly GameSnapSettings _settings;

        // Known emulator screenshot folder patterns
        // Each entry: (EmulatorName, ScreenshotFolderResolver)
        private readonly List<(string Name, Func<string?> FolderResolver)> _emulators;

        public EmulatorService(IPlayniteAPI playniteApi, GameSnapSettings settings, GameSnapLogger logger)
        {
            _playniteApi = playniteApi;
            _settings    = settings;
            _logger      = logger;

            _emulators = new List<(string, Func<string?>)>
            {
                ("RetroArch",  ResolveRetroArch),
                ("PCSX2",      ResolvePCSX2),
                ("Dolphin",    ResolveDolphin),
                ("RPCS3",      ResolveRPCS3),
                ("Cemu",       ResolveCemu),
                ("PPSSPP",     ResolvePPSSPP),
                ("mGBA",       ResolveMGBA),
                ("DuckStation", ResolveDuckStation),
            };
        }

        // ──────────────────────────────────────────────
        // Returns all pending emulator screenshots with resolved game names
        // ──────────────────────────────────────────────
        public List<EmulatorScreenshot> GetPendingScreenshots()
        {
            var result = new List<EmulatorScreenshot>();

            foreach (var (name, resolver) in _emulators)
            {
                try
                {
                    var folder = resolver();
                    if (folder == null || !Directory.Exists(folder)) continue;

                    foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                        var gameName = ResolveGameName(file, name);
                        if (string.IsNullOrEmpty(gameName)) continue;

                        result.Add(new EmulatorScreenshot
                        {
                            FilePath = file,
                            GameName = gameName,
                            Emulator = name
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"EmulatorService [{name}]: {ex.Message}");
                }
            }

            // Custom emulator folders
            foreach (var folder in _settings.CustomEmulatorFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;
                    var gameName = ResolveGameName(file, "Custom");
                    if (string.IsNullOrEmpty(gameName)) continue;
                    result.Add(new EmulatorScreenshot
                    {
                        FilePath = file,
                        GameName = gameName,
                        Emulator = "Custom"
                    });
                }
            }

            return result;
        }

        // ──────────────────────────────────────────────
        // Resolve game name from file path
        // Most emulators use: screenshots/GameName/screenshot.png
        // or: screenshots/GameName_timestamp.png
        // ──────────────────────────────────────────────
        private string? ResolveGameName(string filePath, string emulatorName)
        {
            // Strategy 1: file is inside a subfolder named after the game
            var parent = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");
            if (!string.IsNullOrEmpty(parent) &&
                !parent.Equals(emulatorName, StringComparison.OrdinalIgnoreCase) &&
                parent.Length > 2)
            {
                // Try to match against Playnite library
                var match = FindPlayniteGame(parent);
                if (match != null) return match;
                return parent; // use folder name as-is
            }

            // Strategy 2: filename starts with game name (RetroArch pattern: GameName_YYYY-MM-DD.png)
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var m = Regex.Match(fileName, @"^(.+?)[\s_-]\d{4}");
            if (m.Success)
            {
                var candidate = m.Groups[1].Value.Trim();
                var match = FindPlayniteGame(candidate);
                if (match != null) return match;
                return candidate;
            }

            return null;
        }

        // ──────────────────────────────────────────────
        // Try to match a name against the Playnite library
        // ──────────────────────────────────────────────
        private string? FindPlayniteGame(string name)
        {
            var norm = DictionaryService.Normalize(name);
            foreach (var game in _playniteApi.Database.Games)
            {
                var normGame = DictionaryService.Normalize(game.Name);
                if (normGame == norm ||
                    normGame.Contains(norm) ||
                    norm.Contains(normGame))
                    return game.Name;
            }
            return null;
        }

        // ──────────────────────────────────────────────
        // Emulator folder resolvers
        // ──────────────────────────────────────────────

        private static string? ResolveRetroArch()
        {
            // RetroArch screenshots: %APPDATA%\RetroArch\screenshots
            // or custom config path
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path    = Path.Combine(appdata, "RetroArch", "screenshots");
            return Directory.Exists(path) ? path : null;
        }

        private static string? ResolvePCSX2()
        {
            // PCSX2 screenshots: Documents\PCSX2\snaps
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var candidates = new[]
            {
                Path.Combine(docs, "PCSX2", "snaps"),
                Path.Combine(docs, "PCSX2 1.7.0", "snaps"),
            };
            return candidates.FirstOrDefault(Directory.Exists);
        }

        private static string? ResolveDolphin()
        {
            // Dolphin: Documents\Dolphin Emulator\ScreenShots
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var path = Path.Combine(docs, "Dolphin Emulator", "ScreenShots");
            return Directory.Exists(path) ? path : null;
        }

        private static string? ResolveRPCS3()
        {
            // RPCS3: dev_hdd0\game\<gameid>\screenshots (next to rpcs3.exe)
            // Hard to detect without knowing install path — skip for now
            return null;
        }

        private static string? ResolveCemu()
        {
            // Cemu: screenshots folder next to Cemu.exe — no standard location
            // Try common locations
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Cemu", "screenshots"),
                Path.Combine("C:\\Cemu", "screenshots"),
            };
            return candidates.FirstOrDefault(Directory.Exists);
        }

        private static string? ResolvePPSSPP()
        {
            // PPSSPP: Documents\PPSSPP\screenshots
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var path = Path.Combine(docs, "PPSSPP", "screenshots");
            return Directory.Exists(path) ? path : null;
        }

        private static string? ResolveMGBA()
        {
            // mGBA: same folder as the ROM or custom — no standard location
            return null;
        }

        private static string? ResolveDuckStation()
        {
            // DuckStation: Documents\DuckStation\screenshots
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var path = Path.Combine(docs, "DuckStation", "screenshots");
            return Directory.Exists(path) ? path : null;
        }
    }
}
