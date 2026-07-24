using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RightClickGuardian
{
    public static class IconResolver
    {
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private static readonly object CacheSync = new object();
        private static readonly Dictionary<string, ImageSource> Cache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Missing =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string path, uint fileAttributes, out SHFILEINFO info,
            uint infoSize, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(
            string file, int index, IntPtr[] largeIcons, IntPtr[] smallIcons, uint iconCount);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        public static ImageSource Resolve(MenuEntry entry)
        {
            if (entry == null) return null;
            string hint = entry.IconHint ?? "";
            string normalizedHint = CleanPath(hint);
            string cacheKey = !string.IsNullOrWhiteSpace(normalizedHint)
                ? normalizedHint + "#" + ParseIconIndex(hint) :
                !string.IsNullOrWhiteSpace(entry.FilePath) ? entry.FilePath : "";
            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                lock (CacheSync)
                {
                    ImageSource cached;
                    if (Cache.TryGetValue(cacheKey, out cached)) return cached;
                    if (Missing.Contains(cacheKey)) return null;
                }
            }
            ImageSource resolved = null;
            try
            {
                if (hint.StartsWith("ext:", StringComparison.OrdinalIgnoreCase))
                    resolved = FromShell("sample" + hint.Substring(4), true);
                string imagePath = CleanPath(hint);
                if (resolved == null && IsBitmap(imagePath) && File.Exists(imagePath))
                    resolved = FromBitmap(imagePath);
                if (resolved == null && !string.IsNullOrWhiteSpace(imagePath) &&
                    File.Exists(imagePath))
                {
                    resolved = FromResource(imagePath, ParseIconIndex(hint));
                    if (resolved == null) resolved = FromShell(imagePath, false);
                }
                if (resolved == null && !string.IsNullOrWhiteSpace(entry.FilePath) &&
                    File.Exists(entry.FilePath))
                    resolved = FromShell(entry.FilePath, false);
            }
            catch { }
            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                lock (CacheSync)
                {
                    if (resolved == null) Missing.Add(cacheKey);
                    else Cache[cacheKey] = resolved;
                }
            }
            return resolved;
        }

        private static ImageSource FromBitmap(string path)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = 64;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static ImageSource FromResource(string path, int index)
        {
            IntPtr[] small = new IntPtr[1];
            uint count = ExtractIconEx(path, index, null, small, 1);
            if (count == 0 || small[0] == IntPtr.Zero) return null;
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                    small[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally { DestroyIcon(small[0]); }
        }

        private static ImageSource FromShell(string path, bool useAttributes)
        {
            SHFILEINFO info;
            uint flags = SHGFI_ICON | SHGFI_SMALLICON;
            if (useAttributes) flags |= SHGFI_USEFILEATTRIBUTES;
            IntPtr result = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, out info,
                (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally { DestroyIcon(info.hIcon); }
        }

        private static string CleanPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            value = Environment.ExpandEnvironmentVariables(value.Trim());
            if (value.StartsWith("@")) value = value.Substring(1);
            Match quoted = Regex.Match(value, "^\"([^\"]+)\"");
            if (quoted.Success) return quoted.Groups[1].Value;
            Match executable = Regex.Match(value,
                @"^(.+?\.(?:exe|dll|ico|png|jpg|jpeg|bmp))(?:,?-?\d+|\s|$)",
                RegexOptions.IgnoreCase);
            if (executable.Success) return executable.Groups[1].Value.Trim();
            int comma = value.LastIndexOf(',');
            if (comma > 2)
            {
                int ignored;
                if (int.TryParse(value.Substring(comma + 1).Trim(), out ignored))
                    return value.Substring(0, comma).Trim().Trim('"');
            }
            return value.Trim('"');
        }

        private static int ParseIconIndex(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            int comma = value.LastIndexOf(',');
            if (comma < 0) return 0;
            int index;
            return int.TryParse(value.Substring(comma + 1).Trim(), out index) ? index : 0;
        }

        private static bool IsBitmap(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase);
        }
    }
}
