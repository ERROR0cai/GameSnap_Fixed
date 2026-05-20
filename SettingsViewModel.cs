using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace GameSnapPlugin
{
    public class SettingsViewModel : ISettings, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly GameSnapPlugin    _plugin;
        private          GameSnapSettings  _settings;
        private          GameSnapSettings? _editingClone;

        public GameSnapSettings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        // Extensions text bindings
        public string ImageExtensionsText
        {
            get => string.Join(", ", _settings.ImageExtensions);
            set
            {
                _settings.ImageExtensions = new List<string>(
                    value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim().ToLowerInvariant())
                         .Where(s => s.StartsWith(".")));
                OnPropertyChanged();
            }
        }

        public string VideoExtensionsText
        {
            get => string.Join(", ", _settings.VideoExtensions);
            set
            {
                _settings.VideoExtensions = new List<string>(
                    value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim().ToLowerInvariant())
                         .Where(s => s.StartsWith(".")));
                OnPropertyChanged();
            }
        }

        // Additional sources text binding (one per line)
        public string AdditionalSourcesText
        {
            get => string.Join(Environment.NewLine, _settings.AdditionalSourceFolders);
            set
            {
                _settings.AdditionalSourceFolders = new List<string>(
                    value.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => !string.IsNullOrEmpty(s)));
                OnPropertyChanged();
            }
        }

        // Backup folder binding
        public string BackupFolder
        {
            get => _settings.BackupFolder;
            set { _settings.BackupFolder = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand BrowseSourceCommand      { get; }
        public ICommand BrowseDestinationCommand { get; }
        public ICommand BrowseBackupCommand      { get; }
        public ICommand BrowseSteamCommand       { get; }
        public ICommand OpenDictionaryCommand    { get; }
        public ICommand OpenLogCommand           { get; }

        public SettingsViewModel(GameSnapPlugin plugin)
        {
            _plugin   = plugin;
            _settings = plugin.LoadSettings();

            BrowseSourceCommand      = new RelayCommand(BrowseSource);
            BrowseDestinationCommand = new RelayCommand(BrowseDestination);
            BrowseBackupCommand      = new RelayCommand(BrowseBackup);
            BrowseSteamCommand       = new RelayCommand(BrowseSteam);
            OpenDictionaryCommand    = new RelayCommand(OpenDictionary);
            OpenLogCommand           = new RelayCommand(OpenLog);
        }

        public void BeginEdit()
        {
            _editingClone = CloneSettings(_settings);
        }

        public void CancelEdit()
        {
            if (_editingClone != null)
                Settings = _editingClone;
        }

        public void EndEdit()
        {
            _plugin.SaveSettings(_settings);
            _plugin.ApplySettings(_settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(_settings.SourceFolder))
                errors.Add("Source folder is required.");
            if (string.IsNullOrWhiteSpace(_settings.DestinationBase))
                errors.Add("Destination folder is required.");
            return errors.Count == 0;
        }

        private void BrowseSource()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) { _settings.SourceFolder = path; OnPropertyChanged(nameof(Settings)); }
        }

        private void BrowseDestination()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) { _settings.DestinationBase = path; OnPropertyChanged(nameof(Settings)); }
        }

        private void BrowseBackup()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) { _settings.BackupFolder = path; OnPropertyChanged(nameof(Settings)); }
        }

        private void BrowseSteam()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) { _settings.SteamPath = path; OnPropertyChanged(nameof(Settings)); }
        }

        private void OpenDictionary()
        {
            var path = Path.Combine(_plugin.GetPluginUserDataPath(), "dictionary.txt");
            if (!File.Exists(path))
                File.WriteAllText(path, "# Format:\n# [Game Name]\n# alias1\n");
            System.Diagnostics.Process.Start("notepad.exe", path);
        }

        private void OpenLog()
        {
            var path = Path.Combine(_plugin.GetPluginUserDataPath(), "gamesnap.log");
            if (File.Exists(path))
                System.Diagnostics.Process.Start("notepad.exe", path);
            else
                _plugin.PlayniteApi.Dialogs.ShowMessage("No log file yet.", "GameSnap");
        }

        private static GameSnapSettings CloneSettings(GameSnapSettings src) => new GameSnapSettings
        {
            SourceFolder              = src.SourceFolder,
            AdditionalSourceFolders   = new List<string>(src.AdditionalSourceFolders),
            DestinationBase           = src.DestinationBase,
            PollingIntervalSeconds    = src.PollingIntervalSeconds,
            UsePlayniteDetection      = src.UsePlayniteDetection,
            UseWindowFallback         = src.UseWindowFallback,
            AutoCreateFolders         = src.AutoCreateFolders,
            MoveUnmatchedToFolder     = src.MoveUnmatchedToFolder,
            UnmatchedFolderName       = src.UnmatchedFolderName,
            ShowNotifications         = src.ShowNotifications,
            RenamePattern             = src.RenamePattern,
            EnableBackup              = src.EnableBackup,
            BackupFolder              = src.BackupFolder,
            EnableSteamSupport              = src.EnableSteamSupport,
            SteamPath                       = src.SteamPath,
            EnableLocalProviderIntegration  = src.EnableLocalProviderIntegration,
            ImageExtensions           = new List<string>(src.ImageExtensions),
            VideoExtensions           = new List<string>(src.VideoExtensions),
            WindowBlacklist           = new List<string>(src.WindowBlacklist),
        };
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
