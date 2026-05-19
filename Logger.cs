using System;
using System.IO;

namespace GameSnapPlugin
{
    public enum LogType { Move, Learn, Fallback, Error, Info }

    public class GameSnapLogger
    {
        private readonly string _logPath;
        private readonly object _lock = new object();

        public GameSnapLogger(string pluginDataPath)
        {
            _logPath = Path.Combine(pluginDataPath, "gamesnap.log");
        }

        public void Write(LogType type, string message)
        {
            var prefix = type switch
            {
                LogType.Move     => "✔ MOVED",
                LogType.Learn    => "🧠 LEARNED",
                LogType.Fallback => "⚠ FALLBACK",
                LogType.Error    => "❌ ERROR",
                _                => "LOG"
            };

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {prefix}\n{message}\n";

            lock (_lock)
            {
                File.AppendAllText(_logPath, line, System.Text.Encoding.UTF8);
            }
        }

        public void Info(string message)  => Write(LogType.Info, message);
        public void Error(string message) => Write(LogType.Error, message);
    }
}
