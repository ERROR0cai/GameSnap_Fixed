using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GameSnapPlugin.Views
{
    public class UnmatchedFileItem
    {
        public string FullPath  { get; set; }
        public string FileName  { get; set; }
        public string DateString { get; set; }

        public UnmatchedFileItem(string path)
        {
            FullPath   = path;
            FileName   = Path.GetFileName(path);
            var info   = new FileInfo(path);
            DateString = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
        }
    }

    public partial class FullscreenReviewWindow : Window
    {
        private readonly IPlayniteAPI        _api;
        private readonly GameSnapSettings    _settings;
        private readonly DictionaryService   _dict;
        private readonly OrganizerService    _organizer;
        private readonly GameSnapLogger      _logger;

        private ObservableCollection<UnmatchedFileItem> _files;
        private List<Game> _allGames;
        private ObservableCollection<Game> _filteredGames;

        public FullscreenReviewWindow(
            IPlayniteAPI api,
            GameSnapSettings settings,
            DictionaryService dict,
            OrganizerService organizer,
            GameSnapLogger logger)
        {
            InitializeComponent();

            // KeyDown wired here instead of in XAML to avoid a MSBuild/XAML
            // compiler ambiguity (CS0426) when the assembly's root namespace
            // matches the main plugin class name (GameSnapPlugin).
            this.KeyDown += Window_KeyDown;

            _api      = api;
            _settings = settings;
            _dict     = dict;
            _organizer = organizer;
            _logger   = logger;

            LoadFiles();
            LoadGames();

            FileList.ItemsSource = _files;
            GameList.ItemsSource = _filteredGames;

            if (_files.Count > 0)
                FileList.SelectedIndex = 0;

            FileList.Focus();
        }

        // ── Data loading ─────────────────────────────────────────────────────────

        private void LoadFiles()
        {
            var unmatchedPath = Path.Combine(
                _settings.DestinationBase,
                _settings.UnmatchedFolderName);

            var items = new List<UnmatchedFileItem>();

            if (Directory.Exists(unmatchedPath))
            {
                var extensions = _settings.ImageExtensions
                    .Concat(_settings.VideoExtensions)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                items = Directory.GetFiles(unmatchedPath)
                    .Where(f => extensions.Contains(Path.GetExtension(f)))
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .Select(f => new UnmatchedFileItem(f))
                    .ToList();
            }

            _files = new ObservableCollection<UnmatchedFileItem>(items);
            UpdateCounters();
        }

        private void LoadGames()
        {
            _allGames = _api.Database.Games
                .GroupBy(g => g.Name)
                .Select(g => g.First())
                .OrderBy(g => g.Name)
                .ToList();

            _filteredGames = new ObservableCollection<Game>(_allGames);
        }

        private void FilterGames(string query)
        {
            _filteredGames.Clear();

            var matches = string.IsNullOrWhiteSpace(query)
                ? _allGames
                : _allGames.Where(g => g.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var g in matches)
                _filteredGames.Add(g);

            if (_filteredGames.Count > 0)
                GameList.SelectedIndex = 0;
        }

        private void UpdateCounters()
        {
            CounterLabel.Text  = $"{_files.Count} file(s) pending";
            SubtitleLabel.Text = _files.Count > 0
                ? _files[0].FileName
                : "All done!";
        }

        // ── UI events ────────────────────────────────────────────────────────────

        private void FileList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (FileList.SelectedItem is not UnmatchedFileItem item) return;

            SubtitleLabel.Text = item.FileName;

            try
            {
                var ext = Path.GetExtension(item.FullPath).ToLowerInvariant();
                if (_settings.ImageExtensions.Contains(ext))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource     = new Uri(item.FullPath);
                    bmp.CacheOption   = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    PreviewImage.Source       = bmp;
                    PreviewImage.Visibility   = Visibility.Visible;
                    NoPreviewLabel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    PreviewImage.Source       = null;
                    PreviewImage.Visibility   = Visibility.Collapsed;
                    NoPreviewLabel.Text       = "Video preview not available";
                    NoPreviewLabel.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                PreviewImage.Visibility  = Visibility.Collapsed;
                NoPreviewLabel.Visibility = Visibility.Visible;
            }
        }

        // Abre o teclado virtual nativo do Playnite (SelectString) — em
        // fullscreen, isso já mostra o teclado on-screen com suporte a D-pad.
        // Ao confirmar, a lista de jogos é filtrada pelo texto digitado.
        private void OpenGameSearch()
        {
            var result = _api.Dialogs.SelectString(
                GameSearchLabel.Text ?? "",
                "Search Game",
                "Type part of the game name:");

            if (result == null || !result.Result) return;

            var query = result.SelectedString ?? "";
            FilterGames(query);

            if (string.IsNullOrWhiteSpace(query))
            {
                GameSearchPlaceholder.Visibility = Visibility.Visible;
                GameSearchLabel.Visibility       = Visibility.Collapsed;
            }
            else
            {
                GameSearchLabel.Text             = query;
                GameSearchPlaceholder.Visibility = Visibility.Collapsed;
                GameSearchLabel.Visibility       = Visibility.Visible;
            }

            // Após confirmar a busca, move o foco para a lista já filtrada
            GameList.Focus();
            if (_filteredGames.Count > 0)
                GameList.SelectedIndex = 0;
        }

        private void GameSearchButton_Click(object sender, MouseButtonEventArgs e) => OpenGameSearch();

        private void GameSearchButton_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenGameSearch();
                e.Handled = true;
            }
        }

        private void BtnAssign_Click(object sender, RoutedEventArgs e) => Assign();
        private void BtnSkip_Click(object sender, RoutedEventArgs e)   => Skip();
        private void BtnDelete_Click(object sender, RoutedEventArgs e) => DeleteFile();

        // ── Actions ──────────────────────────────────────────────────────────────

        private void Assign()
        {
            if (FileList.SelectedItem is not UnmatchedFileItem file) return;
            if (GameList.SelectedItem is not Game game) return;

            try
            {
                var destFolder = Path.Combine(_settings.DestinationBase, game.Name);
                Directory.CreateDirectory(destFolder);

                var destPath = Path.Combine(destFolder, Path.GetFileName(file.FullPath));
                File.Move(file.FullPath, destPath);

                // Learn alias from file prefix
                var prefix = Path.GetFileNameWithoutExtension(file.FileName)
                    .Split('_')[0];
                if (!string.IsNullOrWhiteSpace(prefix))
                    _dict.SaveAlias(prefix, game.Name);

                _logger.Info($"[Fullscreen Review] Assigned {file.FileName} -> {game.Name}");

                _files.Remove(file);
                UpdateCounters();

                if (_files.Count > 0)
                    FileList.SelectedIndex = 0;
                else
                    Close();
            }
            catch (Exception ex)
            {
                _logger.Error($"[Fullscreen Review] Assign failed: {ex.Message}");
            }
        }

        private void Skip()
        {
            if (_files.Count == 0) return;
            var idx = FileList.SelectedIndex;
            FileList.SelectedIndex = (idx + 1) % _files.Count;
        }

        private void DeleteFile()
        {
            if (FileList.SelectedItem is not UnmatchedFileItem file) return;

            var result = _api.Dialogs.ShowMessage(
                $"Delete {file.FileName}?",
                "GameSnap",
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                File.Delete(file.FullPath);
                _files.Remove(file);
                UpdateCounters();

                if (_files.Count > 0)
                    FileList.SelectedIndex = 0;
                else
                    Close();
            }
            catch (Exception ex)
            {
                _logger.Error($"[Fullscreen Review] Delete failed: {ex.Message}");
            }
        }

        // ── Keyboard / gamepad input ─────────────────────────────────────────────
        // Xbox controller maps to keyboard via Windows:
        //   A     → Enter
        //   B     → Escape
        //   Start → F1  (via XInput)
        //   D-pad → Arrow keys (WPF handles ListBox navigation natively)
        //
        // Fluxo de busca:
        //   1. D-pad direita (na lista de arquivos) -> foca o bota de busca
        //   2. A -> abre o teclado virtual nativo do Playnite (SelectString)
        //   3. Digite e confirme -> a lista de jogos ja aparece filtrada
        //   4. D-pad baixo -> entra na lista filtrada
        //   5. A -> Assign

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:       // A button
                    if (GameSearchButton.IsFocused)
                        OpenGameSearch();
                    else if (FileList.IsFocused || PreviewImage.IsFocused)
                        GameSearchButton.Focus();
                    else
                        Assign();
                    e.Handled = true;
                    break;

                case Key.Escape:      // B button
                    Close();
                    e.Handled = true;
                    break;

                case Key.F1:          // Start button
                    Skip();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Tab cycles: file list -> search button -> game list -> file list
                    if (FileList.IsKeyboardFocusWithin)
                        GameSearchButton.Focus();
                    else if (GameSearchButton.IsFocused)
                        GameList.Focus();
                    else
                        FileList.Focus();
                    e.Handled = true;
                    break;

                case Key.Left:
                    FileList.Focus();
                    e.Handled = true;
                    break;

                case Key.Right:
                    GameSearchButton.Focus();
                    e.Handled = true;
                    break;

                case Key.Down when GameSearchButton.IsFocused:
                    GameList.Focus();
                    if (GameList.Items.Count > 0 && GameList.SelectedIndex < 0)
                        GameList.SelectedIndex = 0;
                    e.Handled = true;
                    break;
            }
        }
    }
}
