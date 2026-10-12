using System;
using System.IO;
using System.Reflection;
using EnhancedMap.Drawing;

namespace EnhancedMap.Properties
{
    /// <summary>
    ///     Cross-platform replacement for the original WinForms / .resx generated resource class.
    ///     Images are embedded from the repository Resources/ folder and decoded with SkiaSharp.
    /// </summary>
    internal static class Resources
    {
        private static readonly Assembly _assembly = typeof(Resources).Assembly;
        private static readonly string _prefix = typeof(Resources).Namespace + ".";

        private static Bitmap LoadBitmap(string fileName)
        {
            using (Stream stream = _assembly.GetManifestResourceStream(_prefix + fileName))
            {
                if (stream == null)
                    return new Bitmap(1, 1);

                return (Bitmap) Image.FromStream(stream);
            }
        }

        private static Stream LoadStream(string fileName)
        {
            return _assembly.GetManifestResourceStream(_prefix + fileName) ?? Stream.Null;
        }

        private static readonly Lazy<Bitmap> _arrow_flat = new Lazy<Bitmap>(() => LoadBitmap("arrow-flat.png"));
        private static readonly Lazy<Bitmap> _backgroundEM = new Lazy<Bitmap>(() => LoadBitmap("backgroundEM.png"));
        private static readonly Lazy<Bitmap> _discord = new Lazy<Bitmap>(() => LoadBitmap("discord.png"));
        private static readonly Lazy<Bitmap> _facebook_256 = new Lazy<Bitmap>(() => LoadBitmap("facebook-256.png"));
        private static readonly Lazy<Bitmap> _FinalMarker = new Lazy<Bitmap>(() => LoadBitmap("FinalMarker.png"));
        private static readonly Lazy<Bitmap> _hide = new Lazy<Bitmap>(() => LoadBitmap("hide.png"));
        private static readonly Lazy<Bitmap> _if_external_link = new Lazy<Bitmap>(() => LoadBitmap("if_external-link.png"));
        private static readonly Lazy<Bitmap> _mapExNormal = new Lazy<Bitmap>(() => LoadBitmap("mapExNormal.png"));
        private static readonly Lazy<Bitmap> _PayPal = new Lazy<Bitmap>(() => LoadBitmap("PayPal.png"));
        private static readonly Lazy<Bitmap> _pin = new Lazy<Bitmap>(() => LoadBitmap("pin.png"));
        private static readonly Lazy<Bitmap> _refresh_icon = new Lazy<Bitmap>(() => LoadBitmap("refresh-icon.png"));
        private static readonly Lazy<Bitmap> _right = new Lazy<Bitmap>(() => LoadBitmap("right.png"));
        private static readonly Lazy<Bitmap> _rose = new Lazy<Bitmap>(() => LoadBitmap("rose.gif"));
        private static readonly Lazy<Bitmap> _SHAREDLABEL = new Lazy<Bitmap>(() => LoadBitmap("SHAREDLABEL.png"));
        private static readonly Lazy<Bitmap> _skull2 = new Lazy<Bitmap>(() => LoadBitmap("skull2.png"));
        private static readonly Lazy<Bitmap> _tombstone = new Lazy<Bitmap>(() => LoadBitmap("tombstone.png"));
        private static readonly Lazy<Bitmap> _uoclassic = new Lazy<Bitmap>(() => LoadBitmap("uoclassic.png"));
        private static readonly Lazy<Bitmap> _uoenhanced = new Lazy<Bitmap>(() => LoadBitmap("uoenhanced.png"));
        private static readonly Lazy<Bitmap> _wrong = new Lazy<Bitmap>(() => LoadBitmap("wrong.png"));

        public static Bitmap arrow_flat => _arrow_flat.Value;
        public static Bitmap backgroundEM => _backgroundEM.Value;
        public static Bitmap discord => _discord.Value;
        public static Bitmap facebook_256 => _facebook_256.Value;
        public static Bitmap FinalMarker => _FinalMarker.Value;
        public static Bitmap hide => _hide.Value;
        public static Bitmap if_external_link => _if_external_link.Value;
        public static Bitmap mapExNormal => _mapExNormal.Value;
        public static Bitmap PayPal => _PayPal.Value;
        public static Bitmap pin => _pin.Value;
        public static Bitmap refresh_icon => _refresh_icon.Value;
        public static Bitmap right => _right.Value;
        public static Bitmap rose => _rose.Value;
        public static Bitmap SHAREDLABEL => _SHAREDLABEL.Value;
        public static Bitmap skull2 => _skull2.Value;
        public static Bitmap tombstone => _tombstone.Value;
        public static Bitmap uoclassic => _uoclassic.Value;
        public static Bitmap uoenhanced => _uoenhanced.Value;
        public static Bitmap wrong => _wrong.Value;

        public static Stream light => LoadStream("light.wav");
        public static Stream stairs => LoadStream("stairs.wav");
        public static Stream unsure => LoadStream("unsure.wav");
    }
}
