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

        private readonly GameSnapPlugin   _plugin;
        private          GameSnapSettings _settings;
        private          GameSnapSettings? _editingClone;

        public GameSnapSettings Settings => _settings;

        // ── Text bindings ────────────────────────────────────────────────────────

        public string ImageExtensionsText
        {
            get => string.Join(", ", _settings.ImageExtensions);
            set
            {
                _settings.ImageExtensions = ParseExtensions(value);
                OnPropertyChanged();
            }
        }

        public string VideoExtensionsText
        {
            get => string.Join(", ", _settings.VideoExtensions);
            set
            {
                _settings.VideoExtensions = ParseExtensions(value);
                OnPropertyChanged();
            }
        }

        public string AdditionalSourcesText
        {
            get => string.Join(Environment.NewLine, _settings.AdditionalSourceFolders);
            set
            {
                _settings.AdditionalSourceFolders = ParseLines(value);
                OnPropertyChanged();
            }
        }

        public string BackupFolder
        {
            get => _settings.BackupFolder;
            set { _settings.BackupFolder = value; OnPropertyChanged(); }
        }

        // ── Commands ─────────────────────────────────────────────────────────────

        public ICommand BrowseSourceCommand      { get; }
        public ICommand BrowseDestinationCommand { get; }
        public ICommand BrowseBackupCommand      { get; }
        public ICommand BrowseSteamCommand       { get; }
        public ICommand OpenDictionaryCommand    { get; }
        public ICommand OpenLogCommand           { get; }
        public ICommand AddEmulatorCommand       { get; }
        public ICommand RemoveEmulatorCommand    { get; }

        // ── Constructor ──────────────────────────────────────────────────────────

        public SettingsViewModel(GameSnapPlugin plugin)
        {
            _plugin   = plugin;
            _settings = _plugin.LoadSettings(); // carrega do disco imediatamente

            BrowseSourceCommand      = new RelayCommand(BrowseSource);
            BrowseDestinationCommand = new RelayCommand(BrowseDestination);
            BrowseBackupCommand      = new RelayCommand(BrowseBackup);
            BrowseSteamCommand       = new RelayCommand(BrowseSteam);
            OpenDictionaryCommand    = new RelayCommand(OpenDictionary);
            OpenLogCommand           = new RelayCommand(OpenLog);
            AddEmulatorCommand       = new RelayCommand(AddEmulator);
            RemoveEmulatorCommand    = new RelayCommand(RemoveEmulator);
        }

        // ── ISettings ────────────────────────────────────────────────────────────

        public void BeginEdit()
        {
            // Recarrega do disco e atualiza os valores in-place no _settings existente.
            //
            // POR QUÊ in-place e não "_settings = fresh"?
            // A EmulatorsView faz two-way binding (Text="{Binding CustomPath}") direto
            // nos objetos EmulatorProfile dentro da lista. Se substituirmos a referência
            // de _settings ou de EmulatorProfiles, o WPF perde o binding e descarta o
            // que o usuário acabou de digitar/selecionar — por isso atualizamos campo a
            // campo e item a item, preservando as referências de objeto.

            var fresh = _plugin.LoadSettings();

            _settings.SourceFolder                   = fresh.SourceFolder;
            _settings.DestinationBase                = fresh.DestinationBase;
            _settings.PollingIntervalSeconds         = fresh.PollingIntervalSeconds;
            _settings.UsePlayniteDetection           = fresh.UsePlayniteDetection;
            _settings.UseWindowFallback              = fresh.UseWindowFallback;
            _settings.AutoCreateFolders              = fresh.AutoCreateFolders;
            _settings.MoveUnmatchedToFolder          = fresh.MoveUnmatchedToFolder;
            _settings.UnmatchedFolderName            = fresh.UnmatchedFolderName;
            _settings.ShowNotifications              = fresh.ShowNotifications;
            _settings.RenamePattern                  = fresh.RenamePattern;
            _settings.EnableBackup                   = fresh.EnableBackup;
            _settings.BackupFolder                   = fresh.BackupFolder;
            _settings.EnableSteamSupport             = fresh.EnableSteamSupport;
            _settings.SteamPath                      = fresh.SteamPath;
            _settings.EnableLocalProviderIntegration = fresh.EnableLocalProviderIntegration;
            _settings.EnableEmulatorSupport          = fresh.EnableEmulatorSupport;
            _settings.ImageExtensions                = fresh.ImageExtensions;
            _settings.VideoExtensions                = fresh.VideoExtensions;
            _settings.AdditionalSourceFolders        = fresh.AdditionalSourceFolders;
            _settings.WindowBlacklist                = fresh.WindowBlacklist;

            // Atualiza perfis in-place: limpa e re-adiciona na mesma List<>
            // para não quebrar o ItemsSource binding da EmulatorsView
            _settings.EmulatorProfiles ??= new List<EmulatorProfile>();
            _settings.EmulatorProfiles.Clear();
            if (fresh.EmulatorProfiles != null)
                foreach (var p in fresh.EmulatorProfiles)
                    _settings.EmulatorProfiles.Add(p);

            // Snapshot para CancelEdit
            _editingClone = CloneSettings(_settings);

            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(ImageExtensionsText));
            OnPropertyChanged(nameof(VideoExtensionsText));
            OnPropertyChanged(nameof(AdditionalSourcesText));
        }

        public void CancelEdit()
        {
            if (_editingClone == null) return;

            // Restaura in-place — mesma lógica do BeginEdit
            _settings.SourceFolder                   = _editingClone.SourceFolder;
            _settings.DestinationBase                = _editingClone.DestinationBase;
            _settings.PollingIntervalSeconds         = _editingClone.PollingIntervalSeconds;
            _settings.UsePlayniteDetection           = _editingClone.UsePlayniteDetection;
            _settings.UseWindowFallback              = _editingClone.UseWindowFallback;
            _settings.AutoCreateFolders              = _editingClone.AutoCreateFolders;
            _settings.MoveUnmatchedToFolder          = _editingClone.MoveUnmatchedToFolder;
            _settings.UnmatchedFolderName            = _editingClone.UnmatchedFolderName;
            _settings.ShowNotifications              = _editingClone.ShowNotifications;
            _settings.RenamePattern                  = _editingClone.RenamePattern;
            _settings.EnableBackup                   = _editingClone.EnableBackup;
            _settings.BackupFolder                   = _editingClone.BackupFolder;
            _settings.EnableSteamSupport             = _editingClone.EnableSteamSupport;
            _settings.SteamPath                      = _editingClone.SteamPath;
            _settings.EnableLocalProviderIntegration = _editingClone.EnableLocalProviderIntegration;
            _settings.EnableEmulatorSupport          = _editingClone.EnableEmulatorSupport;
            _settings.ImageExtensions                = _editingClone.ImageExtensions;
            _settings.VideoExtensions                = _editingClone.VideoExtensions;
            _settings.AdditionalSourceFolders        = _editingClone.AdditionalSourceFolders;
            _settings.WindowBlacklist                = _editingClone.WindowBlacklist;

            _settings.EmulatorProfiles ??= new List<EmulatorProfile>();
            _settings.EmulatorProfiles.Clear();
            if (_editingClone.EmulatorProfiles != null)
                foreach (var p in _editingClone.EmulatorProfiles)
                    _settings.EmulatorProfiles.Add(p);

            _editingClone = null;
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(ImageExtensionsText));
            OnPropertyChanged(nameof(VideoExtensionsText));
            OnPropertyChanged(nameof(AdditionalSourcesText));
        }

        public void EndEdit()
        {
            // Sincroniza campos de texto de volta para o modelo
            _settings.ImageExtensions         = ParseExtensions(ImageExtensionsText);
            _settings.VideoExtensions         = ParseExtensions(VideoExtensionsText);
            _settings.AdditionalSourceFolders = ParseLines(AdditionalSourcesText);

            _plugin.SaveSettings(_settings);
            _plugin.ApplySettings(_settings);
            _editingClone = null;
        }

        // ── Helpers públicos ─────────────────────────────────────────────────────

        public string? BrowseForFolder()
            => _plugin.PlayniteApi.Dialogs.SelectFolder();

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(_settings.SourceFolder))
                errors.Add("Source folder is required.");
            if (string.IsNullOrWhiteSpace(_settings.DestinationBase))
                errors.Add("Destination folder is required.");
            return errors.Count == 0;
        }

        // ── Comandos de navegação ────────────────────────────────────────────────

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
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
        }

        private void OpenLog()
        {
            var path = Path.Combine(_plugin.GetPluginUserDataPath(), "gamesnap.log");
            if (File.Exists(path))
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
            else
                _plugin.PlayniteApi.Dialogs.ShowMessage("No log file yet.", "GameSnap");
        }

        // ── Comandos de emulador ─────────────────────────────────────────────────

        private void AddEmulator()
        {
            var result = _plugin.PlayniteApi.Dialogs.SelectString("", "Add Emulator", "Emulator name:");
            if (result == null || !result.Result || string.IsNullOrWhiteSpace(result.SelectedString)) return;

            _settings.EmulatorProfiles ??= new List<EmulatorProfile>();
            _settings.EmulatorProfiles.Add(new EmulatorProfile
            {
                Name        = result.SelectedString.Trim(),
                Enabled     = true,
                IsUserAdded = true
            });
            OnPropertyChanged(nameof(Settings));
        }

        private void RemoveEmulator()
        {
            if (_settings.EmulatorProfiles == null) return;
            for (int i = _settings.EmulatorProfiles.Count - 1; i >= 0; i--)
            {
                if (_settings.EmulatorProfiles[i].IsUserAdded)
                {
                    _settings.EmulatorProfiles.RemoveAt(i);
                    OnPropertyChanged(nameof(Settings));
                    return;
                }
            }
        }

        // ── CloneSettings — deep clone completo ──────────────────────────────────

        private static GameSnapSettings CloneSettings(GameSnapSettings src) => new GameSnapSettings
        {
            SourceFolder                   = src.SourceFolder,
            DestinationBase                = src.DestinationBase,
            PollingIntervalSeconds         = src.PollingIntervalSeconds,
            UsePlayniteDetection           = src.UsePlayniteDetection,
            UseWindowFallback              = src.UseWindowFallback,
            AutoCreateFolders              = src.AutoCreateFolders,
            MoveUnmatchedToFolder          = src.MoveUnmatchedToFolder,
            UnmatchedFolderName            = src.UnmatchedFolderName,
            ShowNotifications              = src.ShowNotifications,
            RenamePattern                  = src.RenamePattern,
            EnableBackup                   = src.EnableBackup,
            BackupFolder                   = src.BackupFolder,
            EnableSteamSupport             = src.EnableSteamSupport,
            SteamPath                      = src.SteamPath,
            EnableLocalProviderIntegration = src.EnableLocalProviderIntegration,
            EnableEmulatorSupport          = src.EnableEmulatorSupport,
            AdditionalSourceFolders        = new List<string>(src.AdditionalSourceFolders),
            ImageExtensions                = new List<string>(src.ImageExtensions),
            VideoExtensions                = new List<string>(src.VideoExtensions),
            WindowBlacklist                = new List<string>(src.WindowBlacklist),
            EmulatorProfiles               = src.EmulatorProfiles == null
                ? EmulatorProfile.CreateDefaults()
                : src.EmulatorProfiles.Select(p => new EmulatorProfile
                {
                    Name        = p.Name,
                    Enabled     = p.Enabled,
                    CustomPath  = p.CustomPath,
                    IsUserAdded = p.IsUserAdded,
                }).ToList(),
        };

        // ── Parsers ──────────────────────────────────────────────────────────────

        private static List<string> ParseExtensions(string text)
            => text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim().ToLowerInvariant())
                   .Where(s => s.StartsWith("."))
                   .ToList();

        private static List<string> ParseLines(string text)
            => text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim())
                   .Where(s => !string.IsNullOrEmpty(s))
                   .ToList();
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
