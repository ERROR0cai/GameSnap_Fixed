using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace GameSnapPlugin
{
    public class EmulatorProfile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static readonly string[] BuiltInNames =
        {
            "RetroArch", "PCSX2", "Dolphin", "RPCS3",
            "Cemu", "PPSSPP", "mGBA", "DuckStation"
        };

        public static List<EmulatorProfile> CreateDefaults()
        {
            var list = new List<EmulatorProfile>();
            foreach (var name in BuiltInNames)
                list.Add(new EmulatorProfile { Name = name, Enabled = false });
            return list;
        }

        // ── Propriedades persistidas no JSON ─────────────────────────────────────

        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; Notify(); NotifyComputed(); }
        }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; Notify(); }
        }

        private string _customPath = "";
        public string CustomPath
        {
            get => _customPath;
            set { _customPath = value; Notify(); NotifyComputed(); }
        }

        public bool IsUserAdded { get; set; } = false;

        // ── Propriedades computed — NÃO persistidas no JSON ──────────────────────

        [IgnoreDataMember]
        public bool IsCustom => !string.IsNullOrEmpty(CustomPath);

        [IgnoreDataMember]
        public string DisplayPath
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPath)) return CustomPath;
                var auto = EmulatorService.GetDefaultFolder(Name);
                return auto ?? "(auto)";
            }
        }

        [IgnoreDataMember]
        public string StatusText
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPath))
                    return System.IO.Directory.Exists(CustomPath) ? "OK Custom" : "X Not found";
                var auto = EmulatorService.GetDefaultFolder(Name);
                if (auto == null) return "X Not found";
                return System.IO.Directory.Exists(auto) ? "OK Detected" : "X Not found";
            }
        }

        [IgnoreDataMember]
        public string StatusColor => StatusText.StartsWith("OK") ? "#4caf50" : "#f44336";

        [IgnoreDataMember]
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

        private void NotifyComputed()
        {
            Notify(nameof(IsCustom));
            Notify(nameof(DisplayPath));
            Notify(nameof(StatusText));
            Notify(nameof(StatusColor));
            Notify(nameof(ResolvedPath));
        }
    }
}
