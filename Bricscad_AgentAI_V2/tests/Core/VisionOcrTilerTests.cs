using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    public static class VisionOcrTilerTests
    {
        public static void RunTests()
        {
            TestSquareSheet();
            TestWideSheet();
            TestTallSheet();
            TestLargeSheetMaxTiles();
        }

        private static void TestSquareSheet()
        {
            var tiles = CreateAndTile(4096, 4096);
            Debug.Assert(tiles.IsTiled, "4096x4096 powinien byc kafelkowany.");
            Debug.Assert(tiles.TileCount <= 16, "4096x4096 przekroczyl limit kafelkow.");
            Debug.Assert(tiles.Rows >= 2 && tiles.Columns >= 2, "4096x4096 powinien miec siatke w obu osiach.");
        }

        private static void TestWideSheet()
        {
            var tiles = CreateAndTile(6000, 2000);
            Debug.Assert(tiles.IsTiled, "6000x2000 powinien byc kafelkowany.");
            Debug.Assert(tiles.Columns > tiles.Rows, "6000x2000 powinien miec uklad panoramiczny.");
            Debug.Assert(tiles.Rows == 1, "6000x2000 nie powinien sztucznie tworzyc dolnego rzedu.");
            Debug.Assert(tiles.TileCount <= 16, "6000x2000 przekroczyl limit kafelkow.");
        }

        private static void TestTallSheet()
        {
            var tiles = CreateAndTile(3000, 9000);
            Debug.Assert(tiles.IsTiled, "3000x9000 powinien byc kafelkowany.");
            Debug.Assert(tiles.Rows > tiles.Columns, "3000x9000 powinien miec uklad pionowy.");
            Debug.Assert(tiles.TileCount <= 16, "3000x9000 przekroczyl limit kafelkow.");
        }

        private static void TestLargeSheetMaxTiles()
        {
            var tiles = CreateAndTile(12000, 4000);
            Debug.Assert(tiles.IsTiled, "12000x4000 powinien byc kafelkowany.");
            Debug.Assert(tiles.TileCount <= 16, "12000x4000 musi zmiescic sie w limicie 16 kafelkow.");
            Debug.Assert(tiles.Columns > tiles.Rows, "12000x4000 powinien zachowac panoramiczny uklad.");
        }

        private static VisionTileSet CreateAndTile(int width, int height)
        {
            string root = Path.Combine(Path.GetTempPath(), "BricscadAgentAI_VisionOcrTilerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string imagePath = Path.Combine(root, "source.png");
            string outputDir = Path.Combine(root, "tiles");

            using (var bitmap = new Bitmap(width, height))
            {
                bitmap.Save(imagePath, ImageFormat.Png);
            }

            return VisionOcrTiler.CreateTiles(imagePath, outputDir, "Auto", 2100, 200, 16, 2.0);
        }
    }
}
