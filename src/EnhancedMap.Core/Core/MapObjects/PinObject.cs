using EnhancedMap.Drawing;
using EnhancedMap.Properties;

namespace EnhancedMap.Core.MapObjects
{
    public class PinObject : RenderObject
    {
        private static readonly Pen _pen = new Pen(Color.White) {DashStyle = DashStyle.Dot, DashPattern = new float[] {9, 4, 3, 4, 3, 4}};

        private readonly Image _img;

        public PinObject(UserObject parent, int x, int y) : base("pin")
        {
            Parent = parent;
            UpdatePosition(x, y);

            _img = new Bitmap(Resources.pin, 12, 18);
        }

        public UserObject Parent { get; }

        public override bool Render(Graphics g, int x, int y, int canvasW, int canvasH)
        {
            if (Parent != null && Parent.IsDisposing)
                Dispose();

            if (IsDisposing || Map != Global.Facet)
                return false;

            int gameX = RelativePosition.X;
            int gameY = RelativePosition.Y;

            (gameX, gameY) = Geometry.RotatePoint(gameX, gameY, Global.Zoom, 1, Global.Angle);
            AdjustPosition(gameX, gameY, x - 4, y - 4, out int relativeX, out int relativeY);

            gameX = relativeX;
            gameY = relativeY;

            relativeX = gameX + x;
            relativeY = gameY + y;


            // With no local player the pin's line points at the view center (the reference point).
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawLine(_pen, relativeX, relativeY, x, y);
            g.ResetTransform();

            g.DrawImage(_img, relativeX - _img.Width / 2, relativeY - _img.Height, _img.Width, _img.Height);

            return false;
        }
    }
}