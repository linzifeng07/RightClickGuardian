using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RightClickGuardian
{
    public sealed class LabSample
    {
        public string Label { get; set; }
        public string Extension { get; set; }
        public string Icon { get; set; }
        public bool IsFolder { get; set; }

        public LabSample(string label, string extension, string icon, bool isFolder)
        {
            Label = label;
            Extension = extension;
            Icon = icon;
            IsFolder = isFolder;
        }
    }

    public sealed class ContextMenuLabService
    {
        public static readonly string LabDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RightClickGuardian", "Lab");

        public List<LabSample> Samples { get; private set; }

        public ContextMenuLabService()
        {
            Samples = new List<LabSample>
            {
                new LabSample("图片 PNG", ".png", "🖼", false),
                new LabSample("照片 JPG", ".jpg", "🌄", false),
                new LabSample("视频 MP4", ".mp4", "🎬", false),
                new LabSample("音频 MP3", ".mp3", "🎵", false),
                new LabSample("文本 TXT", ".txt", "📄", false),
                new LabSample("压缩包 ZIP", ".zip", "🗜", false),
                new LabSample("Word 文档", ".docx", "📝", false),
                new LabSample("PDF 文档", ".pdf", "📕", false),
                new LabSample("快捷方式", ".lnk", "↗", false),
                new LabSample("文件夹", "", "📁", true)
            };
            Directory.CreateDirectory(LabDirectory);
        }

        public string EnsureSample(LabSample sample)
        {
            Directory.CreateDirectory(LabDirectory);
            if (sample.IsFolder)
            {
                string folder = Path.Combine(LabDirectory, "测试文件夹");
                Directory.CreateDirectory(folder);
                return folder;
            }
            string extension = NormalizeExtension(sample.Extension);
            string path = Path.Combine(LabDirectory, "右键测试" + extension);
            if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                CreateShortcut(path);
                return path;
            }
            if (!File.Exists(path)) File.WriteAllBytes(path, new byte[0]);
            return path;
        }

        public List<string> GetNativeVerbNames(LabSample sample)
        {
            string path = EnsureSample(sample);
            List<string> names = new List<string>();
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return names;
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic folder = shell.NameSpace(Path.GetDirectoryName(path));
            dynamic item = folder.ParseName(Path.GetFileName(path));
            dynamic verbs = item.Verbs();
            int count = verbs.Count;
            for (int index = 0; index < count; index++)
            {
                dynamic verb = verbs.Item(index);
                string name = Convert.ToString(verb.Name);
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (names.Count > 0 && names[names.Count - 1] != "") names.Add("");
                }
                else names.Add(name.Replace("&", "").Trim());
            }
            while (names.Count > 0 && names[names.Count - 1] == "") names.RemoveAt(names.Count - 1);
            return names;
        }

        public LabSample CreateCustom(string extension)
        {
            extension = NormalizeExtension(extension);
            return new LabSample("自定义 " + extension.ToUpperInvariant(),
                extension, "🧩", false);
        }

        public void OpenInExplorer(LabSample sample)
        {
            string path = EnsureSample(sample);
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "explorer.exe";
            info.Arguments = "/select,\"" + path + "\"";
            info.UseShellExecute = true;
            Process.Start(info);
        }

        public static bool LooksLikeSubmenu(string text)
        {
            string[] known = new[]
            {
                "打开方式", "发送到", "播放到设备", "授予访问权限", "包含到库中",
                "固定到快速访问", "使用以下方式共享"
            };
            foreach (string item in known)
            {
                if (text.IndexOf(item, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string NormalizeExtension(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0) return ".test";
            if (!value.StartsWith(".")) value = "." + value;
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), "");
            return value.Length > 16 ? value.Substring(0, 16) : value;
        }

        private static void CreateShortcut(string path)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                if (!File.Exists(path)) File.WriteAllBytes(path, new byte[0]);
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            shortcut.WorkingDirectory = Environment.SystemDirectory;
            shortcut.Description = "右键守护喵测试快捷方式";
            shortcut.Save();
        }
    }
}
