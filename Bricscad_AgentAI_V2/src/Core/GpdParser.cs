using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bricscad_AgentAI_V2.Core
{
    public class GpdMediaInfo
    {
        public string OptionName { get; set; }
        public string PrintSchemaKeyword { get; set; }
        public string ResourceId { get; set; }
        public double WidthUnits { get; set; }
        public double HeightUnits { get; set; }
        public double UnitsPerInch { get; set; }

        public double WidthMm => WidthUnits / UnitsPerInch * 25.4;
        public double HeightMm => HeightUnits / UnitsPerInch * 25.4;

        public bool IsStandardSize { get; set; }
        public bool IsRollPaper { get; set; }

        public string Category
        {
            get
            {
                if (IsRollPaper) return "Roll";
                if (OptionName.StartsWith("ISOA", StringComparison.OrdinalIgnoreCase) ||
                    OptionName == "A4" || OptionName.StartsWith("NorthAmericaArchitecture")) return "ISO/Arch";
                if (OptionName.StartsWith("ISOB", StringComparison.OrdinalIgnoreCase) ||
                    OptionName.StartsWith("JISB", StringComparison.OrdinalIgnoreCase)) return "B-series";
                if (OptionName.StartsWith("NorthAmerica", StringComparison.OrdinalIgnoreCase) ||
                    OptionName == "LETTER" || OptionName == "11X14" || OptionName == "F") return "ANSI";
                if (OptionName == "CUSTOMSIZE") return "Custom";
                return "Other";
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0,-40} {1,7:F1}x{2,7:F1}mm ({3})",
                OptionName, WidthMm, HeightMm, Category);
        }
    }

    public class GpdCustomSize
    {
        public double MinWidthUnits { get; set; }
        public double MinHeightUnits { get; set; }
        public double MaxWidthUnits { get; set; }
        public double MaxHeightUnits { get; set; }
        public double UnitsPerInch { get; set; }

        public double MinWidthMm => MinWidthUnits / UnitsPerInch * 25.4;
        public double MinHeightMm => MinHeightUnits / UnitsPerInch * 25.4;
        public double MaxWidthMm => MaxWidthUnits / UnitsPerInch * 25.4;
        public double MaxHeightMm => MaxHeightUnits / UnitsPerInch * 25.4;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Custom: {0:F1}x{1:F1}mm min, {2:F1}x{3:F1}mm max",
                MinWidthMm, MinHeightMm, MaxWidthMm, MaxHeightMm);
        }
    }

    public class GpdInfo
    {
        public string FilePath { get; set; }
        public bool ParseSucceeded { get; set; }
        public string ParseError { get; set; }
        public string ModelName { get; set; }
        public double UnitsPerInch { get; set; } = 1200.0;
        public List<GpdMediaInfo> MediaFormats { get; } = new List<GpdMediaInfo>();
        public GpdCustomSize CustomSize { get; set; }

        public List<GpdMediaInfo> StandardFormats =>
            MediaFormats.Where(m => !m.IsRollPaper && m.OptionName != "CUSTOMSIZE").ToList();

        public List<GpdMediaInfo> RollFormats =>
            MediaFormats.Where(m => m.IsRollPaper).ToList();
    }

    public static class GpdParser
    {
        private static readonly Regex MasterUnitsRegex = new Regex(
            @"\*MasterUnits:\s*PAIR\(\s*(\d+)\s*,\s*(\d+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ModelNameRegex = new Regex(
            @"^\*ModelName:\s*""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PageDimRegex = new Regex(
            @"\*PageDimensions:\s*PAIR\(\s*(\d+)\s*,\s*(\d+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PrintableAreaRegex = new Regex(
            @"\*PrintableArea:\s*PAIR\(\s*(\d+)\s*,\s*(\d+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OptionStartRegex = new Regex(
            @"^\s*\*Option:\s+(\S+)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PrintSchemaRegex = new Regex(
            @"\*PrintSchemaKeywordMap:\s*""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RcNameRegex = new Regex(
            @"\*rcNameID:\s*=\s*(\S+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MinSizeRegex = new Regex(
            @"\*MinSize:\s*PAIR\(\s*(\d+)\s*,\s*(\d+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MaxSizeRegex = new Regex(
            @"\*MaxSize:\s*PAIR\(\s*(\d+)\s*,\s*(\d+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static GpdInfo Parse(string filePath)
        {
            var info = new GpdInfo { FilePath = filePath };

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

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception ex)
            {
                info.ParseError = "Blad odczytu pliku: " + ex.Message;
                return info;
            }

            try
            {
                ParseGpdContent(lines, info);
                info.ParseSucceeded = true;
            }
            catch (Exception ex)
            {
                info.ParseError = "Blad parsowania GPD: " + ex.Message;
            }

            return info;
        }

        private static void ParseGpdContent(string[] lines, GpdInfo info)
        {
            bool inPaperSize = false;
            int paperSizeBraceDepth = 0;
            GpdMediaInfo currentMedia = null;
            int currentMediaBraceDepth = 0;

            bool inCustomSize = false;
            int customSizeBraceDepth = 0;
            GpdCustomSize currentCustom = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (!inPaperSize)
                {
                    var masterMatch = MasterUnitsRegex.Match(line);
                    if (masterMatch.Success)
                    {
                        int x = int.Parse(masterMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                        info.UnitsPerInch = x > 0 ? x : 1200;
                        continue;
                    }

                    var modelMatch = ModelNameRegex.Match(line);
                    if (modelMatch.Success)
                    {
                        info.ModelName = modelMatch.Groups[1].Value;
                        continue;
                    }

                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("*Feature: PaperSize", StringComparison.OrdinalIgnoreCase))
                    {
                        inPaperSize = true;
                        paperSizeBraceDepth = 0;
                        continue;
                    }
                }
                else
                {
                    paperSizeBraceDepth += CountBraces(line, '{');
                    paperSizeBraceDepth -= CountBraces(line, '}');

                    if (currentMedia == null)
                    {
                        var optionMatch = OptionStartRegex.Match(line);
                        if (optionMatch.Success)
                        {
                            string optionName = optionMatch.Groups[1].Value;
                            if (optionName == "CUSTOMSIZE")
                            {
                                inCustomSize = true;
                                customSizeBraceDepth = 0;
                                currentCustom = new GpdCustomSize { UnitsPerInch = info.UnitsPerInch };
                                continue;
                            }
                            currentMedia = new GpdMediaInfo
                            {
                                OptionName = optionName,
                                UnitsPerInch = info.UnitsPerInch
                            };
                            currentMediaBraceDepth = 0;
                            continue;
                        }
                    }
                    else
                    {
                        currentMediaBraceDepth += CountBraces(line, '{');
                        currentMediaBraceDepth -= CountBraces(line, '}');

                        if (line.IndexOf("PaperDimensions", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            line.IndexOf("PrintableArea", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var dimMatch = PageDimRegex.Match(line);
                            if (!dimMatch.Success)
                            {
                                dimMatch = PrintableAreaRegex.Match(line);
                            }
                            if (dimMatch.Success)
                            {
                                currentMedia.WidthUnits = int.Parse(dimMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                                currentMedia.HeightUnits = int.Parse(dimMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                            }
                        }
                        else
                        {
                            var psMatch = PrintSchemaRegex.Match(line);
                            if (psMatch.Success)
                            {
                                currentMedia.PrintSchemaKeyword = psMatch.Groups[1].Value;
                            }
                            else
                            {
                                var rcMatch = RcNameRegex.Match(line);
                                if (rcMatch.Success)
                                {
                                    currentMedia.ResourceId = rcMatch.Groups[1].Value;
                                }
                            }
                        }

                        if (currentMediaBraceDepth == 0 && line.TrimEnd().EndsWith("}"))
                        {
                            currentMedia.IsStandardSize = !IsRollName(currentMedia.OptionName);
                            currentMedia.IsRollPaper = !currentMedia.IsStandardSize;
                            info.MediaFormats.Add(currentMedia);
                            currentMedia = null;
                        }
                    }

                    if (inCustomSize && currentCustom != null)
                    {
                        customSizeBraceDepth += CountBraces(line, '{');
                        customSizeBraceDepth -= CountBraces(line, '}');

                        var minMatch = MinSizeRegex.Match(line);
                        if (minMatch.Success)
                        {
                            currentCustom.MinWidthUnits = int.Parse(minMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                            currentCustom.MinHeightUnits = int.Parse(minMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                        }

                        var maxMatch = MaxSizeRegex.Match(line);
                        if (maxMatch.Success)
                        {
                            currentCustom.MaxWidthUnits = int.Parse(maxMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                            currentCustom.MaxHeightUnits = int.Parse(maxMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                        }

                        if (customSizeBraceDepth == 0 && line.TrimEnd().EndsWith("}"))
                        {
                            info.CustomSize = currentCustom;
                            currentCustom = null;
                            inCustomSize = false;
                        }
                    }

                    if (paperSizeBraceDepth == 0 && line.TrimEnd().EndsWith("}"))
                    {
                        inPaperSize = false;
                        if (currentMedia != null)
                        {
                            currentMedia.IsStandardSize = !IsRollName(currentMedia.OptionName);
                            currentMedia.IsRollPaper = !currentMedia.IsStandardSize;
                            info.MediaFormats.Add(currentMedia);
                            currentMedia = null;
                        }
                        if (currentCustom != null)
                        {
                            info.CustomSize = currentCustom;
                            currentCustom = null;
                            inCustomSize = false;
                        }
                    }
                }
            }
        }

        private static bool IsRollName(string optionName)
        {
            if (string.IsNullOrEmpty(optionName)) return false;
            return optionName.EndsWith("Roll", StringComparison.OrdinalIgnoreCase) ||
                   optionName.IndexOf("RollPaper", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountBraces(string line, char brace)
        {
            int count = 0;
            foreach (char c in line)
            {
                if (c == brace) count++;
            }
            return count;
        }

        public static string FindHpGpdForDevice(string pc3DeviceName, string friendlyName, string winDriverName)
        {
            if (string.IsNullOrWhiteSpace(pc3DeviceName)) return null;

            string searchText = string.Format("{0} {1} {2}",
                pc3DeviceName ?? "",
                friendlyName ?? "",
                winDriverName ?? "").ToUpperInvariant();

            string[] gpdSearchDirs;
            try
            {
                var dirs = new List<string>
                {
                    @"C:\Windows\System32\DriverStore\FileRepository",
                    @"C:\Windows\System32\spool\drivers\x64\PCC",
                    @"C:\Windows\System32\spool\drivers\x64\3"
                };

                if (Directory.Exists(@"C:\Windows\System32\DriverStore\FileRepository"))
                {
                    foreach (string sub in Directory.GetDirectories(@"C:\Windows\System32\DriverStore\FileRepository", "hpi*"))
                    {
                        dirs.Add(sub);
                    }
                }

                gpdSearchDirs = dirs.ToArray();
            }
            catch
            {
                gpdSearchDirs = new[] {
                    @"C:\Windows\System32\DriverStore\FileRepository",
                    @"C:\Windows\System32\spool\drivers\x64\PCC",
                    @"C:\Windows\System32\spool\drivers\x64\3"
                };
            }

            var candidates = new List<(string Path, int Score)>();
            foreach (string dir in gpdSearchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                IEnumerable<string> gpdFiles;
                try
                {
                    gpdFiles = Directory.GetFiles(dir, "hpi*.gpd", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }
                foreach (string gpd in gpdFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(gpd);
                    int score = ScoreGpdMatch(fileName, searchText);
                    if (score > 0) candidates.Add((gpd, score));
                }
            }

            if (candidates.Count == 0) return null;
            return candidates.OrderByDescending(c => c.Score).First().Path;
        }

        private static int ScoreGpdMatch(string gpdFileName, string searchText)
        {
            string upper = gpdFileName.ToUpperInvariant();
            int score = 0;

            if (searchText.Contains("T120") && upper.Contains("T120") && !upper.Contains("MFP") && !upper.Contains("PS"))
                score += 100;
            if (searchText.Contains("T125") && upper.Contains("T125")) score += 100;
            if (searchText.Contains("T130") && upper.Contains("T130")) score += 100;
            if (searchText.Contains("T520") && upper.Contains("T520")) score += 100;
            if (searchText.Contains("T525") && upper.Contains("T525")) score += 100;
            if (searchText.Contains("T530") && upper.Contains("T530")) score += 100;
            if (searchText.Contains("T630") && upper.Contains("T630")) score += 100;
            if (searchText.Contains("T650") && upper.Contains("T650")) score += 100;
            if (searchText.Contains("T730") && upper.Contains("T730")) score += 100;
            if (searchText.Contains("T790") && upper.Contains("T790")) score += 100;
            if (searchText.Contains("T795") && upper.Contains("T795")) score += 100;
            if (searchText.Contains("T830") && upper.Contains("T830")) score += 100;
            if (searchText.Contains("T850") && upper.Contains("T850") && !upper.Contains("MFP")) score += 100;
            if (searchText.Contains("T920") && upper.Contains("T920")) score += 100;
            if (searchText.Contains("T930") && upper.Contains("T930")) score += 100;
            if (searchText.Contains("T940") && upper.Contains("T940")) score += 100;
            if (searchText.Contains("T950") && upper.Contains("T950")) score += 100;
            if (searchText.Contains("T1500") && upper.Contains("T1500")) score += 100;
            if (searchText.Contains("T1530") && upper.Contains("T1530")) score += 100;
            if (searchText.Contains("T1600") && upper.Contains("T1600")) score += 100;
            if (searchText.Contains("T1700") && upper.Contains("T1700")) score += 100;
            if (searchText.Contains("T2300") && upper.Contains("T2300")) score += 100;
            if (searchText.Contains("T2500") && upper.Contains("T2500")) score += 100;
            if (searchText.Contains("T2530") && upper.Contains("T2530")) score += 100;
            if (searchText.Contains("T2600") && upper.Contains("T2600")) score += 100;
            if (searchText.Contains("T3500") && upper.Contains("T3500")) score += 100;
            if (searchText.Contains("T7100") && upper.Contains("T7100")) score += 100;
            if (searchText.Contains("T7200") && upper.Contains("T7200")) score += 100;
            if (searchText.Contains("Z2") && upper.Contains("Z2")) score += 90;
            if (searchText.Contains("Z3") && upper.Contains("Z3")) score += 90;
            if (searchText.Contains("Z5") && upper.Contains("Z5")) score += 90;
            if (searchText.Contains("Z6") && upper.Contains("Z6")) score += 90;
            if (searchText.Contains("Z9") && upper.Contains("Z9")) score += 90;
            if (searchText.Contains("XL3600") && upper.Contains("XL3600")) score += 80;
            if (searchText.Contains("XL3800") && upper.Contains("XL3800")) score += 80;

            if (searchText.Contains("36") && (upper.Contains("36") || upper.Contains("36IN"))) score += 20;
            if (searchText.Contains("24") && (upper.Contains("24") || upper.Contains("24IN"))) score += 20;
            if (searchText.Contains("44") && (upper.Contains("44") || upper.Contains("44IN"))) score += 20;

            return score;
        }
    }
}