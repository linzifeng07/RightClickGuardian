using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RightClickGuardian
{
    public static class RegistryUtil
    {
        public const string UserBlockedPath =
            @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
        public const string MachineBlockedPath =
            @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

        public static RegistryKey OpenRoot(string scope, bool writable)
        {
            bool isUser = scope != null &&
                          scope.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
            bool is32 = scope != null &&
                        scope.EndsWith("32", StringComparison.OrdinalIgnoreCase);
            return RegistryKey.OpenBaseKey(
                isUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine,
                is32 ? RegistryView.Registry32 : RegistryView.Registry64);
        }

        public static RegistryKey OpenPath(string scope, string relativePath, bool writable)
        {
            RegistryKey root = OpenRoot(scope, writable);
            RegistryKey key = root.OpenSubKey(relativePath, writable);
            root.Dispose();
            return key;
        }

        public static RegistryKey CreatePath(string scope, string relativePath)
        {
            RegistryKey root = OpenRoot(scope, true);
            RegistryKey key = root.CreateSubKey(relativePath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            root.Dispose();
            return key;
        }

        public static bool ValueExists(RegistryKey key, string name)
        {
            if (key == null) return false;
            foreach (string valueName in key.GetValueNames())
            {
                if (string.Equals(valueName, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool IsClsidBlocked(string clsid)
        {
            if (string.IsNullOrWhiteSpace(clsid)) return false;
            return BlockValueExists("HKCU", clsid) || BlockValueExists("HKLM", clsid);
        }

        public static bool BlockValueExists(string scope, string clsid)
        {
            try
            {
                bool isUser = scope != null &&
                              scope.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase);
                using (RegistryKey key = OpenPath(scope, isUser ? UserBlockedPath : MachineBlockedPath, false))
                {
                    return ValueExists(key, clsid);
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetClsidBlocked(string clsid, string label)
        {
            if (string.IsNullOrWhiteSpace(clsid)) return;
            using (RegistryKey user = CreatePath("HKCU", UserBlockedPath))
            {
                user.SetValue(clsid, label ?? "RightClickGuardian", RegistryValueKind.String);
            }
            try
            {
                using (RegistryKey machine = CreatePath("HKLM", MachineBlockedPath))
                {
                    machine.SetValue(clsid, label ?? "RightClickGuardian", RegistryValueKind.String);
                }
            }
            catch
            {
                // HKCU block is sufficient for the current desktop session.
            }
        }

        public static void RemoveClsidBlock(string clsid, bool removeUser, bool removeMachine)
        {
            if (string.IsNullOrWhiteSpace(clsid)) return;
            if (removeUser)
            {
                try
                {
                    using (RegistryKey user = OpenPath("HKCU", UserBlockedPath, true))
                    {
                        if (user != null) user.DeleteValue(clsid, false);
                    }
                }
                catch { }
            }
            if (removeMachine)
            {
                try
                {
                    using (RegistryKey machine = OpenPath("HKLM", MachineBlockedPath, true))
                    {
                        if (machine != null) machine.DeleteValue(clsid, false);
                    }
                }
                catch { }
            }
        }

        public static string ResolveClsidName(string clsid)
        {
            if (string.IsNullOrWhiteSpace(clsid)) return "";
            string[] scopes = new[] { "HKCU", "HKLM" };
            foreach (string scope in scopes)
            {
                try
                {
                    using (RegistryKey key = OpenPath(scope, @"Software\Classes\CLSID\" + clsid, false))
                    {
                        if (key == null) continue;
                        string display = Convert.ToString(key.GetValue(""));
                        if (!string.IsNullOrWhiteSpace(display)) return display;
                        using (RegistryKey inproc = key.OpenSubKey("InprocServer32"))
                        {
                            if (inproc != null)
                            {
                                string path = Environment.ExpandEnvironmentVariables(Convert.ToString(inproc.GetValue("")));
                                if (File.Exists(path))
                                {
                                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                                    if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription;
                                    if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName;
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return "";
        }

        public static string ResolveClsidServerPath(string clsid)
        {
            if (string.IsNullOrWhiteSpace(clsid)) return "";
            foreach (string scope in new[] { "HKCU64", "HKLM64", "HKCU32", "HKLM32" })
            {
                try
                {
                    using (RegistryKey key = OpenPath(scope,
                        @"Software\Classes\CLSID\" + clsid + @"\InprocServer32", false))
                    {
                        if (key == null) continue;
                        string path = Environment.ExpandEnvironmentVariables(
                            Convert.ToString(key.GetValue("")));
                        if (!string.IsNullOrWhiteSpace(path)) return path;
                    }
                    using (RegistryKey key = OpenPath(scope,
                        @"Software\Classes\CLSID\" + clsid + @"\LocalServer32", false))
                    {
                        if (key == null) continue;
                        string path = Environment.ExpandEnvironmentVariables(
                            Convert.ToString(key.GetValue("")));
                        if (!string.IsNullOrWhiteSpace(path)) return path;
                    }
                }
                catch { }
            }
            return "";
        }

        public static string ReadIconHint(RegistryKey key, string fallbackCommand)
        {
            if (key != null)
            {
                try
                {
                    string icon = Convert.ToString(key.GetValue("Icon"));
                    if (!string.IsNullOrWhiteSpace(icon)) return icon;
                }
                catch { }
            }
            return fallbackCommand ?? "";
        }

        public static string ReadCommand(RegistryKey verbKey)
        {
            if (verbKey == null) return "";
            try
            {
                using (RegistryKey command = verbKey.OpenSubKey("command"))
                {
                    if (command != null) return Convert.ToString(command.GetValue(""));
                }
                string handler = Convert.ToString(verbKey.GetValue("ExplorerCommandHandler"));
                if (!string.IsNullOrWhiteSpace(handler)) return "ExplorerCommandHandler " + handler;
                string delegateExecute = Convert.ToString(verbKey.GetValue("DelegateExecute"));
                if (!string.IsNullOrWhiteSpace(delegateExecute)) return "DelegateExecute " + delegateExecute;
            }
            catch { }
            return "";
        }

        public static string ReadFriendlyVerbName(RegistryKey verbKey, string fallback)
        {
            if (verbKey == null) return fallback;
            string[] valueNames = new[] { "MUIVerb", "", "LocalizedString" };
            foreach (string name in valueNames)
            {
                try
                {
                    string value = Convert.ToString(verbKey.GetValue(name));
                    if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("@"))
                        return value.Replace("&", "");
                }
                catch { }
            }
            return string.IsNullOrWhiteSpace(fallback) ? "未命名菜单" : fallback.Replace("&", "");
        }

        public static bool LooksMicrosoft(string commandOrPath)
        {
            if (string.IsNullOrWhiteSpace(commandOrPath)) return false;
            string expanded = Environment.ExpandEnvironmentVariables(commandOrPath).ToLowerInvariant();
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).ToLowerInvariant();
            return expanded.Contains(windows) ||
                   expanded.Contains("microsoft") ||
                   expanded.Contains(Path.Combine(programFiles, "windowsapps").ToLowerInvariant());
        }

        public static void CopyTree(RegistryKey source, RegistryKey destination)
        {
            foreach (string valueName in source.GetValueNames())
            {
                destination.SetValue(valueName, source.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                    source.GetValueKind(valueName));
            }
            foreach (string subName in source.GetSubKeyNames())
            {
                using (RegistryKey sourceSub = source.OpenSubKey(subName))
                using (RegistryKey destinationSub = destination.CreateSubKey(subName))
                {
                    CopyTree(sourceSub, destinationSub);
                }
            }
        }

        public static string[] SafeSubKeyNames(RegistryKey key)
        {
            try { return key == null ? new string[0] : key.GetSubKeyNames(); }
            catch { return new string[0]; }
        }
    }
}
