using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ExcelDataReader;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Bricscad_AgentAI_V2.Core
{
    public static class FileExtractor
    {
        static FileExtractor()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public static string ExtractText(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Plik nie istnieje: {path}");
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".txt":
                case ".py":
                case ".csv":
                case ".json":
                case ".xml":
                case ".md":
                case ".lsp":
                    return File.ReadAllText(path);

                case ".xls":
                case ".xlsx":
                    return ExtractExcel(path);

                case ".pdf":
                    return RepairPolishMojibake(ExtractPdf(path));

                default:
                    throw new NotSupportedException($"Format {ext} nie jest obsługiwany przez ekstrakcję tekstu.");
            }
        }

        private static string ExtractExcel(string path)
        {
            var sb = new StringBuilder();
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        while (reader.Read())
                        {
                            var rowValues = new string[reader.FieldCount];
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                rowValues[i] = reader.GetValue(i)?.ToString() ?? "";
                            }
                            sb.AppendLine(string.Join(";", rowValues));
                        }
                    } while (reader.NextResult());
                }
            }
            return sb.ToString();
        }

        private static string ExtractPdf(string path)
        {
            var sb = new StringBuilder();
            using (PdfDocument document = PdfDocument.Open(path))
            {
                foreach (var page in document.GetPages())
                {
                    string text = ContentOrderTextExtractor.GetText(page, true);
                    sb.AppendLine(text);
                }
            }
            return sb.ToString();
        }

        public static string RepairPolishMojibake(string text)
        {
            if (string.IsNullOrEmpty(text) || !LooksLikePolishMojibake(text))
            {
                return text;
            }

            try
            {
                Encoding windows1250 = Encoding.GetEncoding(1250);
                string repaired = Encoding.UTF8.GetString(windows1250.GetBytes(text));
                return ScorePolishText(repaired) > ScorePolishText(text) ? repaired : text;
            }
            catch
            {
                return text;
            }
        }

        private static bool LooksLikePolishMojibake(string text)
        {
            string[] markers =
            {
                "Ä…", "Ä‡", "Ä™", "Ĺ‚", "Ĺ„", "Ăł", "Ĺ›", "Ĺş", "ĹĽ",
                "Ä„", "Ä†", "Ä", "Ĺ", "Ĺ", "Ă“", "Ĺš", "Ĺą", "Ĺ»"
            };

            return markers.Any(marker => text.IndexOf(marker, StringComparison.Ordinal) >= 0);
        }

        private static int ScorePolishText(string text)
        {
            if (string.IsNullOrEmpty(text)) return int.MinValue;

            int score = 0;
            foreach (char c in text)
            {
                if ("ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".IndexOf(c) >= 0) score += 4;
                if (c == 'Ă' || c == 'Ä' || c == 'Ĺ') score -= 3;
                if (c == '\uFFFD') score -= 20;
            }

            return score;
        }

        public static List<string> RenderPdfPagesToPng(string pdfPath, string outputDir, int dpi = 300, int maxPages = 3, string rendererPath = "pdftoppm.exe")
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException($"Plik PDF nie istnieje: {pdfPath}");
            }

            Directory.CreateDirectory(outputDir);
            int safeDpi = Math.Max(72, Math.Min(dpi <= 0 ? 300 : dpi, 600));
            int safeMaxPages = Math.Max(1, Math.Min(maxPages <= 0 ? 1 : maxPages, 50));
            string safeRenderer = string.IsNullOrWhiteSpace(rendererPath) ? "pdftoppm.exe" : rendererPath.Trim();
            string prefix = Path.Combine(outputDir, "pdf_page_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));

            var psi = new ProcessStartInfo
            {
                FileName = safeRenderer,
                Arguments = $"-png -r {safeDpi} -f 1 -l {safeMaxPages} \"{pdfPath}\" \"{prefix}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Nie udalo sie uruchomic pdftoppm.");
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"pdftoppm zakonczyl sie kodem {process.ExitCode}. {stderr} {stdout}".Trim());
                }
            }

            var files = Directory.GetFiles(outputDir, Path.GetFileName(prefix) + "*.png")
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                throw new InvalidOperationException("pdftoppm nie wygenerowal zadnych plikow PNG.");
            }

            return files;
        }

        public static string GetImageBase64(Image originalImage, string ext = ".png", int maxPixels = 1024)
        {
            string mimeType = ext == ".png" ? "image/png" : "image/jpeg";
            byte[] imageBytes;

            int safeMaxPixels = Math.Max(256, Math.Min(maxPixels <= 0 ? 1024 : maxPixels, 4096));
            int maxWidth = safeMaxPixels;
            int maxHeight = safeMaxPixels;

            if (originalImage.Width > maxWidth || originalImage.Height > maxHeight)
            {
                float ratioX = (float)maxWidth / originalImage.Width;
                float ratioY = (float)maxHeight / originalImage.Height;
                float ratio = Math.Min(ratioX, ratioY);

                int newWidth = (int)(originalImage.Width * ratio);
                int newHeight = (int)(originalImage.Height * ratio);

                using (var newImage = new Bitmap(newWidth, newHeight))
                {
                    using (var graphics = Graphics.FromImage(newImage))
                    {
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        
                        graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
                    }

                    using (var ms = new MemoryStream())
                    {
                        ImageFormat format = ext == ".png" ? ImageFormat.Png : ImageFormat.Jpeg;
                        newImage.Save(ms, format);
                        imageBytes = ms.ToArray();
                    }
                }
            }
            else
            {
                using (var ms = new MemoryStream())
                {
                    ImageFormat format = ext == ".png" ? ImageFormat.Png : ImageFormat.Jpeg;
                    originalImage.Save(ms, format);
                    imageBytes = ms.ToArray();
                }
            }

            string base64String = Convert.ToBase64String(imageBytes);
            return $"data:{mimeType};base64,{base64String}";
        }

        public static string GetImageBase64(string path, int maxPixels = 1024)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Plik nie istnieje: {path}");
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                throw new NotSupportedException($"Format {ext} nie jest obsługiwany jako obraz.");
            }

            using (var originalImage = Image.FromFile(path))
            {
                return GetImageBase64(originalImage, ext, maxPixels);
            }
        }
    }
}
