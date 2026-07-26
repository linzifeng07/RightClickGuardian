using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RightClickGuardian
{
    internal static class ThemeAssets
    {
        private const string MascotResource =
            "RightClickGuardian.ThemeMascot.png";
        private static readonly ImageSource mascotPortrait =
            LoadImage(MascotResource);

        public static ImageSource MascotPortrait
        {
            get { return mascotPortrait; }
        }

        private static ImageSource LoadImage(string resourceName)
        {
            Assembly assembly = typeof(ThemeAssets).Assembly;
            using (Stream stream =
                assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
    }
}
