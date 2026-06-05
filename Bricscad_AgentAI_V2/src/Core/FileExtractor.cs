using System;
using System.IO;
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
                case ".md":
                case ".lsp":
                    return File.ReadAllText(path);

                case ".xls":
                case ".xlsx":
                    return ExtractExcel(path);

                case ".pdf":
                    return ExtractPdf(path);

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

        public static string GetImageBase64(string path)
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

            string mimeType = ext == ".png" ? "image/png" : "image/jpeg";
            byte[] imageBytes;

            using (var originalImage = Image.FromFile(path))
            {
                int maxWidth = 1024;
                int maxHeight = 1024;

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
                    // No resize needed
                    using (var ms = new MemoryStream())
                    {
                        ImageFormat format = ext == ".png" ? ImageFormat.Png : ImageFormat.Jpeg;
                        originalImage.Save(ms, format);
                        imageBytes = ms.ToArray();
                    }
                }
            }

            string base64String = Convert.ToBase64String(imageBytes);
            return $"data:{mimeType};base64,{base64String}";
        }
    }
}
