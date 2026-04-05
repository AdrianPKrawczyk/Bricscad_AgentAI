using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ReadTextSampleTool : IToolV2
    {
        public string[] ToolTags => new[] { "#tekst" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadTextSampleTool",
                    Description = "Pobiera reprezentatywną próbkę tekstów z obiektów (DBText, MText, MLeader) w zaznaczeniu. Chroni kontekst Agenta przed przepełnieniem.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "SaveAs", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Opcjonalna nazwa zmiennej (bez @), pod którą próbki zostaną zapisane w pamięci Agenta." 
                                }
                            }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
            {
                return "BŁĄD: Brak zaznaczonych obiektów w pamięci. Selektuj obiekty przed wywołaniem tego narzędzia.";
            }

            string saveAs = args["SaveAs"]?.ToString();
            List<string> allTexts = new List<string>();

            try
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        string textContent = "";

                        if (ent is DBText dbText)
                        {
                            textContent = dbText.TextString;
                        }
                        else if (ent is MText mText)
                        {
                            // Używamy .Text zamist .Contents, aby pobrać czysty tekst bez kodów RTF
                            textContent = mText.Text;
                        }
                        else if (ent is MLeader mLeader)
                        {
                            if (mLeader.ContentType == ContentType.MTextContent)
                            {
                                // W MLeaderze tekst siedzi w zagnieżdżonym obiekcie MText
                                textContent = mLeader.MText?.Text ?? "";
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(textContent))
                        {
                            allTexts.Add(textContent);
                        }
                    }
                    tr.Commit();
                }

                if (allTexts.Count == 0)
                {
                    return "WYNIK: W zaznaczeniu nie znaleziono żadnych obiektów tekstowych posiadających treść.";
                }

                // Inteligentny algorytm próbkowania (sqrt scale, max 15)
                int totalCount = allTexts.Count;
                int sampleSize = Math.Min(15, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(totalCount))));
                
                List<string> samples = new List<string>();
                double step = totalCount > 1 && sampleSize > 1 ? (double)(totalCount - 1) / (sampleSize - 1) : 1;

                for (int i = 0; i < sampleSize; i++)
                {
                    int index = (int)Math.Round(i * step);
                    if (index >= totalCount) index = totalCount - 1;
                    
                    string candidate = allTexts[index];
                    // Unikamy duplikowania próbek, jeśli teksty są identyczne
                    if (!samples.Contains(candidate))
                    {
                        samples.Add(candidate);
                    }
                }

                string report = $"WYNIK: Pobrano {samples.Count} próbek (z {totalCount} tekstów w zaznaczeniu):\n" + 
                               string.Join("\n", samples.Select(s => $"- {s}"));

                if (!string.IsNullOrEmpty(saveAs))
                {
                    string combined = string.Join(" | ", samples);
                    AgentMemoryState.Variables[saveAs] = combined;
                    report = $"ZAPISANO W PAMIĘCI JAKO: @{saveAs}\n{report}";
                }

                return report;
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY NARZĘDZIA: {ex.Message}";
            }
        }
    }
}
