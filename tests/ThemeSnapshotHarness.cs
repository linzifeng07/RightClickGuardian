using RightClickGuardian;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class ThemeSnapshotHarness
{
    [STAThread]
    private static int Main()
    {
        try { return RenderPreview(); }
        catch (Exception ex)
        {
            return Fail(ex.GetType().FullName + ": " + ex.Message);
        }
    }

    private static int RenderPreview()
    {
        string data = Path.Combine(Path.GetTempPath(),
            "RightClickGuardian-ThemeSnapshot");
        Environment.SetEnvironmentVariable(
            "RIGHT_CLICK_GUARDIAN_DATA_DIR", data);
        Application application = new Application();
        MainWindow window = new MainWindow();
        FrameworkElement root = window.Content as FrameworkElement;
        if (root == null) return Fail("The main UI root is missing.");

        const int width = 1180;
        const int height = 780;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();

        RenderTargetBitmap rendered = new RenderTargetBitmap(
            width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(root);
        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        string folder = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location);
        string output = Path.Combine(folder, "theme-preview.png");
        using (FileStream stream = new FileStream(
            output, FileMode.Create, FileAccess.Write))
            encoder.Save(stream);

        if (!File.Exists(output) || new FileInfo(output).Length < 30000)
            return Fail("The rendered theme preview is unexpectedly empty.");
        Console.WriteLine("PASS: off-screen theme preview rendered to " + output);
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("FAIL: " + message);
        return 1;
    }
}
