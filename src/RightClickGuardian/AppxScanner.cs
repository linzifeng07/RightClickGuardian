using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace RightClickGuardian
{
    public sealed class AppxScanner
    {
        public List<MenuEntry> Scan(PolicyDocument policy, List<string> warnings)
        {
            List<MenuEntry> entries = new List<MenuEntry>();
            string windowsApps = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            if (!Directory.Exists(windowsApps)) return entries;

            IEnumerable<string> manifests = GetManifestPaths(windowsApps, warnings);

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string manifest in manifests)
            {
                try
                {
                    XDocument document = XDocument.Load(manifest, LoadOptions.None);
                    XElement identity = document.Descendants()
                        .FirstOrDefault(node => node.Name.LocalName == "Identity");
                    string packageName = identity == null
                        ? new DirectoryInfo(Path.GetDirectoryName(manifest)).Name
                        : (string)identity.Attribute("Name");
                    if (string.IsNullOrWhiteSpace(packageName))
                        packageName = new DirectoryInfo(Path.GetDirectoryName(manifest)).Name;
                    string packageIcon = ResolvePackageLogo(document, manifest);

                    IEnumerable<XElement> extensions = document.Descendants()
                        .Where(node => node.Name.LocalName == "Extension" &&
                               string.Equals((string)node.Attribute("Category"),
                                   "windows.fileExplorerContextMenus",
                                   StringComparison.OrdinalIgnoreCase));
                    foreach (XElement extension in extensions)
                    {
                        foreach (XElement verb in extension.Descendants()
                            .Where(node => node.Name.LocalName == "Verb"))
                        {
                            string clsid = NormalizeClsid((string)verb.Attribute("Clsid"));
                            string verbId = (string)verb.Attribute("Id");
                            if (string.IsNullOrWhiteSpace(clsid)) continue;
                            string dedupe = packageName + "|" + verbId + "|" + clsid;
                            if (!seen.Add(dedupe)) continue;

                            string itemTypes = string.Join(", ",
                                verb.Ancestors().Where(node => node.Name.LocalName == "ItemType")
                                    .Select(node => (string)node.Attribute("Type"))
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .Distinct().ToArray());
                            string id = HashUtil.StableId("Appx", packageName, verbId, clsid);
                            MenuEntry entry = new MenuEntry();
                            entry.Id = id;
                            entry.Name = FriendlyVerbName(packageName, verbId);
                            entry.Category = CategoryNames.ModernApps;
                            entry.Kind = EntryKind.ModernVerb;
                            entry.Scope = "APPX";
                            entry.Clsid = clsid;
                            entry.PackageName = packageName;
                            entry.VerbId = verbId;
                            entry.Source = "现代应用清单";
                            entry.Command = Path.GetDirectoryName(manifest);
                            entry.IconHint = packageIcon;
                            entry.Details = string.IsNullOrWhiteSpace(itemTypes)
                                ? "适用对象由应用定义"
                                : "适用：" + itemTypes;
                            entry.IsMicrosoft =
                                packageName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                                packageName.StartsWith("Clipchamp.", StringComparison.OrdinalIgnoreCase);
                            entry.IsCritical = false;
                            entry.Protected = policy.Rules.Any(rule =>
                                string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase));
                            entry.Enabled = !entry.Protected && !RegistryUtil.IsClsidBlocked(clsid);
                            entries.Add(entry);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Some retired packages are inaccessible even when elevated.
                }
                catch (Exception ex)
                {
                    if (warnings.Count < 20)
                        warnings.Add("应用清单解析失败：" + Path.GetFileName(Path.GetDirectoryName(manifest)) +
                                     " · " + ex.Message);
                }
            }
            return entries;
        }

        private static IEnumerable<string> GetManifestPaths(string windowsApps,
            List<string> warnings)
        {
            HashSet<string> manifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string path in Directory.EnumerateDirectories(windowsApps))
                {
                    string manifest = Path.Combine(path, "AppxManifest.xml");
                    if (File.Exists(manifest)) manifests.Add(manifest);
                }
            }
            catch
            {
                // WindowsApps commonly denies directory listing. The package API fallback below
                // still provides exact registered install locations.
            }

            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "powershell.exe";
                info.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
                    "\"[Console]::OutputEncoding=[Text.Encoding]::UTF8; " +
                    "Get-AppxPackage | ForEach-Object { $_.InstallLocation }\"";
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.StandardOutputEncoding = Encoding.UTF8;
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit(20000);
                    foreach (string line in output.Split(new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries))
                    {
                        string manifest = Path.Combine(line.Trim(), "AppxManifest.xml");
                        if (File.Exists(manifest)) manifests.Add(manifest);
                    }
                    if (process.ExitCode != 0 && warnings.Count < 20 &&
                        !string.IsNullOrWhiteSpace(error))
                    {
                        warnings.Add("现代应用注册列表读取不完整：" + error.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                if (manifests.Count == 0)
                    warnings.Add("现代应用目录读取失败：" + ex.Message);
            }
            return manifests.ToArray();
        }

        public List<string> FindMatchingClsids(string packageName, string verbId)
        {
            PolicyDocument empty = new PolicyDocument();
            List<string> warnings = new List<string>();
            return Scan(empty, warnings)
                .Where(entry =>
                    string.Equals(entry.PackageName, packageName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.VerbId, verbId, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Clsid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeClsid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            value = value.Trim();
            if (!value.StartsWith("{")) value = "{" + value;
            if (!value.EndsWith("}")) value += "}";
            return value.ToUpperInvariant();
        }

        private static string FriendlyPackageName(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName)) return "现代应用";
            Dictionary<string, string> known = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "Microsoft.Windows.Photos", "照片" },
                { "Microsoft.Paint", "Microsoft 画图" },
                { "Clipchamp.Clipchamp", "Clipchamp" },
                { "Microsoft.OneDriveSync", "OneDrive" },
                { "Microsoft.WindowsNotepad", "记事本" },
                { "Microsoft.ScreenSketch", "截图工具" }
            };
            string friendly;
            return known.TryGetValue(packageName, out friendly) ? friendly : packageName;
        }

        private static string FriendlyVerbName(string packageName, string verbId)
        {
            string app = FriendlyPackageName(packageName);
            string id = verbId ?? "";
            if (string.Equals(packageName, "Microsoft.Windows.Photos",
                StringComparison.OrdinalIgnoreCase))
            {
                if (id.IndexOf("Designer", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "使用 Microsoft Designer 创建";
                return "使用“照片”编辑";
            }
            if (string.Equals(packageName, "Microsoft.Paint",
                StringComparison.OrdinalIgnoreCase)) return "使用 Microsoft 画图进行编辑";
            if (string.Equals(packageName, "Clipchamp.Clipchamp",
                StringComparison.OrdinalIgnoreCase)) return "使用 Clipchamp 进行编辑";
            if (string.Equals(packageName, "Microsoft.OneDriveSync",
                StringComparison.OrdinalIgnoreCase))
            {
                return id.StartsWith("Command", StringComparison.OrdinalIgnoreCase)
                    ? "移动到 OneDrive（账户动态项）" : "OneDrive 云端操作";
            }
            if (string.Equals(packageName, "Microsoft.WindowsNotepad",
                StringComparison.OrdinalIgnoreCase)) return "在记事本中编辑";
            if (packageName.IndexOf("BandizipShellext",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return id.IndexOf("Extension", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "Bandizip · 目录空白处菜单"
                    : "Bandizip · 文件与文件夹菜单";
            }
            if (packageName.IndexOf("WindowsTerminal", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Windows 终端 · 在此处打开";
            if (packageName.IndexOf("AMDRadeon", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AMD Radeon Software";
            if (packageName.IndexOf("PythonManager", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Python · 使用 IDLE 编辑";
            return app + (string.IsNullOrWhiteSpace(id) ? "" : " · " + id);
        }

        private static string ResolvePackageLogo(XDocument document, string manifest)
        {
            try
            {
                string packageDirectory = Path.GetDirectoryName(manifest);
                string relative = document.Descendants()
                    .Where(node => node.Name.LocalName == "VisualElements")
                    .Select(node => (string)node.Attribute("Square44x44Logo"))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (string.IsNullOrWhiteSpace(relative))
                {
                    relative = document.Descendants()
                        .Where(node => node.Name.LocalName == "Logo")
                        .Select(node => node.Value)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                }
                if (string.IsNullOrWhiteSpace(relative)) return "";
                string exact = Path.Combine(packageDirectory,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(exact)) return exact;
                string directory = Path.GetDirectoryName(exact);
                string stem = Path.GetFileNameWithoutExtension(exact);
                string extension = Path.GetExtension(exact);
                if (Directory.Exists(directory))
                {
                    string variant = Directory.EnumerateFiles(directory,
                        stem + "*" + extension, SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(variant)) return variant;
                }
            }
            catch { }
            return "";
        }
    }
}
