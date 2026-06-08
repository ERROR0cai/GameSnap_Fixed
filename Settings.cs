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
        public List<EmulatorProfile> EmulatorProfiles { get; set; } = EmulatorProfile.CreateDefaults();
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

        // ObservableCollection separada para binding do ItemsControl
        private ObservableCollection<EmulatorProfile> _emulatorProfiles = new ObservableCollection<EmulatorProfile>();
        public ObservableCollection<EmulatorProfile> EmulatorProfiles
        {
            get => _emulatorProfiles;
            set => SetValue(ref _emulatorProfiles, value);
        }

        private void SyncProfilesFromSettings()
        {
            EmulatorProfiles = new ObservableCollection<EmulatorProfile>(
                Settings.EmulatorProfiles ?? EmulatorProfile.CreateDefaults());
        }

        private void SyncProfilesToSettings()
        {
            Settings.EmulatorProfiles = EmulatorProfiles.ToList();
        }

        public GameSnapSettingsViewModel(GameSnapPlugin plugin)
        {
            _plugin = plugin;
            var saved = plugin.LoadPluginSettings<GameSnapSettings>();

            if (saved == null)
            {
                // Primeira execucao — usa defaults
                Settings = new GameSnapSettings();
            }
            else
            {
                Settings = saved;

                // Converte para ObservableCollection (JSON desserializa como List)
                if (Settings.EmulatorProfiles == null || Settings.EmulatorProfiles.Count == 0)
                {
                    Settings.EmulatorProfiles = EmulatorProfile.CreateDefaults();
                }
                else
                {
                    // Adiciona apenas built-ins que nao existem ainda (novos emuladores em versoes futuras)
                    // Nao adiciona nada se o nome ja existe — evita duplicatas
                    var existing = new ObservableCollection<EmulatorProfile>(Settings.EmulatorProfiles);
                    var existingNames = new HashSet<string>(existing.Select(p => p.Name));
                    foreach (var def in EmulatorProfile.CreateDefaults())
                        if (!existingNames.Contains(def.Name))
                            existing.Add(def);
                    Settings.EmulatorProfiles = existing.ToList();
                }
            }
            SyncProfilesFromSettings();
        }

        // ISettings — igual ao ScreenshotsVisualizer
        public void BeginEdit()
        {
            _editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            if (_editingClone == null) return;
            Settings = _editingClone;
            SyncProfilesFromSettings();
        }

        public void EndEdit()
        {
            SyncProfilesToSettings();
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
            EmulatorProfiles.Add(new EmulatorProfile
            {
                Name        = result.SelectedString.Trim(),
                Enabled     = true,
                IsUserAdded = true
            });
        });

        public RelayCommand<object> RemoveEmulatorCommand => new RelayCommand<object>((a) =>
        {
            for (int i = EmulatorProfiles.Count - 1; i >= 0; i--)
                if (EmulatorProfiles[i].IsUserAdded)
                {
                    EmulatorProfiles.RemoveAt(i);
                    return;
                }
        });

        public string? BrowseForFolder()
            => _plugin.PlayniteApi.Dialogs.SelectFolder();
    }
}
