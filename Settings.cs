using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace GameSnapPlugin
{
    // Dados puros — exatamente como ScreenshotsVisualizerSettings.
    // ObservableObject do SDK, [DontSerialize] do SDK para excluir da serialização.
    public class GameSnapSettings : ObservableObject
    {
        public string SourceFolder                 { get; set; } = "";
        public List<string> AdditionalSourceFolders { get; set; } = new List<string>();
        public string DestinationBase              { get; set; } = "";
        public int PollingIntervalSeconds          { get; set; } = 30;
        public bool UsePlayniteDetection           { get; set; } = true;
        public bool UseWindowFallback              { get; set; } = true;
        public bool AutoCreateFolders              { get; set; } = false;
        public bool MoveUnmatchedToFolder          { get; set; } = false;
        public string UnmatchedFolderName          { get; set; } = "_Unmatched";
        public bool ShowNotifications              { get; set; } = true;
        public string RenamePattern                { get; set; } = "{game}_{date}_{time}";
        public bool EnableBackup                   { get; set; } = false;
        public string BackupFolder                 { get; set; } = "";
        public bool EnableSteamSupport             { get; set; } = false;
        public string SteamPath                    { get; set; } = "";
        public bool EnableLocalProviderIntegration { get; set; } = false;
        public bool EnableEmulatorSupport          { get; set; } = false;
        public ObservableCollection<EmulatorProfile> EmulatorProfiles { get; set; } = new ObservableCollection<EmulatorProfile>(EmulatorProfile.CreateDefaults());
        public List<string> ImageExtensions        { get; set; } = new List<string> { ".png", ".jpg", ".jpeg" };
        public List<string> VideoExtensions        { get; set; } = new List<string> { ".mp4", ".wmv" };
        public List<string> WindowBlacklist        { get; set; } = new List<string>
        {
            "explorer", "notepad", "settings", "task manager",
            "chrome", "edge", "opera", "firefox", "brave",
            "discord", "steam", "launcher", "update", "setup",
            "windows", "desktop", "playnite", "visual studio",
            "code", "powershell", "cmd", "terminal"
        };
    }

    // ViewModel — implementa ISettings, exatamente como ScreenshotsVisualizerSettingsViewModel.
    // O Playnite chama BeginEdit/CancelEdit/EndEdit neste objeto.
    // O DataContext da view é este objeto (tem propriedade Settings para {Binding Settings.X}).
    public class GameSnapSettingsViewModel : ObservableObject, ISettings
    {
        private readonly GameSnapPlugin _plugin;
        private GameSnapSettings _editingClone;

        private GameSnapSettings _settings;
        public GameSnapSettings Settings { get => _settings; set => SetValue(ref _settings, value); }

        public GameSnapSettingsViewModel(GameSnapPlugin plugin)
        {
            _plugin = plugin;
            var saved = plugin.LoadPluginSettings<GameSnapSettings>();
            Settings = saved ?? new GameSnapSettings();

            // Merge: adiciona built-ins novos que nao existiam em versoes anteriores
            if (Settings.EmulatorProfiles == null || Settings.EmulatorProfiles.Count == 0)
            {
                Settings.EmulatorProfiles = new ObservableCollection<EmulatorProfile>(EmulatorProfile.CreateDefaults());
            }
            else
            {
                var existingNames = new HashSet<string>(Settings.EmulatorProfiles.Select(p => p.Name));
                foreach (var def in EmulatorProfile.CreateDefaults())
                    if (!existingNames.Contains(def.Name))
                        Settings.EmulatorProfiles.Add(def);
            }
        }

        // ISettings — igual ao ScreenshotsVisualizer
        public void BeginEdit()
        {
            // Serialization.GetClone faz deep clone via JSON — igual ao ScreenshotsVisualizer
            _editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = _editingClone;
        }

        public void EndEdit()
        {
            _plugin.SavePluginSettings(Settings);
            _plugin.ApplySettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        // Text bindings para o XAML
        [DontSerialize]
        public string ImageExtensionsText
        {
            get => string.Join(", ", Settings.ImageExtensions);
            set
            {
                Settings.ImageExtensions = value
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Where(s => s.StartsWith("."))
                    .ToList();
                OnPropertyChanged();
            }
        }

        [DontSerialize]
        public string VideoExtensionsText
        {
            get => string.Join(", ", Settings.VideoExtensions);
            set
            {
                Settings.VideoExtensions = value
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Where(s => s.StartsWith("."))
                    .ToList();
                OnPropertyChanged();
            }
        }

        [DontSerialize]
        public string AdditionalSourcesText
        {
            get => string.Join(Environment.NewLine, Settings.AdditionalSourceFolders);
            set
            {
                Settings.AdditionalSourceFolders = value
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                OnPropertyChanged();
            }
        }

        [DontSerialize]
        public System.Windows.Visibility AutoCreateFoldersWarningVisibility
            => Settings.AutoCreateFolders
               ? System.Windows.Visibility.Collapsed
               : System.Windows.Visibility.Visible;

        // Commands — RelayCommand<object> igual ao ScreenshotsVisualizer
        public RelayCommand<object> BrowseSourceCommand => new RelayCommand<object>((a) =>
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) Settings.SourceFolder = path;
        });

        public RelayCommand<object> BrowseDestinationCommand => new RelayCommand<object>((a) =>
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) Settings.DestinationBase = path;
        });

        public RelayCommand<object> BrowseBackupCommand => new RelayCommand<object>((a) =>
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) Settings.BackupFolder = path;
        });

        public RelayCommand<object> BrowseSteamCommand => new RelayCommand<object>((a) =>
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) Settings.SteamPath = path;
        });

        public RelayCommand<object> OpenDictionaryCommand => new RelayCommand<object>((a) =>
        {
            var path = Path.Combine(_plugin.GetPluginUserDataPath(), "dictionary.txt");
            if (!File.Exists(path)) File.WriteAllText(path, "# Format:\n# [Game Name]\n# alias1\n");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
        });

        public RelayCommand<object> OpenLogCommand => new RelayCommand<object>((a) =>
        {
            var path = Path.Combine(_plugin.GetPluginUserDataPath(), "gamesnap.log");
            if (File.Exists(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
            else
                _plugin.PlayniteApi.Dialogs.ShowMessage("No log file yet.", "GameSnap");
        });

        public RelayCommand<object> AddEmulatorCommand => new RelayCommand<object>((a) =>
        {
            var result = _plugin.PlayniteApi.Dialogs.SelectString("", "Add Emulator", "Emulator name:");
            if (result == null || !result.Result || string.IsNullOrWhiteSpace(result.SelectedString)) return;
            Settings.EmulatorProfiles.Add(new EmulatorProfile
            {
                Name        = result.SelectedString.Trim(),
                Enabled     = true,
                IsUserAdded = true
            });
            OnPropertyChanged(nameof(Settings));
        });

        public RelayCommand<object> RemoveEmulatorCommand => new RelayCommand<object>((a) =>
        {
            if (Settings.EmulatorProfiles == null) return;
            for (int i = Settings.EmulatorProfiles.Count - 1; i >= 0; i--)
                if (Settings.EmulatorProfiles[i].IsUserAdded)
                {
                    Settings.EmulatorProfiles.RemoveAt(i);
                    OnPropertyChanged(nameof(Settings));
                    return;
                }
        });

        public string? BrowseForFolder()
            => _plugin.PlayniteApi.Dialogs.SelectFolder();
    }
}
