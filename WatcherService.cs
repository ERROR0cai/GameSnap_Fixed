using System;
using System.Collections.Concurrent;
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

        // Debounce — evita processar o mesmo arquivo duas vezes em < 5s
        private readonly ConcurrentDictionary<string, DateTime> _recentlyProcessed
            = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

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
            var path = e.FullPath;

            // Debounce: ignorar se já processamos este arquivo nos últimos 5 segundos
            var now = DateTime.UtcNow;
            if (_recentlyProcessed.TryGetValue(path, out var lastSeen) &&
                (now - lastSeen).TotalSeconds < 5)
                return;

            _recentlyProcessed[path] = now;

            // Limpa entradas antigas do debounce (> 30s) para não acumular memória
            foreach (var key in _recentlyProcessed.Keys)
                if ((now - _recentlyProcessed[key]).TotalSeconds > 30)
                    _recentlyProcessed.TryRemove(key, out _);

            // Delay de 2s para o arquivo terminar de ser escrito antes de processar
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
