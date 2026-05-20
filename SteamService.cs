using Microsoft.Win32;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameSnapPlugin
{
    public class SteamScreenshot
    {
        public string FilePath   { get; set; } = "";
        public string AppId      { get; set; } = "";
        public string SteamUserId { get; set; } = "";
    }

    public class SteamService
    {
        private readonly IPlayniteAPI  _playniteApi;
        private readonly GameSnapLogger _logger;

        // Cache AppID → Game name (built from Playnite library)
        private Dictionary<string, string> _appIdToName = new Dictionary<string, string>();

        public SteamService(IPlayniteAPI playniteApi, GameSnapLogger logger)
        {
            _playniteApi = playniteApi;
            _logger      = logger;
            RebuildCache();
        }

        // ──────────────────────────────────────────────
        // Detecta o caminho do Steam via registro
        // ──────────────────────────────────────────────
        public static string? DetectSteamPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")
                             ?? Registry.LocalMachine.OpenSubKey(@"Software\Valve\Steam")
                             ?? Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Valve\Steam");

                return key?.GetValue("SteamPath") as string;
            }
            catch
            {
                return null;
            }
        }

        // ──────────────────────────────────────────────
        // Retorna todas as pastas de screenshots do Steam
        // ──────────────────────────────────────────────
        public List<string> GetScreenshotFolders(string steamPath)
        {
            var folders = new List<string>();
            var userdataPath = Path.Combine(steamPath, "userdata");

            if (!Directory.Exists(userdataPath)) return folders;

            foreach (var userDir in Directory.GetDirectories(userdataPath))
            {
                var remotePath = Path.Combine(userDir, "760", "remote");
                if (!Directory.Exists(remotePath)) continue;

                foreach (var appDir in Directory.GetDirectories(remotePath))
                {
                    var ssPath = Path.Combine(appDir, "screenshots");
                    if (Directory.Exists(ssPath))
                        folders.Add(ssPath);
                }
            }

            return folders;
        }

        // ──────────────────────────────────────────────
        // Lê screenshots prontas para mover
        // ──────────────────────────────────────────────
        public List<SteamScreenshot> GetPendingScreenshots(string steamPath)
        {
            var result  = new List<SteamScreenshot>();
            var userdataPath = Path.Combine(steamPath, "userdata");

            if (!Directory.Exists(userdataPath)) return result;

            foreach (var userDir in Directory.GetDirectories(userdataPath))
            {
                var userId     = Path.GetFileName(userDir);
                var remotePath = Path.Combine(userDir, "760", "remote");
                if (!Directory.Exists(remotePath)) continue;

                foreach (var appDir in Directory.GetDirectories(remotePath))
                {
                    var appId  = Path.GetFileName(appDir);
                    var ssPath = Path.Combine(appDir, "screenshots");
                    if (!Directory.Exists(ssPath)) continue;

                    foreach (var file in Directory.GetFiles(ssPath))
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".jpg" && ext != ".png") continue;

                        result.Add(new SteamScreenshot
                        {
                            FilePath    = file,
                            AppId       = appId,
                            SteamUserId = userId
                        });
                    }
                }
            }

            return result;
        }

        // ──────────────────────────────────────────────
        // Resolve o nome do jogo a partir do AppID
        // ──────────────────────────────────────────────
        public string? ResolveGameName(string appId)
        {
            if (_appIdToName.TryGetValue(appId, out var name))
                return name;

            return null;
        }

        // ──────────────────────────────────────────────
        // Reconstrói o cache AppID → Nome do jogo
        // usando a biblioteca do Playnite
        // ──────────────────────────────────────────────
        public void RebuildCache()
        {
            _appIdToName.Clear();

            try
            {
                foreach (var game in _playniteApi.Database.Games)
                {
                    if (game.PluginId == Guid.Parse("CB91DFC9-B977-43BF-8E70-55F46E410FAB") // Steam plugin GUID
                        && !string.IsNullOrEmpty(game.GameId))
                    {
                        if (!_appIdToName.ContainsKey(game.GameId))
                            _appIdToName[game.GameId] = game.Name;
                    }
                }

                _logger.Info($"Steam cache: {_appIdToName.Count} games mapped.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Steam cache build failed: {ex.Message}");
            }
        }
    }
}
