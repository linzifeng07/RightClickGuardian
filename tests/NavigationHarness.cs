using RightClickGuardian;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

internal static class NavigationHarness
{
    [STAThread]
    private static int Main()
    {
        string data = Path.Combine(Path.GetTempPath(),
            "RightClickGuardian-Navigation");
        Environment.SetEnvironmentVariable(
            "RIGHT_CLICK_GUARDIAN_DATA_DIR", data);
        try
        {
            MainWindow window = new MainWindow();
            Type type = typeof(MainWindow);
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo navigateTo = type.GetMethod("NavigateTo", flags);
            MethodInfo navigateBack = type.GetMethod("NavigateBack", flags);
            MethodInfo navigateForward = type.GetMethod("NavigateForward", flags);
            MethodInfo mouseHandler = type.GetMethod(
                "OnPreviewMouseButtonDown", flags);
            MethodInfo classifyTouchSwipe = type.GetMethod(
                "ClassifyTouchSwipe", BindingFlags.Static |
                BindingFlags.NonPublic);
            FieldInfo category = type.GetField("selectedCategory", flags);
            FieldInfo software = type.GetField("selectedSoftwareKey", flags);
            FieldInfo entries = type.GetField("allEntries", flags);
            FieldInfo listScroll = type.GetField("listScroll", flags);

            if (navigateTo == null || navigateBack == null ||
                navigateForward == null || mouseHandler == null ||
                classifyTouchSwipe == null || entries == null ||
                listScroll == null)
                return Fail("Navigation methods are missing.");

            ScrollViewer touchScroll =
                (ScrollViewer)listScroll.GetValue(window);
            if (touchScroll.PanningMode != PanningMode.VerticalOnly ||
                touchScroll.CanContentScroll ||
                touchScroll.IsDeferredScrollingEnabled ||
                Math.Abs(touchScroll.PanningRatio - 1.05) > 0.001 ||
                Math.Abs(touchScroll.PanningDeceleration - 0.0012) > 0.00001)
                return Fail("Touch inertia was not configured on the list.");

            if (Classify(classifyTouchSwipe, new Vector(110, 12), 420) != 1 ||
                Classify(classifyTouchSwipe, new Vector(-110, 12), 420) != -1 ||
                Classify(classifyTouchSwipe, new Vector(55, 3), 300) != 0 ||
                Classify(classifyTouchSwipe, new Vector(110, 90), 400) != 0 ||
                Classify(classifyTouchSwipe, new Vector(110, 8), 1800) != 0)
                return Fail("Touch swipe direction filtering is unsafe.");

            entries.SetValue(window, new List<MenuEntry>
            {
                new MenuEntry
                {
                    Id = "bandizip",
                    Name = "Bandizip",
                    Command = @"D:\Bandizip\Bandizip.exe",
                    Enabled = true
                },
                new MenuEntry
                {
                    Id = "potplayer",
                    Name = "PotPlayer",
                    Command = @"D:\PotPlayer\PotPlayerMini64.exe",
                    Enabled = true
                }
            });

            navigateTo.Invoke(window, new object[] { CategoryNames.Software, "" });
            navigateTo.Invoke(window, new object[]
                { CategoryNames.Software, "bandizip" });
            navigateTo.Invoke(window, new object[] { CategoryNames.Lab, "" });

            if (!(bool)navigateBack.Invoke(window, null) ||
                (string)category.GetValue(window) != CategoryNames.Software ||
                (string)software.GetValue(window) != "bandizip")
                return Fail("Back did not restore software details.");

            if (!(bool)navigateBack.Invoke(window, null) ||
                (string)category.GetValue(window) != CategoryNames.Software ||
                !string.IsNullOrEmpty((string)software.GetValue(window)))
                return Fail("Back did not restore the software list.");

            if (!(bool)navigateForward.Invoke(window, null) ||
                (string)software.GetValue(window) != "bandizip")
                return Fail("Forward did not restore software details.");

            navigateTo.Invoke(window, new object[] { CategoryNames.File, "" });
            if ((bool)navigateForward.Invoke(window, null))
                return Fail("A new navigation did not clear forward history.");

            navigateTo.Invoke(window, new object[] { CategoryNames.Software, "" });
            navigateTo.Invoke(window, new object[]
                { CategoryNames.Software, "potplayer" });

            MouseButtonEventArgs backButton = new MouseButtonEventArgs(
                Mouse.PrimaryDevice, Environment.TickCount, MouseButton.XButton1);
            backButton.RoutedEvent = Mouse.PreviewMouseDownEvent;
            mouseHandler.Invoke(window, new object[] { window, backButton });
            if (!backButton.Handled ||
                (string)category.GetValue(window) != CategoryNames.Software ||
                !string.IsNullOrEmpty((string)software.GetValue(window)))
                return Fail("Mouse XButton1 did not navigate back.");

            MouseButtonEventArgs forwardButton = new MouseButtonEventArgs(
                Mouse.PrimaryDevice, Environment.TickCount, MouseButton.XButton2);
            forwardButton.RoutedEvent = Mouse.PreviewMouseDownEvent;
            mouseHandler.Invoke(window, new object[] { window, forwardButton });
            if (!forwardButton.Handled ||
                (string)software.GetValue(window) != "potplayer")
                return Fail("Mouse XButton2 did not navigate forward.");

            Console.WriteLine(
                "PASS: mouse and touch back/forward navigation, inertia, and history");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail(ex.ToString());
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

    private static int Classify(MethodInfo method, Vector travel,
        double elapsedMilliseconds)
    {
        return (int)method.Invoke(null,
            new object[] { travel, elapsedMilliseconds });
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("FAIL: " + message);
        return 1;
    }
}
