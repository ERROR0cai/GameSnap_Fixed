using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameSnapPlugin
{
    /// <summary>
    /// Gerencia o dictionary.txt com blocos [NomeJogo] e aliases abaixo.
    /// Formato:
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
        /// Carrega o dicionário: chave = alias normalizado, valor = nome do jogo.
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

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentGame = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                if (!string.IsNullOrEmpty(line) && currentGame != null)
                {
                    var key = Normalize(line);
                    if (!dict.ContainsKey(key))
                        dict[key] = currentGame;
                }
            }

            return dict;
        }

        /// <summary>
        /// Salva um alias aprendido automaticamente no bloco do jogo.
        /// Não duplica se já existir.
        /// </summary>
        public void SaveAlias(string prefix, string gameName)
        {
            if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(gameName))
                return;

            prefix   = prefix.Trim();
            gameName = gameName.Trim();

            var dict = Load();
            if (dict.ContainsKey(Normalize(prefix)))
                return; // já existe

            var lines = File.Exists(_dictionaryPath)
                ? new List<string>(File.ReadAllLines(_dictionaryPath, Encoding.UTF8))
                : new List<string>();

            var newLines  = new List<string>();
            bool inBlock  = false;
            bool blockFound   = false;
            bool inserted = false;
            string? currentGame = null;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                newLines.Add(line);

                if (line.Trim().StartsWith("[") && line.Trim().EndsWith("]"))
                {
                    currentGame = line.Trim().Substring(1, line.Trim().Length - 2).Trim();
                    inBlock = string.Equals(currentGame, gameName, StringComparison.OrdinalIgnoreCase);

                    if (inBlock)
                    {
                        blockFound = true;

                        // Procura o fim do bloco e insere antes
                        int j = i + 1;
                        bool alreadyThere = false;
                        while (j < lines.Count && !(lines[j].Trim().StartsWith("[") && lines[j].Trim().EndsWith("]")))
                        {
                            if (string.Equals(Normalize(lines[j]), Normalize(prefix), StringComparison.OrdinalIgnoreCase))
                            {
                                alreadyThere = true;
                                break;
                            }
                            newLines.Add(lines[j]);
                            j++;
                        }

                        if (!alreadyThere)
                            newLines.Add(prefix);

                        // Continua do ponto correto
                        i = j - 1;
                        inserted = true;
                    }
                }
            }

            if (!blockFound)
            {
                if (newLines.Count > 0)
                    newLines.Add("");
                newLines.Add($"[{gameName}]");
                newLines.Add(prefix);
            }

            File.WriteAllLines(_dictionaryPath, newLines, Encoding.UTF8);
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
