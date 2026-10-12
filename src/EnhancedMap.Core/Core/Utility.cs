using System;
using EnhancedMap.Drawing;
using System.IO;
using System.Text;
using System.Xml;

namespace EnhancedMap.Core
{
    public static class Utility
    {
        public static string GetSizeAdaptive(long bytes)
        {
            decimal num = bytes;
            var arg = "KB";
            num /= 1024m;
            if (!(num < 1024m))
            {
                arg = "MB";
                num /= 1024m;
                if (!(num < 1024m))
                {
                    arg = "GB";
                    num /= 1024m;
                }
            }

            return $"{Math.Round(num, 2):0.##} {arg}";
        }

        public static bool IsOnScreen(object window)
        {
            // Window placement is now handled by the Avalonia windowing layer.
            return true;
        }

        public static string GetText(XmlElement node, string defaultValue)
        {
            if (node == null)
                return defaultValue;
            return node.InnerText;
        }

        //public static void CreateConsole()
        //{
        //    AllocConsole();

        //    var hOut = GetStdHandle(StdOutputHandle);
        //    var hRealOut = CreateFile("CONOUT$", GenericRead | GenericWrite, FileShare.Write, IntPtr.Zero,
        //        FileMode.OpenOrCreate, 0, IntPtr.Zero);
        //    if (hRealOut != hOut)
        //    {
        //        SetStdHandle(StdOutputHandle, hRealOut);
        //        ConsoleHook.Initialize();
        //    }
        //}

        //public static void RemoveConsole()
        //{
        //    FreeConsole();
        //}

        public static string TraceData(byte[] data, byte id, int length)
        {
            StringBuilder sb = new StringBuilder(4096);
            sb.AppendLine($"ID: {id:X2}, Length: {length}");
            sb.AppendLine(" 0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F");
            sb.AppendLine("-- -- -- -- -- -- -- --  -- -- -- -- -- -- -- --");

            for (int i = 0; i < length; i++)
            {
                if (i % 16 == 0 && i != 0)
                    sb.AppendLine();
                if (i % 8 == 0 && i % 16 != 0)
                    sb.Append(" ");
                sb.Append(data[i].ToString("X2"));
                sb.Append(" ");
            }

            sb.AppendLine();
            sb.AppendLine();

            return sb.ToString();
        }

        public static int Distance(int fx, int fy, int tx, int ty)
        {
            int xDelta = Math.Abs(fx - tx);
            int yDelta = Math.Abs(fy - ty);

            return xDelta > yDelta ? xDelta : yDelta;
        }

        public static string MD5(this string s)
        {
            using (var provider = System.Security.Cryptography.MD5.Create())
            {
                StringBuilder builder = new StringBuilder();

                foreach (byte b in provider.ComputeHash(Encoding.UTF8.GetBytes(s)))
                    builder.Append(b.ToString("x2").ToLower());

                return builder.ToString();
            }
        }


        public static bool FormatCoordinates(Point p, MapEntry map, ref int xLong, ref int yLat, ref int xMins, ref int yMins, ref bool xEast, ref bool ySouth)
        {
            if (map == null)
                return false;

            int x = p.X, y = p.Y;

            if (!ComputeMapDetails(map, x, y, out int xCenter, out int yCenter, out int xWidth, out int yHeight))
                return false;

            double absLong = (double) ((x - xCenter) * 360) / xWidth;
            double absLat = (double) ((y - yCenter) * 360) / yHeight;

            if (absLong > 180.0)
                absLong = -180.0 + absLong % 180.0;

            if (absLat > 180.0)
                absLat = -180.0 + absLat % 180.0;

            bool east = absLong >= 0, south = absLat >= 0;

            if (absLong < 0.0)
                absLong = -absLong;

            if (absLat < 0.0)
                absLat = -absLat;

            xLong = (int) absLong;
            yLat = (int) absLat;

            xMins = (int) (absLong % 1.0 * 60);
            yMins = (int) (absLat % 1.0 * 60);

            xEast = east;
            ySouth = south;

            return true;
        }

        public static bool ComputeMapDetails(MapEntry map, int x, int y, out int xCenter, out int yCenter, out int xWidth, out int yHeight)
        {
            xWidth = 5120;
            yHeight = 4096;

            xCenter = (map.Index == 0 || map.Index == 1) && x >= 5120 ? 5936 : 1323;
            yCenter = (map.Index == 0 || map.Index == 1) && y >= 2304 ? 3112 : 1624;

            return true;
        }

        public static Point GetLatLong(double degreeLat, double minLat, double degreeLong, double minLong, string direction1, string direction2)
        {
            Point pnt = Point.Empty;

            try
            {
                var lat = degreeLat + minLat / 100.0;
                var longi = degreeLong + minLong / 100.0;
                pnt = new Point(GetXFromLatLong(lat, longi, direction1, direction2), GetYFromLatLong(lat, longi, direction1, direction2));
            }
            catch
            {
            }

            return pnt;
        }

        private static int GetXFromLatLong(double lat, double lon, string direction1, string direction2)
        {
            const int centerX = 1323;
            var tempLon = direction2 != "W" ? Math.Floor(lon) * 60.0 + lon % 1 * 100.0 : -1.0 * Math.Ceiling(lon) * 60.0 + lon % 1 * 100.0;
            var resultX = (int) (tempLon / 21600.0 * 5120.0) + centerX;
            if (resultX < 0)
                resultX += 5120;
            if (resultX >= 5120)
                resultX -= 5120;

            return resultX;
        }

        private static int GetYFromLatLong(double lat, double lon, string direction1, string direction2)
        {
            const int centerY = 1624;
            var tempLat = direction1 != "N" ? Math.Floor(lat) * 60.0 + lat % 1 * 100.0 : -1.0 * Math.Ceiling(lat) * 60.0 + lat % 1.0 * 100.0;
            var resultY = (int) (tempLat / 21600.0 * 4096.0) + centerY;
            if (resultY < 0)
                resultY += 4096;
            if (resultY >= 4096)
                resultY -= 4096;

            return resultY;
        }
    }
}