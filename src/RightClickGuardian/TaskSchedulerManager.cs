using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace RightClickGuardian
{
    public static class TaskSchedulerManager
    {
        private const string FolderName = "RightClickGuardian";
        private const string TaskName = "Guard";

        public static bool IsInstalled()
        {
            try
            {
                dynamic service = CreateService();
                dynamic folder = service.GetFolder("\\" + FolderName);
                dynamic task = folder.GetTask(TaskName);
                return task != null && task.Enabled;
            }
            catch { return false; }
        }

        public static void StopOtherVersionGuards()
        {
            Process current = Process.GetCurrentProcess();
            string currentPath = current.MainModule.FileName;
            string processName = Path.GetFileNameWithoutExtension(currentPath);
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                if (process.Id == current.Id || process.MainWindowHandle != IntPtr.Zero) continue;
                try
                {
                    string otherPath = process.MainModule.FileName;
                    string product = process.MainModule.FileVersionInfo.ProductName;
                    if (string.Equals(otherPath, currentPath,
                        StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(product, "右键小守卫",
                        StringComparison.CurrentCultureIgnoreCase)) continue;
                    process.Kill();
                    process.WaitForExit(2500);
                }
                catch { }
            }
        }

        public static void Install()
        {
            dynamic service = CreateService();
            dynamic root = service.GetFolder("\\");
            dynamic folder;
            try { folder = root.GetFolder("\\" + FolderName); }
            catch { folder = root.CreateFolder(FolderName); }

            dynamic definition = service.NewTask(0);
            definition.RegistrationInfo.Description =
                "右键小守卫：持续压制已关闭的 Windows 右键菜单注册项";
            definition.Settings.Enabled = true;
            definition.Settings.Hidden = true;
            definition.Settings.AllowDemandStart = true;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = "PT0S";
            definition.Settings.MultipleInstances = 2;
            definition.Settings.RestartCount = 999;
            definition.Settings.RestartInterval = "PT1M";

            dynamic trigger = definition.Triggers.Create(9);
            trigger.Enabled = true;
            trigger.UserId = WindowsIdentity.GetCurrent().Name;

            dynamic action = definition.Actions.Create(0);
            action.Path = Process.GetCurrentProcess().MainModule.FileName;
            action.Arguments = "--guard";
            action.WorkingDirectory = Path.GetDirectoryName(action.Path);

            folder.RegisterTaskDefinition(TaskName, definition, 6,
                null, null, 3, null);
        }

        public static void Uninstall()
        {
            try
            {
                dynamic service = CreateService();
                dynamic root = service.GetFolder("\\");
                dynamic folder = root.GetFolder("\\" + FolderName);
                folder.DeleteTask(TaskName, 0);
                try { root.DeleteFolder(FolderName, 0); } catch { }
            }
            catch { }
        }

        public static void StartGuardNow()
        {
            if (Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName))
                .Length > 1) return;
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = Process.GetCurrentProcess().MainModule.FileName;
            info.Arguments = "--guard";
            info.UseShellExecute = true;
            Process.Start(info);
        }

        private static dynamic CreateService()
        {
            Type type = Type.GetTypeFromProgID("Schedule.Service");
            if (type == null) throw new InvalidOperationException("任务计划程序不可用。");
            dynamic service = Activator.CreateInstance(type);
            service.Connect();
            return service;
        }
    }
}
