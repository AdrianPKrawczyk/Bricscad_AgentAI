using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Bricscad_AgentAI_V2.Core
{
    public class Pc3MediaInfo
    {
        public string Name { get; set; }
        public int Group { get; set; }
        public bool LandscapeMode { get; set; }
        public double MediaBoundsUrx { get; set; }
        public double MediaBoundsUry { get; set; }
        public double PrintableBoundsLlx { get; set; }
        public double PrintableBoundsLly { get; set; }
        public double PrintableBoundsUrx { get; set; }
        public double PrintableBoundsUry { get; set; }
        public double PrintableArea { get; set; }
        public bool Dimensional { get; set; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} ({1:F2}x{2:F2}mm, printable {3:F2}x{4:F2}mm)",
                Name, MediaBoundsUrx, MediaBoundsUry,
                PrintableBoundsUrx - PrintableBoundsLlx,
                PrintableBoundsUry - PrintableBoundsLly);
        }
    }

    public class Pc3ResolutionInfo
    {
        public string Name { get; set; }
        public double PhysResolutionX { get; set; }
        public double PhysResolutionY { get; set; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} ({1:F0}x{2:F0} dpi)",
                Name, PhysResolutionX, PhysResolutionY);
        }
    }

    public class Pc3Info
    {
        public string FilePath { get; set; }
        public bool ParseSucceeded { get; set; }
        public string ParseError { get; set; }

        public string DriverPath { get; set; }
        public string DriverVersion { get; set; }
        public string DriverTagline { get; set; }
        public string CanonicalFamily { get; set; }
        public string CanonicalModel { get; set; }
        public string LocalizedFamily { get; set; }
        public string LocalizedModel { get; set; }
        public string WinDriverName { get; set; }
        public string WinDeviceName { get; set; }
        public string ShortNetName { get; set; }
        public string FriendlyNetName { get; set; }
        public int ToolkitVersion { get; set; }
        public int DriverType { get; set; }

        public string SelectionMethod { get; set; }
        public Pc3MediaInfo SelectedMedia { get; set; }
        public Pc3ResolutionInfo Resolution { get; set; }
        public int NumberOfCopies { get; set; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} [{1}] {2}",
                FriendlyNetName ?? WinDriverName ?? CanonicalModel ?? "(unknown)",
                DriverPath ?? "?",
                SelectedMedia ?? (object)"(no media)");
        }
    }

    public static class Pc3Parser
    {
        private const int HeaderSize = 60;
        private const int ZlibHeaderSize = 2;

        public static Pc3Info Parse(string filePath)
        {
            var info = new Pc3Info { FilePath = filePath };

            if (string.IsNullOrWhiteSpace(filePath))
            {
                info.ParseError = "Sciezka pliku jest pusta.";
                return info;
            }

            if (!File.Exists(filePath))
            {
                info.ParseError = "Plik nie istnieje: " + filePath;
                return info;
            }

            string decompressed;
            try
            {
                decompressed = DecompressPc3(filePath);
            }
            catch (Exception ex)
            {
                info.ParseError = "Blad dekompresji PC3: " + ex.Message;
                return info;
            }

            try
            {
                ParseNodes(decompressed, info);
                info.ParseSucceeded = true;
            }
            catch (Exception ex)
            {
                info.ParseError = "Blad parsowania struktury PC3: " + ex.Message;
            }

            return info;
        }

        private static string DecompressPc3(string filePath)
        {
            byte[] raw = File.ReadAllBytes(filePath);
            if (raw.Length < HeaderSize + ZlibHeaderSize)
            {
                throw new InvalidDataException(
                    "Plik za krotki (mniej niz " + (HeaderSize + ZlibHeaderSize) + " bajtow).");
            }

            int payloadOffset = HeaderSize + ZlibHeaderSize;
            if (raw.Length <= payloadOffset)
            {
                throw new InvalidDataException("Brak danych po naglowku zlib.");
            }

            using (var input = new MemoryStream(raw, payloadOffset, raw.Length - payloadOffset, false))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(deflate, Encoding.Default))
            {
                return reader.ReadToEnd();
            }
        }

        private static void ParseNodes(string content, Pc3Info info)
        {
            var meta = ExtractBlock(content, "meta");
            if (meta != null)
            {
                info.DriverPath = ExtractValue(meta, "driver_pathname");
                info.DriverVersion = ExtractValue(meta, "driver_version");
                info.DriverTagline = ExtractValue(meta, "driver_tag_line");
                info.CanonicalFamily = ExtractValue(meta, "canonical_family_name");
                info.CanonicalModel = ExtractValue(meta, "canonical_model_name");
                info.LocalizedFamily = ExtractValue(meta, "localized_family_name");
                info.LocalizedModel = ExtractValue(meta, "localized_model_name");
                info.WinDriverName = ExtractValue(meta, "win_driver_name");
                info.WinDeviceName = ExtractValue(meta, "win_device_name");
                info.ShortNetName = ExtractValue(meta, "short_net_name");
                info.FriendlyNetName = ExtractValue(meta, "friendly_net_name");
                info.ToolkitVersion = ParseIntSafe(ExtractValue(meta, "toolkit_version"));
                info.DriverType = ParseIntSafe(ExtractValue(meta, "driver_type"));
            }

            var media = ExtractBlock(content, "media");
            if (media != null)
            {
                info.SelectionMethod = ExtractValue(media, "selection_method");
                info.NumberOfCopies = ParseIntSafe(ExtractValue(media, "number_of_copies"));

                var size = ExtractBlock(media, "size");
                if (size != null)
                {
                    info.SelectedMedia = new Pc3MediaInfo
                    {
                        Name = ExtractValue(size, "name"),
                        Group = ParseIntSafe(ExtractValue(size, "group")),
                        LandscapeMode = ParseBoolSafe(ExtractValue(size, "landscape_mode"))
                    };

                    var mediaDesc = ExtractBlock(size, "media_description");
                    if (mediaDesc != null)
                    {
                        info.SelectedMedia.PrintableBoundsLlx = ParseDoubleSafe(ExtractValue(mediaDesc, "printable_bounds_llx"));
                        info.SelectedMedia.PrintableBoundsLly = ParseDoubleSafe(ExtractValue(mediaDesc, "printable_bounds_lly"));
                        info.SelectedMedia.PrintableBoundsUrx = ParseDoubleSafe(ExtractValue(mediaDesc, "printable_bounds_urx"));
                        info.SelectedMedia.PrintableBoundsUry = ParseDoubleSafe(ExtractValue(mediaDesc, "printable_bounds_ury"));
                        info.SelectedMedia.PrintableArea = ParseDoubleSafe(ExtractValue(mediaDesc, "printable_area"));
                        info.SelectedMedia.Dimensional = ParseBoolSafe(ExtractValue(mediaDesc, "dimensional"));

                        var mediaBounds = ExtractBlock(mediaDesc, "media_bounds");
                        if (mediaBounds != null)
                        {
                            info.SelectedMedia.MediaBoundsUrx = ParseDoubleSafe(ExtractValue(mediaBounds, "urx"));
                            info.SelectedMedia.MediaBoundsUry = ParseDoubleSafe(ExtractValue(mediaBounds, "ury"));
                        }
                    }
                }
            }

            var resColor = ExtractBlock(content, "res_color_mem");
            if (resColor != null)
            {
                var resolution = ExtractBlock(resColor, "resolution");
                if (resolution != null)
                {
                    info.Resolution = new Pc3ResolutionInfo
                    {
                        Name = ExtractValue(resolution, "name"),
                        PhysResolutionX = ParseDoubleSafe(ExtractValue(resolution, "phys_resolution_x")),
                        PhysResolutionY = ParseDoubleSafe(ExtractValue(resolution, "phys_resolution_y"))
                    };
                }
            }
        }

        private static string ExtractBlock(string content, string blockName)
        {
            if (string.IsNullOrEmpty(content)) return null;

            string pattern = blockName + "{";
            int start = content.IndexOf(pattern, StringComparison.Ordinal);
            if (start < 0) return null;

            int bodyStart = start + pattern.Length;
            int depth = 1;
            int i = bodyStart;

            while (i < content.Length && depth > 0)
            {
                char c = content[i];
                if (c == '{') depth++;
                else if (c == '}') depth--;
                i++;
            }

            if (depth != 0) return null;
            return content.Substring(bodyStart, i - bodyStart - 1);
        }

        private static string ExtractValue(string blockContent, string key)
        {
            if (string.IsNullOrEmpty(blockContent)) return null;

            int idx = blockContent.IndexOf(key + "=", StringComparison.Ordinal);
            if (idx < 0) return null;

            int valStart = idx + key.Length + 1;
            if (valStart >= blockContent.Length) return null;

            int lineEnd = blockContent.IndexOf('\n', valStart);
            if (lineEnd < 0) lineEnd = blockContent.Length;

            string raw = blockContent.Substring(valStart, lineEnd - valStart).Trim();

            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            {
                return raw.Substring(1, raw.Length - 2);
            }
            if (raw.Length >= 1 && raw[0] == '"')
            {
                return raw.Substring(1);
            }
            return raw;
        }

        private static int ParseIntSafe(string value)
        {
            int result;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                return result;
            return 0;
        }

        private static double ParseDoubleSafe(string value)
        {
            double result;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return result;
            return 0.0;
        }

        private static bool ParseBoolSafe(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
        }
    }
}