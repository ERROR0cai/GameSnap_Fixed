using Playnite.SDK;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.IO;
using System;
using System.Linq;

namespace GameSnapPlugin
{
    public class SettingsViewModel : ObservableObject, ISettings
    {
        private readonly GameSnapPlugin        _plugin;
        private          GameSnapSettings      _settings;
        private          GameSnapSettings?     _editingClone;

        public GameSnapSettings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        // ── Binding helpers para listas (WPF não edita List<T> direto) ──

        public ObservableCollection<string> ImageExtensions { get; private set; } = new();
        public ObservableCollection<string> VideoExtensions { get; private set; } = new();
        public ObservableCollection<string> WindowBlacklist { get; private set; } = new();

        // Texto editável para extensões (comma separated)
        public string ImageExtensionsText
        {
            get => string.Join(", ", _settings.ImageExtensions);
            set
            {
                _settings.ImageExtensions = new System.Collections.Generic.List<string>(
                    value.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
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
                _settings.VideoExtensions = new System.Collections.Generic.List<string>(
                    value.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim().ToLowerInvariant())
                         .Where(s => s.StartsWith(".")));
                OnPropertyChanged();
            }
        }

        // ── Commands ──

        public ICommand BrowseSourceCommand      { get; }
        public ICommand BrowseDestinationCommand { get; }
        public ICommand OpenDictionaryCommand    { get; }
        public ICommand OpenLogCommand           { get; }

        public SettingsViewModel(GameSnapPlugin plugin)
        {
            _plugin   = plugin;
            _settings = plugin.LoadSettings();

            BrowseSourceCommand      = new RelayCommand(BrowseSource);
            BrowseDestinationCommand = new RelayCommand(BrowseDestination);
            OpenDictionaryCommand    = new RelayCommand(OpenDictionary);
            OpenLogCommand           = new RelayCommand(OpenLog);
        }

        public void BeginEdit()
        {
            _editingClone = CloneSettings(_settings);
            SyncCollections();
        }

        public void CancelEdit()
        {
            if (_editingClone != null)
                Settings = _editingClone;
        }

        public void EndEdit()
        {
            _settings.ImageExtensions = new System.Collections.Generic.List<string>(ImageExtensions);
            _settings.VideoExtensions = new System.Collections.Generic.List<string>(VideoExtensions);
            _settings.WindowBlacklist = new System.Collections.Generic.List<string>(WindowBlacklist);
            _plugin.SaveSettings(_settings);
            _plugin.ApplySettings(_settings);
        }

        public bool VerifySettings(out System.Collections.Generic.List<string> errors)
        {
            errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(_settings.SourceFolder))
                errors.Add("Source folder is required.");

            if (string.IsNullOrWhiteSpace(_settings.DestinationBase))
                errors.Add("Destination folder is required.");

            return errors.Count == 0;
        }

        private void SyncCollections()
        {
            ImageExtensions = new ObservableCollection<string>(_settings.ImageExtensions);
            VideoExtensions = new ObservableCollection<string>(_settings.VideoExtensions);
            WindowBlacklist = new ObservableCollection<string>(_settings.WindowBlacklist);
            OnPropertyChanged(nameof(ImageExtensions));
            OnPropertyChanged(nameof(VideoExtensions));
            OnPropertyChanged(nameof(WindowBlacklist));
        }

        private void BrowseSource()
        {
            var path = BrowseFolder();
            if (path != null) _settings.SourceFolder = path;
            OnPropertyChanged(nameof(Settings));
        }

        private void BrowseDestination()
        {
            var path = BrowseFolder();
            if (path != null) _settings.DestinationBase = path;
            OnPropertyChanged(nameof(Settings));
        }

        private void OpenDictionary()
        {
            var path = Path.Combine(_plugin.GetPluginUserDataPath(), "dictionary.txt");
            if (!File.Exists(path))
                File.WriteAllText(path, "# Formato:\n# [Nome do Jogo]\n# alias1\n# alias2\n");
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

        private string? BrowseFolder()
        {
            return _plugin.PlayniteApi.Dialogs.SelectFolder();
        }

        private static GameSnapSettings CloneSettings(GameSnapSettings src) => new GameSnapSettings
        {
            SourceFolder            = src.SourceFolder,
            DestinationBase         = src.DestinationBase,
            PollingIntervalSeconds  = src.PollingIntervalSeconds,
            UsePlayniteDetection    = src.UsePlayniteDetection,
            UseWindowFallback       = src.UseWindowFallback,
            ImageExtensions         = new System.Collections.Generic.List<string>(src.ImageExtensions),
            VideoExtensions         = new System.Collections.Generic.List<string>(src.VideoExtensions),
            WindowBlacklist         = new System.Collections.Generic.List<string>(src.WindowBlacklist),
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
