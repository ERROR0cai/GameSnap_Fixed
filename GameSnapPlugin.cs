using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;

namespace GameSnapPlugin
{
    public class GameSnapPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        private GameSnapSettings   _settings  = new GameSnapSettings();
        public  GameSnapSettings   CurrentSettings => _settings;
        private GameSnapLogger?    _logger;
        private DictionaryService? _dict;
        private OrganizerService?  _organizer;
        private WatcherService?    _watcher;
        private SteamService?           _steam;
        private LocalProviderService?   _localProvider;

        public GameSnapPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };

            // Load settings synchronously so CurrentSettings is always populated before
            // Playnite can open the Settings UI.  Service initialisation (watchers, Steam,
            // etc.) is still deferred to OnApplicationStarted to avoid blocking startup.
            try
            {
                _settings = LoadSettingsWithDefaults();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameSnap: failed to load settings in constructor: {ex.Message}");
                _settings = new GameSnapSettings();
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Settings already loaded in constructor — just spin up the heavyweight services
            // (FileSystemWatcher, Steam scan) off the UI thread.
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    InitServices(_settings);
                    _watcher?.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GameSnap init error: {ex.Message}");
                }
            });
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            _watcher?.Stop();
            _watcher?.Dispose();
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            _organizer?.SetCurrentGame(args.Game.Name);
            TryAutoCreateFolder(args.Game.Name);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            _organizer?.SetCurrentGame(null);
        }

        // ──────────────────────────────────────────────
        // Auto-create folder
        // ──────────────────────────────────────────────

        private void TryAutoCreateFolder(string gameName)
        {
            if (!_settings.AutoCreateFolders) return;
            if (string.IsNullOrWhiteSpace(_settings.DestinationBase)) return;
            if (!Directory.Exists(_settings.DestinationBase)) return;

            var invalid    = Path.GetInvalidFileNameChars();
            var folderName = string.Concat(gameName.Split(invalid)).Trim();
            if (string.IsNullOrWhiteSpace(folderName)) return;

            var folderPath = Path.Combine(_settings.DestinationBase, folderName);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                _logger?.Info($"Auto-created folder: {folderPath}");
            }
        }

        // ──────────────────────────────────────────────
        // Settings
        // ──────────────────────────────────────────────

        public override ISettings GetSettings(bool firstRunSettings)
            => new SettingsViewModel(this);

        public override UserControl GetSettingsView(bool firstRunSettings)
            => new Views.SettingsTabView();

        public GameSnapSettings LoadSettings() => LoadSettingsWithDefaults();

        private GameSnapSettings LoadSettingsWithDefaults()
        {
            var saved    = LoadPluginSettings<GameSnapSettings>();
            var defaults = new GameSnapSettings();

            if (saved == null)
            {
                _settings = defaults;
                return _settings;
            }

            // Merge: preserve saved values, fill new fields with defaults
            // This ensures settings survive plugin updates that add new options
            if (saved.ImageExtensions == null || saved.ImageExtensions.Count == 0)
                saved.ImageExtensions = defaults.ImageExtensions;
            if (saved.VideoExtensions == null || saved.VideoExtensions.Count == 0)
                saved.VideoExtensions = defaults.VideoExtensions;
            if (saved.WindowBlacklist == null || saved.WindowBlacklist.Count == 0)
                saved.WindowBlacklist = defaults.WindowBlacklist;
            if (saved.AdditionalSourceFolders == null)
                saved.AdditionalSourceFolders = defaults.AdditionalSourceFolders;
            if (string.IsNullOrEmpty(saved.UnmatchedFolderName))
                saved.UnmatchedFolderName = defaults.UnmatchedFolderName;
            if (string.IsNullOrEmpty(saved.RenamePattern))
                saved.RenamePattern = defaults.RenamePattern;
            if (saved.PollingIntervalSeconds <= 0)
                saved.PollingIntervalSeconds = defaults.PollingIntervalSeconds;

            _settings = saved;
            return _settings;
        }

        public void SaveSettings(GameSnapSettings settings)
        {
            _settings = settings;
            SavePluginSettings(settings);
        }

        public void ApplySettings(GameSnapSettings settings)
        {
            if (_watcher != null)
            {
                _watcher.Stop();
                _watcher.Dispose();
                _watcher = null;
            }
            InitServices(settings);
            _watcher?.Start();
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        private void InitServices(GameSnapSettings settings)
        {
            var dataPath = GetPluginUserDataPath();
            Directory.CreateDirectory(dataPath);

            _logger    = new GameSnapLogger(dataPath);
            _dict      = new DictionaryService(dataPath);
            _organizer = new OrganizerService(settings, _dict, _logger);

            // Steam support
            if (settings.EnableSteamSupport)
            {
                _steam = new SteamService(PlayniteApi, _logger);
                _organizer.SteamService = _steam;
            }
            else
            {
                _steam = null;
                _organizer.SteamService = null;
            }

            // Notificação toast quando arquivos são movidos
            _organizer.OnFileMoved = (title, message) =>
            {
                if (settings.ShowNotifications)
                    PlayniteApi.Notifications.Add(
                        new NotificationMessage(
                            Guid.NewGuid().ToString(),
                            message,
                            NotificationType.Info));
            };

            _watcher = new WatcherService(settings, _organizer, _logger);

            // Local Provider integration
            _localProvider = new LocalProviderService(PlayniteApi, _logger);
            if (settings.EnableLocalProviderIntegration && !string.IsNullOrEmpty(settings.DestinationBase))
            {
                if (_localProvider.IsInstalled())
                    _localProvider.RegisterDestinationFolder(settings.DestinationBase);
                else
                    _logger.Info("Local Provider: not installed, skipping registration.");
            }
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem
            {
                Description = "Organize screenshots now",
                MenuSection = "GameSnap",
                Action = _ => _organizer?.Organize()
            };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Organize screenshots now",
                MenuSection = "@GameSnap",
                Action = _ => _organizer?.Organize()
            };

            yield return new MainMenuItem
            {
                Description = "Open log",
                MenuSection = "@GameSnap",
                Action = _ =>
                {
                    var path = Path.Combine(GetPluginUserDataPath(), "gamesnap.log");
                    if (File.Exists(path))
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo("notepad.exe", path)
                        {
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                }
            };

            yield return new MainMenuItem
            {
                Description = "Open dictionary",
                MenuSection = "@GameSnap",
                Action = _ =>
                {
                    var path = Path.Combine(GetPluginUserDataPath(), "dictionary.txt");
                    if (!File.Exists(path))
                        File.WriteAllText(path, "# Format:\n# [Game Name]\n# alias1\n");
                    var psi = new System.Diagnostics.ProcessStartInfo("notepad.exe", path)
                    {
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            };

            yield return new MainMenuItem
            {
                Description = "Review unmatched screenshots",
                MenuSection = "@GameSnap",
                Action = _ => OpenReviewWindow()
            };
        }
        private void OpenReviewWindow()
        {
            if (_organizer == null || _dict == null || _logger == null)
            {
                PlayniteApi.Dialogs.ShowMessage("GameSnap is not fully initialized.", "GameSnap");
                return;
            }

            var vm = new ReviewViewModel(
                PlayniteApi, _settings, _dict, _organizer, _logger);

            var window = new Views.ReviewWindow(vm);
            vm.SetCloseAction(() => window.Close());
            window.ShowDialog();
        }
    }
}
