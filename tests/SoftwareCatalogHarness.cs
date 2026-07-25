using RightClickGuardian;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class SoftwareCatalogHarness
{
    private static int Main()
    {
        List<MenuEntry> entries = new List<MenuEntry>
        {
            new MenuEntry
            {
                Id = "bandizip-modern",
                Name = "BandizipShellMenu",
                Kind = EntryKind.ModernVerb,
                PackageName = "BandizipShellext2",
                Clsid = "{0001DEAD-9BF7-4CFA-8A5C-DE8679340001}",
                IconHint = @"D:\Bandizip\Bandizip.x64.exe",
                Enabled = true
            },
            new MenuEntry
            {
                Id = "bandizip-handler-duplicate",
                Name = "Bandizip Context Menu",
                Kind = EntryKind.ContextHandler,
                Clsid = "{0001DEAD-9BF7-4CFA-8A5C-DE8679340001}",
                IconHint = @"D:\Bandizip\bdzshl.x64.dll",
                Enabled = true
            },
            new MenuEntry
            {
                Id = "bandizip-command",
                Name = "压缩为 ZIP",
                Kind = EntryKind.StaticVerb,
                Scope = "HKCU64",
                RegistryPath = @"Software\Classes\*\shell\BandizipZip",
                Command = @"""D:\Bandizip\Bandizip.x64.exe"" /zip ""%1""",
                Enabled = true
            },
            new MenuEntry
            {
                Id = "vscode",
                Name = "通过 Code 打开",
                Kind = EntryKind.StaticVerb,
                Command = @"""D:\Microsoft VS Code\Code.exe"" ""%1""",
                Enabled = true
            },
            new MenuEntry
            {
                Id = "windows",
                Name = "系统项目",
                Kind = EntryKind.StaticVerb,
                IsMicrosoft = true,
                Enabled = true
            },
            new MenuEntry
            {
                Id = "unknown",
                Name = "无法归属的项目",
                Kind = EntryKind.StaticVerb,
                Enabled = true
            }
        };

        List<SoftwareGroup> groups = SoftwareCatalog.Build(entries);
        SoftwareGroup bandizip = groups.FirstOrDefault(group =>
            group.Key == "bandizip");
        SoftwareGroup vscode = groups.FirstOrDefault(group =>
            group.Key == "vscode");
        SoftwareGroup windows = groups.FirstOrDefault(group =>
            group.Key == "windows");

        if (bandizip == null || bandizip.Name != "Bandizip" ||
            bandizip.Abbreviation != "BZ" || bandizip.Entries.Count != 2)
            return Fail("Bandizip grouping or handler deduplication failed.");
        if (vscode == null || vscode.Abbreviation != "VS")
            return Fail("Visual Studio Code grouping failed.");
        if (windows == null || windows.Name != "Windows 系统")
            return Fail("Microsoft fallback grouping failed.");
        if (groups.Any(group => string.IsNullOrWhiteSpace(group.Abbreviation)))
            return Fail("A software abbreviation is empty.");
        if (groups.SelectMany(group => group.Entries)
            .Any(entry => entry.Id == "unknown"))
            return Fail("Unowned technical entries leaked into the software zone.");

        Console.WriteLine("softwareGroups=" + groups.Count);
        Console.WriteLine("bandizipFunctions=" + bandizip.Entries.Count);
        Console.WriteLine("PASS: software grouping, abbreviations, and handler deduplication");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("FAIL: " + message);
        return 1;
    }
}
