using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using Bricscad_AgentAI_V2.Models.Session;

namespace Bricscad_AgentAI_V2.Core
{
    public class VisionTileSet
    {
        public bool IsTiled { get; set; }
        public string Mode { get; set; }
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int TileCount => Tiles?.Count ?? 0;
        public int EffectiveTileMaxDim { get; set; }
        public int Overlap { get; set; }
        public double MaxTileAspectRatio { get; set; }
        public List<VisionImageTileContext> Tiles { get; set; } = new List<VisionImageTileContext>();

        public string BuildSpatialPrompt()
        {
            if (!IsTiled || Tiles == null || Tiles.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"Obraz zrodlowy ma rozmiar {OriginalWidth}x{OriginalHeight} px.");
            sb.AppendLine($"Obraz zostal podzielony adaptacyjnie na siatke {Rows} wierszy x {Columns} kolumn ({TileCount} kafelkow).");
            sb.AppendLine($"Kafelki moga byc prostokatne; zachowuja rzeczywiste piksele bez rozciagania. Zakladka nominalna: {Overlap} px. Efektywny maksymalny bok kafelka: {EffectiveTileMaxDim} px.");
            sb.AppendLine("Polozenie kafelkow i ich sasiedzi:");

            foreach (var tile in Tiles.OrderBy(t => t.Row).ThenBy(t => t.Column))
            {
                sb.Append("* Plik [").Append(tile.FileName).Append("] -> Pozycja: ");
                sb.Append(GetLocationLabel(tile.Row, tile.Column, Rows, Columns));
                sb.Append($"; source box: x={tile.SourceX}..{tile.SourceX + tile.SourceWidth}, y={tile.SourceY}..{tile.SourceY + tile.SourceHeight}; ");
                sb.Append("rozmiar kafelka: ").Append(tile.SourceWidth).Append("x").Append(tile.SourceHeight).Append(" px; ");
                sb.Append("sasiaduje z: ").Append(GetNeighbors(tile.Row, tile.Column, Rows, Columns));
                sb.AppendLine(".");
            }

            sb.AppendLine();
            sb.AppendLine("Wszystkie linie, opisy, rury, wymiary i elementy instalacji przechodzace przez krawedzie kafelkow lacza sie w sposob ciagly.");
            sb.AppendLine("Analizuj kafelki jako jeden spojny arkusz techniczny. Przy pytaniach o tabliczke, inwestora, adres, legende lub male napisy sprawdz szczegolnie kafelki skrajne i narozne.");
            return sb.ToString();
        }

        private static string GetLocationLabel(int row, int column, int rows, int columns)
        {
            var parts = new List<string>();
            if (row == 1) parts.Add("GORA");
            else if (row == rows) parts.Add("DOL");
            else parts.Add("wiersz srodkowy " + row);

            if (column == 1) parts.Add("LEWA");
            else if (column == columns) parts.Add("PRAWA");
            else parts.Add("kolumna srodkowa " + column);

            return string.Join(" i ", parts);
        }

        private static string GetNeighbors(int row, int column, int rows, int columns)
        {
            var neighbors = new List<string>();
            if (row > 1) neighbors.Add($"fragment_R{row - 1}_C{column}.png na gorze");
            if (row < rows) neighbors.Add($"fragment_R{row + 1}_C{column}.png na dole");
            if (column > 1) neighbors.Add($"fragment_R{row}_C{column - 1}.png po lewej");
            if (column < columns) neighbors.Add($"fragment_R{row}_C{column + 1}.png po prawej");
            return neighbors.Count == 0 ? "brak - jedyny kafelek" : string.Join(", ", neighbors);
        }
    }

    public static class VisionOcrTiler
    {
        public static VisionTileSet CreateTiles(
            string imagePath,
            string outputDir,
            string mode,
            int tileMaxDim,
            int overlap,
            int maxTiles,
            double maxAspectRatio)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("Nie znaleziono obrazu do tilingu.", imagePath);

            Directory.CreateDirectory(outputDir);

            int safeTileMaxDim = Math.Max(512, Math.Min(tileMaxDim <= 0 ? 2100 : tileMaxDim, 8192));
            int safeOverlap = Math.Max(0, Math.Min(overlap < 0 ? 200 : overlap, safeTileMaxDim / 2));
            int safeMaxTiles = Math.Max(1, Math.Min(maxTiles <= 0 ? 16 : maxTiles, 64));
            double safeMaxAspect = Math.Max(1.0, Math.Min(maxAspectRatio <= 0 ? 2.0 : maxAspectRatio, 6.0));
            string normalizedMode = string.IsNullOrWhiteSpace(mode) ? "Auto" : mode;

