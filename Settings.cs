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

        [IgnoreDataMember]

        public GameSnapSettings Settings => this;

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

        public ICommand? BrowseSourceCommand      { get; private set; }

        public ICommand? BrowseDestinationCommand { get; private set; }

        public ICommand? BrowseBackupCommand      { get; private set; }

        public ICommand? BrowseSteamCommand       { get; private set; }

        public ICommand? OpenDictionaryCommand    { get; private set; }

        public ICommand? OpenLogCommand           { get; private set; }

        public ICommand? AddEmulatorCommand       { get; private set; }

        public ICommand? RemoveEmulatorCommand    { get; private set; }

        private void InitCommands()

        {

            BrowseSourceCommand      = new RelayCommand(() => { var p = Browse(); if (p != null) SourceFolder    = p; });

            BrowseDestinationCommand = new RelayCommand(() => { var p = Browse(); if (p != null) DestinationBase = p; });

            BrowseBackupCommand      = new RelayCommand(() => { var p = Browse(); if (p != null) BackupFolder    = p; });

            BrowseSteamCommand       = new RelayCommand(() => { var p = Browse(); if (p != null) SteamPath      = p; });

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

        public void BeginEdit() { }

        public void CancelEdit() => Load();

        public void EndEdit()

        {

            _plugin!.SavePluginSettings(this);

            _plugin!.ApplySettings(this);

        }

        public bool VerifySettings(out List<string> errors)

        {

            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(SourceFolder))    errors.Add("Source folder is required.");

            if (string.IsNullOrWhiteSpace(DestinationBase)) errors.Add("Destination folder is required.");

            return errors.Count == 0;

        }

        private void Load()

        {

            var s = _plugin!.LoadPluginSettings<GameSnapSettings>();

            if (s == null) return;

            if (s.SourceFolder    != null) SourceFolder    = s.SourceFolder;

            if (s.DestinationBase != null) DestinationBase = s.DestinationBase;

            if (s.PollingIntervalSeconds > 0) PollingIntervalSeconds = s.PollingIntervalSeconds;

            UsePlayniteDetection           = s.UsePlayniteDetection;

            UseWindowFallback              = s.UseWindowFallback;

            AutoCreateFolders              = s.AutoCreateFolders;

            MoveUnmatchedToFolder          = s.MoveUnmatchedToFolder;

            ShowNotifications              = s.ShowNotifications;

            EnableBackup                   = s.EnableBackup;

            EnableSteamSupport             = s.EnableSteamSupport;

            EnableLocalProviderIntegration = s.EnableLocalProviderIntegration;

            EnableEmulatorSupport          = s.EnableEmulatorSupport;

            if (s.UnmatchedFolderName != null) UnmatchedFolderName = s.UnmatchedFolderName;

            if (s.RenamePattern       != null) RenamePattern       = s.RenamePattern;

            if (s.BackupFolder        != null) BackupFolder        = s.BackupFolder;

            if (s.SteamPath           != null) SteamPath           = s.SteamPath;

            if (s.ImageExtensions         != null && s.ImageExtensions.Count > 0) ImageExtensions         = s.ImageExtensions;

            if (s.VideoExtensions         != null && s.VideoExtensions.Count > 0) VideoExtensions         = s.VideoExtensions;

            if (s.WindowBlacklist         != null && s.WindowBlacklist.Count > 0) WindowBlacklist         = s.WindowBlacklist;

            if (s.AdditionalSourceFolders != null)                                 AdditionalSourceFolders = s.AdditionalSourceFolders;

            if (s.EmulatorProfiles == null || s.EmulatorProfiles.Count == 0)

            {

                EmulatorProfiles = new ObservableCollection<EmulatorProfile>(EmulatorProfile.CreateDefaults());

            }

            else

            {

                var existingNames = new HashSet<string>(s.EmulatorProfiles.Select(p => p.Name));

                foreach (var def in EmulatorProfile.CreateDefaults())

                    if (!existingNames.Contains(def.Name))

                        s.EmulatorProfiles.Add(def);

                EmulatorProfiles = new ObservableCollection<EmulatorProfile>(s.EmulatorProfiles);

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

