using System;
using EnhancedMap.Drawing;
using System.IO;

namespace EnhancedMap.Core.MapObjects
{
    public class RenderMap : IRenderObject
    {
        private static readonly byte[] _bytesCode = {0x57, 0x65, 0x6C, 0x63, 0x6F, 0x6D, 0x65, 0x20, 0x74, 0x6F, 0x20, 0x45, 0x6E, 0x68, 0x61, 0x6E, 0x63, 0x65, 0x64, 0x4D, 0x61, 0x70, 0x21, 0x20, 0x54, 0x68, 0x69, 0x73, 0x20, 0x69, 0x73, 0x20, 0x61, 0x20, 0x6E, 0x65, 0x77, 0x20, 0x70, 0x72, 0x6F, 0x6A, 0x65, 0x63, 0x74, 0x20, 0x74, 0x6F, 0x20, 0x65, 0x78, 0x70, 0x6C, 0x6F, 0x72, 0x65, 0x20, 0x55, 0x4F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x21, 0x21};

        // png standard format. Thanks wikipedia :P
        private static readonly byte[] _png_header = {0x89, 0x50, 0x4e, 0x47};

        public RenderMap(MapEntry entry, string path)
        {
            MapEntry = entry;
            Path = path;
            IsVisible = true;
        }

        public string Path { get; }
        public Bitmap Image { get; private set; }
        public MapEntry MapEntry { get; }
        public bool IsVisible { get; set; }
        public bool IsDisposing { get; set; }

        public void Dispose()
        {
            IsDisposing = true;
        }

        public void Unload()
        {
            Image?.Dispose();
            Image = null;
        }

        public void Load()
        {
            if (Image != null)
                Unload();
            Image = Decode();
        }

        public void Renew()
        {
        }

        public bool Render(Graphics g, int w, int h, float angle, short x = -1, short y = -1, float zoom = -1)
        {
            if (!IsVisible)
                return false;

            if (Image == null)
                Load();

            if (Image == null || Image.Width == 0)
                return false;

            int iX = x > -1 ? x : (int) Global.X;
            int iY = y > -1 ? y : (int) Global.Y;
            float iZoom = zoom > -1 ? zoom : Global.Zoom;

            g.TranslateTransform(w / 2f, h / 2f);
            g.RotateTransform(angle);
            g.ScaleTransform(iZoom, iZoom);
            g.TranslateTransform(-iX, -iY);
            g.DrawImage(Image, 0, 0, Image.Width, Image.Height);
            g.ResetTransform();

            return false;
        }

        private Bitmap Decode()
        {
            using (FileStream fs = File.OpenRead(Path))
            {
                byte[] buffer = new byte[(int) fs.Length];
                int read = 0;
                while (read < buffer.Length)
                {
                    int n = fs.Read(buffer, read, buffer.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }

                bool ispng = true;
                for (int i = 0; i < _png_header.Length; i++)
                {
                    if (_png_header[i] != buffer[i])
                    {
                        ispng = false;
                        break;
                    }
                }

                if (!ispng)
                    ReadMapBytes(buffer, fs.Length);

                return (Bitmap) EnhancedMap.Drawing.Image.FromStream(new MemoryStream(buffer));
            }
        }

        private unsafe void ReadMapBytes(byte[] buffer, long len)
        {
            byte l = (byte) ((len >> 2) & byte.MaxValue);
            fixed (byte* bufferPtr = buffer)
            {
                byte* tempPtr = bufferPtr;
                for (; len > 0L; --len)
                {
                    *tempPtr = (byte) (*tempPtr ^ (uint) (byte) (_bytesCode[(int) len & 63] & (uint) byte.MaxValue));
                    *tempPtr = (byte) (*tempPtr + 139U);
                    *tempPtr = (byte) (*tempPtr ^ (uint) l);
                    *tempPtr = (byte) (*tempPtr - 17U);

                    byte shifted = (byte) ((uint) *tempPtr >> 3);
                    ++tempPtr;
                    l -= shifted;
                }
            }
        }
    }
}
