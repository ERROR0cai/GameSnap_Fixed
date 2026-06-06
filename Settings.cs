using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GameSnapPlugin
{
    public class GameSnapSettings : ISettings, INotifyPropertyChanged
    {
        // Referência ao plugin — necessária para Load/Save (igual ao Ludusavi)
        private readonly GameSnapPlugin _plugin;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Construtor sem parâmetros obrigatório para LoadPluginSettings<T>()
        public GameSnapSettings() { }

        public GameSnapSettings(GameSnapPlugin plugin)
        {
            _plugin = plugin;
            Load();
        }

        // ── Campos com notificação (WPF two-way binding funciona em this) ────────

        private string _sourceFolder = "";
        public string SourceFolder
        {
            get => _sourceFolder;
            set { _sourceFolder = value; Notify(); }
        }

        private List<string> _additionalSourceFolders = new List<string>();
        public List<string> AdditionalSourceFolders
        {
            get => _additionalSourceFolders;
            set { _additionalSourceFolders = value; Notify(); }
        }

        private string _destinationBase = "";
        public string DestinationBase
        {
            get => _destinationBase;
            set { _destinationBase = value; Notify(); }
        }

        private int _pollingIntervalSeconds = 30;
        public int PollingIntervalSeconds
        {
            get => _pollingIntervalSeconds;
            set { _pollingIntervalSeconds = value; Notify(); }
        }

        private bool _usePlayniteDetection = true;
        public bool UsePlayniteDetection
        {
            get => _usePlayniteDetection;
            set { _usePlayniteDetection = value; Notify(); }
        }

        private bool _useWindowFallback = true;
        public bool UseWindowFallback
        {
            get => _useWindowFallback;
            set { _useWindowFallback = value; Notify(); }
        }

        private bool _autoCreateFolders = false;
        public bool AutoCreateFolders
        {
            get => _autoCreateFolders;
            set { _autoCreateFolders = value; Notify(); }
        }

        private bool _moveUnmatchedToFolder = false;
        public bool MoveUnmatchedToFolder
        {
            get => _moveUnmatchedToFolder;
            set { _moveUnmatchedToFolder = value; Notify(); }
        }

        private string _unmatchedFolderName = "_Unmatched";
        public string UnmatchedFolderName
        {
            get => _unmatchedFolderName;
            set { _unmatchedFolderName = value; Notify(); }
        }

        private bool _showNotifications = true;
        public bool ShowNotifications
        {
            get => _showNotifications;
            set { _showNotifications = value; Notify(); }
        }

        private string _renamePattern = "{game}_{date}_{time}";
        public string RenamePattern
        {
            get => _renamePattern;
            set { _renamePattern = value; Notify(); }
        }

        private bool _enableBackup = false;
        public bool EnableBackup
        {
            get => _enableBackup;
            set { _enableBackup = value; Notify(); }
        }

        private string _backupFolder = "";
        public string BackupFolder
        {
            get => _backupFolder;
            set { _backupFolder = value; Notify(); }
        }

        private bool _enableSteamSupport = false;
        public bool EnableSteamSupport
        {
            get => _enableSteamSupport;
            set { _enableSteamSupport = value; Notify(); }
        }

        private string _steamPath = "";
        public string SteamPath
        {
            get => _steamPath;
            set { _steamPath = value; Notify(); }
        }

        private bool _enableLocalProviderIntegration = false;
        public bool EnableLocalProviderIntegration
        {
            get => _enableLocalProviderIntegration;
            set { _enableLocalProviderIntegration = value; Notify(); }
        }

        private bool _enableEmulatorSupport = false;
        public bool EnableEmulatorSupport
        {
            get => _enableEmulatorSupport;
            set { _enableEmulatorSupport = value; Notify(); }
        }

        private List<EmulatorProfile> _emulatorProfiles = EmulatorProfile.CreateDefaults();
        public List<EmulatorProfile> EmulatorProfiles
        {
            get => _emulatorProfiles;
            set { _emulatorProfiles = value; Notify(); }
        }

        private List<string> _imageExtensions = new List<string> { ".png", ".jpg", ".jpeg" };
        public List<string> ImageExtensions
        {
            get => _imageExtensions;
            set { _imageExtensions = value; Notify(); }
        }

        private List<string> _videoExtensions = new List<string> { ".mp4", ".wmv" };
        public List<string> VideoExtensions
        {
            get => _videoExtensions;
            set { _videoExtensions = value; Notify(); }
        }

        private List<string> _windowBlacklist = new List<string>
        {
            "explorer", "notepad", "settings", "task manager",
            "chrome", "edge", "opera", "firefox", "brave",
            "discord", "steam", "launcher", "update", "setup",
            "windows", "desktop", "playnite", "visual studio",
            "code", "powershell", "cmd", "terminal"
        };
        public List<string> WindowBlacklist
        {
            get => _windowBlacklist;
            set { _windowBlacklist = value; Notify(); }
        }

        // ── ISettings — padrão Ludusavi ──────────────────────────────────────────

        public void BeginEdit()
        {
            // Nada — igual ao Ludusavi. Os valores já estão em this.
        }

        public void CancelEdit()
        {
            // Recarrega do disco para this — igual ao Ludusavi
            Load();
        }

        public void EndEdit()
        {
            // Salva this no disco — igual ao Ludusavi
            _plugin.SavePluginSettings(this);
            _plugin.ApplySettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(SourceFolder))
                errors.Add("Source folder is required.");
            if (string.IsNullOrWhiteSpace(DestinationBase))
                errors.Add("Destination folder is required.");
            return errors.Count == 0;
        }

        // ── Load — atualiza this campo a campo (nunca substitui this) ────────────

        private void Load()
        {
            var s = _plugin.LoadPluginSettings<GameSnapSettings>();
            if (s == null) return;

            // Escalares
            if (s.SourceFolder != null)         SourceFolder         = s.SourceFolder;
            if (s.DestinationBase != null)       DestinationBase      = s.DestinationBase;
            if (s.PollingIntervalSeconds > 0)    PollingIntervalSeconds = s.PollingIntervalSeconds;
            UsePlayniteDetection           = s.UsePlayniteDetection;
            UseWindowFallback              = s.UseWindowFallback;
            AutoCreateFolders              = s.AutoCreateFolders;
            MoveUnmatchedToFolder          = s.MoveUnmatchedToFolder;
            if (s.UnmatchedFolderName != null)   UnmatchedFolderName  = s.UnmatchedFolderName;
            ShowNotifications              = s.ShowNotifications;
            if (s.RenamePattern != null)         RenamePattern        = s.RenamePattern;
            EnableBackup                   = s.EnableBackup;
            if (s.BackupFolder != null)          BackupFolder         = s.BackupFolder;
            EnableSteamSupport             = s.EnableSteamSupport;
            if (s.SteamPath != null)             SteamPath            = s.SteamPath;
            EnableLocalProviderIntegration = s.EnableLocalProviderIntegration;
            EnableEmulatorSupport          = s.EnableEmulatorSupport;

            // Listas
            if (s.ImageExtensions != null && s.ImageExtensions.Count > 0)
                ImageExtensions = s.ImageExtensions;
            if (s.VideoExtensions != null && s.VideoExtensions.Count > 0)
                VideoExtensions = s.VideoExtensions;
            if (s.WindowBlacklist != null && s.WindowBlacklist.Count > 0)
                WindowBlacklist = s.WindowBlacklist;
            if (s.AdditionalSourceFolders != null)
                AdditionalSourceFolders = s.AdditionalSourceFolders;

            // EmulatorProfiles — merge: preserva salvos, adiciona novos built-ins
            if (s.EmulatorProfiles == null || s.EmulatorProfiles.Count == 0)
            {
                EmulatorProfiles = EmulatorProfile.CreateDefaults();
            }
            else
            {
                var existingNames = new HashSet<string>(s.EmulatorProfiles.Select(p => p.Name));
                foreach (var def in EmulatorProfile.CreateDefaults())
                    if (!existingNames.Contains(def.Name))
                        s.EmulatorProfiles.Add(def);
                EmulatorProfiles = s.EmulatorProfiles;
            }
        }
    }
}
