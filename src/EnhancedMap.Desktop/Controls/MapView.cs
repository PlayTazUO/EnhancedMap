using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EnhancedMap.Core;
using EnhancedMap.Core.MapObjects;
using EnhancedMap.Drawing;
using SkiaSharp;

namespace EnhancedMap.Desktop.Controls
{
    /// <summary>
    ///     Cross-platform replacement for the WinForms PictureBox/EnhancedCanvas. The map and all
    ///     render objects are drawn with SkiaSharp into an offscreen buffer which is then blitted to
    ///     an Avalonia WriteableBitmap every frame.
    /// </summary>
    public class MapView : Control
    {
        private SKBitmap _surface;
        private WriteableBitmap _bitmap;

        private bool _dragging;
        private Avalonia.Point _pointClicked;
        private Avalonia.Point _lastScroll;

        private PixelSize _lastPixelSize;

        public MapView()
        {
            ClipToBounds = true;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Cross);

            PointerMoved += OnPointerMoved;
            PointerPressed += OnPointerPressed;
            PointerReleased += OnPointerReleased;
            PointerWheelChanged += OnPointerWheelChanged;
            PointerEntered += (s, e) => MouseManager.IsEnter = true;
            PointerExited += (s, e) => MouseManager.IsEnter = false;
        }

        /// <summary>Raised when the user requests a signal (Ctrl+G).</summary>
        public event Action SignalRequested;

        /// <summary>Raised when the user toggles panic (space).</summary>
        public event Action PanicRequested;

        /// <summary>Raised when a printable character is typed; the host forwards it to chat.</summary>
        public event Action<string> TextTyped;

        public override void Render(DrawingContext context)
        {
            var size = new PixelSize(Math.Max(1, (int) Bounds.Width), Math.Max(1, (int) Bounds.Height));
            if (_surface == null || _lastPixelSize != size)
            {
                _surface?.Dispose();
                _surface = new SKBitmap(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                _bitmap = new WriteableBitmap(size, new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Premul);
                _lastPixelSize = size;
            }

            using (var canvas = new SKCanvas(_surface))
            {
                canvas.Clear(SKColors.Black);
                var g = new Graphics(canvas);
                int w = size.Width;
                int h = size.Height;
                int centerX = w / 2;
                int centerY = h / 2;

                RenderObjectsManager.Render(g, w, h, Global.Angle);
                RenderObjectsManager.Render(g, centerX, centerY, w, h);
            }

            Blit();

            context.DrawImage(_bitmap, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }

        private void Blit()
        {
            if (_surface == null || _bitmap == null)
                return;

            byte[] bytes = _surface.Bytes;
            int rowBytes = _surface.RowBytes;
            int height = _surface.Height;

            using (ILockedFramebuffer fb = _bitmap.Lock())
            {
                int copy = Math.Min(rowBytes, fb.RowBytes);
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(bytes, y * rowBytes, fb.Address + y * fb.RowBytes, copy);
                }
            }
        }

        private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            Global.ChangeZoom(e.Delta.Y < 0);
            InvalidateVisual();
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            Focus();
            var pt = e.GetCurrentPoint(this);
            var pos = pt.Position;

            if (pt.Properties.IsLeftButtonPressed)
                MouseManager.LeftIsPressed = true;
            else if (pt.Properties.IsRightButtonPressed)
                MouseManager.RightIsPressed = true;

            if (MouseManager.LeftIsPressed || pt.Properties.IsMiddleButtonPressed)
            {
                if (Global.FreeView)
                {
                    Cursor = new Cursor(StandardCursorType.SizeAll);
                    _lastScroll = pos;
                }
            }
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _dragging = false;
            Cursor = new Cursor(StandardCursorType.Cross);

            if (e.InitialPressMouseButton == MouseButton.Left)
                MouseManager.LeftIsPressed = false;
            else if (e.InitialPressMouseButton == MouseButton.Right)
                MouseManager.RightIsPressed = false;
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            var pos = e.GetPosition(this);
            var props = e.GetCurrentPoint(this).Properties;

            bool leftDown = props.IsLeftButtonPressed;
            bool middleDown = props.IsMiddleButtonPressed;

            if (middleDown && !Global.FreeView)
                Global.FreeView = true;

            if ((leftDown && Global.FreeView) || middleDown)
            {
                int x = (int) pos.X;
                int y = (int) pos.Y;

                float scrollX = (float) (_lastScroll.X - x);
                float scrollY = (float) (_lastScroll.Y - y);

                (scrollX, scrollY) = EnhancedMap.Core.Geometry.RotatePoint(scrollX, scrollY, 1f, -1, Global.Angle);

                Global.X += scrollX / Global.Zoom;
                Global.Y += scrollY / Global.Zoom;

                ClampPosition();
                _lastScroll = pos;
                InvalidateVisual();
            }
            else
            {
                int x = (int) pos.X - (int) Bounds.Width / 2;
                int y = (int) pos.Y - (int) Bounds.Height / 2;

                (x, y) = EnhancedMap.Core.Geometry.RotatePoint(x, y, 1f / Global.Zoom, -1, Global.Angle);

                x += (int) Global.X;
                y += (int) Global.Y;
                MouseManager.Location = new Position((short) x, (short) y);
                InvalidateVisual();
            }
        }

        private static void ClampPosition()
        {
            if (Global.Facet < 0 || Global.Facet >= Global.Maps.Length || Global.Maps[Global.Facet] == null)
                return;

            var map = Global.Maps[Global.Facet];
            if (Global.X < 0) Global.X = 0;
            if (Global.Y < 0) Global.Y = 0;
            if (Global.X > map.Width) Global.X = map.Width;
            if (Global.Y > map.Height) Global.Y = map.Height;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    PanicRequested?.Invoke();
                    e.Handled = true;
                    break;
                case Key.G when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                    SignalRequested?.Invoke();
                    e.Handled = true;
                    break;
                case Key.Up:
                    Pan(-50, -50);
                    e.Handled = true;
                    break;
                case Key.Right:
                    Pan(50, -50);
                    e.Handled = true;
                    break;
                case Key.Down:
                    Pan(50, 50);
                    e.Handled = true;
                    break;
                case Key.Left:
                    Pan(-50, 50);
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Text))
                TextTyped?.Invoke(e.Text);

            base.OnTextInput(e);
        }

        private void Pan(int dx, int dy)
        {
            if (!Global.FreeView)
                Global.FreeView = true;

            Global.X += dx / Global.Zoom;
            Global.Y += dy / Global.Zoom;
            ClampPosition();
            InvalidateVisual();
        }
    }
}
