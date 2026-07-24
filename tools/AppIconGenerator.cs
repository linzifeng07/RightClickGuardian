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
        int[] sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> images = new List<byte[]>();
        foreach (int size in sizes)
        {
            using (Bitmap bitmap = DrawIcon(size))
            using (MemoryStream stream = new MemoryStream())
            {
                if (size == 256)
                    bitmap.Save(Path.ChangeExtension(output, ".preview.png"), ImageFormat.Png);
                bitmap.Save(stream, ImageFormat.Png);
                images.Add(stream.ToArray());
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

    private static Bitmap DrawIcon(int size)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            RectangleF bounds = new RectangleF(0, 0, size - 1, size - 1);
            using (GraphicsPath rounded = Rounded(bounds, size * 0.23f))
            using (LinearGradientBrush gradient = new LinearGradientBrush(
                bounds, Color.FromArgb(255, 106, 92, 247),
                Color.FromArgb(255, 244, 146, 199), 35f))
            {
                graphics.FillPath(gradient, rounded);
            }

            float scale = size / 256f;
            PointF[] face = new[]
            {
                P(57, 104, scale), P(63, 52, scale), P(104, 78, scale),
                P(128, 70, scale), P(152, 78, scale), P(193, 52, scale),
                P(199, 104, scale), P(195, 169, scale), P(171, 195, scale),
                P(85, 195, scale), P(61, 169, scale)
            };
            using (GraphicsPath cat = new GraphicsPath())
            using (SolidBrush faceBrush = new SolidBrush(Color.FromArgb(247, 250, 255)))
            using (Pen outline = new Pen(Color.FromArgb(255, 50, 39, 83),
                Math.Max(1.3f, 7f * scale)))
            {
                cat.AddClosedCurve(face, 0.12f);
                graphics.FillPath(faceBrush, cat);
                graphics.DrawPath(outline, cat);
            }

            using (SolidBrush eye = new SolidBrush(Color.FromArgb(255, 50, 39, 83)))
            {
                graphics.FillEllipse(eye, R(92, 121, 13, 18, scale));
                graphics.FillEllipse(eye, R(151, 121, 13, 18, scale));
                graphics.FillEllipse(eye, R(123, 143, 10, 8, scale));
            }
            using (Pen mouth = new Pen(Color.FromArgb(255, 50, 39, 83),
                Math.Max(1f, 5f * scale)))
            {
                graphics.DrawArc(mouth, R(108, 143, 20, 22, scale), 5, 80);
                graphics.DrawArc(mouth, R(128, 143, 20, 22, scale), 95, 80);
            }

            RectangleF badge = R(159, 159, 76, 76, scale);
            using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(255, 85, 205, 169)))
            using (Pen badgeEdge = new Pen(Color.White, Math.Max(1f, 5f * scale)))
            {
                graphics.FillEllipse(badgeBrush, badge);
                graphics.DrawEllipse(badgeEdge, badge);
            }
            using (Pen check = new Pen(Color.White, Math.Max(1.5f, 10f * scale)))
            {
                check.StartCap = LineCap.Round;
                check.EndCap = LineCap.Round;
                graphics.DrawLines(check, new[]
                {
                    P(179, 197, scale), P(192, 211, scale), P(218, 181, scale)
                });
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

    private static PointF P(float x, float y, float scale)
    {
        return new PointF(x * scale, y * scale);
    }

    private static RectangleF R(float x, float y, float width, float height, float scale)
    {
        return new RectangleF(x * scale, y * scale, width * scale, height * scale);
    }
}
