using System;
using SkiaSharp;

namespace EnhancedMap.Drawing
{
    public sealed class Graphics : IDisposable
    {
        private readonly SKCanvas _canvas;

        public Graphics(SKCanvas canvas)
        {
            _canvas = canvas;
        }

        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.Default;
        public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Default;
        public CompositingQuality CompositingQuality { get; set; } = CompositingQuality.Default;
        public PixelOffsetMode PixelOffsetMode { get; set; } = PixelOffsetMode.Default;
        public GraphicsUnit PageUnit { get; set; } = GraphicsUnit.Pixel;
        public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.SystemDefault;

        public static Graphics FromImage(Bitmap bitmap)
        {
            return new Graphics(new SKCanvas(bitmap.Bitmap));
        }

        public void Clear(Color color)
        {
            _canvas.Clear(color.ToSKColor());
        }

        public void TranslateTransform(float dx, float dy)
        {
            _canvas.Translate(dx, dy);
        }

        public void RotateTransform(float angle)
        {
            _canvas.RotateDegrees(angle);
        }

        public void RotateTransform(float angle, MatrixOrder order)
        {
            _canvas.RotateDegrees(angle);
        }

        public void ScaleTransform(float sx, float sy)
        {
            _canvas.Scale(sx, sy);
        }

        public void ResetTransform()
        {
            _canvas.ResetMatrix();
        }

        public int Save()
        {
            return _canvas.Save();
        }

        public void Restore()
        {
            _canvas.Restore();
        }

        public SizeF MeasureString(string text, Font font)
        {
            if (string.IsNullOrEmpty(text))
                return new SizeF(0, font?.Height ?? 0);

            using (var paint = CreateTextPaint(font, Color.White))
            {
                float width = paint.MeasureText(text);
                return new SizeF(width, font.Height);
            }
        }

        public void DrawString(string text, Font font, Brush brush, float x, float y)
        {
            if (string.IsNullOrEmpty(text) || font == null || brush == null)
                return;

            using (var paint = CreateTextPaint(font, brush.Color))
            {
                var metrics = paint.FontMetrics;
                float baseline = y - metrics.Ascent;
                _canvas.DrawText(text, x, baseline, paint);
            }
        }

        public void DrawImage(Image image, float x, float y, float width, float height)
        {
            if (image?.Bitmap == null)
                return;

            using (var paint = CreateImagePaint())
                _canvas.DrawBitmap(image.Bitmap, new SKRect(x, y, x + width, y + height), paint);
        }

        public void DrawImage(Image image, int x, int y, int width, int height)
        {
            DrawImage(image, x, y, (float) width, (float) height);
        }

        public void DrawImageUnscaled(Image image, int x, int y)
        {
            if (image?.Bitmap == null)
                return;

            using (var paint = CreateImagePaint())
                _canvas.DrawBitmap(image.Bitmap, x, y, paint);
        }

        public void FillRectangle(Brush brush, float x, float y, float width, float height)
        {
            if (brush == null)
                return;

            using (var paint = brush.CreatePaint())
                _canvas.DrawRect(x, y, width, height, paint);
        }

        public void FillRectangle(Brush brush, int x, int y, int width, int height)
        {
            FillRectangle(brush, (float) x, (float) y, width, height);
        }

        public void FillRectangle(Brush brush, Rectangle rectangle)
        {
            FillRectangle(brush, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        public void FillRectangle(Brush brush, RectangleF rectangle)
        {
            FillRectangle(brush, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        public void DrawRectangle(Pen pen, Rectangle rectangle)
        {
            DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        public void DrawRectangle(Pen pen, int x, int y, int width, int height)
        {
            DrawRectangle(pen, (float) x, (float) y, width, height);
        }

        public void DrawRectangle(Pen pen, float x, float y, float width, float height)
        {
            if (pen == null)
                return;

            using (var paint = pen.CreatePaint())
                _canvas.DrawRect(x, y, width, height, paint);
        }

        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
        {
            if (pen == null)
                return;

            using (var paint = pen.CreatePaint())
                _canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        public void DrawEllipse(Pen pen, RectangleF rectangle)
        {
            if (pen == null)
                return;

            using (var paint = pen.CreatePaint())
                _canvas.DrawOval(new SKRect(rectangle.X, rectangle.Y, rectangle.Right, rectangle.Bottom), paint);
        }

        public void FillEllipse(Brush brush, float x, float y, float width, float height)
        {
            if (brush == null)
                return;

            using (var paint = brush.CreatePaint())
                _canvas.DrawOval(new SKRect(x, y, x + width, y + height), paint);
        }

        private SKPaint CreateTextPaint(Font font, Color color)
        {
            return new SKPaint
            {
                IsAntialias = SmoothingMode != SmoothingMode.None,
                Color = color.ToSKColor(),
                Typeface = font.Typeface,
                TextSize = font.Size
            };
        }

        private SKPaint CreateImagePaint()
        {
            return new SKPaint
            {
                IsAntialias = SmoothingMode == SmoothingMode.AntiAlias,
                FilterQuality = InterpolationMode == InterpolationMode.NearestNeighbor ? SKFilterQuality.None : SKFilterQuality.High
            };
        }

        public void Dispose()
        {
        }
    }
}
