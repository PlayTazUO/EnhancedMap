using System;

// A small, cross-platform subset of the GDI+ (System.Drawing) API surface used by
// EnhancedMap, implemented on top of SkiaSharp. Living under EnhancedMap.Drawing lets
// the original rendering code keep working while running on .NET 10 / Avalonia.
namespace EnhancedMap.Drawing
{
    public readonly struct Color : IEquatable<Color>
    {
        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public Color(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public static Color FromArgb(int argb)
        {
            return new Color((byte) ((argb >> 24) & 0xff), (byte) ((argb >> 16) & 0xff), (byte) ((argb >> 8) & 0xff), (byte) (argb & 0xff));
        }

        public static Color FromArgb(int r, int g, int b)
        {
            return new Color(255, (byte) r, (byte) g, (byte) b);
        }

        public static Color FromArgb(int a, int r, int g, int b)
        {
            return new Color((byte) a, (byte) r, (byte) g, (byte) b);
        }

        public static Color FromArgb(int a, Color color)
        {
            return new Color((byte) a, color.R, color.G, color.B);
        }

        public int ToArgb()
        {
            return (A << 24) | (R << 16) | (G << 8) | B;
        }

        public SkiaSharp.SKColor ToSKColor()
        {
            return new SkiaSharp.SKColor(R, G, B, A);
        }

        public float GetHue()
        {
            if (R == G && G == B) return 0f;
            float r = R / 255f, g = G / 255f, b = B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;
            float hue;

            if (r == max) hue = (g - b) / delta;
            else if (g == max) hue = 2f + (b - r) / delta;
            else hue = 4f + (r - g) / delta;

            hue *= 60f;
            if (hue < 0f) hue += 360f;
            return hue;
        }

        public float GetSaturation()
        {
            float r = R / 255f, g = G / 255f, b = B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            if (max == min) return 0f;
            float l = (max + min) / 2f;
            return l < 0.5f ? (max - min) / (max + min) : (max - min) / (2f - max - min);
        }

        public float GetBrightness()
        {
            float r = R / 255f, g = G / 255f, b = B / 255f;
            return (Math.Max(r, Math.Max(g, b)) + Math.Min(r, Math.Min(g, b))) / 2f;
        }

        public bool Equals(Color other)
        {
            return A == other.A && R == other.R && G == other.G && B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj is Color c && Equals(c);
        }

        public override int GetHashCode()
        {
            return ToArgb();
        }

        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }

        public static Color White => new Color(255, 255, 255, 255);
        public static Color Black => new Color(255, 0, 0, 0);
        public static Color Red => new Color(255, 255, 0, 0);
        public static Color Green => new Color(255, 0, 128, 0);
        public static Color LimeGreen => new Color(255, 50, 205, 50);
        public static Color Blue => new Color(255, 0, 0, 255);
        public static Color Yellow => new Color(255, 255, 255, 0);
        public static Color Cyan => new Color(255, 0, 255, 255);
        public static Color Gray => new Color(255, 128, 128, 128);
        public static Color DarkGray => new Color(255, 169, 169, 169);
        public static Color LightYellow => new Color(255, 255, 255, 224);
        public static Color AliceBlue => new Color(255, 240, 248, 255);
        public static Color Transparent => new Color(0, 255, 255, 255);
        public static Color Orange => new Color(255, 255, 165, 0);
    }

    public static class SystemColors
    {
        public static Color Control => new Color(255, 240, 240, 240);
        public static Color Window => Color.White;
    }

    public struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public void Offset(int dx, int dy)
        {
            X += dx;
            Y += dy;
        }

        public static Point Empty => new Point(0, 0);

        public static bool operator ==(Point left, Point right)
        {
            return left.X == right.X && left.Y == right.Y;
        }

        public static bool operator !=(Point left, Point right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            return obj is Point p && this == p;
        }

        public override int GetHashCode()
        {
            return X ^ Y;
        }

        public override string ToString()
        {
            return $"{{X={X},Y={Y}}}";
        }
    }

    public struct PointF
    {
        public float X;
        public float Y;

        public PointF(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static PointF Empty => new PointF(0, 0);
    }

    public struct Size
    {
        public int Width;
        public int Height;

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public bool IsEmpty => Width == 0 && Height == 0;

        public static Size Empty => new Size(0, 0);
    }

    public struct SizeF
    {
        public float Width;
        public float Height;

        public SizeF(float width, float height)
        {
            Width = width;
            Height = height;
        }

        public static SizeF Empty => new SizeF(0, 0);
    }

    public struct Rectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Rectangle(Point location, Size size)
        {
            X = location.X;
            Y = location.Y;
            Width = size.Width;
            Height = size.Height;
        }

        public int Left => X;
        public int Top => Y;
        public int Right => X + Width;
        public int Bottom => Y + Height;
        public Size Size => new Size(Width, Height);
        public Point Location => new Point(X, Y);

        public bool Contains(int x, int y)
        {
            return x >= X && x < X + Width && y >= Y && y < Y + Height;
        }

        public bool Contains(Point p)
        {
            return Contains(p.X, p.Y);
        }

        public bool Contains(Rectangle r)
        {
            return r.X >= X && r.Y >= Y && r.X + r.Width <= X + Width && r.Y + r.Height <= Y + Height;
        }

        public bool IntersectsWith(Rectangle r)
        {
            return r.X < X + Width && X < r.X + r.Width && r.Y < Y + Height && Y < r.Y + r.Height;
        }

        public static Rectangle FromLTRB(int left, int top, int right, int bottom)
        {
            return new Rectangle(left, top, right - left, bottom - top);
        }

        public static Rectangle Empty => new Rectangle(0, 0, 0, 0);
    }

    public struct RectangleF
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public RectangleF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Left => X;
        public float Top => Y;
        public float Right => X + Width;
        public float Bottom => Y + Height;

        public bool Contains(float x, float y)
        {
            return x >= X && x < X + Width && y >= Y && y < Y + Height;
        }
    }

    public enum SmoothingMode
    {
        Default,
        HighSpeed,
        HighQuality,
        None,
        AntiAlias
    }

    public enum InterpolationMode
    {
        Default,
        Low,
        High,
        Bilinear,
        Bicubic,
        NearestNeighbor,
        HighQualityBilinear,
        HighQualityBicubic
    }

    public enum CompositingQuality
    {
        Default,
        HighSpeed,
        HighQuality,
        GammaCorrected,
        AssumeLinear
    }

    public enum PixelOffsetMode
    {
        Default,
        HighSpeed,
        HighQuality,
        None,
        Half
    }

    public enum TextRenderingHint
    {
        SystemDefault,
        SingleBitPerPixelGridFit,
        SingleBitPerPixel,
        AntiAliasGridFit,
        AntiAlias,
        ClearTypeGridFit
    }

    public enum MatrixOrder
    {
        Prepend,
        Append
    }

    public enum GraphicsUnit
    {
        World,
        Display,
        Pixel,
        Point,
        Inch,
        Document,
        Millimeter
    }

    public enum ContentAlignment
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}
