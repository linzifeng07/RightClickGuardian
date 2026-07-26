using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class AppIconGenerator
{
    private static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "RightClickGuardian.ico";
        string sourcePath = args.Length > 1 ? args[1] : "ThemeMascot.png";
        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine("Mascot source image not found: " + sourcePath);
            return 1;
        }
        int[] sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> images = new List<byte[]>();
        using (Image source = Image.FromFile(sourcePath))
        {
            using (Bitmap mascot = DrawIcon(512, source))
                mascot.Save(Path.ChangeExtension(output, ".mascot.png"),
                    ImageFormat.Png);
            foreach (int size in sizes)
            {
                using (Bitmap bitmap = DrawIcon(size, source))
                using (MemoryStream stream = new MemoryStream())
                {
                    if (size == 256)
                        bitmap.Save(Path.ChangeExtension(output, ".preview.png"),
                            ImageFormat.Png);
                    bitmap.Save(stream, ImageFormat.Png);
                    images.Add(stream.ToArray());
                }
            }
        }

        using (FileStream file = new FileStream(output, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(file))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int index = 0; index < sizes.Length; index++)
            {
                int size = sizes[index];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(images[index].Length);
                writer.Write(offset);
                offset += images[index].Length;
            }
            foreach (byte[] image in images) writer.Write(image);
        }
        return 0;
    }

    private static Bitmap DrawIcon(int size, Image source)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            RectangleF bounds = new RectangleF(0.5f, 0.5f,
                size - 1f, size - 1f);
            using (GraphicsPath rounded = Rounded(bounds, size * 0.22f))
            {
                GraphicsState state = graphics.Save();
                graphics.SetClip(rounded);
                float crop = Math.Min(source.Width, source.Height) * 0.84f;
                RectangleF sourceRect = new RectangleF(
                    (source.Width - crop) / 2f,
                    Math.Max(0, (source.Height - crop) / 2f - crop * 0.025f),
                    crop, crop);
                graphics.DrawImage(source, bounds, sourceRect,
                    GraphicsUnit.Pixel);
                graphics.Restore(state);
                if (size >= 24)
                {
                    using (Pen edge = new Pen(
                        Color.FromArgb(170, 255, 231, 239),
                        Math.Max(1f, size / 96f)))
                        graphics.DrawPath(edge, rounded);
                }
            }
        }
        return bitmap;
    }

    private static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        float diameter = radius * 2f;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter,
            diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
