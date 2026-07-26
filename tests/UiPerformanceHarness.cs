using RightClickGuardian;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Controls;

internal static class UiPerformanceHarness
{
    [STAThread]
    private static int Main()
    {
        string data = Path.Combine(Path.GetTempPath(), "RightClickGuardian-UiPerformance");
        Environment.SetEnvironmentVariable("RIGHT_CLICK_GUARDIAN_DATA_DIR", data);
        try
        {
            MainWindow window = new MainWindow();
            Type type = typeof(MainWindow);
            FieldInfo entriesField = type.GetField("allEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo render = type.GetMethod("RenderEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo append = type.GetMethod("AppendNextEntryBatch",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo panelField = type.GetField("itemsPanel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo categoryField = type.GetField("selectedCategory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo softwareField = type.GetField("selectedSoftwareKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo softwareCacheField = type.GetField("softwareGroupsCache",
                BindingFlags.Instance | BindingFlags.NonPublic);

            List<MenuEntry> entries = new List<MenuEntry>();
            for (int index = 0; index < 6000; index++)
            {
                entries.Add(new MenuEntry
                {
                    Id = "performance-" + index,
                    Name = "测试右键菜单 " + index,
                    Category = index % 4 == 0 ? CategoryNames.ImageMedia : CategoryNames.File,
                    Source = "性能测试 · 静态命令",
                    Kind = EntryKind.StaticVerb,
                    IconHint = index % 12 == 0 ? "ext:.png" : "",
                    Command = index % 5 == 0 ? @"D:\Bandizip\Bandizip.x64.exe" :
                        index % 5 == 1 ? @"D:\Microsoft VS Code\Code.exe" :
                        index % 5 == 2 ? "OneDrive shell extension" :
                        index % 5 == 3 ? "Clipchamp context menu" :
                        "Microsoft Defender context handler",
                    Enabled = true
                });
            }
            entriesField.SetValue(window, entries);

            Stopwatch watch = Stopwatch.StartNew();
            render.Invoke(window, null);
            watch.Stop();
            StackPanel panel = (StackPanel)panelField.GetValue(window);
            int initialElements = panel.Children.Count;

            Stopwatch appendWatch = Stopwatch.StartNew();
            append.Invoke(window, new object[] { false });
            appendWatch.Stop();
            int afterAppendElements = panel.Children.Count;

            Console.WriteLine("initialMilliseconds=" + watch.ElapsedMilliseconds);
            Console.WriteLine("initialVisualElements=" + initialElements);
            Console.WriteLine("appendMilliseconds=" + appendWatch.ElapsedMilliseconds);
            Console.WriteLine("afterAppendVisualElements=" + afterAppendElements);

            categoryField.SetValue(window, CategoryNames.Software);
            softwareField.SetValue(window, "");
            Stopwatch softwareWatch = Stopwatch.StartNew();
            render.Invoke(window, null);
            softwareWatch.Stop();
            int softwareElements = panel.Children.Count;
            Console.WriteLine("softwareMilliseconds=" + softwareWatch.ElapsedMilliseconds);
            Console.WriteLine("softwareVisualElements=" + softwareElements);
            softwareField.SetValue(window, "bandizip");
            Stopwatch softwareDetailWatch = Stopwatch.StartNew();
            render.Invoke(window, null);
            softwareDetailWatch.Stop();
            Console.WriteLine("softwareDetailMilliseconds=" +
                              softwareDetailWatch.ElapsedMilliseconds);
            categoryField.SetValue(window, "");
            softwareField.SetValue(window, "");

            Stopwatch realScanWatch = Stopwatch.StartNew();
            ScanResult realScan = new MenuScanner().Scan(new PolicyStore().Load());
            realScanWatch.Stop();
            entriesField.SetValue(window, realScan.Entries);
            Stopwatch realGroupWatch = Stopwatch.StartNew();
            List<SoftwareGroup> realGroups =
                SoftwareCatalog.Build(realScan.Entries);
            realGroupWatch.Stop();
            softwareCacheField.SetValue(window, realGroups);
            Stopwatch realRenderWatch = Stopwatch.StartNew();
            render.Invoke(window, null);
            realRenderWatch.Stop();
            int realInitialElements = panel.Children.Count;
            Console.WriteLine("realEntries=" + realScan.Entries.Count);
            Console.WriteLine("realScanMilliseconds=" + realScanWatch.ElapsedMilliseconds);
            Console.WriteLine("realSoftwarePrebuildMilliseconds=" +
                              realGroupWatch.ElapsedMilliseconds);
            Console.WriteLine("realInitialRenderMilliseconds=" +
                              realRenderWatch.ElapsedMilliseconds);
            Console.WriteLine("realInitialVisualElements=" + realInitialElements);
            categoryField.SetValue(window, CategoryNames.Software);
            Stopwatch realSoftwareWatch = Stopwatch.StartNew();
            render.Invoke(window, null);
            realSoftwareWatch.Stop();
            Console.WriteLine("realSoftwareClickMilliseconds=" +
                              realSoftwareWatch.ElapsedMilliseconds);

            if (initialElements > 50 || initialElements < 48 ||
                afterAppendElements > 86 || watch.ElapsedMilliseconds > 1500 ||
                softwareElements > 2 || softwareElements == 0 ||
                softwareWatch.ElapsedMilliseconds > 1500 ||
                softwareDetailWatch.ElapsedMilliseconds > 500 ||
                realInitialElements > 50 || realRenderWatch.ElapsedMilliseconds > 1500)
                return 1;
            if (realSoftwareWatch.ElapsedMilliseconds > 800)
                return 1;
            return 0;
        }
        finally
        {
            try
            {
                if (Directory.Exists(data)) Directory.Delete(data, true);
            }
            catch { }
        }
    }
}
