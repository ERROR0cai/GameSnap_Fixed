using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace GameSnapPlugin
{
    public class OrganizerService
    {
        private readonly GameSnapSettings   _settings;
        private readonly DictionaryService  _dictionary;
        private readonly GameSnapLogger     _logger;

        // Callback para notificações (injetado pelo plugin principal)
        public Action<string, string>? OnFileMoved { get; set; }

        // Lista de jogos organizados neste ciclo — para notificar ScreenshotsVisualizer
        public Action<List<string>>? OnGamesOrganized { get; set; }

        // Jogo atual informado pelo Playnite
        private string? _currentGame;

        // Cache de arquivos já processados nesta sessão
        private readonly HashSet<string> _processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public OrganizerService(GameSnapSettings settings, DictionaryService dictionary, GameSnapLogger logger)
        {
            _settings   = settings;
            _dictionary = dictionary;
            _logger     = logger;
        }

        public void SetCurrentGame(string? name) => _currentGame = name;

        // ──────────────────────────────────────────────
        // Entry point — chamado pelo watcher e pelo loop
        // ──────────────────────────────────────────────
        public SteamService? SteamService { get; set; }
        public EmulatorService? EmulatorService { get; set; }

        public void Organize()
        {
            var dict = _dictionary.Load();

            // Steam screenshots
            if (_settings.EnableSteamSupport && SteamService != null)
                OrganizeSteam();

            // Emulator screenshots
            if (_settings.EnableEmulatorSupport && EmulatorService != null)
                OrganizeEmulators(dict);

            var allSources = new List<string>();

            if (!string.IsNullOrEmpty(_settings.SourceFolder))
                allSources.Add(_settings.SourceFolder);

            allSources.AddRange(_settings.AdditionalSourceFolders
                .Where(f => !string.IsNullOrEmpty(f) && Directory.Exists(f)));

            if (allSources.Count == 0) return;
            if (!Directory.Exists(_settings.DestinationBase)) return;

            var folders = LoadFolders();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var matchScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in allSources)
            {
                if (!Directory.Exists(source)) continue;
                foreach (var file in Directory.GetFiles(source))
                {
                    TryOrganizeFile(file, dict, folders, counts, matchScores);
                }
            }

            if (counts.Count > 0)
            {
                var total = counts.Values.Sum();
                var summary = string.Join(" | ", counts.Select(kv => $"{kv.Key} ({kv.Value})"));
                var scoreSummary = string.Join(" | ", matchScores.Select(kv => $"{kv.Key} ({kv.Value}%)"));

                _logger.Info($"Organized: {summary}");
                OnFileMoved?.Invoke(
                    "GameSnap",
                    $"Organized {total} screenshot(s): {summary}  [Score: {scoreSummary}]"
                );
                OnGamesOrganized?.Invoke(counts.Keys.ToList());
            }
        }

        // ──────────────────────────────────────────────
        // Steam
        // ──────────────────────────────────────────────
        private void OrganizeSteam()
        {
            if (SteamService == null) return;

            var steamPath = !string.IsNullOrEmpty(_settings.SteamPath)
                ? _settings.SteamPath
                : SteamService.DetectSteamPath() ?? "";

            if (string.IsNullOrEmpty(steamPath)) return;

            var pending = SteamService.GetPendingScreenshots(steamPath);
            if (pending.Count == 0) return;

            var folders = LoadFolders();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var matchScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var ss in pending)
            {
                if (_processed.Contains(ss.FilePath)) continue;

                var gameName = SteamService.ResolveGameName(ss.AppId);
                if (gameName == null)
                {
                    _logger.Write(LogType.Error,
                        $"Steam: AppID {ss.AppId} not found in library. File: {Path.GetFileName(ss.FilePath)}");
                    TryMoveToUnmatched(ss.FilePath, Path.GetExtension(ss.FilePath).ToLowerInvariant());
                    continue;
                }

                var matchResult = FindBestMatchByScore(gameName, folders, _settings.MatchThreshold);
                var match = matchResult.Folder;

                if (match == null)
                {
                    _logger.Write(LogType.Error,
                        $"Steam: No folder for '{gameName}'. File: {Path.GetFileName(ss.FilePath)}");
                    TryMoveToUnmatched(ss.FilePath, Path.GetExtension(ss.FilePath).ToLowerInvariant());
                    continue;
                }

                // Record match score
                if (!matchScores.ContainsKey(match.NameOriginal) || matchResult.Score > matchScores[match.NameOriginal])
                {
                    matchScores[match.NameOriginal] = matchResult.Score;
                }

                var ext = Path.GetExtension(ss.FilePath).ToLowerInvariant();
                var date = GetBestDate(ss.FilePath);
                var destName = BuildDestName(match.NameOriginal, date,
                                            Path.GetFileNameWithoutExtension(ss.FilePath), ext);
                var destPath = Path.Combine(match.Path, destName);

                int i = 1;
                while (File.Exists(destPath))
                {
                    var nameNoExt = Path.GetFileNameWithoutExtension(destName);
                    destPath = Path.Combine(match.Path, $"{nameNoExt}_{i}{ext}");
                    i++;
                }

                try
                {
                    File.Move(ss.FilePath, destPath);

                    if (_settings.EnableBackup && !string.IsNullOrEmpty(_settings.BackupFolder))
                        TryBackup(destPath, match.NameOriginal, false);

                    _processed.Add(ss.FilePath);

                    int current = counts.ContainsKey(match.NameOriginal) ? counts[match.NameOriginal] : 0;
                    counts[match.NameOriginal] = current + 1;

                    _logger.Write(LogType.Move,
                        $"Steam: {Path.GetFileName(ss.FilePath)} → {match.NameOriginal} (Score: {matchResult.Score}%)");
                }
                catch (Exception ex)
                {
                    _logger.Write(LogType.Error,
                        $"Steam move failed: {ex.Message}");
                }
            }

            if (counts.Count > 0)
            {
                var total = counts.Values.Sum();
                var summary = string.Join(" | ", counts.Select(kv => $"{kv.Key} ({kv.Value})"));
                var scoreSummary = string.Join(" | ", matchScores.Select(kv => $"{kv.Key} ({kv.Value}%)"));
                OnFileMoved?.Invoke("GameSnap", $"Steam: {total} screenshot(s): {summary}  [Score: {scoreSummary}]");
            }
        }

        // ──────────────────────────────────────────────
        // Emulators
        // ──────────────────────────────────────────────
        private void OrganizeEmulators(Dictionary<string, string> dict)
        {
            if (EmulatorService == null) return;

            var pending = EmulatorService.GetPendingScreenshots();
            if (pending.Count == 0) return;

            var folders = LoadFolders();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var matchScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var ss in pending)
            {
                if (_processed.Contains(ss.FilePath)) continue;

                var resolvedName = ss.GameName;
                var normCandidate = DictionaryService.Normalize(ss.GameName);
                if (dict.TryGetValue(normCandidate, out var fromDict))
                    resolvedName = fromDict;

                var matchResult = FindBestMatchByScore(resolvedName, folders, _settings.MatchThreshold);
                var match = matchResult.Folder;

                if (match == null)
                {
                    // Auto-create if enabled
                    if (_settings.AutoCreateFolders)
                    {
                        var invalid = Path.GetInvalidFileNameChars();
                        var folderName = string.Concat(resolvedName.Split(invalid)).Trim();
                        var newPath = Path.Combine(_settings.DestinationBase, folderName);
                        Directory.CreateDirectory(newPath);
                        folders = LoadFolders(); // refresh
                        match = folders.FirstOrDefault(f =>
                            DictionaryService.Normalize(f.NameOriginal) == DictionaryService.Normalize(folderName));
                    }

                    if (match == null)
                    {
                        _logger.Write(LogType.Error,
                            $"Emulator [{ss.Emulator}]: No folder for '{resolvedName}'. File: {Path.GetFileName(ss.FilePath)}");
                        TryMoveToUnmatched(ss.FilePath, Path.GetExtension(ss.FilePath).ToLowerInvariant());
                        continue;
                    }
                }

                // Record match score
                if (!matchScores.ContainsKey(match.NameOriginal) || matchResult.Score > matchScores[match.NameOriginal])
                {
                    matchScores[match.NameOriginal] = matchResult.Score;
                }

                var ext = Path.GetExtension(ss.FilePath).ToLowerInvariant();
                var date = GetBestDate(ss.FilePath);
                var destName = BuildDestName(match.NameOriginal, date,
                                            Path.GetFileNameWithoutExtension(ss.FilePath), ext);
                var destPath = Path.Combine(match.Path, destName);

                int i = 1;
                while (File.Exists(destPath))
                {
                    var nameNoExt = Path.GetFileNameWithoutExtension(destName);
                    destPath = Path.Combine(match.Path, $"{nameNoExt}_{i}{ext}");
                    i++;
                }

                try
                {
                    File.Move(ss.FilePath, destPath);

                    if (_settings.EnableBackup && !string.IsNullOrEmpty(_settings.BackupFolder))
                        TryBackup(destPath, match.NameOriginal, false);

                    _processed.Add(ss.FilePath);

                    int current = counts.ContainsKey(match.NameOriginal) ? counts[match.NameOriginal] : 0;
                    counts[match.NameOriginal] = current + 1;

                    _logger.Write(LogType.Move,
                        $"Emulator [{ss.Emulator}]: {Path.GetFileName(ss.FilePath)} → {match.NameOriginal} (Score: {matchResult.Score}%)");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Emulator move failed: {ex.Message}");
                }
            }

            if (counts.Count > 0)
            {
                var total = counts.Values.Sum();
                var summary = string.Join(" | ", counts.Select(kv => $"{kv.Key} ({kv.Value})"));
                var scoreSummary = string.Join(" | ", matchScores.Select(kv => $"{kv.Key} ({kv.Value}%)"));
                OnFileMoved?.Invoke("GameSnap", $"Emulators: {total} screenshot(s): {summary}  [Score: {scoreSummary}]");
            }
        }

        // ──────────────────────────────────────────────
        // Processa um arquivo individual
        // ──────────────────────────────────────────────
        private void TryOrganizeFile(
            string filePath,
            Dictionary<string, string> dict,
            List<FolderEntry> folders,
            Dictionary<string, int> counts,
            Dictionary<string, int> matchScores)
        {
            if (_processed.Contains(filePath)) return;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            bool isImage = _settings.ImageExtensions.Contains(ext);
            bool isVideo = _settings.VideoExtensions.Contains(ext);

            if (!isImage && !isVideo) return;

            // Small delay to ensure file is fully written — non-blocking
            System.Threading.Thread.Sleep(800);

            var fileName = Path.GetFileName(filePath);
            var prefix = GetPrefix(fileName);
            var normPfx = DictionaryService.Normalize(prefix);

            string? game = null;
            string method = "UNKNOWN";

            // 0. Bypass de emulador
            bool isEmulatorPrefix = _settings.EmulatorPrefixes
                .Any(p => normPfx.Equals(DictionaryService.Normalize(p), StringComparison.OrdinalIgnoreCase));

            if (isEmulatorPrefix)
            {
                if (!string.IsNullOrEmpty(_currentGame))
                {
                    game = _currentGame;
                    method = "EMULATOR-PLAYNITE";
                }
                else
                {
                    _logger.Write(LogType.Error,
                        $"File: {fileName}\nReason: Emulator prefix '{prefix}' but no active Playnite game");
                    TryMoveToUnmatched(filePath, ext);
                    return;
                }
            }

            // 1. Dicionário
            if (game == null && dict.TryGetValue(normPfx, out var fromDict))
            {
                game = fromDict;
                method = "DICTIONARY";
            }

            // 2. Playnite
            if (game == null && _settings.UsePlayniteDetection && !string.IsNullOrEmpty(_currentGame))
            {
                game = _currentGame;
                method = "PLAYNITE";
                if (!string.IsNullOrEmpty(prefix) && prefix.Length > 2)
                {
                    _dictionary.SaveAlias(prefix, _currentGame);
                    _logger.Write(LogType.Learn, $"Prefix: {prefix}\nGame: {_currentGame}");
                }
            }

            // 3. Janela ativa
            bool inGameSession = _currentGame != null;
            bool prefixKnown = dict.ContainsKey(normPfx);
            bool canUseFallback = _settings.UseWindowFallback && (inGameSession || prefixKnown);

            if (game == null && canUseFallback)
            {
                var win = GetActiveWindowTitle();
                if (!string.IsNullOrEmpty(win) && win.Length > 4)
                {
                    var normWin = DictionaryService.Normalize(win);

                    bool blocked = _settings.WindowBlacklist.Any(b =>
                        normWin.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0);

                    bool looksLikeSystem =
                        normWin.Contains("explorador de arquivos") ||
                        normWin.Contains("file explorer") ||
                        normWin.Contains("mais guias") ||
                        normWin.Contains("more tabs") ||
                        normWin.Contains("google drive") ||
                        normWin.Contains("onedrive") ||
                        normWin.Contains("hotmail") ||
                        normWin.Contains("playnite") ||
                        normWin.Contains("gmail") ||
                        normWin.Contains("outlook") ||
                        normWin.Contains(" - explorador") ||
                        normWin.Contains(" - explorer") ||
                        normWin.Length < 3;

                    if (!blocked && !looksLikeSystem)
                    {
                        game = win;
                        method = "WINDOW";
                        _logger.Write(LogType.Fallback, $"Prefix: {prefix}\nDetected: {win}");
                    }
                    else
                    {
                        _logger.Write(LogType.Info,
                            $"Fallback blocked: {win}\nFile: {fileName}");
                    }
                }
            }

            // Sem match
            if (game == null)
            {
                _logger.Write(LogType.Error, $"File: {fileName}\nReason: No detection");
                TryMoveToUnmatched(filePath, ext);
                return;
            }

            // Encontra pasta de destino usando similaridade
            var matchResult = FindBestMatchByScore(game, folders, _settings.MatchThreshold);
            var match = matchResult.Folder;
            var matchScore = matchResult.Score;
            var matchType = matchResult.MatchType;

            if (match == null)
            {
                _logger.Write(LogType.Error, $"File: {fileName}\nGame: {game}\nNo folder found (score: {matchScore})");
                TryMoveToUnmatched(filePath, ext);
                return;
            }

            // Record match score
            if (!matchScores.ContainsKey(match.NameOriginal) || matchResult.Score > matchScores[match.NameOriginal])
            {
                matchScores[match.NameOriginal] = matchResult.Score;
            }

            // Destino final
            var destDir = isVideo
                ? EnsureDir(Path.Combine(match.Path, "Videos"))
                : match.Path;

            var date = GetBestDate(filePath);
            var destName = BuildDestName(match.NameOriginal, date, Path.GetFileNameWithoutExtension(fileName), ext);
            var destPath = Path.Combine(destDir, destName);

            int i = 1;
            while (File.Exists(destPath))
            {
                var nameNoExt = Path.GetFileNameWithoutExtension(destName);
                destPath = Path.Combine(destDir, $"{nameNoExt}_{i}{ext}");
                i++;
            }

            try
            {
                File.Move(filePath, destPath);

                if (_settings.EnableBackup && !string.IsNullOrEmpty(_settings.BackupFolder))
                    TryBackup(destPath, match.NameOriginal, isVideo);

                _processed.Add(filePath);

                int current = counts.ContainsKey(match.NameOriginal) ? counts[match.NameOriginal] : 0;
                counts[match.NameOriginal] = current + 1;

                _logger.Write(LogType.Move, $"File: {fileName}\nGame: {game}\nMethod: {method}\nScore: {matchScore}% ({matchType})");
            }
            catch (Exception ex)
            {
                _logger.Write(LogType.Error, $"File: {fileName}\nMove failed: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        // Pasta Unmatched
        // ──────────────────────────────────────────────
        private void TryMoveToUnmatched(string filePath, string ext)
        {
            if (!_settings.MoveUnmatchedToFolder) return;
            if (string.IsNullOrWhiteSpace(_settings.DestinationBase)) return;

            try
            {
                var unmatchedDir = EnsureDir(
                    Path.Combine(_settings.DestinationBase, _settings.UnmatchedFolderName));

                var destPath = Path.Combine(unmatchedDir, Path.GetFileName(filePath));
                int i = 1;
                while (File.Exists(destPath))
                {
                    var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
                    destPath = Path.Combine(unmatchedDir, $"{nameNoExt}_{i}{ext}");
                    i++;
                }

                File.Move(filePath, destPath);
                _processed.Add(filePath);
                _logger.Write(LogType.Info, $"Moved to unmatched: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                _logger.Write(LogType.Error, $"Unmatched move failed: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        // Backup
        // ──────────────────────────────────────────────
        private void TryBackup(string sourcePath, string gameName, bool isVideo)
        {
            try
            {
                var backupGame = EnsureDir(Path.Combine(_settings.BackupFolder, gameName));
                var backupDir = isVideo ? EnsureDir(Path.Combine(backupGame, "Videos")) : backupGame;
                var destPath = Path.Combine(backupDir, Path.GetFileName(sourcePath));

                if (!File.Exists(destPath))
                    File.Copy(sourcePath, destPath);
            }
            catch (Exception ex)
            {
                _logger.Write(LogType.Error, $"Backup failed: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        // Renomeação customizável
        // ──────────────────────────────────────────────
        private string BuildDestName(string gameName, DateTime date, string originalName, string ext)
        {
            var pattern = string.IsNullOrWhiteSpace(_settings.RenamePattern)
                ? "{game}_{date}_{time}"
                : _settings.RenamePattern;

            var result = pattern
                .Replace("{game}", SanitizeFileName(gameName))
                .Replace("{date}", date.ToString("yyyy-MM-dd"))
                .Replace("{time}", date.ToString("HH_mm_ss"))
                .Replace("{datetime}", date.ToString("yyyy-MM-dd_HH_mm_ss"))
                .Replace("{original}", SanitizeFileName(originalName));

            return result + ext;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Split(invalid)).Trim();
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        private List<FolderEntry> LoadFolders()
        {
            return Directory.GetDirectories(_settings.DestinationBase)
                .Select(d => new FolderEntry
                {
                    NameOriginal = Path.GetFileName(d),
                    NameNorm = DictionaryService.Normalize(Path.GetFileName(d)),
                    Path = d
                })
                .ToList();
        }

        private static string GetPrefix(string filename)
        {
            var m = Regex.Match(filename, @"^([^_]+)_");
            return m.Success
                ? m.Groups[1].Value
                : Path.GetFileNameWithoutExtension(filename);
        }

        private static DateTime GetBestDate(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var m = Regex.Match(name, @"(\d{4})[-_](\d{2})[-_](\d{2}).*?(\d{2})[-_](\d{2})[-_](\d{2})");
            if (m.Success)
            {
                try
                {
                    return new DateTime(
                        int.Parse(m.Groups[1].Value),
                        int.Parse(m.Groups[2].Value),
                        int.Parse(m.Groups[3].Value),
                        int.Parse(m.Groups[4].Value),
                        int.Parse(m.Groups[5].Value),
                        int.Parse(m.Groups[6].Value));
                }
                catch { }
            }

            var info = new FileInfo(filePath);
            return info.LastWriteTime != default ? info.LastWriteTime : info.CreationTime;
        }

        private static string EnsureDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private static string GetActiveWindowTitle()
        {
            var sb = new StringBuilder(256);
            GetWindowText(GetForegroundWindow(), sb, sb.Capacity);
            return sb.ToString();
        }

        // ──────────────────────────────────────────────
        // Inner classes
        // ──────────────────────────────────────────────

        private class FolderEntry
        {
            public string NameOriginal { get; set; } = "";
            public string NameNorm { get; set; } = "";
            public string Path { get; set; } = "";
        }

        private class MatchResult
        {
            public FolderEntry? Folder { get; set; }
            public int Score { get; set; }
            public string MatchType { get; set; } = "none";
            public bool IsMatch => Folder != null && Score > 0;
        }

        // ──────────────────────────────────────────────
        // Similarity score matching
        // ──────────────────────────────────────────────

        private MatchResult FindBestMatchByScore(string gameName, List<FolderEntry> folders, int minScore = 50)
        {
            if (string.IsNullOrEmpty(gameName) || folders.Count == 0)
                return new MatchResult { Score = 0, MatchType = "none" };

            var normGame = DictionaryService.Normalize(gameName);
            var candidates = new List<(FolderEntry Folder, int Score, string MatchType)>();

            foreach (var folder in folders)
            {
                int score = 0;
                string matchType = "none";
                var normFolder = folder.NameNorm;

                // ─── 1. Exact match (100 points) ───
                if (string.Equals(normFolder, normGame, StringComparison.OrdinalIgnoreCase))
                {
                    score = 100;
                    matchType = "exact";
                    candidates.Add((folder, score, matchType));
                    continue;
                }

                // ─── 2. Containment match ───
                if (normFolder.Contains(normGame))
                {
                    var ratio = (double)normGame.Length / normFolder.Length;
                    score = 60 + (int)(ratio * 20);
                    matchType = "folder_contains_game";
                }
                else if (normGame.Contains(normFolder))
                {
                    var ratio = (double)normFolder.Length / normGame.Length;
                    score = 50 + (int)(ratio * 30);
                    matchType = "game_contains_folder";
                }

                // ─── 3. Word match ───
                var gameWords = normGame.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
                var folderWords = normFolder.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

                if (gameWords.Length > 0 && folderWords.Length > 0)
                {
                    var commonWords = gameWords.Intersect(folderWords, StringComparer.OrdinalIgnoreCase).ToList();
                    var commonCount = commonWords.Count;

                    if (commonCount > 0)
                    {
                        var maxWords = Math.Max(gameWords.Length, folderWords.Length);
                        var wordScore = (int)((double)commonCount / maxWords * 40);

                        if (score > 0)
                        {
                            score = Math.Min(score + wordScore / 2, 95);
                        }
                        else
                        {
                            score = 30 + wordScore;
                            matchType = "word_match";
                        }
                    }
                }

                // ─── 4. Prefix match ───
                if (normGame.StartsWith(normFolder, StringComparison.OrdinalIgnoreCase) ||
                    normFolder.StartsWith(normGame, StringComparison.OrdinalIgnoreCase))
                {
                    if (score < 70) score = 70;
                    matchType = "prefix_match";
                }

                if (score > 0)
                {
                    candidates.Add((folder, score, matchType));
                }
            }

            // ─── 5. Sort by score descending ───
            var best = candidates
                .OrderByDescending(c => c.Score)
                .FirstOrDefault();

            if (best.Folder == null || best.Score < minScore)
            {
                _logger.Write(LogType.Info,
                    best.Folder == null
                        ? $"No match found for '{gameName}'"
                        : $"Match score {best.Score} below threshold {minScore} for '{gameName}' → no match");
                return new MatchResult { Score = best.Score, MatchType = best.MatchType };
            }

            _logger.Write(LogType.Info,
                $"Best match for '{gameName}': '{best.Folder.NameOriginal}' (score: {best.Score}, type: {best.MatchType})");

            // Check for candidates with close scores (difference <= 5)
            var closeCandidates = candidates
                .Where(c => c.Score >= best.Score - 5 && c.Folder != best.Folder)
                .ToList();
            if (closeCandidates.Any())
            {
                var others = string.Join(", ", closeCandidates.Select(c => $"{c.Folder.NameOriginal}({c.Score})"));
                _logger.Write(LogType.Info,
                    $"⚠️ Multiple close matches for '{gameName}': {others}. Selected: {best.Folder.NameOriginal}");
            }

            return new MatchResult
            {
                Folder = best.Folder,
                Score = best.Score,
                MatchType = best.MatchType
            };
        }
    }
}