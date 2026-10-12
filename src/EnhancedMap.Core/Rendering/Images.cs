using System;
using System.IO;
using SkiaSharp;

namespace EnhancedMap.Drawing
{
    public enum PixelFormat
    {
        Format32bppArgb,
        Format32bppPArgb,
        Format32bppRgb,
        Format24bppRgb,
        Format16bppRgb565,
        Format8bppIndexed
    }

    public enum ImageLockMode
    {
        ReadOnly,
        WriteOnly,
        ReadWrite,
        UserInputBuffer
    }

    public sealed class ColorPalette
    {
        public Color[] Entries { get; }

        public ColorPalette(int length)
        {
            Entries = new Color[length];
        }
    }

    public class BitmapData
    {
        public IntPtr Scan0 { get; internal set; }
        public int Stride { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public PixelFormat PixelFormat { get; internal set; }
    }

    public class Image : IDisposable
    {
        protected BitmapData _locked;

        internal SKBitmap Bitmap { get; set; }

        public int Width => Bitmap?.Width ?? 0;
        public int Height => Bitmap?.Height ?? 0;

        public virtual void Dispose()
        {
            Bitmap?.Dispose();
            Bitmap = null;
        }

        public static Bitmap FromFile(string path)
        {
            return new Bitmap(SKBitmap.Decode(path));
        }

        public static Bitmap FromStream(Stream stream)
        {
            return new Bitmap(SKBitmap.Decode(stream));
        }
    }

    public sealed class Bitmap : Image
    {
        public Bitmap(int width, int height)
            : this(width, height, PixelFormat.Format32bppPArgb)
        {
        }

        public Bitmap(int width, int height, PixelFormat format)
        {
            Bitmap = new SKBitmap(width, height, ToColorType(format), ToAlphaType(format));
        }

        public Bitmap(Image source)
        {
            Bitmap = source.Bitmap?.Copy() ?? new SKBitmap(1, 1);
        }

        internal Bitmap(SKBitmap source)
        {
            Bitmap = source ?? new SKBitmap(1, 1);
        }

        public Bitmap(Image source, int width, int height)
        {
            var result = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(result))
            {
                var paint = new SKPaint {FilterQuality = SKFilterQuality.High, IsAntialias = true};
                canvas.DrawBitmap(source.Bitmap, new SKRect(0, 0, width, height), paint);
            }

            Bitmap = result;
        }

        public ColorPalette Palette => new ColorPalette(256);

        public BitmapData LockBits(Rectangle rect, ImageLockMode mode, PixelFormat format)
        {
            _locked = new BitmapData
            {
                Scan0 = Bitmap.GetPixels(),
                Stride = Bitmap.RowBytes,
                Width = rect.Width,
                Height = rect.Height,
                PixelFormat = format
            };
            return _locked;
        }

        public void UnlockBits(BitmapData data)
        {
            _locked = null;
        }

        public Color GetPixel(int x, int y)
        {
            var c = Bitmap.GetPixel(x, y);
            return Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
        }

        public void Save(string path)
        {
            using (var image = SKImage.FromBitmap(Bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.OpenWrite(path))
                data.SaveTo(stream);
        }

        internal static SKColorType ToColorType(PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.Format32bppRgb:
                case PixelFormat.Format24bppRgb:
                    return SKColorType.Bgra8888;
                case PixelFormat.Format8bppIndexed:
                    return SKColorType.Gray8;
                default:
                    return SKColorType.Bgra8888;
            }
        }

        internal static SKAlphaType ToAlphaType(PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.Format32bppPArgb:
                    return SKAlphaType.Premul;
                case PixelFormat.Format32bppRgb:
                case PixelFormat.Format24bppRgb:
                    return SKAlphaType.Opaque;
                default:
                    return SKAlphaType.Unpremul;
            }
        }
    }
}
