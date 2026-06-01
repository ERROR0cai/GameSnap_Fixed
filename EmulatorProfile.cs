using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameSnapPlugin
{
    public class EmulatorProfile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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

        private bool   _enabled;
        private string _name       = "";
        private string _customPath = "";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayPath)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        public string CustomPath
        {
            get => _customPath;
            set
            {
                _customPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayPath));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(IsCustom));
                OnPropertyChanged(nameof(ResolvedPath));
            }
        }

        public bool IsUserAdded { get; set; } = false;

        // ── Computed properties with empty setters so JSON deserialization never fails ──

        public bool IsCustom
        {
            get => !string.IsNullOrEmpty(CustomPath);
            set { } // computed — setter intentionally empty, required for JSON compatibility
        }

        public string DisplayPath
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPath)) return CustomPath;
                var auto = EmulatorService.GetDefaultFolder(Name);
                return auto ?? "(auto)";
            }
            set { } // computed
        }

        public string StatusText
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPath))
                    return System.IO.Directory.Exists(CustomPath) ? "✓ Custom" : "✗ Not found";
                var auto = EmulatorService.GetDefaultFolder(Name);
                if (auto == null) return "✗ Not found";
                return System.IO.Directory.Exists(auto) ? "✓ Detected" : "✗ Not found";
            }
            set { } // computed
        }

        public string StatusColor
        {
            get => StatusText.StartsWith("✓") ? "#4caf50" : "#f44336";
            set { } // computed
        }

        public string? ResolvedPath
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPath))
                    return System.IO.Directory.Exists(CustomPath) ? CustomPath : null;
                var auto = EmulatorService.GetDefaultFolder(Name);
                return auto != null && System.IO.Directory.Exists(auto) ? auto : null;
            }
            set { } // computed
        }
    }
}
