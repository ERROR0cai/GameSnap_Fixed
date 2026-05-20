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
        public void Organize()
        {
            var allSources = new List<string>();

            if (!string.IsNullOrEmpty(_settings.SourceFolder))
                allSources.Add(_settings.SourceFolder);

            allSources.AddRange(_settings.AdditionalSourceFolders
                .Where(f => !string.IsNullOrEmpty(f) && Directory.Exists(f)));

            if (allSources.Count == 0) return;
            if (!Directory.Exists(_settings.DestinationBase)) return;

            var dict    = _dictionary.Load();
            var folders = LoadFolders();
            var counts  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in allSources)
            {
                if (!Directory.Exists(source)) continue;
                foreach (var file in Directory.GetFiles(source))
                {
                    TryOrganizeFile(file, dict, folders, counts);
                }
            }

            if (counts.Count > 0)
            {
                var summary = string.Join(" | ", counts.Select(kv => $"{kv.Key} ({kv.Value})"));
                _logger.Info($"Organized: {summary}");

                // Dispara notificação agregada
                OnFileMoved?.Invoke(
                    "GameSnap",
                    $"Organized {counts.Values.Sum()} screenshot(s): {summary}"
                );
            }
        }

        // ──────────────────────────────────────────────
        // Processa um arquivo individual
        // ──────────────────────────────────────────────
        private void TryOrganizeFile(
            string filePath,
            Dictionary<string, string> dict,
            List<FolderEntry> folders,
            Dictionary<string, int> counts)
        {
            if (_processed.Contains(filePath)) return;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            bool isImage = _settings.ImageExtensions.Contains(ext);
            bool isVideo = _settings.VideoExtensions.Contains(ext);

            if (!isImage && !isVideo) return;

            System.Threading.Thread.Sleep(1000);

            var fileName = Path.GetFileName(filePath);
            var prefix   = GetPrefix(fileName);
            var normPfx  = DictionaryService.Normalize(prefix);

            string? game   = null;
            string  method = "UNKNOWN";

            // 1. Dicionário
            if (dict.TryGetValue(normPfx, out var fromDict))
            {
                game   = fromDict;
                method = "DICTIONARY";
            }

            // 2. Playnite
            if (game == null && _settings.UsePlayniteDetection && !string.IsNullOrEmpty(_currentGame))
            {
                game   = _currentGame;
                method = "PLAYNITE";
                _dictionary.SaveAlias(prefix, _currentGame);
                _logger.Write(LogType.Learn, $"Prefix: {prefix}\nGame: {_currentGame}");
            }

            // 3. Janela ativa
            if (game == null && _settings.UseWindowFallback)
            {
                var win = GetActiveWindowTitle();
                if (!string.IsNullOrEmpty(win) && win.Length > 4)
                {
                    var normWin = DictionaryService.Normalize(win);
                    bool blocked = _settings.WindowBlacklist.Any(b =>
                        normWin.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!blocked)
                    {
                        game   = win;
                        method = "WINDOW";
                        _logger.Write(LogType.Fallback, $"Prefix: {prefix}\nDetected: {win}");
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

            // Encontra pasta de destino
            var normGame = DictionaryService.Normalize(game);
            var match = folders
                .Where(f => f.NameNorm.Contains(normGame) || normGame.Contains(f.NameNorm))
                .OrderByDescending(f => f.NameNorm.Length)
                .FirstOrDefault();

            if (match == null)
            {
                _logger.Write(LogType.Error, $"File: {fileName}\nGame: {game}\nNo folder found");
                TryMoveToUnmatched(filePath, ext);
                return;
            }

            // Destino final
            var destDir = isVideo
                ? EnsureDir(Path.Combine(match.Path, "Videos"))
                : match.Path;

            var date     = GetBestDate(filePath);
            var destName = BuildDestName(match.NameOriginal, date, Path.GetFileNameWithoutExtension(fileName), ext);
            var destPath = Path.Combine(destDir, destName);

            // Evita colisão
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

                // Backup opcional
                if (_settings.EnableBackup && !string.IsNullOrEmpty(_settings.BackupFolder))
                    TryBackup(destPath, match.NameOriginal, isVideo);

                _processed.Add(filePath);

                int current = counts.ContainsKey(match.NameOriginal) ? counts[match.NameOriginal] : 0;
                counts[match.NameOriginal] = current + 1;

                _logger.Write(LogType.Move, $"File: {fileName}\nGame: {game}\nMethod: {method}");
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
                var backupDir  = isVideo ? EnsureDir(Path.Combine(backupGame, "Videos")) : backupGame;
                var destPath   = Path.Combine(backupDir, Path.GetFileName(sourcePath));

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
                .Replace("{game}",     SanitizeFileName(gameName))
                .Replace("{date}",     date.ToString("yyyy-MM-dd"))
                .Replace("{time}",     date.ToString("HH_mm_ss"))
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
                    NameNorm     = DictionaryService.Normalize(Path.GetFileName(d)),
                    Path         = d
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

        private class FolderEntry
        {
            public string NameOriginal { get; set; } = "";
            public string NameNorm     { get; set; } = "";
            public string Path         { get; set; } = "";
        }
    }
}
