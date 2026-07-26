using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RightClickGuardian
{
    public sealed class SoftwareIdentity
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Abbreviation { get; set; }

        public SoftwareIdentity(string key, string name, string abbreviation)
        {
            Key = key ?? "";
            Name = name ?? "";
            Abbreviation = abbreviation ?? "";
        }
    }

    public sealed class SoftwareGroup
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Abbreviation { get; set; }
        public MenuEntry IconEntry { get; set; }
        public List<MenuEntry> Entries { get; set; }

        public SoftwareGroup()
        {
            Key = "";
            Name = "";
            Abbreviation = "";
            Entries = new List<MenuEntry>();
        }
    }

    public static class SoftwareCatalog
    {
        private sealed class KnownSoftware
        {
            public string[] Tokens;
            public SoftwareIdentity Identity;

            public KnownSoftware(string key, string name, string abbreviation,
                params string[] tokens)
            {
                Tokens = tokens;
                Identity = new SoftwareIdentity(key, name, abbreviation);
            }
        }

        private static readonly KnownSoftware[] Known = new[]
        {
            new KnownSoftware("bandizip", "Bandizip", "BZ",
                "bandizip", "bandisoft", "bzshell", "bdzshl"),
            new KnownSoftware("onedrive", "OneDrive", "OD",
                "onedrive", "filesyncex"),
            new KnownSoftware("baidunetdisk", "百度网盘", "BD",
                "baidunetdisk", "yunshellext", "yunshillexplorercommand"),
            new KnownSoftware("123pan", "123云盘", "123",
                "123pan", "123synccloud"),
            new KnownSoftware("quark", "夸克", "QK",
                "quark", "quarkai"),
            new KnownSoftware("thunder", "迅雷", "XL",
                "thunder", "xunlei"),
            new KnownSoftware("vscode", "Visual Studio Code", "VS",
                "visual studio code", "vscode", "\\code.exe"),
            new KnownSoftware("git", "Git", "GIT",
                "git_shell", "git_gui", "git-bash", "git-gui"),
            new KnownSoftware("potplayer", "PotPlayer", "PT",
                "potplayer", "daum"),
            new KnownSoftware("epic", "Epic Games", "EP",
                "epic games", "epicgames", "unreal engine"),
            new KnownSoftware("wegame", "WeGame", "WG",
                "wegame"),
            new KnownSoftware("douyin", "抖音", "DY",
                "douyin", "抖音"),
            new KnownSoftware("neteaseuu", "网易 UU", "UU",
                "netease", "uu远程", "uu加速"),
            new KnownSoftware("xiaoheihe", "小黑盒", "XHH",
                "小黑盒"),
            new KnownSoftware("adobe", "Adobe", "AD",
                "adobe", "coresync"),
            new KnownSoftware("steam", "Steam", "ST",
                "steam"),
            new KnownSoftware("wechat", "微信", "WX",
                "wechat", "weixin", "微信"),
            new KnownSoftware("winrar", "WinRAR", "RAR",
                "winrar", "rar shell"),
            new KnownSoftware("7zip", "7-Zip", "7Z",
                "7-zip", "7zip"),
            new KnownSoftware("icloud", "iCloud", "IC",
                "icloud"),
            new KnownSoftware("nvidia", "NVIDIA", "NV",
                "nvidia"),
            new KnownSoftware("amd", "AMD Software", "AMD",
                "amdradeon", "amd software", "radeon"),
            new KnownSoftware("python", "Python", "PY",
                "pythonmanager", "python", "idle"),
            new KnownSoftware("clipchamp", "Clipchamp", "CC",
                "clipchamp"),
            new KnownSoftware("photos", "Windows 照片", "PH",
                "microsoft.windows.photos", "windows photos"),
            new KnownSoftware("paint", "Microsoft 画图", "PT",
                "microsoft.paint", "mspaint"),
            new KnownSoftware("notepad", "Windows 记事本", "NP",
                "microsoft.windowsnotepad", "notepad"),
            new KnownSoftware("terminal", "Windows 终端", "WT",
                "windowsterminal", "windows terminal"),
            new KnownSoftware("defender", "Microsoft Defender", "MD",
                "defender", "epp")
        };

        public static List<SoftwareGroup> Build(IEnumerable<MenuEntry> entries)
        {
            Dictionary<string, SoftwareGroup> groups =
                new Dictionary<string, SoftwareGroup>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> functions =
                new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (MenuEntry entry in entries ?? Enumerable.Empty<MenuEntry>())
            {
                SoftwareIdentity identity = Identify(entry);
                if (identity == null || string.IsNullOrWhiteSpace(identity.Key)) continue;

                SoftwareGroup group;
                if (!groups.TryGetValue(identity.Key, out group))
                {
                    group = new SoftwareGroup();
                    group.Key = identity.Key;
                    group.Name = identity.Name;
                    group.Abbreviation = identity.Abbreviation;
                    groups.Add(identity.Key, group);
                    functions.Add(identity.Key,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }

                string functionKey = ControlKey(entry);
                if (functions[identity.Key].Add(functionKey))
                    group.Entries.Add(entry);

                if (group.IconEntry == null && HasUsefulIcon(entry))
                    group.IconEntry = entry;
            }

            foreach (SoftwareGroup group in groups.Values)
            {
                group.Entries = group.Entries
                    .OrderByDescending(entry => entry.Enabled && !entry.Protected)
                    .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                if (group.IconEntry == null && group.Entries.Count > 0)
                    group.IconEntry = group.Entries[0];
            }

            return groups.Values
                .OrderByDescending(group => group.Entries.Count)
                .ThenBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static SoftwareIdentity Identify(MenuEntry entry)
        {
            if (entry == null) return null;
            string combined = string.Join(" ", new[]
            {
                entry.PackageName, entry.Name, entry.Source, entry.Command,
                entry.IconHint, entry.FilePath, entry.Details
            }).ToLowerInvariant();

            foreach (KnownSoftware known in Known)
            {
                if (known.Tokens.Any(token =>
                    combined.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                    return known.Identity;
            }

            if (!string.IsNullOrWhiteSpace(entry.PackageName))
            {
                string package = FriendlyPackageName(entry.PackageName);
                return new SoftwareIdentity("package:" + NormalizeKey(entry.PackageName),
                    package, MakeAbbreviation(package));
            }

            string path = ExtractProgramPath(entry.IconHint);
            if (string.IsNullOrWhiteSpace(path)) path = ExtractProgramPath(entry.Command);
            if (string.IsNullOrWhiteSpace(path)) path = ExtractProgramPath(entry.FilePath);
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (IsWindowsPath(path))
                    return new SoftwareIdentity("windows", "Windows 系统", "WIN");

                string product = ProductName(path);
                if (!string.IsNullOrWhiteSpace(product))
                {
                    if (string.Equals(product, "Windows 系统",
                        StringComparison.OrdinalIgnoreCase))
                        return new SoftwareIdentity("windows", "Windows 系统", "WIN");
                    return new SoftwareIdentity("product:" + NormalizeKey(product),
                        product, MakeAbbreviation(product));
                }

                string folder = ProductFolder(path);
                if (!string.IsNullOrWhiteSpace(folder))
                    return new SoftwareIdentity("folder:" + NormalizeKey(folder),
                        folder, MakeAbbreviation(folder));
            }

            if (entry.IsMicrosoft)
                return new SoftwareIdentity("windows", "Windows 系统", "WIN");
            return null;
        }

        public static string ControlKey(MenuEntry entry)
        {
            if (entry == null) return "";
            if ((entry.Kind == EntryKind.ContextHandler ||
                 entry.Kind == EntryKind.ModernVerb) &&
                !string.IsNullOrWhiteSpace(entry.Clsid))
                return "handler:" + entry.Clsid.Trim().ToUpperInvariant();
            if (entry.Kind == EntryKind.ShellNew)
                return "shellnew:" + NormalizeKey(entry.Name);
            if (!string.IsNullOrWhiteSpace(entry.Command))
                return "command:" + NormalizeCommand(entry.Command) +
                       "|name:" + NormalizeKey(entry.Name);
            if (!string.IsNullOrWhiteSpace(entry.RegistryPath))
                return "registry:" + entry.Scope + ":" + entry.RegistryPath;
            if (!string.IsNullOrWhiteSpace(entry.FilePath))
                return "file:" + entry.FilePath;
            return "entry:" + entry.Id;
        }

        private static bool HasUsefulIcon(MenuEntry entry)
        {
            string hint = entry == null ? "" : entry.IconHint ?? "";
            return !string.IsNullOrWhiteSpace(hint) &&
                   !hint.StartsWith("ext:", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractProgramPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.StartsWith("ext:", StringComparison.OrdinalIgnoreCase)) return "";
            string expanded = Environment.ExpandEnvironmentVariables(value);
            Match match = Regex.Match(expanded,
                @"(?<path>[A-Za-z]:\\[^""<>|?*\r\n]+?\.(?:exe|dll|ico|png))",
                RegexOptions.IgnoreCase);
            if (!match.Success) return "";
            return match.Groups["path"].Value.Trim().Trim('"');
        }

        private static bool IsWindowsPath(string path)
        {
            string windows = Environment.GetFolderPath(
                Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
            return path.StartsWith(windows, StringComparison.OrdinalIgnoreCase);
        }

        private static string ProductName(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                string name = !string.IsNullOrWhiteSpace(info.ProductName)
                    ? info.ProductName : info.FileDescription;
                name = CleanProductName(name);
                if (IsGenericProductName(name))
                    name = CleanProductName(info.CompanyName);
                if (name.IndexOf("Microsoft Windows", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Windows 系统";
                return name;
            }
            catch { return ""; }
        }

        private static string CleanProductName(string value)
        {
            value = (value ?? "").Trim();
            string[] suffixes = new[]
            {
                " Shell Extension", " Context Menu", " Extension",
                " (R)", "®", "™"
            };
            foreach (string suffix in suffixes)
                value = value.Replace(suffix, "");
            Match version = Regex.Match(value, @"\s+v?\d+(?:\.\d+){1,3}$",
                RegexOptions.IgnoreCase);
            if (version.Success) value = value.Substring(0, version.Index);
            return value.Trim();
        }

        private static string ProductFolder(string path)
        {
            try
            {
                DirectoryInfo directory = new FileInfo(path).Directory;
                while (directory != null)
                {
                    string name = directory.Name;
                    if (!string.IsNullOrWhiteSpace(name) &&
                        !string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "data", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "Program Files", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "Program Files (x86)", StringComparison.OrdinalIgnoreCase))
                        return name;
                    directory = directory.Parent;
                }
            }
            catch { }
            return "";
        }

        private static bool IsGenericProductName(string value)
        {
            string name = (value ?? "").Trim();
            if (name.Length <= 2) return true;
            string[] generic = new[]
            {
                "UI", "win64", "win32", "x64", "x86", "application",
                "shell extension", "context menu"
            };
            return generic.Any(item =>
                string.Equals(name, item, StringComparison.OrdinalIgnoreCase));
        }

        private static string FriendlyPackageName(string packageName)
        {
            string value = packageName ?? "";
            int underscore = value.IndexOf('_');
            if (underscore > 0) value = value.Substring(0, underscore);
            value = Regex.Replace(value, @"(?i)(shellext|shellextension|contextmenu)\d*$", "");
            value = value.Replace("_", " ").Trim(' ', '.', '-');
            return string.IsNullOrWhiteSpace(value) ? "现代应用" : value;
        }

        private static string NormalizeKey(string value)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char character in (value ?? "").ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character)) builder.Append(character);
            }
            return builder.Length == 0 ? "software" : builder.ToString();
        }

        private static string NormalizeCommand(string value)
        {
            string normalized = Environment.ExpandEnvironmentVariables(
                value ?? "").Trim().ToLowerInvariant();
            normalized = normalized.Replace("\"", "");
            normalized = Regex.Replace(normalized, @"\s+", " ");
            return normalized;
        }

        private static string MakeAbbreviation(string name)
        {
            string value = (name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value)) return "APP";
            List<string> words = Regex.Matches(value, @"[A-Za-z0-9]+")
                .Cast<Match>().Select(match => match.Value).ToList();
            if (words.Count > 1)
                return string.Concat(words.Take(3).Select(word =>
                    char.ToUpperInvariant(word[0]).ToString()).ToArray());
            if (words.Count == 1)
            {
                string word = words[0].ToUpperInvariant();
                return word.Length <= 3 ? word : word.Substring(0, 2);
            }
            return value.Length <= 2 ? value : value.Substring(0, 2);
        }
    }
}
