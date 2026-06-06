using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace GameSnapPlugin
{
    // Agora é só um wrapper de commands e bindings de texto.
    // O ISettings está em GameSnapSettings diretamente — igual ao Ludusavi.
    public class SettingsViewModel
    {
        private readonly GameSnapPlugin  _plugin;
        public  GameSnapSettings         Settings => _plugin.settings;

        // ── Text bindings ────────────────────────────────────────────────────────

        public string ImageExtensionsText
        {
            get => string.Join(", ", Settings.ImageExtensions);
            set => Settings.ImageExtensions = value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => s.StartsWith("."))
                .ToList();
        }

        public string VideoExtensionsText
        {
            get => string.Join(", ", Settings.VideoExtensions);
            set => Settings.VideoExtensions = value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => s.StartsWith("."))
                .ToList();
        }

        public string AdditionalSourcesText
        {
            get => string.Join(Environment.NewLine, Settings.AdditionalSourceFolders);
            set => Settings.AdditionalSourceFolders = value
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        // Visibilidade do tip na aba Emulators
        public System.Windows.Visibility AutoCreateFoldersWarningVisibility
            => Settings.AutoCreateFolders
               ? System.Windows.Visibility.Collapsed
               : System.Windows.Visibility.Visible;

        // ── Commands ─────────────────────────────────────────────────────────────

        public ICommand BrowseSourceCommand      { get; }
        public ICommand BrowseDestinationCommand { get; }
        public ICommand BrowseBackupCommand      { get; }
        public ICommand BrowseSteamCommand       { get; }
        public ICommand OpenDictionaryCommand    { get; }
        public ICommand OpenLogCommand           { get; }
        public ICommand AddEmulatorCommand       { get; }
        public ICommand RemoveEmulatorCommand    { get; }

        public SettingsViewModel(GameSnapPlugin plugin)
        {
            _plugin = plugin;

            BrowseSourceCommand      = new RelayCommand(BrowseSource);
            BrowseDestinationCommand = new RelayCommand(BrowseDestination);
            BrowseBackupCommand      = new RelayCommand(BrowseBackup);
            BrowseSteamCommand       = new RelayCommand(BrowseSteam);
            OpenDictionaryCommand    = new RelayCommand(OpenDictionary);
            OpenLogCommand           = new RelayCommand(OpenLog);
            AddEmulatorCommand       = new RelayCommand(AddEmulator);
            RemoveEmulatorCommand    = new RelayCommand(RemoveEmulator);
        }

        // ── Helpers públicos ─────────────────────────────────────────────────────

        public string? BrowseForFolder()
            => _plugin.PlayniteApi.Dialogs.SelectFolder();

        // ── Commands impl ────────────────────────────────────────────────────────

        private void BrowseSource()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) Settings.SourceFolder = path;
        }

        private void BrowseDestination()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) Settings.DestinationBase = path;
        }

        private void BrowseBackup()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) Settings.BackupFolder = path;
        }

        private void BrowseSteam()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (path != null) Settings.SteamPath = path;
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

        private void AddEmulator()
        {
            var result = _plugin.PlayniteApi.Dialogs.SelectString("", "Add Emulator", "Emulator name:");
            if (result == null || !result.Result || string.IsNullOrWhiteSpace(result.SelectedString)) return;
            Settings.EmulatorProfiles ??= new List<EmulatorProfile>();
            Settings.EmulatorProfiles.Add(new EmulatorProfile
            {
                Name        = result.SelectedString.Trim(),
                Enabled     = true,
                IsUserAdded = true
            });
        }

        private void RemoveEmulator()
        {
            if (Settings.EmulatorProfiles == null) return;
            for (int i = Settings.EmulatorProfiles.Count - 1; i >= 0; i--)
            {
                if (Settings.EmulatorProfiles[i].IsUserAdded)
                {
                    Settings.EmulatorProfiles.RemoveAt(i);
                    return;
                }
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
