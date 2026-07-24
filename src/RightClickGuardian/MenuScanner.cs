using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RightClickGuardian
{
    public sealed class MenuScanner
    {
        private sealed class Location
        {
            public string Category;
            public string Path;
            public bool Handler;
            public string Label;

            public Location(string category, string path, bool handler, string label)
            {
                Category = category;
                Path = path;
                Handler = handler;
                Label = label;
            }
        }

        private static readonly Location[] Locations = new[]
        {
            new Location(CategoryNames.File, @"Software\Classes\*\shell", false, "所有文件"),
            new Location(CategoryNames.File, @"Software\Classes\*\shellex\ContextMenuHandlers", true, "所有文件扩展"),
            new Location(CategoryNames.Folder, @"Software\Classes\Folder\shell", false, "文件夹"),
            new Location(CategoryNames.Folder, @"Software\Classes\Folder\shellex\ContextMenuHandlers", true, "文件夹扩展"),
            new Location(CategoryNames.Directory, @"Software\Classes\Directory\shell", false, "目录"),
            new Location(CategoryNames.Directory, @"Software\Classes\Directory\shellex\ContextMenuHandlers", true, "目录扩展"),
            new Location(CategoryNames.DirectoryBackground, @"Software\Classes\Directory\Background\shell", false, "目录空白处"),
            new Location(CategoryNames.DirectoryBackground, @"Software\Classes\Directory\Background\shellex\ContextMenuHandlers", true, "目录空白扩展"),
            new Location(CategoryNames.DesktopBackground, @"Software\Classes\DesktopBackground\Shell", false, "桌面空白处"),
            new Location(CategoryNames.DesktopBackground, @"Software\Classes\DesktopBackground\ShellEx\ContextMenuHandlers", true, "桌面空白扩展"),
            new Location(CategoryNames.Drive, @"Software\Classes\Drive\shell", false, "磁盘分区"),
            new Location(CategoryNames.Drive, @"Software\Classes\Drive\shellex\ContextMenuHandlers", true, "磁盘扩展"),
            new Location(CategoryNames.AllObjects, @"Software\Classes\AllFilesystemObjects\shell", false, "所有文件系统对象"),
            new Location(CategoryNames.AllObjects, @"Software\Classes\AllFilesystemObjects\shellex\ContextMenuHandlers", true, "所有对象扩展"),
            new Location(CategoryNames.ThisPc, @"Software\Classes\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}\shell", false, "此电脑"),
            new Location(CategoryNames.ThisPc, @"Software\Classes\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}\shellex\ContextMenuHandlers", true, "此电脑扩展"),
            new Location(CategoryNames.RecycleBin, @"Software\Classes\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shell", false, "回收站"),
            new Location(CategoryNames.RecycleBin, @"Software\Classes\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shellex\ContextMenuHandlers", true, "回收站扩展"),
            new Location(CategoryNames.Library, @"Software\Classes\LibraryFolder\shell", false, "库"),
            new Location(CategoryNames.Library, @"Software\Classes\LibraryFolder\shellex\ContextMenuHandlers", true, "库扩展"),
            new Location(CategoryNames.Library, @"Software\Classes\LibraryFolder\Background\shell", false, "库空白处"),
            new Location(CategoryNames.CommandStore, @"Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell", false, "资源管理器命令仓库")
        };

        public ScanResult Scan(PolicyDocument policy)
        {
            ScanResult result = new ScanResult();
            string[] scopes = new[] { "HKCU64", "HKLM64", "HKCU32", "HKLM32" };
            foreach (string scope in scopes)
            {
                foreach (Location location in Locations)
                {
                    ScanLocation(scope, location, policy, result);
                }
                ScanClassSpecificMenus(scope, policy, result);
                ScanSystemFileAssociations(scope, policy, result);
                ScanNewMenu(scope, policy, result);
                ScanOpenWith(scope, policy, result);
            }
            ScanFileBacked(policy, result);
            try
            {
                result.Entries.AddRange(new AppxScanner().Scan(policy, result.Warnings));
            }
            catch (Exception ex)
            {
                result.Warnings.Add("现代应用扫描失败：" + ex.Message);
            }
            AddMissingProtectedEntries(policy, result);
            result.Entries = result.Entries
                .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(entry => Array.IndexOf(CategoryNames.Ordered, entry.Category))
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            result.CompletedAt = DateTime.Now;
            return result;
        }

        private static void ScanLocation(string scope, Location location,
            PolicyDocument policy, ScanResult result)
        {
            try
            {
                using (RegistryKey container = RegistryUtil.OpenPath(scope, location.Path, false))
                {
                    if (container == null) return;
                    foreach (string subName in RegistryUtil.SafeSubKeyNames(container))
                    {
                        using (RegistryKey subKey = container.OpenSubKey(subName))
                        {
                            if (subKey == null) continue;
                            if (location.Handler)
                                AddHandler(scope, location, subName, subKey, policy, result);
                            else
                                AddVerb(scope, location, subName, subKey, policy, result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddWarning(result, scope + " " + location.Label + "：" + ex.Message);
            }
        }

        private static void AddVerb(string scope, Location location, string subName,
            RegistryKey key, PolicyDocument policy, ScanResult result)
        {
            string path = location.Path + "\\" + subName;
            string command = RegistryUtil.ReadCommand(key);
            string name = RegistryUtil.ReadFriendlyVerbName(key, subName);
            string id = HashUtil.StableId("verb", scope, path);
            bool protectedRule = HasRule(policy, id);
            bool disabled = RegistryUtil.ValueExists(key, "LegacyDisable") ||
                            RegistryUtil.ValueExists(key, "ProgrammaticAccessOnly");
            MenuEntry entry = new MenuEntry();
            entry.Id = id;
            entry.Name = name;
            entry.Category = location.Category;
            entry.Kind = EntryKind.StaticVerb;
            entry.Scope = scope;
            entry.RegistryPath = path;
            entry.Command = command;
            entry.IconHint = RegistryUtil.ReadIconHint(key, command);
            entry.Source = location.Label + " · " + scope;
            entry.Enabled = !disabled && !protectedRule;
            entry.Protected = protectedRule;
            entry.IsMicrosoft = RegistryUtil.LooksMicrosoft(command) ||
                                scope.StartsWith("HKLM") && command.IndexOf("shell32", StringComparison.OrdinalIgnoreCase) >= 0;
            entry.IsCritical = IsCriticalVerb(subName, name, location.Category);
            entry.Details = string.IsNullOrWhiteSpace(command) ? path : command;
            result.Entries.Add(entry);
        }

        private static void AddHandler(string scope, Location location, string subName,
            RegistryKey key, PolicyDocument policy, ScanResult result)
        {
            string rawDefault = Convert.ToString(key.GetValue(""));
            string clsid = LooksLikeClsid(rawDefault) ? rawDefault :
                LooksLikeClsid(subName) ? subName : rawDefault;
            string resolved = RegistryUtil.ResolveClsidName(clsid);
            string name = !string.IsNullOrWhiteSpace(resolved) ? resolved :
                !string.IsNullOrWhiteSpace(rawDefault) && !LooksLikeClsid(rawDefault)
                    ? rawDefault.Trim() : subName.Trim();
            name = FriendlyHandlerName(clsid, name);
            string path = location.Path + "\\" + subName;
            string id = HashUtil.StableId("handler", scope, path, clsid);
            bool protectedRule = HasRule(policy, id);
            MenuEntry entry = new MenuEntry();
            entry.Id = id;
            entry.Name = name;
            entry.Category = location.Category;
            entry.Kind = EntryKind.ContextHandler;
            entry.Scope = scope;
            entry.RegistryPath = path;
            entry.Clsid = clsid;
            entry.IconHint = RegistryUtil.ResolveClsidServerPath(clsid);
            entry.Source = location.Label + " · 动态扩展";
            entry.Enabled = !protectedRule && !RegistryUtil.IsClsidBlocked(clsid);
            entry.Protected = protectedRule;
            entry.IsMicrosoft = name.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0;
            entry.IsCritical = name.IndexOf("Open With", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("SendTo", StringComparison.OrdinalIgnoreCase) >= 0;
            entry.Details = clsid + " · " + path;
            result.Entries.Add(entry);
        }

        private static void ScanClassSpecificMenus(string scope,
            PolicyDocument policy, ScanResult result)
        {
            const string classesPath = @"Software\Classes";
            HashSet<string> alreadyCovered = new HashSet<string>(
                new[]
                {
                    "*", "Folder", "Directory", "Drive", "AllFilesystemObjects",
                    "DesktopBackground", "LibraryFolder", "CLSID", "Applications",
                    "SystemFileAssociations"
                },
                StringComparer.OrdinalIgnoreCase);
            try
            {
                using (RegistryKey root = RegistryUtil.OpenPath(scope, classesPath, false))
                {
                    if (root == null) return;
                    foreach (string className in RegistryUtil.SafeSubKeyNames(root))
                    {
                        if (alreadyCovered.Contains(className)) continue;
                        using (RegistryKey classKey = root.OpenSubKey(className))
                        {
                            if (classKey == null) continue;
                            string[] children = RegistryUtil.SafeSubKeyNames(classKey);
                            bool hasShell = children.Any(name =>
                                string.Equals(name, "shell", StringComparison.OrdinalIgnoreCase));
                            bool hasHandlers = false;
                            using (RegistryKey shellEx = classKey.OpenSubKey("shellex"))
                            {
                                hasHandlers = RegistryUtil.SafeSubKeyNames(shellEx).Any(name =>
                                    string.Equals(name, "ContextMenuHandlers",
                                        StringComparison.OrdinalIgnoreCase));
                            }
                            if (!hasShell && !hasHandlers) continue;

                            string friendly = Convert.ToString(classKey.GetValue("FriendlyTypeName"));
                            if (string.IsNullOrWhiteSpace(friendly) || friendly.StartsWith("@"))
                                friendly = Convert.ToString(classKey.GetValue(""));
                            if (string.IsNullOrWhiteSpace(friendly) || LooksLikeClsid(friendly))
                                friendly = className;
                            string category = IsMediaClass(className, friendly)
                                ? CategoryNames.ImageMedia : CategoryNames.File;
                            string basePath = classesPath + "\\" + className;
                            if (hasShell)
                            {
                                ScanLocation(scope, new Location(category,
                                    basePath + "\\shell", false,
                                    "文件类型 " + friendly), policy, result);
                            }
                            if (hasHandlers)
                            {
                                ScanLocation(scope, new Location(category,
                                    basePath + "\\shellex\\ContextMenuHandlers", true,
                                    "文件类型扩展 " + friendly), policy, result);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddWarning(result, scope + " 专属文件菜单：" + ex.Message);
            }
        }

        private static bool IsMediaClass(string className, string friendly)
        {
            string combined = (className + " " + friendly).ToLowerInvariant();
            string[] mediaTokens = new[]
            {
                "image", "photo", "picture", "video", "audio", "media",
                "图片", "照片", "图像", "视频", "音频", "音乐"
            };
            if (mediaTokens.Any(token => combined.IndexOf(token,
                StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            string extension = className.StartsWith(".", StringComparison.Ordinal)
                ? className.ToLowerInvariant() : "";
            string[] mediaExtensions = new[]
            {
                ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".heic", ".tif", ".tiff",
                ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm",
                ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg"
            };
            return mediaExtensions.Contains(extension);
        }

        private static void ScanSystemFileAssociations(string scope,
            PolicyDocument policy, ScanResult result)
        {
            const string rootPath = @"Software\Classes\SystemFileAssociations";
            try
            {
                using (RegistryKey root = RegistryUtil.OpenPath(scope, rootPath, false))
                {
                    if (root == null) return;
                    foreach (string association in RegistryUtil.SafeSubKeyNames(root))
                    {
                        string basePath = rootPath + "\\" + association;
                        Location shell = new Location(CategoryNames.ImageMedia,
                            basePath + "\\shell", false, "文件类型 " + association);
                        Location handlers = new Location(CategoryNames.ImageMedia,
                            basePath + "\\shellex\\ContextMenuHandlers", true,
                            "文件类型扩展 " + association);
                        ScanLocation(scope, shell, policy, result);
                        ScanLocation(scope, handlers, policy, result);
                    }
                }
            }
            catch (Exception ex)
            {
                AddWarning(result, scope + " 文件类型：" + ex.Message);
            }
        }

        private static void ScanNewMenu(string scope, PolicyDocument policy, ScanResult result)
        {
            const string classesPath = @"Software\Classes";
            try
            {
                using (RegistryKey root = RegistryUtil.OpenPath(scope, classesPath, false))
                {
                    if (root == null) return;
                    foreach (string extension in RegistryUtil.SafeSubKeyNames(root))
                    {
                        if (!extension.StartsWith(".", StringComparison.Ordinal)) continue;
                        using (RegistryKey extensionKey = root.OpenSubKey(extension))
                        using (RegistryKey shellNew = extensionKey == null ? null : extensionKey.OpenSubKey("ShellNew"))
                        {
                            if (shellNew == null) continue;
                            string path = classesPath + "\\" + extension + "\\ShellNew";
                            string id = HashUtil.StableId("shellnew", scope, path);
                            bool protectedRule = HasRule(policy, id);
                            string typeName = Convert.ToString(extensionKey.GetValue(""));
                            MenuEntry entry = new MenuEntry();
                            entry.Id = id;
                            entry.Name = extension + (string.IsNullOrWhiteSpace(typeName) ? "" : " · " + typeName);
                            entry.Category = CategoryNames.NewMenu;
                            entry.Kind = EntryKind.ShellNew;
                            entry.Scope = scope;
                            entry.RegistryPath = path;
                            entry.Source = "新建菜单 · " + scope;
                            entry.Enabled = !protectedRule;
                            entry.Protected = protectedRule;
                            entry.IsMicrosoft = false;
                            entry.IsCritical = false;
                            entry.IconHint = "ext:" + extension;
                            entry.Details = DescribeShellNew(shellNew);
                            result.Entries.Add(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddWarning(result, scope + " 新建菜单：" + ex.Message);
            }
        }

        private static void ScanOpenWith(string scope, PolicyDocument policy, ScanResult result)
        {
            const string applicationsPath = @"Software\Classes\Applications";
            try
            {
                using (RegistryKey root = RegistryUtil.OpenPath(scope, applicationsPath, false))
                {
                    if (root == null) return;
                    foreach (string appName in RegistryUtil.SafeSubKeyNames(root))
                    {
                        using (RegistryKey appKey = root.OpenSubKey(appName))
                        {
                            if (appKey == null) continue;
                            using (RegistryKey commandKey = appKey.OpenSubKey(@"shell\open\command"))
                            {
                                if (commandKey == null) continue;
                                string path = applicationsPath + "\\" + appName;
                                string command = Convert.ToString(commandKey.GetValue(""));
                                string friendly = Convert.ToString(appKey.GetValue("FriendlyAppName"));
                                if (string.IsNullOrWhiteSpace(friendly)) friendly = Path.GetFileNameWithoutExtension(appName);
                                string id = HashUtil.StableId("openwith", scope, path);
                                bool protectedRule = HasRule(policy, id);
                                MenuEntry entry = new MenuEntry();
                                entry.Id = id;
                                entry.Name = friendly;
                                entry.Category = CategoryNames.OpenWith;
                                entry.Kind = EntryKind.OpenWithApplication;
                                entry.Scope = scope;
                                entry.RegistryPath = path;
                                entry.Command = command;
                                entry.IconHint = command;
                                entry.Source = "打开方式 · " + scope;
                                entry.Enabled = !protectedRule && !RegistryUtil.ValueExists(appKey, "NoOpenWith");
                                entry.Protected = protectedRule;
                                entry.IsMicrosoft = RegistryUtil.LooksMicrosoft(command);
                                entry.IsCritical = false;
                                entry.Details = command;
                                result.Entries.Add(entry);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddWarning(result, scope + " 打开方式：" + ex.Message);
            }
        }

        private static void ScanFileBacked(PolicyDocument policy, ScanResult result)
        {
            string sendTo = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            ScanFiles(sendTo, CategoryNames.SendTo, EntryKind.SendToFile, policy, result, false);
            string winX = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "WinX");
            ScanFiles(winX, CategoryNames.WinX, EntryKind.WinXFile, policy, result, true);
        }

        private static void ScanFiles(string root, string category, EntryKind kind,
            PolicyDocument policy, ScanResult result, bool recursive)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (string path in Directory.EnumerateFiles(root, "*", option))
                {
                    string id = HashUtil.StableId(kind.ToString(), path);
                    bool protectedRule = HasRule(policy, id);
                    MenuEntry entry = new MenuEntry();
                    entry.Id = id;
                    entry.Name = Path.GetFileNameWithoutExtension(path);
                    entry.Category = category;
                    entry.Kind = kind;
                    entry.Scope = "FILE";
                    entry.FilePath = path;
                    entry.IconHint = path;
                    entry.Source = category + "文件";
                    entry.Enabled = !protectedRule;
                    entry.Protected = protectedRule;
                    entry.IsCritical = category == CategoryNames.WinX;
                    entry.IsMicrosoft = path.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0;
                    entry.Details = path;
                    result.Entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                AddWarning(result, category + "：" + ex.Message);
            }
        }

        private static void AddMissingProtectedEntries(PolicyDocument policy, ScanResult result)
        {
            HashSet<string> existing = new HashSet<string>(
                result.Entries.Select(entry => entry.Id), StringComparer.OrdinalIgnoreCase);
            foreach (PolicyRule rule in policy.Rules)
            {
                if (existing.Contains(rule.Id)) continue;
                MenuEntry entry = new MenuEntry();
                entry.Id = rule.Id;
                entry.Name = rule.Name;
                entry.Category = rule.Category;
                entry.Kind = rule.Kind;
                entry.Scope = rule.Scope;
                entry.RegistryPath = rule.RegistryPath;
                entry.RegistryValueName = rule.RegistryValueName;
                entry.Clsid = rule.Clsid;
                entry.FilePath = rule.FilePath;
                entry.PackageName = rule.PackageName;
                entry.VerbId = rule.VerbId;
                entry.IconHint = rule.IconHint;
                entry.Source = "受保护策略";
                entry.Enabled = false;
                entry.Protected = true;
                entry.Details = "原项目已消失；守护仍会阻止它被软件重新写回";
                result.Entries.Add(entry);
            }
        }

        private static string DescribeShellNew(RegistryKey key)
        {
            List<string> values = new List<string>();
            foreach (string name in key.GetValueNames())
            {
                string display = string.IsNullOrEmpty(name) ? "(默认)" : name;
                values.Add(display + "=" + Convert.ToString(key.GetValue(name)));
            }
            return values.Count == 0 ? "空 ShellNew 注册" : string.Join("；", values.ToArray());
        }

        private static bool HasRule(PolicyDocument policy, string id)
        {
            return policy.Rules.Any(rule =>
                string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCriticalVerb(string keyName, string displayName, string category)
        {
            string combined = (keyName + " " + displayName).ToLowerInvariant();
            string[] critical = new[]
            {
                "open", "properties", "delete", "rename", "copy", "cut", "paste",
                "打开", "属性", "删除", "重命名", "复制", "剪切", "粘贴"
            };
            return critical.Any(token => combined == token || combined.StartsWith(token + " ")) ||
                   category == CategoryNames.WinX;
        }

        private static bool LooksLikeClsid(string value)
        {
            Guid guid;
            return !string.IsNullOrWhiteSpace(value) &&
                   Guid.TryParse(value.Trim(), out guid);
        }

        private static string FriendlyHandlerName(string clsid, string fallback)
        {
            Dictionary<string, string> known = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "{90AA3A4E-1CBA-4233-B8BB-535773D48449}", "固定到任务栏" },
                { "{A2A9545D-A0C2-42B4-9708-A0B2BADD77C8}", "固定到“开始”" },
                { "{09A47860-11B0-4DA5-AFA5-26D86198A780}", "使用 Microsoft Defender 扫描…" },
                { "{09799AFB-AD67-11D1-ABCD-00C04FC30936}", "打开方式" },
                { "{F81E9010-6EA4-11CE-A7FF-00AA003CA9F6}", "授予访问权限" },
                { "{F3D06E7C-1E45-4A26-847E-F9FCDEE59BE0}", "复制文件地址" },
                { "{E2BF9676-5F8F-435C-97EB-11607A5BEDF7}", "共享" },
                { "{7BA4C740-9E81-11CF-99D3-00AA004AE837}", "发送到" },
                { "{596AB062-B4D2-4215-9F74-E9109B0A8153}", "还原以前的版本" },
                { "{CB3D0F55-BC2C-4C1A-85ED-23ED75B5106B}", "OneDrive 菜单" }
            };
            string friendly;
            return known.TryGetValue((clsid ?? "").ToUpperInvariant(), out friendly)
                ? friendly : fallback;
        }

        private static void AddWarning(ScanResult result, string message)
        {
            if (result.Warnings.Count < 30) result.Warnings.Add(message);
        }
    }
}
