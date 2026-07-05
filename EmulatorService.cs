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
        public string FilePath { get; set; } = "";
        public string GameName { get; set; } = "";
        public string Emulator { get; set; } = "";
    }

    public class EmulatorService
    {
        private readonly IPlayniteAPI    _playniteApi;
        private readonly GameSnapSettings _settings;
        private readonly GameSnapLogger   _logger;

        public EmulatorService(IPlayniteAPI playniteApi, GameSnapSettings settings, GameSnapLogger logger)
        {
            _playniteApi = playniteApi;
            _settings    = settings;
            _logger      = logger;
        }

        // ──────────────────────────────────────────────
        // Static folder resolver — used by EmulatorProfile for status display
        // ──────────────────────────────────────────────
        public static string? GetDefaultFolder(string emulatorName)
        {
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var docs    = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            switch (emulatorName)
            {
                case "RetroArch":
                    return Check(Path.Combine(appdata, "RetroArch", "screenshots"));

                case "PCSX2":
                    return Check(Path.Combine(docs, "PCSX2", "snaps"))
                        ?? Check(Path.Combine(docs, "PCSX2 1.7.0", "snaps"));

                case "Dolphin":
                    return Check(Path.Combine(docs, "Dolphin Emulator", "ScreenShots"));

                case "RPCS3":
                    // No standard path — user must set custom
                    return null;

                case "Cemu":
                    return Check(Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFilesX86), "Cemu", "screenshots"))
                        ?? Check(Path.Combine("C:\\Cemu", "screenshots"));

                case "PPSSPP":
                    return Check(Path.Combine(docs, "PPSSPP", "screenshots"));

                case "mGBA":
                    return null; // no standard path

                case "DuckStation":
                    return Check(Path.Combine(docs, "DuckStation", "screenshots"));

                default:
                    return null;
            }
        }

        private static string? Check(string path)
            => Directory.Exists(path) ? path : null;

        // ──────────────────────────────────────────────
        // Returns all pending screenshots from active profiles
        // ──────────────────────────────────────────────
        public List<EmulatorScreenshot> GetPendingScreenshots()
        {
            var result = new List<EmulatorScreenshot>();

            foreach (var profile in _settings.EmulatorProfiles)
            {
                if (!profile.Enabled) continue;

                var folder = profile.ResolvedPath;
                if (folder == null) continue;

                try
                {
                    foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                        var gameName = ResolveGameName(file, profile.Name);
                        if (string.IsNullOrEmpty(gameName)) continue;

                        result.Add(new EmulatorScreenshot
                        {
                            FilePath = file,
                            GameName = gameName,
                            Emulator = profile.Name
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"EmulatorService [{profile.Name}]: {ex.Message}");
                }
            }

            return result;
        }

        // ──────────────────────────────────────────────
        // Resolve game name from file path
        // ──────────────────────────────────────────────
        private string? ResolveGameName(string filePath, string emulatorName)
        {
            // Strategy 1: file is inside a subfolder named after the game.
            // IMPORTANT: only trust this if the folder name actually matches something in
            // the Playnite library. RetroBat/RetroArch commonly organize screenshots into
            // subfolders named after the SYSTEM/CORE (e.g. "pcsx2", "duckstation",
            // "screenshots"), not the game — blindly using an unmatched folder name here
            // creates bogus folders named after cores instead of games.
            var parent = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");
            if (!string.IsNullOrEmpty(parent) &&
                !parent.Equals(emulatorName, StringComparison.OrdinalIgnoreCase) &&
                parent.Length > 2)
            {
                var match = FindPlayniteGame(parent);
                if (match != null) return match;
                // No match — don't trust the folder name, fall through to filename parsing.
            }

            // Strategy 2: filename starts with game name (RetroArch: GameName_YYYY-MM-DD.png)
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var m = Regex.Match(fileName, @"^(.+?)[\s_-]\d{4}");
            if (m.Success)
            {
                var candidate = m.Groups[1].Value.Trim();
                var match = FindPlayniteGame(candidate);
                if (match != null) return match;
                return CleanRomName(candidate);
            }

            // Strategy 3: use full filename without extension as game name
            var cleaned = CleanRomName(Path.GetFileNameWithoutExtension(filePath));
            if (!string.IsNullOrEmpty(cleaned))
            {
                var match = FindPlayniteGame(cleaned);
                return match ?? cleaned;
            }

            return null;
        }

        // ──────────────────────────────────────────────
        // Remove ROM-specific suffixes: (USA), [!], (Rev 1), etc.
        // ──────────────────────────────────────────────
        private static string CleanRomName(string name)
        {
            // Remove parentheses content: (USA), (Europe), (Rev 1), etc.
            name = Regex.Replace(name, @"\s*\([^)]*\)", "");
            // Remove bracket content: [!], [b], etc.
            name = Regex.Replace(name, @"\s*\[[^\]]*\]", "");
            // Remove trailing dashes and underscores
            name = name.Trim(' ', '-', '_');
            return name;
        }

        // ──────────────────────────────────────────────
        // Match against Playnite library
        // ──────────────────────────────────────────────
        private string? FindPlayniteGame(string name)
        {
            var norm = DictionaryService.Normalize(name);
            if (string.IsNullOrEmpty(norm)) return null;

            string? bestMatch    = null;
            int     bestDistance = int.MaxValue;

            foreach (var game in _playniteApi.Database.Games)
            {
                var normGame = DictionaryService.Normalize(game.Name);
                if (normGame == norm) return game.Name; // exact match

                if (normGame.Contains(norm) || norm.Contains(normGame))
                {
                    var dist = Math.Abs(normGame.Length - norm.Length);
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestMatch    = game.Name;
                    }
                }
            }

            return bestMatch;
        }
    }
}
