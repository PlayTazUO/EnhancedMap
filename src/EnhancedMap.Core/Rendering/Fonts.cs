using System;
using System.Collections.Generic;
using System.Linq;

namespace EnhancedMap.Drawing
{
    public enum DashStyle
    {
        Solid,
        Dash,
        Dot,
        DashDot,
        DashDotDot,
        Custom
    }

    public enum LineCap
    {
        Flat,
        Square,
        Round,
        Triangle,
        NoAnchor,
        SquareAnchor,
        RoundAnchor,
        DiamondAnchor,
        ArrowAnchor,
        Custom
    }

    public sealed class Pen : IDisposable
    {
        public Pen(Color color)
        {
            Color = color;
            Width = 1f;
        }

        public Pen(Color color, float width)
        {
            Color = color;
            Width = width;
        }

        public Pen(Brush brush, float width)
        {
            Color = brush?.Color ?? Color.Black;
            Width = width;
        }

        public Pen(Brush brush) : this(brush, 1f)
        {
        }

        public Color Color { get; set; }
        public float Width { get; set; }
        public DashStyle DashStyle { get; set; } = DashStyle.Solid;
        public float[] DashPattern { get; set; }
        public LineCap StartCap { get; set; } = LineCap.Flat;
        public LineCap EndCap { get; set; } = LineCap.Flat;

        internal SkiaSharp.SKPaint CreatePaint()
        {
            var paint = new SkiaSharp.SKPaint
            {
                Style = SkiaSharp.SKPaintStyle.Stroke,
                Color = Color.ToSKColor(),
                StrokeWidth = Width,
                IsAntialias = true,
                StrokeCap = ToSKCap(StartCap)
            };

            if (DashStyle != DashStyle.Solid)
            {
                float[] intervals = DashPattern;
                if (intervals == null)
                {
                    switch (DashStyle)
                    {
                        case DashStyle.Dot:
                            intervals = new[] {Width, Width};
                            break;
                        case DashStyle.Dash:
                            intervals = new[] {Width * 3f, Width};
                            break;
                        case DashStyle.DashDot:
                            intervals = new[] {Width * 3f, Width, Width, Width};
                            break;
                        case DashStyle.DashDotDot:
                            intervals = new[] {Width * 3f, Width, Width, Width, Width, Width};
                            break;
                    }
                }

                if (intervals != null)
                    paint.PathEffect = SkiaSharp.SKPathEffect.CreateDash(intervals, 0);
            }

            return paint;
        }

        private static SkiaSharp.SKStrokeCap ToSKCap(LineCap cap)
        {
            switch (cap)
            {
                case LineCap.Round:
                case LineCap.RoundAnchor:
                    return SkiaSharp.SKStrokeCap.Round;
                case LineCap.Square:
                case LineCap.SquareAnchor:
                    return SkiaSharp.SKStrokeCap.Square;
                default:
                    return SkiaSharp.SKStrokeCap.Butt;
            }
        }

        public void Dispose()
        {
        }
    }

    public static class Pens
    {
        public static Pen Black => new Pen(Color.Black);
        public static Pen White => new Pen(Color.White);
        public static Pen Red => new Pen(Color.Red);
        public static Pen Green => new Pen(Color.Green);
        public static Pen Blue => new Pen(Color.Blue);
        public static Pen Yellow => new Pen(Color.Yellow);
        public static Pen Gray => new Pen(Color.Gray);
        public static Pen Cyan => new Pen(Color.Cyan);
    }

    public enum FontStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        Strikeout = 8
    }

    public sealed class FontFamily
    {
        public FontFamily(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public static FontFamily[] Families
        {
            get
            {
                var names = SkiaSharp.SKFontManager.Default.FontFamilies.ToList();
                var result = new FontFamily[names.Count];
                for (int i = 0; i < names.Count; i++)
                    result[i] = new FontFamily(names[i]);
                return result;
            }
        }

        public bool IsStyleAvailable(FontStyle style)
        {
            return true;
        }

        public static FontFamily GenericSansSerif => new FontFamily("sans-serif");
        public static FontFamily GenericMonospace => new FontFamily("monospace");
        public static FontFamily GenericSerif => new FontFamily("serif");
    }

    public sealed class Font : IDisposable
    {
        private SkiaSharp.SKTypeface _typeface;

        public Font(string familyName, float size)
            : this(familyName, size, FontStyle.Regular, GraphicsUnit.Pixel)
        {
        }

        public Font(string familyName, float size, FontStyle style)
            : this(familyName, size, style, GraphicsUnit.Pixel)
        {
        }

        public Font(string familyName, float size, FontStyle style, GraphicsUnit unit)
        {
            Name = string.IsNullOrEmpty(familyName) ? "sans-serif" : familyName;
            Size = size;
            Style = style;
            Unit = unit;
        }

        public Font(FontFamily family, float size, FontStyle style)
            : this(family?.Name, size, style, GraphicsUnit.Pixel)
        {
        }

        public string Name { get; }
        public float Size { get; }
        public FontStyle Style { get; }
        public GraphicsUnit Unit { get; }

        public float Height => Size * 1.25f;

        internal SkiaSharp.SKTypeface Typeface
        {
            get
            {
                if (_typeface == null)
                {
                    bool bold = (Style & FontStyle.Bold) != 0;
                    bool italic = (Style & FontStyle.Italic) != 0;
                    var weight = bold ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal;
                    var slant = italic ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright;
                    var style = new SkiaSharp.SKFontStyle(weight, SkiaSharp.SKFontStyleWidth.Normal, slant);

                    _typeface = SkiaSharp.SKTypeface.FromFamilyName(Name, style) ?? SkiaSharp.SKTypeface.Default;
                }

                return _typeface;
            }
        }

        public float GetHeight()
        {
            return Height;
        }

        public void Dispose()
        {
        }
    }
}
