using EnhancedMap.Drawing;

namespace EnhancedMap.Core.MapObjects
{
    public class MobileObject : RenderObject
    {
        public MobileObject(uint serial) : base("0x" + serial.ToString("X"))
        {
            Serial = serial;
            Map = (byte) Global.FirstValidFacet;
        }

        public uint Serial { get; }

        public override bool Render(Graphics g, int x, int y, int canvasW, int canvasH)
        {
            if (IsDisposing || Map != Global.Facet)
                return false;

            int gameX = RelativePosition.X;
            int gameY = RelativePosition.Y;

            (gameX, gameY) = Geometry.RotatePoint(gameX, gameY, Global.Zoom, 1, Global.Angle);

            g.FillRectangle(Brushes.Red, gameX + x - 2, gameY + y - 2, 4, 4);

            return false;
        }
    }
}