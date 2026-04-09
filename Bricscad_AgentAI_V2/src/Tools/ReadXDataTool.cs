using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ReadXDataTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadXData",
                    Description = "Odczytuje rozszerzone dane (XData) z obiektów w aktywnym zaznaczeniu. Pozwala na wyciągnięcie metadanych ukrytych w obiektach DWG.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { 
                                "AppName", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Opcjonalnie: Nazwa zarejestrowanej aplikacji (RegApp), dla której chcemy odczytać dane. Jeśli brak, pobrane zostaną wszystkie dane XData." 
                                } 
                            },
                            { 
                                "SaveAs", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Opcjonalna nazwa zmiennej (np. MojeDaneX), pod którą wynik zostanie zapisany w pamięci Agenta (@zmienna)." 
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
                return "BŁĄD: Brak zaznaczonych obiektów. Najpierw wybierz obiekty (np. przez SelectEntities).";

            string filterAppName = args["AppName"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();

            Database db = doc.Database;
            JArray allResults = new JArray();

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        // Pobieranie XData (ResultBuffer)
                        // Jeśli filterAppName jest pusty, pobiera wszystkie dane
                        using (ResultBuffer rb = string.IsNullOrEmpty(filterAppName) ? ent.XData : ent.GetXDataForApplication(filterAppName))
                        {
                            if (rb == null) continue;

                            JObject entityData = new JObject();
                            entityData["Handle"] = ent.Handle.ToString();
                            entityData["Type"] = ent.GetType().Name;
                            
                            JArray xdataList = new JArray();
                            foreach (TypedValue tv in rb)
                            {
                                JObject item = new JObject();
                                item["TypeCode"] = (int)tv.TypeCode;
                                item["Value"] = tv.Value?.ToString();
                                xdataList.Add(item);
                            }
                            
                            entityData["XData"] = xdataList;
                            allResults.Add(entityData);
                        }
                    }
                    tr.Commit();
                }

                if (allResults.Count == 0)
                    return $"INFO: Nie znaleziono danych XData{(string.IsNullOrEmpty(filterAppName) ? "" : " dla aplikacji " + filterAppName)} w wybranych obiektach.";

                string jsonOutput = allResults.ToString(Newtonsoft.Json.Formatting.Indented);

                // Zapis do pamięci Agenta
                if (!string.IsNullOrEmpty(saveAs))
                {
                    AgentMemoryState.Variables[saveAs] = jsonOutput;
                    return $"ZAPISANO W @{saveAs}. Wynik:\n{jsonOutput}";
                }

                return jsonOutput;
            }
            catch (Exception ex)
            {
                return $"BŁĄD ODCZYTU XDATA: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>
        {
            "{\"AppName\": \"MOJA_APLIKACJA\"}",
            "{\"SaveAs\": \"MetadataRecord\"}",
            "{}"
        };
    }
}