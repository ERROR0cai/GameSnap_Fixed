using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameSnapPlugin
{
    /// <summary>
    /// Manages dictionary.txt with blocks [GameName] and aliases below.
    /// Format:
    ///   [Cyberpunk 2077]
    ///   Cyberpunk2077
    ///   cyberpunk
    /// </summary>
    public class DictionaryService
    {
        private readonly string _dictionaryPath;

        public DictionaryService(string pluginDataPath)
        {
            _dictionaryPath = Path.Combine(pluginDataPath, "dictionary.txt");
        }

        public string DictionaryPath => _dictionaryPath;

        /// <summary>
        /// Loads dictionary: key = normalized alias, value = game name.
        /// </summary>
        public Dictionary<string, string> Load()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(_dictionaryPath))
                return dict;

            string? currentGame = null;

            foreach (var rawLine in File.ReadAllLines(_dictionaryPath, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentGame = line.Substring(1, line.Length - 2).Trim();
                    // Also map the game name itself as an alias
                    var normGame = Normalize(currentGame);
                    if (!string.IsNullOrEmpty(normGame) && !dict.ContainsKey(normGame))
                        dict[normGame] = currentGame;
                    continue;
                }

                if (currentGame != null && !string.IsNullOrEmpty(line))
                {
                    var key = Normalize(line);
                    if (!string.IsNullOrEmpty(key) && !dict.ContainsKey(key))
                        dict[key] = currentGame;
                }
            }

            return dict;
        }

        /// <summary>
        /// Saves a learned alias to dictionary.txt.
        /// Creates the file with a header if it does not exist.
        /// </summary>
        public void SaveAlias(string prefix, string gameName)
        {
            if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(gameName))
                return;

            prefix   = prefix.Trim();
            gameName = gameName.Trim();

            // Don't save if already known
            var existing = Load();
            if (existing.TryGetValue(Normalize(prefix), out var known) &&
                string.Equals(known, gameName, StringComparison.OrdinalIgnoreCase))
                return;

            // Create file with header if missing
            if (!File.Exists(_dictionaryPath))
            {
                File.WriteAllText(_dictionaryPath,
                    "# GameSnap Dictionary\n" +
                    "# Format:\n" +
                    "# [Game Name]\n" +
                    "# alias1\n" +
                    "# alias2\n\n",
                    Encoding.UTF8);
            }

            var lines    = new List<string>(File.ReadAllLines(_dictionaryPath, Encoding.UTF8));
            var header   = $"[{gameName}]";
            int blockIdx = -1;

            // Find existing block for this game
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i].Trim(), header, StringComparison.OrdinalIgnoreCase))
                {
                    blockIdx = i;
                    break;
                }
            }

            if (blockIdx >= 0)
            {
                // Check if alias already exists in the block
                int j = blockIdx + 1;
                while (j < lines.Count &&
                       !(lines[j].Trim().StartsWith("[") && lines[j].Trim().EndsWith("]")))
                {
                    if (string.Equals(lines[j].Trim(), prefix, StringComparison.OrdinalIgnoreCase))
                        return; // already there
                    j++;
                }

                // Insert alias right after the block header
                lines.Insert(blockIdx + 1, prefix);
            }
            else
            {
                // Add new block at the end
                if (lines.Count > 0 && !string.IsNullOrEmpty(lines[lines.Count - 1]))
                    lines.Add("");
                lines.Add(header);
                lines.Add(prefix);
            }

            File.WriteAllLines(_dictionaryPath, lines, Encoding.UTF8);
        }

        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var result = new StringBuilder();
            foreach (var c in text.ToLowerInvariant())
                if (char.IsLetterOrDigit(c) || c == ' ')
                    result.Append(c);
            return result.ToString().Trim();
        }
    }
}
