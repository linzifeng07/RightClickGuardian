using RightClickGuardian;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class ScanHarness
{
    private static int Main()
    {
        string data = Path.Combine(Path.GetTempPath(), "RightClickGuardian-ScanTest");
        Environment.SetEnvironmentVariable("RIGHT_CLICK_GUARDIAN_DATA_DIR", data);
        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            PolicyDocument policy = new PolicyStore().Load();
            ScanResult result = new MenuScanner().Scan(policy);
            watch.Stop();
            int rawNames = result.Entries.Count(entry =>
                IsRawIdentifier(entry.Name));
            int rawNamesLeakedByUi = 0;
            int technicalNamesLeakedByUi = 0;
            Type windowType = Type.GetType(
                "RightClickGuardian.MainWindow, RightClickGuardian", true);
            MethodInfo friendlyName = windowType.GetMethod("FriendlyDisplayName",
                BindingFlags.Static | BindingFlags.NonPublic);
            foreach (MenuEntry entry in result.Entries.Where(item =>
                IsRawIdentifier(item.Name)))
            {
                string displayed = Convert.ToString(friendlyName.Invoke(null,
                    new object[] { entry }));
                if (IsRawIdentifier(displayed)) rawNamesLeakedByUi++;
            }
            foreach (MenuEntry entry in result.Entries.Where(item =>
                LooksLikeTechnicalMenuName(item.Name)))
            {
                string displayed = Convert.ToString(friendlyName.Invoke(null,
                    new object[] { entry }));
                if (LooksLikeTechnicalMenuName(displayed)) technicalNamesLeakedByUi++;
            }
            int modern = result.Entries.Count(entry =>
                entry.Kind == EntryKind.ModernVerb);
            int classSpecific = result.Entries.Count(entry =>
                entry.Source.StartsWith("文件类型 ", StringComparison.Ordinal));
            Console.WriteLine("entries=" + result.Entries.Count);
            Console.WriteLine("modern=" + modern);
            Console.WriteLine("classSpecific=" + classSpecific);
            Console.WriteLine("rawVisibleNames=" + rawNames);
            Console.WriteLine("rawNamesLeakedByUi=" + rawNamesLeakedByUi);
            Console.WriteLine("technicalNamesLeakedByUi=" + technicalNamesLeakedByUi);
            Console.WriteLine("warnings=" + result.Warnings.Count);
            Console.WriteLine("milliseconds=" + watch.ElapsedMilliseconds);
            if (result.Entries.Count < 100 || classSpecific == 0 ||
                rawNamesLeakedByUi != 0 || technicalNamesLeakedByUi != 0 ||
                result.Warnings.Count != 0)
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

    private static bool IsRawIdentifier(string value)
    {
        Guid guid;
        if (Guid.TryParse((value ?? "").Trim('{', '}'), out guid)) return true;
        string text = value ?? "";
        return text.Length > 40 && text.All(character =>
            char.IsLetterOrDigit(character) || character == '-' ||
            character == '_' || character == '.' || character == '{' ||
            character == '}');
    }

    private static bool LooksLikeTechnicalMenuName(string value)
    {
        value = value ?? "";
        return value.IndexOf(@":\", StringComparison.Ordinal) >= 0 &&
               value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("shell context menu",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
