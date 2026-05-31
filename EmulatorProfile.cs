using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace GameSnapPlugin
{
    /// <summary>
    /// Represents a single emulator entry in the Settings emulator list.
    /// </summary>
    public class EmulatorProfile : INotifyPropertyChanged
    {
        public static readonly string[] BuiltInNames =
        {
            "RetroArch", "PCSX2", "Dolphin", "RPCS3",
            "Cemu", "PPSSPP", "mGBA", "DuckStation"
        };

        public static System.Collections.Generic.List<EmulatorProfile> CreateDefaults()
        {
            var list = new System.Collections.Generic.List<EmulatorProfile>();
            foreach (var name in BuiltInNames)
                list.Add(new EmulatorProfile { Name = name, Enabled = false });
            return list;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool   _enabled;
        private string _name        = "";
        private string _customPath  = "";

        // Display name (e.g. "RetroArch")
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        // Whether this emulator is active
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        // Empty = use auto-detection; non-empty = override
        public string CustomPath
        {
            get => _customPath;
            set
            {
                _customPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayPath));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsCustom));
            }
        }

        // True if this is a user-added emulator (not in the built-in list)
        public bool IsUserAdded { get; set; } = false;

        // ── Computed display properties ──

        [JsonIgnore]
        public bool IsCustom => !string.IsNullOrEmpty(CustomPath);

        [JsonIgnore]
        public string DisplayPath
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPath)) return CustomPath;
                var auto = EmulatorService.GetDefaultFolder(Name);
                return auto ?? "(auto)";
            }
        }

        [JsonIgnore]
        public string StatusText
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPath))
                {
                    return System.IO.Directory.Exists(CustomPath)
                        ? "✓ Custom"
                        : "✗ Not found";
                }

                var auto = EmulatorService.GetDefaultFolder(Name);
                if (auto == null) return "✗ Not found";
                return System.IO.Directory.Exists(auto) ? "✓ Detected" : "✗ Not found";
            }
        }

        [JsonIgnore]
        public string StatusColor =>
            StatusText.StartsWith("✓") ? "#4caf50" : "#f44336";

        [JsonIgnore]
        public string? ResolvedPath
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPath))
                    return System.IO.Directory.Exists(CustomPath) ? CustomPath : null;
                var auto = EmulatorService.GetDefaultFolder(Name);
                return auto != null && System.IO.Directory.Exists(auto) ? auto : null;
            }
        }
    }
}
