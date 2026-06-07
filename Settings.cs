using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Windows.Input;

namespace GameSnapPlugin
{
    // DTO puro — só dados, sem commands, sem ISettings, sem referencias circulares.
    // Este é o unico objeto que o Playnite serializa/desserializa via SavePluginSettings.
    public class GameSnapSettingsData
    {
        public string SourceFolder                   { get; set; } = "";
        public List<string> AdditionalSourceFolders  { get; set; } = new List<string>();
        public string DestinationBase                { get; set; } = "";
        public int PollingIntervalSeconds            { get; set; } = 30;
        public bool UsePlayniteDetection             { get; set; } = true;
        public bool UseWindowFallback                { get; set; } = true;
        public bool AutoCreateFolders                { get; set; } = false;
        public bool MoveUnmatchedToFolder            { get; set; } = false;
        public string UnmatchedFolderName            { get; set; } = "_Unmatched";
        public bool ShowNotifications                { get; set; } = true;
        public string RenamePattern                  { get; set; } = "{game}_{date}_{time}";
        public bool EnableBackup                     { get; set; } = false;
        public string BackupFolder                   { get; set; } = "";
        public bool EnableSteamSupport               { get; set; } = false;
        public string SteamPath                      { get; set; } = "";
        public bool EnableLocalProviderIntegration   { get; set; } = false;
        public bool EnableEmulatorSupport            { get; set; } = false;
        public List<EmulatorProfile> EmulatorProfiles { get; set; } = EmulatorProfile.CreateDefaults();
        public List<string> ImageExtensions          { get; set; } = new List<string> { ".png", ".jpg", ".jpeg" };
        public List<string> VideoExtensions          { get; set; } = new List<string> { ".mp4", ".wmv" };
        public List<string> WindowBlacklist          { get; set; } = new List<string>
        {
            "explorer", "notepad", "settings", "task manager",
            "chrome", "edge", "opera", "firefox", "brave",
            "discord", "steam", "launcher", "update", "setup",
            "windows", "desktop", "playnite", "visual studio",
            "code", "powershell", "cmd", "terminal"
        };
    }

    // Classe principal — implementa ISettings e expoe bindings para o XAML.
    // Internamente usa GameSnapSettingsData para persistencia.
    public class GameSnapSettings : ISettings, INotifyPropertyChanged
    {
        private readonly GameSnapPlugin? _plugin = null!;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public GameSnapSettings() { }

        public GameSnapSettings(GameSnapPlugin plugin)
        {
            _plugin = plugin;
            InitCommands();
            Load();
        }

        // Propriedade Settings => this para compatibilidade com {Binding Settings.X} no XAML
        [IgnoreDataMember]
        public GameSnapSettings Settings => this;

        // Propriedades com INotifyPropertyChanged
        private string _sourceFolder = "";
        public string SourceFolder { get => _sourceFolder; set { _sourceFolder = value; Notify(); } }

        private List<string> _additionalSourceFolders = new List<string>();
        public List<string> AdditionalSourceFolders { get => _additionalSourceFolders; set { _additionalSourceFolders = value; Notify(); } }

        private string _destinationBase = "";
        public string DestinationBase { get => _destinationBase; set { _destinationBase = value; Notify(); } }

        private int _pollingIntervalSeconds = 30;
        public int PollingIntervalSeconds { get => _pollingIntervalSeconds; set { _pollingIntervalSeconds = value; Notify(); } }

        private bool _usePlayniteDetection = true;
        public bool UsePlayniteDetection { get => _usePlayniteDetection; set { _usePlayniteDetection = value; Notify(); } }

        private bool _useWindowFallback = true;
        public bool UseWindowFallback { get => _useWindowFallback; set { _useWindowFallback = value; Notify(); } }

        private bool _autoCreateFolders = false;
        public bool AutoCreateFolders { get => _autoCreateFolders; set { _autoCreateFolders = value; Notify(); } }

        private bool _moveUnmatchedToFolder = false;
        public bool MoveUnmatchedToFolder { get => _moveUnmatchedToFolder; set { _moveUnmatchedToFolder = value; Notify(); } }

        private string _unmatchedFolderName = "_Unmatched";
        public string UnmatchedFolderName { get => _unmatchedFolderName; set { _unmatchedFolderName = value; Notify(); } }

        private bool _showNotifications = true;
        public bool ShowNotifications { get => _showNotifications; set { _showNotifications = value; Notify(); } }

        private string _renamePattern = "{game}_{date}_{time}";
        public string RenamePattern { get => _renamePattern; set { _renamePattern = value; Notify(); } }

        private bool _enableBackup = false;
        public bool EnableBackup { get => _enableBackup; set { _enableBackup = value; Notify(); } }

        private string _backupFolder = "";
        public string BackupFolder { get => _backupFolder; set { _backupFolder = value; Notify(); } }

        private bool _enableSteamSupport = false;
        public bool EnableSteamSupport { get => _enableSteamSupport; set { _enableSteamSupport = value; Notify(); } }

