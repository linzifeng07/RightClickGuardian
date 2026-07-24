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

            Stopwatch realScanWatch = Stopwatch.StartNew();
            ScanResult realScan = new MenuScanner().Scan(new PolicyStore().Load());
            realScanWatch.Stop();
            entriesField.SetValue(window, realScan.Entries);
            Stopwatch realRenderWatch = Stopwatch.StartNew();
            render.Invoke(window, null);
            realRenderWatch.Stop();
            int realInitialElements = panel.Children.Count;
            Console.WriteLine("realEntries=" + realScan.Entries.Count);
            Console.WriteLine("realScanMilliseconds=" + realScanWatch.ElapsedMilliseconds);
            Console.WriteLine("realInitialRenderMilliseconds=" +
                              realRenderWatch.ElapsedMilliseconds);
            Console.WriteLine("realInitialVisualElements=" + realInitialElements);

            if (initialElements > 50 || initialElements < 48 ||
                afterAppendElements > 86 || watch.ElapsedMilliseconds > 1500 ||
                realInitialElements > 50 || realRenderWatch.ElapsedMilliseconds > 1500)
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
