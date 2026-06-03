using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Application = Bricscad.ApplicationServices.Application;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using System.Runtime.InteropServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class CaptureVisionAreaTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "CaptureVisionArea",
                    Description = "Pozwala na wykonanie zrzutu ekranu z wybranego obszaru rysunku (wskazanego przez użytkownika), abyś mógł go przeanalizować wzrokowo (OCR/Vision).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>(),
                        Required = new List<string>()
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var ed = doc.Editor;
            
            // 1. Wskazanie obszaru przez użytkownika
            PromptPointResult ppr1 = ed.GetPoint("\nWskaż pierwszy narożnik obszaru do skanowania: ");
            if (ppr1.Status != PromptStatus.OK) return "BŁĄD: Użytkownik anulował wskazywanie obszaru.";

            PromptCornerOptions pco = new PromptCornerOptions("\nWskaż drugi narożnik: ", ppr1.Value);
            PromptPointResult ppr2 = ed.GetCorner(pco);
            if (ppr2.Status != PromptStatus.OK) return "BŁĄD: Użytkownik anulował wskazywanie obszaru.";

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"AgentVision_{Guid.NewGuid()}.jpg");

            // 2. Synchronizacja i dokumentowanie widoku
            ViewTableRecord currentView = ed.GetCurrentView();
            try
            {
                // Tworzymy nową definicję widoku dla Zooma
                using (ViewTableRecord zoomView = ed.GetCurrentView())
                {
                    double minX = Math.Min(ppr1.Value.X, ppr2.Value.X);
                    double maxX = Math.Max(ppr1.Value.X, ppr2.Value.X);
                    double minY = Math.Min(ppr1.Value.Y, ppr2.Value.Y);
                    double maxY = Math.Max(ppr1.Value.Y, ppr2.Value.Y);

                    double width = maxX - minX;
                    double height = maxY - minY;
                    Point2d center = new Point2d(minX + width / 2.0, minY + height / 2.0);

                    zoomView.CenterPoint = center;
                    zoomView.Height = height;
                    zoomView.Width = width;

                    // APLIKUJEMY WIDOK DOCELOWY (.NET Native - bezpieczniejsze niż COM)
                    ed.SetCurrentView(zoomView);
                }

                // 3. Krytyczna synchronizacja grafiki przed zrzutem
                doc.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                
                // Czekamy 200ms na dorysowanie tekstu i cieni (Timing Fix)
                System.Threading.Thread.Sleep(200);

                // 4. Zrzut ekranu aktywnego okna rysunku (Win32 API fallback)
                IntPtr hWnd = doc.Window.Handle;
                RECT rect;
                if (GetWindowRect(hWnd, out rect))
                {
                    int winWidth = rect.Right - rect.Left;
                    int winHeight = rect.Bottom - rect.Top;

                    using (Bitmap bmp = new Bitmap(winWidth, winHeight))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(winWidth, winHeight));
                        }
                        bmp.Save(tempFilePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                }
                else
                {
                    return "BŁĄD: Nie udało się pobrać współrzędnych okna BricsCAD.";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD WIZJI: {ex.Message}";
            }
            finally
            {
                // 5. Powrót do widoku użytkownika
                if (currentView != null)
                {
                    try 
                    { 
                        ed.SetCurrentView(currentView);
                        ed.UpdateScreen();
                        currentView.Dispose(); 
                    } 
                    catch { }
                }
            }

            return $"[VISION_IMAGE_CAPTURED]|{tempFilePath}";
        }

        public List<string> Examples => new List<string> { "{}" };

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