        private string _steamPath = "";
        public string SteamPath { get => _steamPath; set { _steamPath = value; Notify(); } }

        private bool _enableLocalProviderIntegration = false;
        public bool EnableLocalProviderIntegration { get => _enableLocalProviderIntegration; set { _enableLocalProviderIntegration = value; Notify(); } }

        private bool _enableEmulatorSupport = false;
        public bool EnableEmulatorSupport { get => _enableEmulatorSupport; set { _enableEmulatorSupport = value; Notify(); } }

        private ObservableCollection<EmulatorProfile> _emulatorProfiles = new ObservableCollection<EmulatorProfile>(EmulatorProfile.CreateDefaults());
        public ObservableCollection<EmulatorProfile> EmulatorProfiles { get => _emulatorProfiles; set { _emulatorProfiles = value; Notify(); } }

        private List<string> _imageExtensions = new List<string> { ".png", ".jpg", ".jpeg" };
        public List<string> ImageExtensions { get => _imageExtensions; set { _imageExtensions = value; Notify(); } }

        private List<string> _videoExtensions = new List<string> { ".mp4", ".wmv" };
        public List<string> VideoExtensions { get => _videoExtensions; set { _videoExtensions = value; Notify(); } }

        private List<string> _windowBlacklist = new List<string>
        {
            "explorer", "notepad", "settings", "task manager",
            "chrome", "edge", "opera", "firefox", "brave",
            "discord", "steam", "launcher", "update", "setup",
            "windows", "desktop", "playnite", "visual studio",
            "code", "powershell", "cmd", "terminal"
        };
        public List<string> WindowBlacklist { get => _windowBlacklist; set { _windowBlacklist = value; Notify(); } }

        // Text bindings para o XAML
        [IgnoreDataMember]
        public string ImageExtensionsText
        {
            get => string.Join(", ", ImageExtensions);
            set { ImageExtensions = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).Where(s => s.StartsWith(".")).ToList(); Notify(); }
        }

