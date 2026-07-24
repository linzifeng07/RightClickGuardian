using Microsoft.Win32;
using RightClickGuardian;
using System;
using System.IO;

internal static class IntegrationHarness
{
    private const string TestVerbPath =
        @"Software\Classes\RightClickGuardian.IntegrationTest\shell\guardian-test";
    private const string TestClsid = "{72B98989-5787-4D87-A7C7-41FFB7E62026}";

    [STAThread]
    private static int Main()
    {
        string data = Path.Combine(Path.GetTempPath(), "RightClickGuardian-IntegrationTest");
        Environment.SetEnvironmentVariable("RIGHT_CLICK_GUARDIAN_DATA_DIR", data);

        try
        {
            TestStaticVerbGuard();
            TestAdoptAlreadyDisabledVerb();
            TestHandlerGuard();
            TestLabSamples();
            Console.WriteLine("PASS: disable, tamper, enforce, restore, lab samples");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
        finally
        {
            CleanupRegistry();
            try
            {
                if (Directory.Exists(data)) Directory.Delete(data, true);
            }
            catch { }
        }
    }

    private static void TestStaticVerbGuard()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(TestVerbPath))
        {
            key.SetValue("", "右键守护喵集成测试");
        }

        PolicyStore store = new PolicyStore();
        EnforcementService service = new EnforcementService(store);
        MenuEntry entry = new MenuEntry
        {
            Id = "integration-static-verb",
            Name = "右键守护喵集成测试",
            Kind = EntryKind.StaticVerb,
            Scope = "HKCU64",
            RegistryPath = TestVerbPath
        };

        service.Disable(entry);
        AssertValue(TestVerbPath, "LegacyDisable", true, "静态菜单没有被关闭");
        AssertValue(TestVerbPath, "ProgrammaticAccessOnly", true, "静态菜单缺少双重关闭标记");

        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(TestVerbPath, true))
        {
            key.DeleteValue("LegacyDisable", false);
            key.DeleteValue("ProgrammaticAccessOnly", false);
        }
        service.EnforceAll();
        AssertValue(TestVerbPath, "LegacyDisable", true, "软件篡改后未被守卫重新关闭");
        AssertValue(TestVerbPath, "ProgrammaticAccessOnly", true, "第二个关闭标记未恢复");

        service.Enable(entry);
        AssertValue(TestVerbPath, "LegacyDisable", false, "恢复时遗留 LegacyDisable");
        AssertValue(TestVerbPath, "ProgrammaticAccessOnly", false, "恢复时遗留 ProgrammaticAccessOnly");
    }

    private static void TestAdoptAlreadyDisabledVerb()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(TestVerbPath, true))
        {
            key.SetValue("LegacyDisable", "");
        }
        PolicyStore store = new PolicyStore();
        EnforcementService service = new EnforcementService(store);
        MenuEntry entry = new MenuEntry
        {
            Id = "integration-adopt-disabled",
            Name = "右键守护喵接管测试",
            Kind = EntryKind.StaticVerb,
            Scope = "HKCU64",
            RegistryPath = TestVerbPath,
            Enabled = false
        };
        service.AdoptDisabled(entry);
        AssertValue(TestVerbPath, "LegacyDisable", true, "接管已禁用菜单失败");
        AssertValue(TestVerbPath, "ProgrammaticAccessOnly", true, "接管后未加双重标记");
        service.Enable(entry);
        AssertValue(TestVerbPath, "LegacyDisable", false, "接管项目恢复时仍被禁用");
        AssertValue(TestVerbPath, "ProgrammaticAccessOnly", false, "接管项目恢复不完整");
    }

    private static void TestHandlerGuard()
    {
        PolicyStore store = new PolicyStore();
        EnforcementService service = new EnforcementService(store);
        MenuEntry entry = new MenuEntry
        {
            Id = "integration-handler",
            Name = "右键守护喵处理器测试",
            Kind = EntryKind.ContextHandler,
            Scope = "HKCU64",
            Clsid = TestClsid
        };

        service.Disable(entry);
        AssertBlocked(true, "处理器没有进入阻止名单");
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
            RegistryUtil.UserBlockedPath, true))
        {
            key.DeleteValue(TestClsid, false);
        }
        service.EnforceAll();
        AssertBlocked(true, "处理器阻止名单被删除后没有自动恢复");
        service.Enable(entry);
        AssertBlocked(false, "恢复处理器时阻止项仍存在");
    }

    private static void TestLabSamples()
    {
        ContextMenuLabService lab = new ContextMenuLabService();
        LabSample shortcut = null;
        LabSample image = null;
        foreach (LabSample sample in lab.Samples)
        {
            if (sample.Extension == ".lnk") shortcut = sample;
            if (sample.Extension == ".png") image = sample;
        }
        if (shortcut == null || image == null) throw new Exception("实验室样本类型不完整");

        string shortcutPath = lab.EnsureSample(shortcut);
        if (!File.Exists(shortcutPath) || new FileInfo(shortcutPath).Length == 0)
            throw new Exception("快捷方式样本无效");

        if (lab.GetNativeVerbNames(image).Count == 0)
            throw new Exception("没有读取到图片的原生右键菜单");
        if (lab.GetNativeVerbNames(shortcut).Count == 0)
            throw new Exception("没有读取到快捷方式的原生右键菜单");
    }

    private static void AssertValue(string path, string name, bool expected, string message)
    {
        bool actual = false;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
        {
            if (key != null)
            {
                foreach (string valueName in key.GetValueNames())
                    if (string.Equals(valueName, name, StringComparison.OrdinalIgnoreCase))
                        actual = true;
            }
        }
        if (actual != expected) throw new Exception(message);
    }

    private static void AssertBlocked(bool expected, string message)
    {
        bool actual = RegistryUtil.BlockValueExists("HKCU64", TestClsid);
        if (actual != expected) throw new Exception(message);
    }

    private static void CleanupRegistry()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Classes\RightClickGuardian.IntegrationTest", false);
        }
        catch { }
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                RegistryUtil.UserBlockedPath, true))
            {
                if (key != null) key.DeleteValue(TestClsid, false);
            }
        }
        catch { }
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                RegistryUtil.MachineBlockedPath, true))
            {
                if (key != null) key.DeleteValue(TestClsid, false);
            }
        }
        catch { }
    }
}
