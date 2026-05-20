using System.Collections.Generic;

namespace GameSnapPlugin
{
    public class GameSnapSettings
    {
        // Pasta onde o Game Bar / ShareX / outros jogam as capturas
        public string SourceFolder { get; set; } = "";

        // Pastas adicionais de origem (múltiplas fontes)
        public List<string> AdditionalSourceFolders { get; set; } = new List<string>();

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

        // Mover arquivos sem match para pasta "Sem Jogo" em vez de ignorar
        public bool MoveUnmatchedToFolder { get; set; } = false;

        // Nome da pasta para arquivos sem match
        public string UnmatchedFolderName { get; set; } = "_Unmatched";

        // Mostrar notificação no Playnite ao mover arquivos
        public bool ShowNotifications { get; set; } = true;

        // Padrão de renomeação do arquivo de destino
        // Tokens: {game}, {date}, {time}, {datetime}, {original}
        public string RenamePattern { get; set; } = "{game}_{date}_{time}";

        // Backup automático (desativado por padrão)
        public bool EnableBackup { get; set; } = false;

        // Pasta de backup
        public string BackupFolder { get; set; } = "";

        // Suporte a screenshots do Steam (desativado por padrão)
        public bool EnableSteamSupport { get; set; } = false;

        // Caminho do Steam (detectado automaticamente se vazio)
        public string SteamPath { get; set; } = "";

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
