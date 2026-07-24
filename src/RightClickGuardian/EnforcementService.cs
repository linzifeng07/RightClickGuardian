using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RightClickGuardian
{
    public sealed class EnforcementService
    {
        private readonly PolicyStore store;
        private readonly AppxScanner appxScanner;

        public EnforcementService(PolicyStore policyStore)
        {
            store = policyStore;
            appxScanner = new AppxScanner();
        }

        public PolicyRule Disable(MenuEntry entry)
        {
            PolicyDocument policy = store.Load();
            PolicyRule existing = policy.Rules.FirstOrDefault(item =>
                string.Equals(item.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                EnforceRule(existing, true);
                return existing;
            }

            PolicyRule rule = CreateRule(entry);
            switch (entry.Kind)
            {
                case EntryKind.StaticVerb:
                    DisableStaticVerb(rule);
                    break;
                case EntryKind.ContextHandler:
                case EntryKind.ModernVerb:
                    DisableHandler(rule);
                    break;
                case EntryKind.OpenWithApplication:
                    DisableOpenWith(rule);
                    break;
                case EntryKind.ShellNew:
                    DisableShellNew(rule);
                    break;
                case EntryKind.SendToFile:
                case EntryKind.WinXFile:
                    DisableFileItem(rule);
                    break;
                default:
                    throw new NotSupportedException("暂不支持此类型：" + entry.Kind);
            }
            policy.Rules.Add(rule);
            policy.GuardEnabled = true;
            store.Save(policy);
            NativeMethods.NotifyShellChanged();
            return rule;
        }

        public PolicyRule AdoptDisabled(MenuEntry entry)
        {
            EnableUnmanagedEntry(entry);
            entry.Enabled = true;
            return Disable(entry);
        }

        public void Enable(MenuEntry entry)
        {
            PolicyDocument policy = store.Load();
            PolicyRule rule = policy.Rules.FirstOrDefault(item =>
                string.Equals(item.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
            if (rule == null)
            {
                EnableUnmanagedEntry(entry);
                NativeMethods.NotifyShellChanged();
                return;
            }
            RestoreRule(rule, policy);
            policy.Rules.Remove(rule);
            store.Save(policy);
            NativeMethods.NotifyShellChanged();
        }

        public void EnforceAll()
        {
            EnforceAll(false);
        }

        public void EnforceAll(bool refreshModern)
        {
            PolicyDocument policy = store.Load();
            if (!policy.GuardEnabled) return;
            foreach (PolicyRule rule in policy.Rules.ToArray())
            {
                try { EnforceRule(rule, refreshModern); }
                catch { }
            }
        }

        public void SetGuardEnabled(bool enabled)
        {
            PolicyDocument policy = store.Load();
            policy.GuardEnabled = enabled;
            store.Save(policy);
            if (enabled) EnforceAll();
        }

        private static PolicyRule CreateRule(MenuEntry entry)
        {
            PolicyRule rule = new PolicyRule();
            rule.Id = entry.Id;
            rule.Name = entry.Name;
            rule.Category = entry.Category;
            rule.Kind = entry.Kind;
            rule.Scope = entry.Scope;
            rule.RegistryPath = entry.RegistryPath;
            rule.RegistryValueName = entry.RegistryValueName;
            rule.Clsid = entry.Clsid;
            rule.FilePath = entry.FilePath;
            rule.PackageName = entry.PackageName;
            rule.VerbId = entry.VerbId;
            rule.IconHint = entry.IconHint;
            rule.DisabledAtUtc = DateTime.UtcNow;
            return rule;
        }

        private static void DisableStaticVerb(PolicyRule rule)
        {
            using (RegistryKey key = RegistryUtil.OpenPath(rule.Scope, rule.RegistryPath, true))
            {
                if (key == null) throw new InvalidOperationException("菜单注册项已经不存在。");
                rule.LegacyDisableOriginallyPresent = RegistryUtil.ValueExists(key, "LegacyDisable");
                rule.ProgrammaticAccessOnlyOriginallyPresent =
                    RegistryUtil.ValueExists(key, "ProgrammaticAccessOnly");
                key.SetValue("LegacyDisable", "", RegistryValueKind.String);
                key.SetValue("ProgrammaticAccessOnly", "", RegistryValueKind.String);
            }
        }

        private static void DisableHandler(PolicyRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.Clsid))
                throw new InvalidOperationException("该扩展没有可识别的 CLSID。");
            rule.UserBlockOriginallyPresent = RegistryUtil.BlockValueExists("HKCU64", rule.Clsid);
            rule.MachineBlockOriginallyPresent = RegistryUtil.BlockValueExists("HKLM64", rule.Clsid);
            RegistryUtil.SetClsidBlocked(rule.Clsid, "RightClickGuardian · " + rule.Name);
        }

        private static void DisableOpenWith(PolicyRule rule)
        {
            using (RegistryKey key = RegistryUtil.OpenPath(rule.Scope, rule.RegistryPath, true))
            {
                if (key == null) throw new InvalidOperationException("打开方式注册项已经不存在。");
                rule.NoOpenWithOriginallyPresent = RegistryUtil.ValueExists(key, "NoOpenWith");
                key.SetValue("NoOpenWith", "", RegistryValueKind.String);
            }
        }

        private static void DisableShellNew(PolicyRule rule)
        {
            using (RegistryKey source = RegistryUtil.OpenPath(rule.Scope, rule.RegistryPath, false))
            {
                if (source == null) throw new InvalidOperationException("新建菜单注册项已经不存在。");
                string backupPath = @"Software\RightClickGuardian\Backups\" + rule.Id;
                using (RegistryKey backupRoot = RegistryUtil.OpenRoot("HKLM64", true))
                {
                    try { backupRoot.DeleteSubKeyTree(backupPath, false); } catch { }
                    using (RegistryKey backup = backupRoot.CreateSubKey(backupPath))
                    {
                        RegistryUtil.CopyTree(source, backup);
                    }
                }
                rule.BackupPath = backupPath;
            }
            using (RegistryKey parent = OpenParent(rule.Scope, rule.RegistryPath, true))
            {
                if (parent != null) parent.DeleteSubKeyTree(GetLeaf(rule.RegistryPath), false);
            }
        }

        private static void DisableFileItem(PolicyRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.FilePath) || !File.Exists(rule.FilePath))
                throw new FileNotFoundException("菜单文件已经不存在。", rule.FilePath);
            string extension = Path.GetExtension(rule.FilePath);
            string backup = Path.Combine(PolicyStore.QuarantineDirectory,
                rule.Id + extension + ".disabled");
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(rule.FilePath, backup);
            rule.BackupPath = backup;
        }

        private void EnforceRule(PolicyRule rule, bool refreshModern)
        {
            switch (rule.Kind)
            {
                case EntryKind.StaticVerb:
                    using (RegistryKey key = RegistryUtil.OpenPath(rule.Scope, rule.RegistryPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("LegacyDisable", "", RegistryValueKind.String);
                            key.SetValue("ProgrammaticAccessOnly", "", RegistryValueKind.String);
                        }
                    }
                    break;
                case EntryKind.ContextHandler:
                    RegistryUtil.SetClsidBlocked(rule.Clsid, "RightClickGuardian · " + rule.Name);
                    break;
                case EntryKind.ModernVerb:
                    RegistryUtil.SetClsidBlocked(rule.Clsid, "RightClickGuardian · " + rule.Name);
                    if (refreshModern && !string.IsNullOrWhiteSpace(rule.PackageName))
                    {
                        foreach (string clsid in appxScanner.FindMatchingClsids(
                            rule.PackageName, rule.VerbId))
                        {
                            RegistryUtil.SetClsidBlocked(clsid,
                                "RightClickGuardian · " + rule.PackageName);
                        }
                    }
                    break;
                case EntryKind.OpenWithApplication:
                    using (RegistryKey key = RegistryUtil.OpenPath(rule.Scope, rule.RegistryPath, true))
                    {
                        if (key != null) key.SetValue("NoOpenWith", "", RegistryValueKind.String);
                    }
                    break;
                case EntryKind.ShellNew:
                    using (RegistryKey parent = OpenParent(rule.Scope, rule.RegistryPath, true))
                    {
                        if (parent != null) parent.DeleteSubKeyTree(GetLeaf(rule.RegistryPath), false);
                    }
                    break;
                case EntryKind.SendToFile:
                case EntryKind.WinXFile:
                    if (File.Exists(rule.FilePath))
                    {
                        if (string.IsNullOrWhiteSpace(rule.BackupPath))
                            rule.BackupPath = Path.Combine(PolicyStore.QuarantineDirectory,
                                rule.Id + Path.GetExtension(rule.FilePath) + ".disabled");
                        if (!File.Exists(rule.BackupPath))
                            File.Move(rule.FilePath, rule.BackupPath);
                        else
                            File.Delete(rule.FilePath);
                    }
                    break;
            }
        }

        private void RestoreRule(PolicyRule rule, PolicyDocument policy)
        {
            switch (rule.Kind)
            {
                case EntryKind.StaticVerb:
                    using (RegistryKey key = RegistryUtil.OpenPath(rule.Scope, rule.RegistryPath, true))
                    {
                        if (key != null)
                        {
                            if (!rule.LegacyDisableOriginallyPresent)
                                key.DeleteValue("LegacyDisable", false);
                            if (!rule.ProgrammaticAccessOnlyOriginallyPresent)
                                key.DeleteValue("ProgrammaticAccessOnly", false);
                        }
                    }
                    break;
                case EntryKind.ContextHandler:
                case EntryKind.ModernVerb:
                    RestoreHandler(rule, policy);
                    break;
                case EntryKind.OpenWithApplication:
                    using (RegistryKey key = RegistryUtil.OpenPath(rule.Scope, rule.RegistryPath, true))
                    {
                        if (key != null && !rule.NoOpenWithOriginallyPresent)
                            key.DeleteValue("NoOpenWith", false);
                    }
                    break;
                case EntryKind.ShellNew:
                    RestoreShellNew(rule);
                    break;
                case EntryKind.SendToFile:
                case EntryKind.WinXFile:
                    RestoreFileItem(rule);
                    break;
            }
        }

        private void RestoreHandler(PolicyRule rule, PolicyDocument policy)
        {
            bool usedElsewhere = policy.Rules.Any(other =>
                !object.ReferenceEquals(other, rule) &&
                string.Equals(other.Clsid, rule.Clsid, StringComparison.OrdinalIgnoreCase));
            if (!usedElsewhere)
            {
                RegistryUtil.RemoveClsidBlock(rule.Clsid,
                    !rule.UserBlockOriginallyPresent, !rule.MachineBlockOriginallyPresent);
            }
            if (rule.Kind == EntryKind.ModernVerb && !string.IsNullOrWhiteSpace(rule.PackageName))
            {
                foreach (string clsid in appxScanner.FindMatchingClsids(rule.PackageName, rule.VerbId))
                {
                    if (string.Equals(clsid, rule.Clsid, StringComparison.OrdinalIgnoreCase)) continue;
                    bool currentUsed = policy.Rules.Any(other =>
                        !object.ReferenceEquals(other, rule) &&
                        string.Equals(other.Clsid, clsid, StringComparison.OrdinalIgnoreCase));
                    if (!currentUsed) RegistryUtil.RemoveClsidBlock(clsid, true, true);
                }
            }
        }

        private static void RestoreShellNew(PolicyRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.BackupPath)) return;
            using (RegistryKey backup = RegistryUtil.OpenPath("HKLM64", rule.BackupPath, false))
            {
                if (backup == null) return;
                using (RegistryKey destination = RegistryUtil.CreatePath(rule.Scope, rule.RegistryPath))
                {
                    RegistryUtil.CopyTree(backup, destination);
                }
            }
            using (RegistryKey root = RegistryUtil.OpenRoot("HKLM64", true))
            {
                root.DeleteSubKeyTree(rule.BackupPath, false);
            }
        }

        private static void RestoreFileItem(PolicyRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.BackupPath) || !File.Exists(rule.BackupPath)) return;
            string directory = Path.GetDirectoryName(rule.FilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            if (File.Exists(rule.FilePath))
            {
                string conflict = rule.FilePath + ".RightClickGuardian-conflict-" +
                                  DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Move(rule.FilePath, conflict);
            }
            File.Move(rule.BackupPath, rule.FilePath);
        }

        private static void EnableUnmanagedEntry(MenuEntry entry)
        {
            if (entry.Kind == EntryKind.StaticVerb)
            {
                using (RegistryKey key = RegistryUtil.OpenPath(entry.Scope, entry.RegistryPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue("LegacyDisable", false);
                        key.DeleteValue("ProgrammaticAccessOnly", false);
                    }
                }
            }
            else if (entry.Kind == EntryKind.ContextHandler || entry.Kind == EntryKind.ModernVerb)
            {
                RegistryUtil.RemoveClsidBlock(entry.Clsid, true, true);
            }
            else if (entry.Kind == EntryKind.OpenWithApplication)
            {
                using (RegistryKey key = RegistryUtil.OpenPath(entry.Scope, entry.RegistryPath, true))
                {
                    if (key != null) key.DeleteValue("NoOpenWith", false);
                }
            }
        }

        private static RegistryKey OpenParent(string scope, string path, bool writable)
        {
            int slash = path.LastIndexOf('\\');
            if (slash <= 0) return null;
            return RegistryUtil.OpenPath(scope, path.Substring(0, slash), writable);
        }

        private static string GetLeaf(string path)
        {
            int slash = path.LastIndexOf('\\');
            return slash < 0 ? path : path.Substring(slash + 1);
        }
    }
}
