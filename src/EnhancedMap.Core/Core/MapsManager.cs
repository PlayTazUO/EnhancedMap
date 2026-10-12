using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;
using EnhancedMap.Diagnostic;
using SkiaSharp;

namespace EnhancedMap.Core
{
    public class MapEntry
    {
        public int FileIndex;
        public int Height;

        public int Index;
        public string Name;
        public int Width;

        public MapEntry(int index, int fileindex, int w, int h, string name)
        {
            Index = index;
            Width = w;
            Height = h;
            Name = name;

            FileIndex = fileindex;
        }

        public override string ToString()
        {
            return string.Format("Name: {0}\r\nIndex: {1}\r\nSize: {2}x{3}", Name, Index, Width, Height);
        }
    }

    /// <summary>
    ///     Reads/writes the maps.xml manifest, checks whether generated map images exist, and generates
    ///     the map PNGs from the Ultima Online client data (MUL / LegacyMUL.uop terrain + statics,
    ///     coloured via radarcol.mul). Rendering/encoding is done with SkiaSharp.
    /// </summary>
    public static class MapsManager
    {
        private const string MAPS_XML_NAME = "maps.xml";

        private static readonly string MAPS_XML = Path.Combine(AppContext.BaseDirectory, MAPS_XML_NAME);

        private static readonly DirectoryInfo _pathMaps = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "Maps"));

        private static float[] _radPix0, _radPix1, _radPix2;

        /// <summary>Terrain block size in bytes: 4-byte header + 64 cells * 3 bytes.</summary>
        private const int MAP_BLOCK_BYTES = 196;

        /// <summary>Tiles covered by a single terrain block (8x8).</summary>
        private const int MAP_BLOCK_TILES = 64;

        /// <summary>
        ///     Standard retail UO facet dimensions, indexed by facet. Map files do not store
        ///     their size, so these are used to pre-fill and validate size detection.
        /// </summary>
        public static readonly (int Width, int Height)[] StandardMapSizes =
        {
            (7168, 4096), // 0 Felucca
            (7168, 4096), // 1 Trammel
            (2304, 1600), // 2 Ilshenar
            (2560, 2048), // 3 Malas
            (1448, 1448), // 4 Tokuno
            (1280, 4096) // 5 TerMur
        };

        public static void RemoveMapsFiles()
        {
            if (File.Exists(MAPS_XML))
                File.Delete(MAPS_XML);

            if (_pathMaps.Exists)
            {
                foreach (FileInfo file in _pathMaps.EnumerateFiles("*.png"))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        /// <summary>
        ///     Enumerate the supported map terrain files (<c>mapN.mul</c> / <c>mapNLegacyMUL.uop</c>)
        ///     present in <paramref name="folder"/>, preferring the UOP when both exist.
        /// </summary>
        public static List<(int Index, string Path)> FindMapFiles(string folder)
        {
            var found = new Dictionary<int, (string Path, bool IsUop)>();

            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                try
                {
                    foreach (string full in Directory.EnumerateFiles(folder))
                    {
                        string lower = Path.GetFileName(full).ToLowerInvariant();
                        if (!lower.StartsWith("map"))
                            continue;

                        string digits;
                        bool isUop;

                        if (lower.EndsWith(".mul"))
                        {
                            digits = lower.Substring(3, lower.Length - 3 - 4);
                            isUop = false;
                        }
                        else if (lower.EndsWith("legacymul.uop"))
                        {
                            digits = lower.Substring(3, lower.Length - 3 - "legacymul.uop".Length);
                            isUop = true;
                        }
                        else
                        {
                            continue;
                        }

                        if (digits.Length == 0 || !digits.All(char.IsDigit))
                            continue;

                        if (!int.TryParse(digits, out int index))
                            continue;

                        if (index < 0 || index >= Global.Maps.Length)
                            continue; // only facets the map model can hold

                        if (!found.TryGetValue(index, out var existing) || (isUop && !existing.IsUop))
                            found[index] = (full, isUop);
                    }
                }
                catch
                {
                    // ignore unreadable folders
                }
            }

            return found.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, kvp.Value.Path)).ToList();
        }

        /// <summary>
        ///     Try to determine a map's width and height from its terrain file. Map files do
        ///     not store their dimensions, but the terrain is a fixed 196-byte block per 8x8
        ///     tiles, so the total tile count (Width * Height) is derivable from the data
        ///     size. A matching standard dimension pair is returned when one exists.
        /// </summary>
        public static bool TryDetectMapSize(int index, string path, out int width, out int height)
        {
            width = 0;
            height = 0;

            long size;
            try
            {
                size = GetMapDataSize(path);
            }
            catch
            {
                return false;
            }

            long totalTiles = size / MAP_BLOCK_BYTES * MAP_BLOCK_TILES;

            // Prefer the facet's own standard size when it matches the file.
            if (index >= 0 && index < StandardMapSizes.Length
                           && (long) StandardMapSizes[index].Width * StandardMapSizes[index].Height == totalTiles)
            {
                width = StandardMapSizes[index].Width;
                height = StandardMapSizes[index].Height;
                return true;
            }

            // Otherwise accept any known standard size with the same tile count.
            foreach ((int w, int h) in StandardMapSizes)
            {
                if ((long) w * h == totalTiles)
                {
                    width = w;
                    height = h;
                    return true;
                }
            }

            return false;
        }

        private static long GetMapDataSize(string path)
        {
            if (path.EndsWith(".uop", StringComparison.OrdinalIgnoreCase))
            {
                UOPFile[] files = ReadUOPFile(path, Path.GetFileName(path).Replace(".uop", string.Empty).ToLowerInvariant());

                long total = 0;
                for (int i = 0; i < files.Length; i++)
                    total += files[i].Length;

                return total;
            }

            return new FileInfo(path).Length;
        }

        public static bool CheckMapExists()
        {
            if (!_pathMaps.Exists)
                _pathMaps.Create();

            if (!File.Exists(MAPS_XML))
            {
                foreach (FileInfo file in _pathMaps.EnumerateFiles("*.png"))
                    file.Delete();

                return false;
            }

            return _pathMaps.EnumerateFiles("*.png").Any(s => s.Name.ToLower().Contains("map"));
        }

        public static void LoadMaps()
        {
            Global.Maps.Clear();

            XmlDocument doc = new XmlDocument();
            doc.Load(MAPS_XML);
            XmlElement root = doc["maps"];

            foreach (XmlElement e in root.ChildNodes)
            {
                MapEntry map = new MapEntry(e.GetAttribute("index").ToInt(), e.HasAttribute("fileindex") ? e.GetAttribute("fileindex").ToInt() : e.GetAttribute("index").ToInt(), e.GetAttribute("width").ToInt(), e.GetAttribute("height").ToInt(), e.Name);
                Global.Maps[map.Index] = map;
            }
        }

        public static void SaveMaps()
        {
            using (StreamWriter wr = new StreamWriter(MAPS_XML))
            {
                XmlTextWriter xml = new XmlTextWriter(wr) {Formatting = Formatting.Indented, IndentChar = '\t', Indentation = 1};

                xml.WriteStartDocument(true);
                xml.WriteStartElement("maps");

                foreach (MapEntry map in Global.Maps)
                {
                    if (map != null)
                    {
                        xml.WriteStartElement(map.Name);
                        xml.WriteAttributeString("index", map.Index.ToString());
                        xml.WriteAttributeString("fileindex", map.FileIndex.ToString());
                        xml.WriteAttributeString("width", map.Width.ToString());
                        xml.WriteAttributeString("height", map.Height.ToString());
                        xml.WriteEndElement();
                    }
                }

                xml.WriteEndElement();
                xml.Close();
            }
        }

        /// <summary>
        ///     Generates the map PNGs for the given entries. When <paramref name="detailed" /> is true a
        ///     shaded "2DmapN.png" is produced, otherwise the flat "mapN.png".
        /// </summary>
        public static bool CreateImages(List<MapEntry> maps, bool detailed, IProgress<string> progress)
        {
            if (_radPix0 == null || _radPix1 == null || _radPix2 == null)
            {
                if (!ReadRadarcol(progress))
                    return false;
            }

            if (!_pathMaps.Exists)
                _pathMaps.Create();

            progress?.Report("Maps found: " + maps.Count);
            progress?.Report("Starting map creation...");

            bool ok = true;

            foreach (MapEntry map in maps)
            {
                progress?.Report(new string('#', 38));
                progress?.Report("Current map:\r\n" + map);

                string uop = Path.Combine(Global.UOPath ?? string.Empty, $"map{map.Index}LegacyMUL.uop");
                string mul = Path.Combine(Global.UOPath ?? string.Empty, $"map{map.Index}.mul");
                string dds = Path.Combine(Global.UOPath ?? string.Empty, $"facet0{map.Index}.dds");

                if (File.Exists(uop) || File.Exists(mul))
                {
                    bool isuop = File.Exists(uop);

                    try
                    {
                        GenerateFromMul(map, isuop ? uop : mul, isuop, detailed, progress);
                    }
                    catch (Exception e)
                    {
                        progress?.Report($"Error generating map {map.Index}: {e.Message}");
                        Logger.Error($"Map generation failed for {map.Index}: {e}");
                        ok = false;
                    }
                }
                else
                {
                    progress?.Report(File.Exists(dds)
                        ? $"{dds}: DDS-only maps are not supported yet."
                        : $"No map data found ({uop} / {mul}).");
                    ok = false;
                }
            }

            _radPix0 = _radPix1 = _radPix2 = null;
            return ok;
        }

        private static void GenerateFromMul(MapEntry map, string path, bool isuop, bool detailed, IProgress<string> progress)
        {
            int width = map.Width;
            int height = map.Height;

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Invalid map size");

            var pixels = new byte[width * height * 4];
            var zBuffer = new sbyte[width * height];

            for (int i = 3; i < pixels.Length; i += 4)
                pixels[i] = 255;

            UOPFile[] files = null;
            if (isuop)
            {
                progress?.Report($"Unboxing {path}...");
                files = ReadUOPFile(path, Path.GetFileName(path).Replace(".uop", string.Empty).ToLowerInvariant());
                progress?.Report($"{path} unboxed.");
            }

            progress?.Report("Reading pixels...");

            string staidxPath = Path.Combine(Global.UOPath, $"staidx{map.Index}.mul");
            string staticsPath = Path.Combine(Global.UOPath, $"statics{map.Index}.mul");

            using (var mapReader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            using (var staidxReader = new BinaryReader(File.Open(staidxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            using (var staticsReader = new BinaryReader(File.Open(staticsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                int w = 0;
                int h = 0;

                while (w < width)
                {
                    long offset = (w / 8 * (height >> 3) + h / 8) * 196 + 4;

                    if (isuop)
                        offset = CalculateUOPOffset(files, mapReader, offset);

                    mapReader.BaseStream.Seek(offset, SeekOrigin.Begin);

                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            ushort hue = mapReader.ReadUInt16();
                            sbyte z = mapReader.ReadSByte();

                            int px = w + x;
                            int py = h + y;

                            if (px < width && py < height && hue < _radPix0.Length)
                            {
                                int idx = (py * width + px) * 4;
                                pixels[idx + 2] = (byte) (_radPix0[hue] * 255f); // R
                                pixels[idx + 1] = (byte) (_radPix1[hue] * 255f); // G
                                pixels[idx + 0] = (byte) (_radPix2[hue] * 255f); // B
                                zBuffer[py * width + px] = z;
                            }
                        }
                    }

                    uint start = staidxReader.ReadUInt32();
                    uint end = staidxReader.ReadUInt32();
                    staidxReader.ReadUInt32(); // unknown

                    if (start != 0xFFFFFFFF)
                    {
                        staticsReader.BaseStream.Seek(start, SeekOrigin.Begin);

                        while (end > 0)
                        {
                            ushort hue = (ushort) (staticsReader.ReadUInt16() + 0x4000);
                            int x = staticsReader.ReadByte();
                            int y = staticsReader.ReadByte();
                            sbyte z = staticsReader.ReadSByte();
                            staticsReader.ReadUInt16(); // unknown

                            int px = w + x;
                            int py = h + y;

                            if (px < width && py < height && hue < _radPix0.Length)
                            {
                                if (z >= zBuffer[py * width + px])
                                {
                                    ushort p0 = (ushort) (_radPix0[hue] * 255f);
                                    ushort p1 = (ushort) (_radPix1[hue] * 255f);
                                    ushort p2 = (ushort) (_radPix2[hue] * 255f);

                                    if (p0 > 15 || p1 > 15 || p2 > 15)
                                    {
                                        int idx = (py * width + px) * 4;
                                        pixels[idx + 2] = (byte) p0;
                                        pixels[idx + 1] = (byte) p1;
                                        pixels[idx + 0] = (byte) p2;
                                        zBuffer[py * width + px] = z;
                                    }
                                }
                            }

                            end -= 7;
                        }
                    }

                    h += 8;
                    if (h < height)
                        continue;

                    h = 0;
                    w += 8;
                }

                if (detailed)
                {
                    for (h = 1; h < height - 1; h++)
                    {
                        for (w = 1; w < width - 1; w++)
                        {
                            sbyte z = zBuffer[h * width + w];
                            sbyte ss = zBuffer[(h + 1) * width + (w - 1)];

                            ushort p0;
                            ushort p1;
                            ushort p2;
                            int di = ((h + 1) * width + (w + 1)) * 4;

                            if (z < ss)
                            {
                                p0 = (ushort) (pixels[di + 2] * 80 / 100);
                                p1 = (ushort) (pixels[di + 1] * 80 / 100);
                                p2 = (ushort) (pixels[di + 0] * 80 / 100);
                            }
                            else if (z > ss)
                            {
                                p0 = (ushort) (pixels[di + 2] * 100 / 80);
                                p1 = (ushort) (pixels[di + 1] * 100 / 80);
                                p2 = (ushort) (pixels[di + 0] * 100 / 80);
                            }
                            else
                                continue;

                            if (p0 > 255) p0 = 255;
                            if (p1 > 255) p1 = 255;
                            if (p2 > 255) p2 = 255;

                            pixels[di + 2] = (byte) p0;
                            pixels[di + 1] = (byte) p1;
                            pixels[di + 0] = (byte) p2;
                        }
                    }
                }
            }

            progress?.Report("Pixels read.");
            progress?.Report("Saving on /Maps ...");

            using (var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque))
            {
                Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

                string outPath = Path.Combine(_pathMaps.FullName, (detailed ? "2D" : "") + "map" + map.Index + ".png");

                using (SKImage image = SKImage.FromBitmap(bitmap))
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (FileStream fs = File.Create(outPath))
                    data.SaveTo(fs);

                progress?.Report("Saved " + outPath);
            }
        }

        private static bool ReadRadarcol(IProgress<string> progress)
        {
            string radCol = Path.Combine(Global.UOPath ?? string.Empty, "radarcol.mul");

            progress?.Report("Checking for 'radarcol.mul'...");
            if (!File.Exists(radCol))
            {
                progress?.Report("'radarcol.mul' not found.");
                return false;
            }

            progress?.Report("'radarcol.mul' found!");

            using (var reader = new BinaryReader(File.Open(radCol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                int length = (int) reader.BaseStream.Length / 2;

                _radPix0 = new float[length];
                _radPix1 = new float[length];
                _radPix2 = new float[length];

                for (int i = 0; i < length; i++)
                {
                    ushort pix = (ushort) (reader.ReadByte() | (reader.ReadByte() << 8));
                    _radPix0[i] = ((pix >> 10) & 31) / 31f;
                    _radPix1[i] = ((pix >> 5) & 31) / 31f;
                    _radPix2[i] = (pix & 31) / 31f;
                }
            }

            return true;
        }

        private static UOPFile[] ReadUOPFile(string path, string pattern)
        {
            using (var reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                reader.BaseStream.Seek(0, SeekOrigin.Begin);

                if (reader.ReadUInt32() != 0x50594D)
                    throw new ArgumentException("Bad UOP file.");

                reader.ReadInt64();
                long nextBlock = reader.ReadInt64();
                reader.ReadInt32();
                int count = reader.ReadInt32();

                var files = new UOPFile[count];
                var hashes = new Dictionary<ulong, int>();

                for (int i = 0; i < count; i++)
                {
                    string file = string.Format("build/{0}/{1:D8}.dat", pattern, i);
                    ulong hash = HashFileName(file);
                    if (!hashes.ContainsKey(hash))
                        hashes.Add(hash, i);
                }

                reader.BaseStream.Seek(nextBlock, SeekOrigin.Begin);

                do
                {
                    int filesCount = reader.ReadInt32();
                    nextBlock = reader.ReadInt64();

                    for (int i = 0; i < filesCount; i++)
                    {
                        long offset = reader.ReadInt64();
                        int headerLength = reader.ReadInt32();
                        int compressedLength = reader.ReadInt32();
                        int decompressedLength = reader.ReadInt32();
                        ulong hash = reader.ReadUInt64();
                        reader.ReadInt32();
                        short flag = reader.ReadInt16();

                        int length = flag == 1 ? compressedLength : decompressedLength;
                        if (offset == 0)
                            continue;

                        if (hashes.TryGetValue(hash, out int idx))
                        {
                            if (idx < 0 || idx > files.Length)
                                throw new IndexOutOfRangeException("hashes dictionary and files collection have different count of entries!");

                            files[idx] = new UOPFile(offset + headerLength, length);
                        }
                        else
                            throw new ArgumentException($"File with hash 0x{hash:X8} was not found in hashes dictionary! EA Mythic changed UOP format!");
                    }
                } while (reader.BaseStream.Seek(nextBlock, SeekOrigin.Begin) != 0);

                return files;
            }
        }

        private static long CalculateUOPOffset(UOPFile[] files, BinaryReader reader, long offset)
        {
            long pos = 0;

            for (int i = 0; i < files.Length; i++)
            {
                long currpos = pos + files[i].Length;
                if (offset < currpos)
                    return files[i].Offset + (offset - pos);
                pos = currpos;
            }

            return reader.BaseStream.Length;
        }

        private static ulong HashFileName(string s)
        {
            uint eax, ecx, edx, ebx, esi, edi;

            eax = ecx = edx = ebx = esi = edi = 0;
            ebx = edi = esi = (uint) s.Length + 0xDEADBEEF;

            int i = 0;

            for (i = 0; i + 12 < s.Length; i += 12)
            {
                edi = (uint) ((s[i + 7] << 24) | (s[i + 6] << 16) | (s[i + 5] << 8) | s[i + 4]) + edi;
                esi = (uint) ((s[i + 11] << 24) | (s[i + 10] << 16) | (s[i + 9] << 8) | s[i + 8]) + esi;
                edx = (uint) ((s[i + 3] << 24) | (s[i + 2] << 16) | (s[i + 1] << 8) | s[i]) - esi;

                edx = (edx + ebx) ^ (esi >> 28) ^ (esi << 4);
                esi += edi;
                edi = (edi - edx) ^ (edx >> 26) ^ (edx << 6);
                edx += esi;
                esi = (esi - edi) ^ (edi >> 24) ^ (edi << 8);
                edi += edx;
                ebx = (edx - esi) ^ (esi >> 16) ^ (esi << 16);
                esi += edi;
                edi = (edi - ebx) ^ (ebx >> 13) ^ (ebx << 19);
                ebx += esi;
                esi = (esi - edi) ^ (edi >> 28) ^ (edi << 4);
                edi += ebx;
            }

            if (s.Length - i > 0)
            {
                switch (s.Length - i)
                {
                    case 12:
                        esi += (uint) s[i + 11] << 24;
                        goto case 11;
                    case 11:
                        esi += (uint) s[i + 10] << 16;
                        goto case 10;
                    case 10:
                        esi += (uint) s[i + 9] << 8;
                        goto case 9;
                    case 9:
                        esi += s[i + 8];
                        goto case 8;
                    case 8:
                        edi += (uint) s[i + 7] << 24;
                        goto case 7;
                    case 7:
                        edi += (uint) s[i + 6] << 16;
                        goto case 6;
                    case 6:
                        edi += (uint) s[i + 5] << 8;
                        goto case 5;
                    case 5:
                        edi += s[i + 4];
                        goto case 4;
                    case 4:
                        ebx += (uint) s[i + 3] << 24;
                        goto case 3;
                    case 3:
                        ebx += (uint) s[i + 2] << 16;
                        goto case 2;
                    case 2:
                        ebx += (uint) s[i + 1] << 8;
                        goto case 1;
                    case 1:
                        ebx += s[i];
                        break;
                }

                esi = (esi ^ edi) - ((edi >> 18) ^ (edi << 14));
                ecx = (esi ^ ebx) - ((esi >> 21) ^ (esi << 11));
                edi = (edi ^ ecx) - ((ecx >> 7) ^ (ecx << 25));
                esi = (esi ^ edi) - ((edi >> 16) ^ (edi << 16));
                edx = (esi ^ ecx) - ((esi >> 28) ^ (esi << 4));
                edi = (edi ^ edx) - ((edx >> 18) ^ (edx << 14));
                eax = (esi ^ edi) - ((edi >> 8) ^ (edi << 24));

                return ((ulong) edi << 32) | eax;
            }

            return ((ulong) esi << 32) | eax;
        }

        private readonly struct UOPFile
        {
            public UOPFile(long offset, int length)
            {
                Offset = offset;
                Length = length;
            }

            public readonly long Offset;
            public readonly int Length;
        }
    }
}
