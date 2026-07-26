using RightClickGuardian;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;

internal static class ThemeAssetHarness
{
    private static int Main()
    {
        Assembly assembly = typeof(MainWindow).Assembly;
        const string name = "RightClickGuardian.ThemeMascot.png";
        using (Stream stream = assembly.GetManifestResourceStream(name))
        {
            if (stream == null)
                return Fail("The mascot image was not embedded.");
            using (Bitmap image = new Bitmap(stream))
            {
                if (image.Width < 512 || image.Height < 512 ||
                    image.Width != image.Height)
                    return Fail("The mascot source is not a high-resolution square.");

                Color center = image.GetPixel(
                    image.Width / 2, image.Height / 2);
                if (center.A < 220 || center.R < 90 ||
                    center.B < 80)
                    return Fail("The mascot image content is invalid.");
            }
        }

        Console.WriteLine(
            "PASS: embedded silver-haired pink mascot theme asset");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("FAIL: " + message);
        return 1;
    }
}