        [IgnoreDataMember]
        public string VideoExtensionsText
        {
            get => string.Join(", ", VideoExtensions);
            set { VideoExtensions = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).Where(s => s.StartsWith(".")).ToList(); Notify(); }
        }

        [IgnoreDataMember]
        public string AdditionalSourcesText
        {
            get => string.Join(Environment.NewLine, AdditionalSourceFolders);
            set { AdditionalSourceFolders = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList(); Notify(); }
        }

        [IgnoreDataMember]
        public System.Windows.Visibility AutoCreateFoldersWarningVisibility
            => AutoCreateFolders ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

        // Commands
        [IgnoreDataMember] public ICommand? BrowseSourceCommand      { get; private set; }
        [IgnoreDataMember] public ICommand? BrowseDestinationCommand { get; private set; }
        [IgnoreDataMember] public ICommand? BrowseBackupCommand      { get; private set; }
        [IgnoreDataMember] public ICommand? BrowseSteamCommand       { get; private set; }
        [IgnoreDataMember] public ICommand? OpenDictionaryCommand    { get; private set; }
        [IgnoreDataMember] public ICommand? OpenLogCommand           { get; private set; }
        [IgnoreDataMember] public ICommand? AddEmulatorCommand       { get; private set; }
        [IgnoreDataMember] public ICommand? RemoveEmulatorCommand    { get; private set; }

        private void InitCommands()
        {
            BrowseSourceCommand      = new RelayCommand(() => { var p = Browse(); if (p != null) SourceFolder    = p; });
            BrowseDestinationCommand = new RelayCommand(() => { var p = Browse(); if (p != null) DestinationBase = p; });
            BrowseBackupCommand      = new RelayCommand(() => { var p = Browse(); if (p != null) BackupFolder    = p; });
            BrowseSteamCommand       = new RelayCommand(() => { var p = Browse(); if (p != null) SteamPath       = p; });
            OpenDictionaryCommand    = new RelayCommand(OpenDictionary);
            OpenLogCommand           = new RelayCommand(OpenLog);
            AddEmulatorCommand       = new RelayCommand(AddEmulator);
            RemoveEmulatorCommand    = new RelayCommand(RemoveEmulator);
        }

        public string? BrowseForFolder() => Browse();
        private string? Browse() => _plugin?.PlayniteApi.Dialogs.SelectFolder();

        private void OpenDictionary()
        {
            var path = Path.Combine(_plugin!.GetPluginUserDataPath(), "dictionary.txt");
            if (!File.Exists(path)) File.WriteAllText(path, "# Format:\n# [Game Name]\n# alias1\n");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
        }

        private void OpenLog()
        {
            var path = Path.Combine(_plugin!.GetPluginUserDataPath(), "gamesnap.log");
            if (File.Exists(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
            else
                _plugin!.PlayniteApi.Dialogs.ShowMessage("No log file yet.", "GameSnap");
        }

        private void AddEmulator()
        {
            var result = _plugin!.PlayniteApi.Dialogs.SelectString("", "Add Emulator", "Emulator name:");
            if (result == null || !result.Result || string.IsNullOrWhiteSpace(result.SelectedString)) return;
            EmulatorProfiles.Add(new EmulatorProfile { Name = result.SelectedString.Trim(), Enabled = true, IsUserAdded = true });
            Notify(nameof(EmulatorProfiles));
        }

        private void RemoveEmulator()
        {
            if (EmulatorProfiles == null) return;
            for (int i = EmulatorProfiles.Count - 1; i >= 0; i--)
                if (EmulatorProfiles[i].IsUserAdded) { EmulatorProfiles.RemoveAt(i); Notify(nameof(EmulatorProfiles)); return; }
        }

        // ISettings
        public void BeginEdit() { }

        public void CancelEdit() => Load();

        public void EndEdit()
        {
            // Serializa via DTO puro — sem references circulares, sem commands
            var data = ToData();
            _plugin!.SavePluginSettings(data);
            _plugin!.ApplySettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(SourceFolder))    errors.Add("Source folder is required.");
            if (string.IsNullOrWhiteSpace(DestinationBase)) errors.Add("Destination folder is required.");
            return errors.Count == 0;
        }

        // Converte para DTO para salvar
        private GameSnapSettingsData ToData() => new GameSnapSettingsData
        {
            SourceFolder                 = SourceFolder,
            AdditionalSourceFolders      = new List<string>(AdditionalSourceFolders),
            DestinationBase              = DestinationBase,
            PollingIntervalSeconds       = PollingIntervalSeconds,
            UsePlayniteDetection         = UsePlayniteDetection,
            UseWindowFallback            = UseWindowFallback,
            AutoCreateFolders            = AutoCreateFolders,
            MoveUnmatchedToFolder        = MoveUnmatchedToFolder,
            UnmatchedFolderName          = UnmatchedFolderName,
            ShowNotifications            = ShowNotifications,
            RenamePattern                = RenamePattern,
            EnableBackup                 = EnableBackup,
            BackupFolder                 = BackupFolder,
            EnableSteamSupport           = EnableSteamSupport,
            SteamPath                    = SteamPath,
            EnableLocalProviderIntegration = EnableLocalProviderIntegration,
            EnableEmulatorSupport        = EnableEmulatorSupport,
            EmulatorProfiles             = EmulatorProfiles.ToList(),
            ImageExtensions              = new List<string>(ImageExtensions),
            VideoExtensions              = new List<string>(VideoExtensions),
            WindowBlacklist              = new List<string>(WindowBlacklist),
        };

        // Carrega do DTO para this
        private void Load()
        {
            var d = _plugin!.LoadPluginSettings<GameSnapSettingsData>();
            if (d == null) return;

            if (d.SourceFolder    != null) SourceFolder    = d.SourceFolder;
            if (d.DestinationBase != null) DestinationBase = d.DestinationBase;
            if (d.PollingIntervalSeconds > 0) PollingIntervalSeconds = d.PollingIntervalSeconds;

            UsePlayniteDetection           = d.UsePlayniteDetection;
            UseWindowFallback              = d.UseWindowFallback;
            AutoCreateFolders              = d.AutoCreateFolders;
            MoveUnmatchedToFolder          = d.MoveUnmatchedToFolder;
            ShowNotifications              = d.ShowNotifications;
            EnableBackup                   = d.EnableBackup;
            EnableSteamSupport             = d.EnableSteamSupport;
            EnableLocalProviderIntegration = d.EnableLocalProviderIntegration;
            EnableEmulatorSupport          = d.EnableEmulatorSupport;

            if (d.UnmatchedFolderName != null) UnmatchedFolderName = d.UnmatchedFolderName;
            if (d.RenamePattern       != null) RenamePattern       = d.RenamePattern;
            if (d.BackupFolder        != null) BackupFolder        = d.BackupFolder;
            if (d.SteamPath           != null) SteamPath           = d.SteamPath;

            if (d.ImageExtensions         != null && d.ImageExtensions.Count > 0) ImageExtensions         = d.ImageExtensions;
            if (d.VideoExtensions         != null && d.VideoExtensions.Count > 0) VideoExtensions         = d.VideoExtensions;
            if (d.WindowBlacklist         != null && d.WindowBlacklist.Count > 0) WindowBlacklist         = d.WindowBlacklist;
            if (d.AdditionalSourceFolders != null)                                 AdditionalSourceFolders = d.AdditionalSourceFolders;

            if (d.EmulatorProfiles == null || d.EmulatorProfiles.Count == 0)
            {
                EmulatorProfiles = new ObservableCollection<EmulatorProfile>(EmulatorProfile.CreateDefaults());
            }
            else
            {
                var existingNames = new HashSet<string>(d.EmulatorProfiles.Select(p => p.Name));
                foreach (var def in EmulatorProfile.CreateDefaults())
                    if (!existingNames.Contains(def.Name))
                        d.EmulatorProfiles.Add(def);
                EmulatorProfiles = new ObservableCollection<EmulatorProfile>(d.EmulatorProfiles);
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
