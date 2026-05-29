using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GameSnapPlugin
{
    public class WatcherService : IDisposable
    {
        private readonly GameSnapSettings  _settings;
        private readonly OrganizerService  _organizer;
        private readonly GameSnapLogger    _logger;

        private FileSystemWatcher? _watcher;
        private Timer?             _pollingTimer;
        private bool               _disposed;

        public WatcherService(GameSnapSettings settings, OrganizerService organizer, GameSnapLogger logger)
        {
            _settings  = settings;
            _organizer = organizer;
            _logger    = logger;
        }

        public void Start()
        {
            if (string.IsNullOrEmpty(_settings.SourceFolder) || !Directory.Exists(_settings.SourceFolder))
            {
                _logger.Error($"Source folder not found: {_settings.SourceFolder}");
                return;
            }

            // FileSystemWatcher — reage imediatamente a novos arquivos
            _watcher = new FileSystemWatcher(_settings.SourceFolder)
            {
                NotifyFilter        = NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Error   += OnWatcherError;

            // Loop de polling — captura arquivos que o watcher possa ter perdido
            var interval = TimeSpan.FromSeconds(_settings.PollingIntervalSeconds);
            _pollingTimer = new Timer(_ => SafeOrganize(), null, interval, interval);

            _logger.Info($"Watcher started on: {_settings.SourceFolder}");
        }

        public void Stop()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            _logger.Info("Watcher stopped.");
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            // Delay then organize fully off the watcher thread — never blocks Playnite
            Task.Delay(2000).ContinueWith(_ =>
                Task.Run(() => SafeOrganize()));
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.Error($"Watcher error: {e.GetException().Message}");

            // Tenta reiniciar o watcher
            Stop();
            Thread.Sleep(5000);
            Start();
        }

        private void SafeOrganize()
        {
            try   { _organizer.Organize(); }
            catch (Exception ex) { _logger.Error($"Organize error: {ex.Message}"); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
        }
    }
}
