namespace EnhancedMap.Drawing
{
    public abstract class Brush
    {
        public Color Color { get; protected set; }

        public abstract SkiaSharp.SKPaint CreatePaint();
    }

    public sealed class SolidBrush : Brush
    {
        public SolidBrush(Color color)
        {
            Color = color;
        }

        public override SkiaSharp.SKPaint CreatePaint()
        {
            return new SkiaSharp.SKPaint
            {
                Style = SkiaSharp.SKPaintStyle.Fill,
                Color = Color.ToSKColor()
            };
        }
    }

    public static class Brushes
    {
        public static Brush Black => new SolidBrush(Color.Black);
        public static Brush White => new SolidBrush(Color.White);
        public static Brush Red => new SolidBrush(Color.Red);
        public static Brush Green => new SolidBrush(Color.Green);
        public static Brush Blue => new SolidBrush(Color.Blue);
        public static Brush Yellow => new SolidBrush(Color.Yellow);
        public static Brush Gray => new SolidBrush(Color.Gray);
        public static Brush Orange => new SolidBrush(Color.Orange);
        public static Brush Cyan => new SolidBrush(Color.Cyan);
        public static Brush Transparent => new SolidBrush(Color.Transparent);
    }
}