            using (var image = Image.FromFile(imagePath))
            {
                int width = image.Width;
                int height = image.Height;
                bool shouldTile = ShouldTile(normalizedMode, width, height, safeTileMaxDim);

                var tileSet = new VisionTileSet
                {
                    IsTiled = shouldTile,
                    Mode = normalizedMode,
                    OriginalWidth = width,
                    OriginalHeight = height,
                    Overlap = safeOverlap,
                    MaxTileAspectRatio = safeMaxAspect
                };

                if (!shouldTile)
                {
                    tileSet.Rows = 1;
                    tileSet.Columns = 1;
                    tileSet.EffectiveTileMaxDim = Math.Max(width, height);
                    return tileSet;
                }

                var grid = ChooseGrid(width, height, safeTileMaxDim, safeOverlap, safeMaxTiles, safeMaxAspect);
                tileSet.Rows = grid.Rows;
                tileSet.Columns = grid.Columns;
                tileSet.EffectiveTileMaxDim = Math.Max(grid.TileWidth, grid.TileHeight);

                for (int r = 1; r <= grid.Rows; r++)
                {
                    for (int c = 1; c <= grid.Columns; c++)
                    {
                        int x = GetStart(c, grid.Columns, width, grid.TileWidth);
                        int y = GetStart(r, grid.Rows, height, grid.TileHeight);
                        int w = Math.Min(grid.TileWidth, width - x);
                        int h = Math.Min(grid.TileHeight, height - y);
                        string fileName = $"fragment_R{r}_C{c}.png";
                        string path = Path.Combine(outputDir, fileName);

                        using (var tile = new Bitmap(w, h))
                        using (var graphics = Graphics.FromImage(tile))
                        {
                            graphics.DrawImage(image, new Rectangle(0, 0, w, h), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
                            tile.Save(path, ImageFormat.Png);
                        }

                        tileSet.Tiles.Add(new VisionImageTileContext
                        {
                            Row = r,
                            Column = c,
                            FileName = fileName,
                            CachedPath = path,
                            SourceX = x,
                            SourceY = y,
                            SourceWidth = w,
                            SourceHeight = h
                        });
                    }
                }

                return tileSet;
            }
        }

        private static bool ShouldTile(string mode, int width, int height, int tileMaxDim)
        {
            if (string.Equals(mode, "Wylaczony", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(mode, "Zawsze", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase))
                return width > tileMaxDim || height > tileMaxDim || width * height > tileMaxDim * tileMaxDim;

            return width > tileMaxDim || height > tileMaxDim;
        }

        private static (int Rows, int Columns, int TileWidth, int TileHeight) ChooseGrid(
            int width,
            int height,
            int tileMaxDim,
            int overlap,
            int maxTiles,
            double maxAspectRatio)
        {
            var candidates = new List<(int Rows, int Columns, int TileWidth, int TileHeight, double Score)>();
            int allowedMax = (int)Math.Ceiling(tileMaxDim * 1.15);

            for (int rows = 1; rows <= maxTiles; rows++)
            {
                for (int columns = 1; columns <= maxTiles; columns++)
                {
                    int count = rows * columns;
                    if (count > maxTiles) continue;

                    int tileWidth = (int)Math.Ceiling((width + (columns - 1) * overlap) / (double)columns);
                    int tileHeight = (int)Math.Ceiling((height + (rows - 1) * overlap) / (double)rows);
                    int maxSide = Math.Max(tileWidth, tileHeight);
                    if (maxSide > allowedMax) continue;

                    double aspect = Math.Max(tileWidth / (double)tileHeight, tileHeight / (double)tileWidth);
                    if (aspect > maxAspectRatio) continue;

                    double targetArea = tileMaxDim * (double)tileMaxDim;
                    double areaScore = Math.Abs((tileWidth * (double)tileHeight) - targetArea) / targetArea;
                    double countPenalty = count * 0.08;
                    double orientationPenalty = Math.Abs((columns / (double)rows) - (width / (double)height)) * 0.04;
                    double sidePenalty = Math.Max(0, maxSide - tileMaxDim) / (double)tileMaxDim;
                    double score = countPenalty + areaScore + orientationPenalty + sidePenalty;
                    candidates.Add((rows, columns, tileWidth, tileHeight, score));
                }
            }

            if (candidates.Count == 0)
            {
                int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(maxTiles * (width / (double)height))));
                int rows = Math.Max(1, (int)Math.Ceiling(maxTiles / (double)columns));
                while (rows * columns > maxTiles)
                {
                    if (columns >= rows) columns--;
                    else rows--;
                }

                int tileWidth = (int)Math.Ceiling((width + (columns - 1) * overlap) / (double)columns);
                int tileHeight = (int)Math.Ceiling((height + (rows - 1) * overlap) / (double)rows);
                return (rows, columns, tileWidth, tileHeight);
            }

            var best = candidates.OrderBy(c => c.Score).First();
            return (best.Rows, best.Columns, best.TileWidth, best.TileHeight);
        }

        private static int GetStart(int index, int count, int fullSize, int tileSize)
        {
            if (count <= 1) return 0;
            double step = (fullSize - tileSize) / (double)(count - 1);
            int start = (int)Math.Round((index - 1) * step);
            if (start < 0) start = 0;
            if (start + tileSize > fullSize) start = Math.Max(0, fullSize - tileSize);
            return start;
        }
    }
}
