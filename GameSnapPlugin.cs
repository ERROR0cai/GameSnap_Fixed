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

        public IPlayniteAPI PlayniteApi => base.PlayniteApi;

        private GameSnapSettings  _settings  = new GameSnapSettings();
        private GameSnapLogger?   _logger;
        private DictionaryService? _dict;
        private OrganizerService? _organizer;
        private WatcherService?   _watcher;

        public GameSnapPlugin(IPlayniteAPI api) : base(api)
        {
            Properties  = new GenericPluginProperties { HasSettings = true };
        }

        // ──────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            InitServices(_settings);
            _watcher?.Start();
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            _watcher?.Stop();
            _watcher?.Dispose();
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            _organizer?.SetCurrentGame(args.Game.Name);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            _organizer?.SetCurrentGame(null);
        }

        // ──────────────────────────────────────────────
        // Settings
        // ──────────────────────────────────────────────

        public override ISettings GetSettings(bool firstRunSettings)
            => new SettingsViewModel(this);

        public override UserControl GetSettingsView(bool firstRunSettings)
            => new Views.SettingsView();

        public GameSnapSettings LoadSettings()
        {
            _settings = LoadPluginSettings<GameSnapSettings>() ?? new GameSnapSettings();
            return _settings;
        }

        public void SaveSettings(GameSnapSettings settings)
        {
            _settings = settings;
            SavePluginSettings(settings);
        }

        public void ApplySettings(GameSnapSettings settings)
        {
            _watcher?.Stop();
            _watcher?.Dispose();
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
            _watcher   = new WatcherService(settings, _organizer, _logger);
        }

        public new string GetPluginUserDataPath()
            => Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, Id.ToString());

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem
            {
                Description = "GameSnap: Organize screenshots now",
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
                        System.Diagnostics.Process.Start("notepad.exe", path);
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
                        File.WriteAllText(path, "# Formato:\n# [Nome do Jogo]\n# alias1\n");
                    System.Diagnostics.Process.Start("notepad.exe", path);
                }
            };
        }
    }
}
