using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace GameSnapPlugin
{
    public class GameSnapPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        private GameSnapSettings   _settings  = new GameSnapSettings();
        private GameSnapLogger?    _logger;
        private DictionaryService? _dict;
        private OrganizerService?  _organizer;
        private WatcherService?    _watcher;
        private SteamService?           _steam;
        private LocalProviderService?   _localProvider;
        private EmulatorService?         _emulator;

        public GameSnapPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            try
            {
                // Load settings synchronously — must complete before GetSettings() is called
                _settings = LoadSettings();
                InitServices(_settings);

                // Start watcher in background to avoid blocking Playnite startup
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { _watcher?.Start(); }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Watcher start error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                // Never crash Playnite — log and continue with defaults
                System.Diagnostics.Debug.WriteLine($"GameSnap OnApplicationStarted error: {ex}");
                try
                {
                    _settings = new GameSnapSettings();
                    InitServices(_settings);
                }
                catch { /* last resort — give up gracefully */ }
            }
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

        public GameSnapSettings LoadSettings()
        {
            try
            {
                var saved    = LoadPluginSettings<GameSnapSettings>();
                var defaults = new GameSnapSettings();

                if (saved == null)
                {
                    _settings = defaults;
                    return _settings;
                }

                // Merge: preserve saved values, fill new fields with defaults
                if (saved.ImageExtensions == null || saved.ImageExtensions.Count == 0)
                    saved.ImageExtensions = defaults.ImageExtensions;
                if (saved.VideoExtensions == null || saved.VideoExtensions.Count == 0)
                    saved.VideoExtensions = defaults.VideoExtensions;
                if (saved.WindowBlacklist == null || saved.WindowBlacklist.Count == 0)
                    saved.WindowBlacklist = defaults.WindowBlacklist;
                if (saved.AdditionalSourceFolders == null)
                    saved.AdditionalSourceFolders = defaults.AdditionalSourceFolders;
                if (saved.EmulatorProfiles == null || saved.EmulatorProfiles.Count == 0)
                {
                    saved.EmulatorProfiles = defaults.EmulatorProfiles;
                }
                else
                {
                    // Remove accidental duplicates first
                    var seen = new System.Collections.Generic.HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
                    saved.EmulatorProfiles = saved.EmulatorProfiles
                        .Where(p => seen.Add(p.Name))
                        .ToList();

                    // Add any built-in emulator that is missing
                    for (int bi = 0; bi < EmulatorProfile.BuiltInNames.Length; bi++)
                    {
                        var builtIn = EmulatorProfile.BuiltInNames[bi];
                        if (!saved.EmulatorProfiles.Any(p =>
                            string.Equals(p.Name, builtIn, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Insert at correct position among built-ins
                            int insertAt = Math.Min(bi, saved.EmulatorProfiles.Count);
                            saved.EmulatorProfiles.Insert(insertAt,
                                new EmulatorProfile { Name = builtIn, Enabled = false });
                        }
                    }
                }
                if (string.IsNullOrEmpty(saved.UnmatchedFolderName))
                    saved.UnmatchedFolderName = defaults.UnmatchedFolderName;
                if (string.IsNullOrEmpty(saved.RenamePattern))
                    saved.RenamePattern = defaults.RenamePattern;
                if (saved.PollingIntervalSeconds <= 0)
                    saved.PollingIntervalSeconds = defaults.PollingIntervalSeconds;

                _settings = saved;
                _logger?.Info($"Settings loaded — Source: '{saved.SourceFolder}' Dest: '{saved.DestinationBase}'");
                return _settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameSnap LoadSettings error: {ex.Message}");
                _logger?.Error($"LoadSettings failed: {ex.Message} — using defaults");
                _settings = new GameSnapSettings();
                return _settings;
            }
        }

        public void SaveSettings(GameSnapSettings settings)
        {
            try
            {
                _settings = settings;
                SavePluginSettings(settings);
                _logger?.Info($"Settings saved — Source: '{settings.SourceFolder}' Dest: '{settings.DestinationBase}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameSnap SaveSettings error: {ex.Message}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"GameSnap failed to save settings:\n{ex.Message}", "GameSnap");
            }
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
            try
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

                // Emulator support
                if (settings.EnableEmulatorSupport)
                    _emulator = new EmulatorService(PlayniteApi, settings, _logger);
                else
                    _emulator = null;

                _organizer.EmulatorService = _emulator;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameSnap InitServices error: {ex}");
                _logger?.Error($"InitServices failed: {ex.Message}");
            }

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
            // Screenshot counter
            var game = args.Games.FirstOrDefault();
            if (game != null && !string.IsNullOrEmpty(_settings.DestinationBase))
            {
                var count = CountScreenshots(game.Name);
                yield return new GameMenuItem
                {
                    Description = count >= 0
                        ? $"Screenshots: {count} file(s)"
                        : "Screenshots: folder not found",
                    MenuSection = "GameSnap",
                    Action = _ =>
                    {
                        var folder = FindGameFolder(game.Name);
                        if (folder != null)
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo(folder)
                            {
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                        }
                    }
                };
            }

            yield return new GameMenuItem
            {
                Description = "Organize screenshots now",
                MenuSection = "GameSnap",
                Action = _ => _organizer?.Organize()
            };
        }

        private int CountScreenshots(string gameName)
        {
            var folder = FindGameFolder(gameName);
            if (folder == null) return -1;

            var allExts = new System.Collections.Generic.HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
            foreach (var e in _settings.ImageExtensions) allExts.Add(e);
            foreach (var e in _settings.VideoExtensions) allExts.Add(e);

            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Count(f => allExts.Contains(Path.GetExtension(f)));
        }

        private string? FindGameFolder(string gameName)
        {
            if (string.IsNullOrEmpty(_settings.DestinationBase) ||
                !Directory.Exists(_settings.DestinationBase))
                return null;

            var normGame = DictionaryService.Normalize(gameName);
            return Directory.GetDirectories(_settings.DestinationBase)
                .Where(d =>
                {
                    var normFolder = DictionaryService.Normalize(Path.GetFileName(d));
                    return normFolder.Contains(normGame) || normGame.Contains(normFolder);
                })
                .OrderByDescending(d => DictionaryService.Normalize(Path.GetFileName(d)).Length)
                .FirstOrDefault();
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
