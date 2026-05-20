using System.Collections.Generic;

namespace GameSnapPlugin
{
    public class GameSnapSettings
    {
        // Pasta onde o Game Bar / ShareX / outros jogam as capturas
        public string SourceFolder { get; set; } = "";

        // Pasta base onde ficam as subpastas dos jogos
        public string DestinationBase { get; set; } = "";

        // Intervalo do loop de organização em segundos
        public int PollingIntervalSeconds { get; set; } = 30;

        // Usar integração com Playnite (jogo atual)
        public bool UsePlayniteDetection { get; set; } = true;

        // Usar fallback por janela ativa
        public bool UseWindowFallback { get; set; } = true;

        // Criar pasta automaticamente quando um jogo é iniciado (desativado por padrão)
        public bool AutoCreateFolders { get; set; } = false;

        // Extensões de imagem monitoradas
        public List<string> ImageExtensions { get; set; } = new List<string>
        {
            ".png", ".jpg", ".jpeg"
        };

        // Extensões de vídeo monitoradas (vão para subpasta Videos)
        public List<string> VideoExtensions { get; set; } = new List<string>
        {
            ".mp4", ".wmv"
        };

        // Janelas ignoradas no fallback
        public List<string> WindowBlacklist { get; set; } = new List<string>
        {
            "explorer", "notepad", "settings", "task manager",
            "chrome", "edge", "opera", "steam", "discord",
            "launcher", "update", "setup", "windows", "desktop"
        };
    }
}
