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
            set
            {
                _settings = value;
                OnPropertyChanged();
                // Force all bound fields to refresh when settings object changes
                OnPropertyChanged(nameof(ImageExtensionsText));
                OnPropertyChanged(nameof(VideoExtensionsText));
                OnPropertyChanged(nameof(AdditionalSourcesText));
            OnPropertyChanged(nameof(CustomEmulatorFoldersText));
            }
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
        public string CustomEmulatorFoldersText
        {
            get => string.Join(Environment.NewLine, _settings.CustomEmulatorFolders);
            set
            {
                _settings.CustomEmulatorFolders = new List<string>(
                    value.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => !string.IsNullOrEmpty(s)));
                OnPropertyChanged();
            }
        }

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
            // Always reload from disk when opening settings
            // This ensures the UI reflects what was actually saved
            _settings      = _plugin.LoadSettings();
            _editingClone  = CloneSettings(_settings);
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(ImageExtensionsText));
            OnPropertyChanged(nameof(VideoExtensionsText));
            OnPropertyChanged(nameof(AdditionalSourcesText));
            OnPropertyChanged(nameof(CustomEmulatorFoldersText));
            OnPropertyChanged(nameof(BackupFolder));
        }

        public void CancelEdit()
        {
            if (_editingClone != null)
                Settings = _editingClone;
        }

        public void EndEdit()
        {
            try
            {
                // Sync all text-bound fields back to settings object before saving
                _settings.ImageExtensions         = ParseExtensions(ImageExtensionsText);
                _settings.VideoExtensions         = ParseExtensions(VideoExtensionsText);
                _settings.AdditionalSourceFolders = ParseLines(AdditionalSourcesText);
                _settings.CustomEmulatorFolders   = ParseLines(CustomEmulatorFoldersText);

                _plugin.SaveSettings(_settings);
                _plugin.ApplySettings(_settings);
            }
            catch (Exception ex)
            {
                _plugin.PlayniteApi.Dialogs.ShowErrorMessage(
                    $"GameSnap failed to save settings:\n{ex.Message}", "GameSnap");
            }
        }

        private static System.Collections.Generic.List<string> ParseExtensions(string text)
        {
            return new System.Collections.Generic.List<string>(
                text.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Where(s => s.StartsWith(".")));
        }

        private static System.Collections.Generic.List<string> ParseLines(string text)
        {
            return new System.Collections.Generic.List<string>(
                text.Split(new char[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s)));
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
            var psi = new System.Diagnostics.ProcessStartInfo("notepad.exe", path)
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }

        private void OpenLog()
        {
            var path = Path.Combine(_plugin.GetPluginUserDataPath(), "gamesnap.log");
            if (File.Exists(path))
            {
                var psi = new System.Diagnostics.ProcessStartInfo("notepad.exe", path)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
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
            EnableEmulatorSupport           = src.EnableEmulatorSupport,
            CustomEmulatorFolders           = new List<string>(src.CustomEmulatorFolders),
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
