using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace Bricscad_AgentAI_V2.Core
{
    public static class MetricVisionRenderer
    {
        public const string MetricVisionToken = "[VISION_METRIC_IMAGE_CAPTURED]|";
        private const double MinCadSize = 0.000001;

        public static MetricVisionTile RenderTile(
            Document doc,
            MetricVisionBounds bounds,
            string outputPath,
            int resolution,
            string profile,
            bool addOverlay,
            bool useExperimentalOffscreen,
            bool allowScreenFallback,
            string tileId,
            int row,
            int col,
            IEnumerable<string> isolateLayers = null,
            IEnumerable<string> hideLayers = null,
            bool fadeOtherLayers = false,
            bool grayOtherLayers = false)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (bounds == null) throw new ArgumentNullException(nameof(bounds));
            if (resolution < 256) resolution = 256;
            if (resolution > 4096) resolution = 4096;

            bounds = NormalizeAndSquare(bounds);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            string backendStatus;
            if (useExperimentalOffscreen)
            {
                backendStatus = "rendered_offscreen_gs";
                try
                {
                    RenderViaOffScreenGraphicsSystem(doc, bounds, outputPath, resolution, addOverlay, isolateLayers, hideLayers, fadeOtherLayers, grayOtherLayers);
                }
                catch (Exception offscreenEx)
                {
                    if (!allowScreenFallback)
                    {
                        throw new InvalidOperationException("Nie udalo sie wyrenderowac kafla przez eksperymentalny off-screen GraphicsSystem. Fallback ekranowy CopyFromScreen jest wylaczony, bo nie daje wiarygodnej kalibracji pixel->CAD. Szczegoly off-screen: " + offscreenEx.Message, offscreenEx);
                    }

                    RenderViaControlledView(doc, bounds, outputPath, resolution, addOverlay, true, isolateLayers, hideLayers, fadeOtherLayers, grayOtherLayers);
                    backendStatus = "rendered_screen_fallback";
                }
            }
            else if (allowScreenFallback)
            {
                RenderViaControlledView(doc, bounds, outputPath, resolution, addOverlay, true, isolateLayers, hideLayers, fadeOtherLayers, grayOtherLayers);
                backendStatus = "rendered_screen_fallback";
            }
            else
            {
                throw new InvalidOperationException("Metric Vision nie ma jeszcze wlaczonego stabilnego backendu renderowania. Ekranowy CopyFromScreen jest wylaczony, bo lapie UI i falszuje pixel->CAD. Eksperymentalny off-screen GraphicsSystem uruchom jawnie przez UseExperimentalOffscreen=true.");
            }

            return new MetricVisionTile
            {
                TileId = tileId,
                Row = row,
                Col = col,
                ImagePath = outputPath,
                CadBounds = bounds,
                Resolution = resolution,
                CadUnitsPerPixelX = (bounds.MaxX - bounds.MinX) / resolution,
                CadUnitsPerPixelY = (bounds.MaxY - bounds.MinY) / resolution,
                UnitType = GetUnitType(doc.Database),
                Profile = NormalizeProfile(profile),
                Status = backendStatus
            };
        }

        public static MetricVisionBounds NormalizeAndSquare(MetricVisionBounds raw)
        {
            double minX = Math.Min(raw.MinX, raw.MaxX);
            double maxX = Math.Max(raw.MinX, raw.MaxX);
            double minY = Math.Min(raw.MinY, raw.MaxY);
            double maxY = Math.Max(raw.MinY, raw.MaxY);
            double width = Math.Max(maxX - minX, MinCadSize);
            double height = Math.Max(maxY - minY, MinCadSize);
            double side = Math.Max(width, height);
            double cx = minX + width / 2.0;
            double cy = minY + height / 2.0;

            return new MetricVisionBounds
            {
                MinX = cx - side / 2.0,
                MaxX = cx + side / 2.0,
                MinY = cy - side / 2.0,
                MaxY = cy + side / 2.0
            };
        }

        public static string NormalizeProfile(string profile)
        {
            if (string.Equals(profile, "Symbols", StringComparison.OrdinalIgnoreCase)) return "Symbols";
            if (string.Equals(profile, "Diagnostics", StringComparison.OrdinalIgnoreCase)) return "Diagnostics";
            return "OcrLabels";
        }

        public static string CreateScanId()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public static string GetLatestScanIndexPath()
        {
            string root = AppPaths.GetVisionScansPath();
            if (!Directory.Exists(root)) return null;
            return Directory.GetFiles(root, "VisionScanIndex.json", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        public static MetricVisionScanIndex LoadScanIndex(string scanId)
        {
            string path = ResolveScanIndexPath(scanId);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("Nie znaleziono indeksu VisionScanIndex.json.", path);
            }

            return JsonConvert.DeserializeObject<MetricVisionScanIndex>(File.ReadAllText(path));
        }

        public static string ResolveScanIndexPath(string scanId)
        {
            if (string.IsNullOrWhiteSpace(scanId) || scanId.Equals("latest", StringComparison.OrdinalIgnoreCase))
            {
                return GetLatestScanIndexPath();
            }

            string direct = Path.Combine(AppPaths.GetVisionScansPath(), scanId, "VisionScanIndex.json");
            if (File.Exists(direct)) return direct;

            if (File.Exists(scanId)) return scanId;
            return null;
        }

        public static MetricVisionBounds GetScopeExtents(Document doc, string scope, IEnumerable<string> layerNames, bool filterOutliers = true, double marginPercent = 0.0, MetricVisionBounds windowBounds = null)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            string normalizedScope = string.IsNullOrWhiteSpace(scope) ? "Model" : scope;

            if (normalizedScope.Equals("Window", StringComparison.OrdinalIgnoreCase))
            {
                if (windowBounds == null) throw new ArgumentException("WindowBounds is required for Scope=Window.");
                return ApplyMargin(windowBounds, marginPercent);
            }

            HashSet<string> layers = new HashSet<string>((layerNames ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);

            List<Extents3d> allExtents = new List<Extents3d>();

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                IEnumerable<ObjectId> ids;
                if (normalizedScope.Equals("Selection", StringComparison.OrdinalIgnoreCase))
                {
                    ids = AgentMemoryState.ActiveSelection ?? new ObjectId[0];
                    filterOutliers = false; // Nie odrzucamy z ręcznego zaznaczenia
                }
                else
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                    ids = btr.Cast<ObjectId>();
                }

                foreach (ObjectId id in ids)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (ent == null) continue;
                    if (normalizedScope.Equals("Layer", StringComparison.OrdinalIgnoreCase) && layers.Count > 0 && !layers.Contains(ent.Layer)) continue;

                    try
                    {
                        allExtents.Add(ent.GeometricExtents);
                    }
                    catch
                    {
                    }
                }
                tr.Commit();
            }

            if (allExtents.Count == 0)
            {
                throw new InvalidOperationException("Nie udalo sie znalezc zadnych elementow w zadanym zakresie (scope).");
            }

            if (filterOutliers && allExtents.Count > 10)
            {
                // Filtrujemy na podstawie środków geometrycznych
                var centersX = allExtents.Select(e => (e.MinPoint.X + e.MaxPoint.X) / 2.0).OrderBy(x => x).ToList();
                var centersY = allExtents.Select(e => (e.MinPoint.Y + e.MaxPoint.Y) / 2.0).OrderBy(y => y).ToList();

                double q1x = centersX[centersX.Count / 4];
                double q3x = centersX[centersX.Count * 3 / 4];
                double iqrX = q3x - q1x;

                double q1y = centersY[centersY.Count / 4];
                double q3y = centersY[centersY.Count * 3 / 4];
                double iqrY = q3y - q1y;

                double lowerBoundX = q1x - 1.5 * iqrX;
                double upperBoundX = q3x + 1.5 * iqrX;
                double lowerBoundY = q1y - 1.5 * iqrY;
                double upperBoundY = q3y + 1.5 * iqrY;

                allExtents = allExtents.Where(e => 
                {
                    double cx = (e.MinPoint.X + e.MaxPoint.X) / 2.0;
                    double cy = (e.MinPoint.Y + e.MaxPoint.Y) / 2.0;
                    return cx >= lowerBoundX && cx <= upperBoundX && cy >= lowerBoundY && cy <= upperBoundY;
                }).ToList();
            }

            if (allExtents.Count == 0)
            {
                throw new InvalidOperationException("Po odrzuceniu elementow odstajacych (outliers) nie pozostal zaden element.");
            }

            Extents3d? finalExt = null;
            foreach (var ext in allExtents)
            {
                finalExt = Union(finalExt, ext);
            }

            MetricVisionBounds resultBounds = new MetricVisionBounds
            {
                MinX = finalExt.Value.MinPoint.X,
                MinY = finalExt.Value.MinPoint.Y,
                MaxX = finalExt.Value.MaxPoint.X,
                MaxY = finalExt.Value.MaxPoint.Y
            };

            return ApplyMargin(resultBounds, marginPercent);
        }

        private static MetricVisionBounds ApplyMargin(MetricVisionBounds bounds, double marginPercent)
        {
            if (marginPercent <= 0) return bounds;
            
            double width = bounds.MaxX - bounds.MinX;
            double height = bounds.MaxY - bounds.MinY;
            double dx = width * (marginPercent / 100.0);
            double dy = height * (marginPercent / 100.0);

            return new MetricVisionBounds
            {
                MinX = bounds.MinX - dx,
                MaxX = bounds.MaxX + dx,
                MinY = bounds.MinY - dy,
                MaxY = bounds.MaxY + dy
            };
        }

        public static List<MetricVisionBounds> BuildTiles(MetricVisionBounds extents, double tileCadSize, double overlapPercent)
        {
            extents = NormalizeBounds(extents);
            if (tileCadSize <= 0)
            {
                tileCadSize = Math.Max(extents.MaxX - extents.MinX, extents.MaxY - extents.MinY);
            }

            overlapPercent = Math.Max(0, Math.Min(50, overlapPercent));
            double step = tileCadSize * (1.0 - overlapPercent / 100.0);
            if (step <= MinCadSize) step = tileCadSize;

            List<MetricVisionBounds> tiles = new List<MetricVisionBounds>();
            List<double> xStarts = BuildAxisStarts(extents.MinX, extents.MaxX, tileCadSize, step);
            List<double> yStarts = BuildAxisStarts(extents.MinY, extents.MaxY, tileCadSize, step);

            foreach (double y in yStarts)
            {
                foreach (double x in xStarts)
                {
                    tiles.Add(new MetricVisionBounds
                    {
                        MinX = x,
                        MinY = y,
                        MaxX = Math.Min(x + tileCadSize, extents.MaxX),
                        MaxY = Math.Min(y + tileCadSize, extents.MaxY)
                    });
                }
            }

            if (tiles.Count == 0) tiles.Add(extents);
            return tiles;
        }

        private static List<double> BuildAxisStarts(double min, double max, double tileCadSize, double step)
        {
            double span = max - min;
            List<double> starts = new List<double>();
            if (span <= MinCadSize)
            {
                starts.Add(min);
                return starts;
            }

            if (span <= tileCadSize + MinCadSize)
            {
                starts.Add(min);
                return starts;
            }

            double lastStart = max - tileCadSize;
            for (double value = min; value <= lastStart + MinCadSize; value += step)
            {
                AddDistinctStart(starts, value);
            }

            AddDistinctStart(starts, lastStart);
            starts.Sort();
            return starts;
        }

        private static void AddDistinctStart(List<double> starts, double value)
        {
            foreach (double existing in starts)
            {
                if (Math.Abs(existing - value) <= MinCadSize) return;
            }
            starts.Add(value);
        }

        public static double[] PixelToCad(MetricVisionTile tile, double pixelX, double pixelY)
        {
            double cadX = tile.CadBounds.MinX + (pixelX / tile.Resolution) * (tile.CadBounds.MaxX - tile.CadBounds.MinX);
            double invertedY = tile.Resolution - pixelY;
            double cadY = tile.CadBounds.MinY + (invertedY / tile.Resolution) * (tile.CadBounds.MaxY - tile.CadBounds.MinY);
            return new[] { cadX, cadY };
        }

        private static MetricVisionBounds NormalizeBounds(MetricVisionBounds raw)
        {
            return new MetricVisionBounds
            {
                MinX = Math.Min(raw.MinX, raw.MaxX),
                MinY = Math.Min(raw.MinY, raw.MaxY),
                MaxX = Math.Max(raw.MinX, raw.MaxX),
                MaxY = Math.Max(raw.MinY, raw.MaxY)
            };
        }

        private static Extents3d? Union(Extents3d? current, Extents3d next)
        {
            if (!current.HasValue) return next;
            Extents3d combined = current.Value;
            combined.AddExtents(next);
            return combined;
        }

        private static string GetUnitType(Database db)
        {
            try
            {
                return db.Insunits.ToString();
            }
            catch
            {
                return "cad_units";
            }
        }

        private static void RenderViaOffScreenGraphicsSystem(Document doc, MetricVisionBounds bounds, string outputPath, int resolution, bool addOverlay, IEnumerable<string> isolateLayers, IEnumerable<string> hideLayers, bool fadeOtherLayers, bool grayOtherLayers)
        {
            Bricscad.GraphicsSystem.Manager manager = doc.GraphicsManager;
            if (manager == null) throw new InvalidOperationException("Document.GraphicsManager zwrocil null.");

            using (Teigha.GraphicsSystem.Device device = manager.CreateAutoCADOffScreenDevice())
            {
                if (device == null) throw new InvalidOperationException("CreateAutoCADOffScreenDevice zwrocil null.");

                device.BackgroundColor = Color.White;
                device.OnSize(new Size(resolution, resolution));

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    ApplyTemporaryLayerStates(tr, doc.Database, isolateLayers, hideLayers, fadeOtherLayers, grayOtherLayers);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                    Teigha.GraphicsSystem.Model model = manager.GetDBModel();
                    if (model == null) throw new InvalidOperationException("manager.GetDBModel() zwrocil null.");

                    using (Teigha.GraphicsSystem.View view = device.CreateView())
                    {
                        if (view == null) throw new InvalidOperationException("device.CreateView() zwrocil null.");
                        if (!view.Add(btr, model)) throw new InvalidOperationException("view.Add(CurrentSpace, DBModel) zwrocil false.");

                        device.Add(view);

                        view.Viewport = new Extents2d(0.0, 0.0, resolution, resolution);
                        
                        // Zamiast recznego SetView, uzywamy wbudowanego ZoomExtents, co automatycznie ustawi prawidlowa kamere i UpVector.
                        try
                        {
                            view.ZoomExtents(
                                new Point3d(bounds.MinX, bounds.MinY, 0),
                                new Point3d(bounds.MaxX, bounds.MaxY, 0)
                            );
                        }
                        catch { }

                        // Próba zignorowania grubości linii (lineweights), aby nie zaciemniały rysunku
                        try { view.LineweightToDcScale = 0.0; } catch { }

                        // Tryb 2D
                        try { view.Mode = Teigha.GraphicsSystem.RenderMode.Wireframe; } catch { }

                        view.Show();
                        device.Update();

                        using (Bitmap snapshot = device.GetSnapshot(new Rectangle(0, 0, resolution, resolution)))
                        {
                            if (snapshot == null) throw new InvalidOperationException("Device.GetSnapshot zwrocil null.");

                            using (Bitmap normalized = new Bitmap(resolution, resolution))
                            {
                                using (Graphics g = Graphics.FromImage(normalized))
                                {
                                    g.Clear(Color.White);
                                    
                                    // BricsCAD Offscreen GS wyrzuca bufor obrócony o 90 stopni (transponowany). 
                                    // Obracamy go o -90 stopni (Rotate270FlipNone), aby odzyskac prawidlowa orientacje z góry na dół.
                                    snapshot.RotateFlip(RotateFlipType.Rotate270FlipNone);
                                    
                                    g.DrawImage(snapshot, new Rectangle(0, 0, resolution, resolution), new Rectangle(0, 0, snapshot.Width, snapshot.Height), GraphicsUnit.Pixel);
                                    if (addOverlay) DrawOverlay(g, resolution, bounds);
                                }

                                normalized.Save(outputPath, ImageFormat.Png);
                            }
                        }
                    }

                    tr.Abort();
                }
            }
        }

        private static void RenderViaControlledView(Document doc, MetricVisionBounds bounds, string outputPath, int resolution, bool addOverlay, bool allowScreenFallback, IEnumerable<string> isolateLayers, IEnumerable<string> hideLayers, bool fadeOtherLayers, bool grayOtherLayers)
        {
            if (!allowScreenFallback)
            {
                throw new InvalidOperationException("Backend ekranowy CopyFromScreen jest wylaczony dla Metric Vision, bo przechwytuje elementy UI BricsCAD/Windows i nie daje wiarygodnej kalibracji pixel->CAD. Ustaw AllowScreenFallback=true tylko do podgladu diagnostycznego albo wdroz backend off-screen GraphicsSystem.");
            }

            Editor ed = doc.Editor;
            ViewTableRecord originalView = ed.GetCurrentView();
            Transaction tr = null;
            try
            {
                tr = doc.TransactionManager.StartTransaction();
                ApplyTemporaryLayerStates(tr, doc.Database, isolateLayers, hideLayers, fadeOtherLayers, grayOtherLayers);

                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    view.CenterPoint = new Point2d((bounds.MinX + bounds.MaxX) / 2.0, (bounds.MinY + bounds.MaxY) / 2.0);
                    view.Width = bounds.MaxX - bounds.MinX;
                    view.Height = bounds.MaxY - bounds.MinY;
                    ed.SetCurrentView(view);
                }

                doc.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                Thread.Sleep(150);

                Rectangle rect = GetDocumentWindowRectangle(doc);
                EnsureDocumentIsForeground(doc);
                using (Bitmap screen = new Bitmap(rect.Width, rect.Height))
                {
                    using (Graphics g = Graphics.FromImage(screen))
                    {
                        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size);
                    }

                    Rectangle sourceSquare = GetCenteredSquare(screen.Width, screen.Height);
                    using (Bitmap normalized = new Bitmap(resolution, resolution))
                    {
                        using (Graphics g = Graphics.FromImage(normalized))
                        {
                            g.Clear(Color.White);
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(screen, new Rectangle(0, 0, resolution, resolution), sourceSquare, GraphicsUnit.Pixel);
                            if (addOverlay) DrawOverlay(g, resolution, bounds);
                        }
                        normalized.Save(outputPath, ImageFormat.Png);
                    }
                }
            }
            finally
            {
                if (tr != null)
                {
                    try { tr.Abort(); tr.Dispose(); } catch { }
                }

                if (originalView != null)
                {
                    try
                    {
                        ed.SetCurrentView(originalView);
                        ed.UpdateScreen();
                        originalView.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void ApplyTemporaryLayerStates(Transaction tr, Database db, IEnumerable<string> isolateLayers, IEnumerable<string> hideLayers, bool fadeOtherLayers, bool grayOtherLayers)
        {
            var isolateSet = new HashSet<string>((isolateLayers ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
            var hideSet = new HashSet<string>((hideLayers ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);

            if (isolateSet.Count == 0 && hideSet.Count == 0)
                return;

            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var grayedLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId layerId in lt)
            {
                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                string name = ltr.Name;

                bool shouldIsolate = isolateSet.Count > 0 && isolateSet.Contains(name);
                bool shouldHide = hideSet.Count > 0 && hideSet.Contains(name);

                bool isIsolated = isolateSet.Count == 0 || shouldIsolate;
                bool isVisible = isIsolated && !shouldHide;

                if (!isVisible)
                {
                    if (!fadeOtherLayers && !grayOtherLayers)
                    {
                        // Całkowite ukrycie
                        ltr.UpgradeOpen();
                        ltr.IsOff = true;
                    }
                    else
                    {
                        ltr.UpgradeOpen();
                        if (grayOtherLayers)
                        {
                            ltr.Color = Teigha.Colors.Color.FromRgb(128, 128, 128); // szary (RGB)
                            grayedLayers.Add(name);
                        }
                        if (fadeOtherLayers)
                        {
                            // Ustawienie przezroczystości (ok. 70% fade)
                            ltr.Transparency = new Teigha.Colors.Transparency(70);
                        }
                    }
                }
            }

            // Fallback: Wymuszenie koloru bezposrednio na encjach (obejscie buga BricsCAD z brakiem regenu koloru warstwy off-screen)
            if (grayOtherLayers && grayedLayers.Count > 0)
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                foreach (ObjectId entId in btr)
                {
                    Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent != null && grayedLayers.Contains(ent.Layer))
                    {
                        ent.UpgradeOpen();
                        ent.Color = Teigha.Colors.Color.FromRgb(128, 128, 128);
                        ent.RecordGraphicsModified(true);
                    }
                }
            }
        }

        private static Rectangle GetCenteredSquare(int width, int height)
        {
            int side = Math.Min(width, height);
            int left = Math.Max(0, (width - side) / 2);
            int top = Math.Max(0, (height - side) / 2);
            return new Rectangle(left, top, side, side);
        }

        private static void EnsureDocumentIsForeground(Document doc)
        {
            IntPtr docHwnd = doc.Window.Handle;
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return;
            if (foreground == docHwnd) return;
            if (IsChild(foreground, docHwnd)) return;
            if (IsChild(docHwnd, foreground)) return;

            throw new InvalidOperationException("Okno BricsCAD nie jest aktywne. Przerwano fallback CopyFromScreen, aby nie przechwycic innych okien.");
        }

        private static void DrawOverlay(Graphics g, int resolution, MetricVisionBounds bounds)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(80, 30, 144, 255), 1))
            using (Pen framePen = new Pen(Color.FromArgb(210, 220, 40, 40), 2))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(230, 20, 20, 20)))
            using (Font font = new Font("Segoe UI", Math.Max(8, resolution / 96f), FontStyle.Regular, GraphicsUnit.Pixel))
            {
                for (int p = 128; p < resolution; p += 128)
                {
                    g.DrawLine(gridPen, p, 0, p, resolution);
                    g.DrawLine(gridPen, 0, p, resolution, p);
                }

                g.DrawRectangle(framePen, 1, 1, resolution - 3, resolution - 3);
                g.DrawString("0,0 px", font, textBrush, 6, 6);
                g.DrawString($"{resolution},{resolution} px", font, textBrush, resolution - 135, resolution - 22);
                g.DrawString($"CAD [{bounds.MinX:0.###},{bounds.MinY:0.###}] - [{bounds.MaxX:0.###},{bounds.MaxY:0.###}]", font, textBrush, 6, resolution - 22);
            }
        }

        private static Rectangle GetDocumentWindowRectangle(Document doc)
        {
            IntPtr hWnd = doc.Window.Handle;
            RECT rect;
            if (!GetClientRect(hWnd, out rect))
            {
                if (!GetWindowRect(hWnd, out rect)) throw new InvalidOperationException("Nie udalo sie pobrac rozmiaru okna dokumentu.");
                return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }

            POINT pt = new POINT { X = 0, Y = 0 };
            ClientToScreen(hWnd, ref pt);
            return new Rectangle(pt.X, pt.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
    }
}
