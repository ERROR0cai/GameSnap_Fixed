using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;

namespace GameSnapPlugin
{
    public class GameSnapPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("1826881c-4e6e-4ed3-ac6c-8605f953daf4");

        // ScreenshotsVisualizer GUID — usado para o refresh automático
        private static readonly Guid ScreenshotsVisualizerId = Guid.Parse("c6c8276f-91bf-48e5-a1d1-4bee0b493488");

        public GameSnapSettingsViewModel PluginSettings { get; private set; }
        private GameSnapSettings S => PluginSettings.Settings;

        private GameSnapLogger?    _logger;
        private DictionaryService? _dict;
        private OrganizerService?  _organizer;
        private WatcherService?    _watcher;
        private SteamService?      _steam;

        public GameSnapPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
            PluginSettings = new GameSnapSettingsViewModel(this);
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try   { InitServices(S); _watcher?.Start(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"GameSnap init error: {ex.Message}"); }
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

        private void TryAutoCreateFolder(string gameName)
        {
            if (!S.AutoCreateFolders) return;
            if (string.IsNullOrWhiteSpace(S.DestinationBase)) return;
            if (!Directory.Exists(S.DestinationBase)) return;

            var folderName = string.Concat(gameName.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(folderName)) return;

            var folderPath = Path.Combine(S.DestinationBase, folderName);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                _logger?.Info($"Auto-created folder: {folderPath}");
            }
        }

        // ── ScreenshotsVisualizer integration ───────────────────────────────────

        // Notifica o ScreenshotsVisualizer para reescanear um jogo após mover screenshots.
        // Usa reflexão para não criar dependência direta no projeto.
        public void NotifyScreenshotsVisualizerRefresh(Game game)
        {
            try
            {
                var sv = PlayniteApi.Addons.Plugins
                    .FirstOrDefault(p => p.Id == ScreenshotsVisualizerId);
                if (sv == null) return;

                // Acessa Database.RefreshData(Game) via reflexão
                var dbProp = sv.GetType().GetProperty("Database",
                    BindingFlags.Public | BindingFlags.Instance);
                if (dbProp == null) return;

                var db = dbProp.GetValue(sv);
                if (db == null) return;

                var refreshMethod = db.GetType().GetMethod("RefreshData",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(Game) }, null);

                refreshMethod?.Invoke(db, new object[] { game });
                _logger?.Info($"ScreenshotsVisualizer refreshed for: {game.Name}");
            }
            catch (Exception ex)
            {
                // Silencioso — SV pode não estar instalado
                _logger?.Info($"ScreenshotsVisualizer refresh skipped: {ex.Message}");
            }
        }

        // ── Settings ─────────────────────────────────────────────────────────────

        public override ISettings GetSettings(bool firstRunSettings) => PluginSettings;

        public override UserControl GetSettingsView(bool firstRunSettings)
            => new Views.SettingsTabView();

        public void ApplySettings(GameSnapSettings s)
        {
            _watcher?.Stop();
            _watcher?.Dispose();
            _watcher = null;
            InitServices(s);
            _watcher?.Start();
        }

        // ── Services ─────────────────────────────────────────────────────────────

        private void InitServices(GameSnapSettings s)
        {
            var dataPath = GetPluginUserDataPath();
            Directory.CreateDirectory(dataPath);

            _logger    = new GameSnapLogger(dataPath);
            _dict      = new DictionaryService(dataPath);
            _organizer = new OrganizerService(s, _dict, _logger);

            if (s.EnableSteamSupport)
            {
                _steam = new SteamService(PlayniteApi, _logger);
                _organizer.SteamService = _steam;
            }
            else
            {
                _steam = null;
                _organizer.SteamService = null;
            }

            _organizer.OnFileMoved = (summary, message) =>
            {
                if (s.ShowNotifications)
                    PlayniteApi.Notifications.Add(
                        new NotificationMessage(Guid.NewGuid().ToString(), message, NotificationType.Info));
            };

            _organizer.OnGamesOrganized = (gameNames) =>
            {
                if (!s.EnableScreenshotsVisualizerRefresh) return;

                // Notifica o ScreenshotsVisualizer para reescanear cada jogo afetado
                foreach (var name in gameNames)
                {
                    var game = PlayniteApi.Database.Games
                        .FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (game != null)
                        NotifyScreenshotsVisualizerRefresh(game);
                }
            };

            _watcher = new WatcherService(s, _organizer, _logger);
        }

        // ── Menus ─────────────────────────────────────────────────────────────────

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
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
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
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
                }
            };
            yield return new MainMenuItem
            {
                Description = "Review unmatched screenshots",
                MenuSection = "@GameSnap",
                Action = _ => OpenReviewWindow()
            };
            yield return new MainMenuItem
            {
                Description = "Review unmatched screenshots (Fullscreen / Gamepad)",
                MenuSection = "@GameSnap",
                Action = _ => OpenFullscreenReviewWindow()
            };
        }

        private void OpenReviewWindow()
        {
            if (_organizer == null || _dict == null || _logger == null)
            {
                PlayniteApi.Dialogs.ShowMessage("GameSnap is not fully initialized.", "GameSnap");
                return;
            }
            var vm     = new ReviewViewModel(PlayniteApi, S, _dict, _organizer, _logger);
            var window = new Views.ReviewWindow(vm);
            vm.SetCloseAction(() => window.Close());
            window.ShowDialog();
        }

        private void OpenFullscreenReviewWindow()
        {
            if (_organizer == null || _dict == null || _logger == null)
            {
                PlayniteApi.Dialogs.ShowMessage("GameSnap is not fully initialized.", "GameSnap");
                return;
            }
            var window = new Views.FullscreenReviewWindow(
                PlayniteApi, S, _dict, _organizer, _logger);
            window.ShowDialog();
        }
    }
}
