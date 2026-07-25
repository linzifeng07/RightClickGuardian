using RightClickGuardian;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class TrayVisualHarness
{
    private static int Main()
    {
        string data = Path.Combine(Path.GetTempPath(),
            "RightClickGuardian-TrayVisual");
        Environment.SetEnvironmentVariable(
            "RIGHT_CLICK_GUARDIAN_DATA_DIR", data);
        Type type = typeof(GuardContext);
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        MethodInfo loadIcon = type.GetMethod("LoadApplicationIcon", flags);
        MethodInfo buildBadge = type.GetMethod("BuildBadgeImage", flags);
        if (loadIcon == null || buildBadge == null)
            return Fail("Tray visual helpers are missing.");

        using (Icon icon = (Icon)loadIcon.Invoke(null, null))
        using (Bitmap bitmap = icon.ToBitmap())
        {
            if (bitmap.Width < 16 || bitmap.Height < 16)
                return Fail("Tray icon is too small.");
            int purplePixels = 0;
            int coloredPixels = 0;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    if (color.A == 0) continue;
                    if (color.R > 45 || color.G > 45 || color.B > 45)
                        coloredPixels++;
                    if (color.B > color.R + 25 &&
                        color.B > color.G + 35 && color.R > 70)
                        purplePixels++;
                }
            }
            if (coloredPixels < 40 || purplePixels < 8)
                return Fail("The application brand icon was not loaded.");
        }

        using (Bitmap badge = (Bitmap)buildBadge.Invoke(null,
            new object[] { Color.FromArgb(80, 197, 160), "✓" }))
        {
            if (badge.Width != 20 || badge.Height != 20 ||
                badge.GetPixel(10, 10).A == 0)
                return Fail("Tray menu badge rendering failed.");
        }

        GuardContext context = null;
        try
        {
            context = new GuardContext();
            FieldInfo trayField = type.GetField("tray",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo menuField = type.GetField("trayMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            NotifyIcon tray = (NotifyIcon)trayField.GetValue(context);
            ContextMenuStrip menu =
                (ContextMenuStrip)menuField.GetValue(context);
            if (tray.Icon == null ||
                tray.Text.IndexOf("右键小守卫",
                    StringComparison.OrdinalIgnoreCase) < 0 ||
                menu.Items.Count < 5)
                return Fail("The branded tray did not initialize.");
        }
        finally
        {
            if (context != null) context.Dispose();
            try
            {
                if (Directory.Exists(data)) Directory.Delete(data, true);
            }
            catch { }
        }

        Console.WriteLine(
            "PASS: branded tray icon, menu rendering, and initialization");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("FAIL: " + message);
        return 1;
    }
}
