using Bricscad.ApplicationServices;
using System.Collections.Generic;
using System.Text.Json;

namespace BricsCAD_AgentAI_V2.Tools
{
    /// <summary>
    /// To jest WZÓR REFERENCYJNY dla Agenta AI pokazujący jak powinno wyglądać narzędzie V2.
    /// Zwróć uwagę na brak Regex i silne typowanie parametrów w klasie Schema.
    /// </summary>
    public class SampleToolV2 : IToolV2
    {
        // 1. Zwracanie definicji narzędzia dla LLM
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Name = "ManageLayers",
                Description = "Zarządza warstwami w rysunku CAD (tworzenie, modyfikacja, usuwanie).",
                Parameters = new
                {
                    Type = "object",
                    Properties = new Dictionary<string, object>
                    {
                        { "Mode", new { Type = "string", Description = "Tryb: Create, Modify, Delete" } },
                        { "LayerName", new { Type = "string", Description = "Nazwa warstwy docelowej" } },
                        { "ColorIndex", new { Type = "integer", Description = "Opcjonalny: Indeks koloru 1-255" } }
                    },
                    Required = new[] { "Mode", "LayerName" }
                }
            };
        }

        // 2. Metoda wykonawcza przyjmująca CZYSTE argumenty
        public string Execute(Document doc, JsonElement args)
        {
            // Pobieramy dane bez używania Regex!
            string mode = args.TryGetProperty("Mode", out var modeProp) ? modeProp.GetString() : "Create";
            string layerName = args.TryGetProperty("LayerName", out var layerProp) ? layerProp.GetString() : "0";
            
            int colorIndex = -1;
            if (args.TryGetProperty("ColorIndex", out var colorProp))
            {
                colorIndex = colorProp.GetInt32();
            }

            // TUTAJ NASTĘPUJE LOGIKA CAD (zamknięta w zwrotce dla Magicznego Wrappera)
            // Przykład symulowany:
            return $"WYNIK: Warstwa {layerName} obsłużona w trybie {mode}.";
        }
    }
}