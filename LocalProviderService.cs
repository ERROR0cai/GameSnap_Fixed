using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameSnapPlugin
{
    /// <summary>
    /// Integração com o Screenshots Utilities Local Provider (HerrKnarz).
    /// Registra automaticamente a pasta de destino do GameSnap no config.json do Local Provider.
    /// </summary>
    public class LocalProviderService
    {
        // GUID do Screenshots Utilities Local Provider
        private static readonly string LocalProviderExtensionId = "ScreenshotsUtilitiesLocalProvider_HerrKnarz";

        private readonly IPlayniteAPI  _playniteApi;
        private readonly GameSnapLogger _logger;

        public LocalProviderService(IPlayniteAPI playniteApi, GameSnapLogger logger)
        {
            _playniteApi = playniteApi;
            _logger      = logger;
        }

        // ──────────────────────────────────────────────
        // Detecta se o Local Provider está instalado
        // ──────────────────────────────────────────────
        public bool IsInstalled()
        {
            var configPath = GetConfigPath();
            return configPath != null && File.Exists(configPath);
        }

        // ──────────────────────────────────────────────
        // Registra a pasta de destino no Local Provider
        // ──────────────────────────────────────────────
        public bool RegisterDestinationFolder(string destinationBase)
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                _logger.Info("Local Provider: config.json not found — plugin may not be installed.");
                return false;
            }

            try
            {
                var json   = File.ReadAllText(configPath, Encoding.UTF8);
                var config = SimpleJson.Deserialize(json);

                if (config == null)
                {
                    _logger.Error("Local Provider: failed to parse config.json.");
                    return false;
                }

                // Path que o GameSnap quer registrar: DestinationBase\{Name}\
                var targetPath = destinationBase.TrimEnd('\\') + "\\{Name}\\";

                // Encontra o perfil global (GameId = 00000000-...)
                var profiles = config.GameProfiles;
                LocalProviderGameProfile? globalProfile = null;

                foreach (var p in profiles)
                {
                    if (p.GameId == "00000000-0000-0000-0000-000000000000")
                    {
                        globalProfile = p;
                        break;
                    }
                }

                // Cria o perfil global se não existir
                if (globalProfile == null)
                {
                    globalProfile = new LocalProviderGameProfile
                    {
                        GameId              = "00000000-0000-0000-0000-000000000000",
                        OverrideGlobalConfigs = false,
                        FolderConfigs       = new List<LocalProviderFolderConfig>()
                    };
                    config.GameProfiles.Add(globalProfile);
                }

                // Verifica se a entrada já existe
                foreach (var fc in globalProfile.FolderConfigs)
                {
                    if (string.Equals(fc.Path, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Info($"Local Provider: path already registered: {targetPath}");
                        return true;
                    }
                }

                // Adiciona nova entrada
                globalProfile.FolderConfigs.Add(new LocalProviderFolderConfig
                {
                    Active                  = true,
                    FileMask                = "*.png;*.jpg;*.jpeg",
                    InvalidCharReplacement  = "_",
                    Name                    = "GameSnap",
                    Path                    = targetPath,
                    RemoveDiacritics        = false,
                    RemoveEditionSuffix     = false,
                    RemoveHyphens           = false,
                    RemoveSpecialChars      = false,
                    RemoveWhitespaces       = false,
                    UnderscoresToWhitespaces = false,
                    WhitespacesToHyphens    = false,
                    WhitespacesToUnderscores = false
                });

                // Serializa e salva
                var newJson = SimpleJson.Serialize(config);
                File.WriteAllText(configPath, newJson, Encoding.UTF8);

                _logger.Info($"Local Provider: registered path: {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Local Provider: registration failed: {ex.Message}");
                return false;
            }
        }

        // ──────────────────────────────────────────────
        // Remove a entrada do GameSnap do Local Provider
        // ──────────────────────────────────────────────
        public bool UnregisterDestinationFolder(string destinationBase)
        {
            var configPath = GetConfigPath();
            if (configPath == null || !File.Exists(configPath)) return false;

            try
            {
                var json   = File.ReadAllText(configPath, Encoding.UTF8);
                var config = SimpleJson.Deserialize(json);
                if (config == null) return false;

                var targetPath = destinationBase.TrimEnd('\\') + "\\{Name}\\";

                foreach (var p in config.GameProfiles)
                {
                    p.FolderConfigs.RemoveAll(fc =>
                        string.Equals(fc.Path, targetPath, StringComparison.OrdinalIgnoreCase)
                        && fc.Name == "GameSnap");
                }

                var newJson = SimpleJson.Serialize(config);
                File.WriteAllText(configPath, newJson, Encoding.UTF8);
                _logger.Info($"Local Provider: unregistered path: {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Local Provider: unregistration failed: {ex.Message}");
                return false;
            }
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────
        private string? GetConfigPath()
        {
            var dataRoot = _playniteApi.Paths.ExtensionsDataPath;

            // Tenta encontrar a pasta do Local Provider por prefixo de nome
            if (!Directory.Exists(dataRoot)) return null;

            foreach (var dir in Directory.GetDirectories(dataRoot))
            {
                var name = Path.GetFileName(dir);
                if (name.IndexOf("LocalProvider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("ScreenshotsUtilities", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var candidate = Path.Combine(dir, "config.json");
                    if (File.Exists(candidate)) return candidate;
                }
            }

            return null;
        }
    }

    // ──────────────────────────────────────────────
    // Modelos do config.json do Local Provider
    // ──────────────────────────────────────────────

    public class LocalProviderConfig
    {
        public List<LocalProviderGameProfile> GameProfiles { get; set; } = new List<LocalProviderGameProfile>();
    }

    public class LocalProviderGameProfile
    {
        public string GameId { get; set; } = "";
        public bool OverrideGlobalConfigs { get; set; } = false;
        public List<LocalProviderFolderConfig> FolderConfigs { get; set; } = new List<LocalProviderFolderConfig>();
    }

    public class LocalProviderFolderConfig
    {
        public bool   Active                   { get; set; } = true;
        public string FileMask                 { get; set; } = "*.png;*.jpg";
        public string InvalidCharReplacement   { get; set; } = "_";
        public string Name                     { get; set; } = "";
        public string Path                     { get; set; } = "";
        public bool   RemoveDiacritics         { get; set; } = false;
        public bool   RemoveEditionSuffix      { get; set; } = false;
        public bool   RemoveHyphens            { get; set; } = false;
        public bool   RemoveSpecialChars       { get; set; } = false;
        public bool   RemoveWhitespaces        { get; set; } = false;
        public bool   UnderscoresToWhitespaces { get; set; } = false;
        public bool   WhitespacesToHyphens     { get; set; } = false;
        public bool   WhitespacesToUnderscores { get; set; } = false;
    }

    // ──────────────────────────────────────────────
    // JSON simples sem dependência externa
    // ──────────────────────────────────────────────
    internal static class SimpleJson
    {
        public static LocalProviderConfig? Deserialize(string json)
        {
            // Usa o serializer do Playnite SDK que já está disponível
            return Newtonsoft.Json.JsonConvert.DeserializeObject<LocalProviderConfig>(json);
        }

        public static string Serialize(LocalProviderConfig config)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(config,
                Newtonsoft.Json.Formatting.Indented);
        }
    }
}
