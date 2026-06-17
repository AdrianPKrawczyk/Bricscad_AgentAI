using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class BatchWriteXDataTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "BatchWriteXData",
                    Description = "Masowo zapisuje/aktualizuje metadane XData dla listy obiektów CAD w JEDNEJ transakcji. Optymalizuje proces dla LLM — zamiast wielokrotnego wywoływania 'WriteXData', agent dostarcza tablicę {Handle, Attributes}, a narzędzie zbiorczo aplikuje dane. Wymaga 'appName' (np. 'BIM_ROOM_DATA'). Każdy obiekt otrzymuje pary klucz-wartość jako DxfCode.ExtendedDataAsciiString (kod 1000). Nadpisuje istniejące XData dla danej appName. Pomija obiekty o nieistniejących Handle'ach (z raportem w komunikacie zwrotnym).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "appName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa rejestrowanej aplikacji XData (np. 'BIM_ROOM_DATA'). Rejestrowana automatycznie w RegAppTable, jeśli nie istnieje."
                                }
                            },
                            {
                                "entitiesData", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Tablica obiektów do zapisu. Każdy element zawiera Handle (uchwyt obiektu) oraz Attributes (słownik par klucz-wartość tekstowych).",
                                    Items = JObject.Parse("{\"type\":\"object\",\"properties\":{\"Handle\":{\"type\":\"string\",\"description\":\"Hex-uchwyt obiektu (np. '1A2').\"},\"Attributes\":{\"type\":\"object\",\"description\":\"Słownik par klucz-wartość (np. {\\\"Numer\\\":\\\"01\\\",\\\"Powierzchnia\\\":\\\"15.5\\\"}).\",\"additionalProperties\":{\"type\":\"string\"}}}}")
                                }
                            }
                        },
                        Required = new List<string> { "appName", "entitiesData" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string appName = args["appName"]?.ToString();
            JArray entitiesData = args["entitiesData"] as JArray;

            if (string.IsNullOrWhiteSpace(appName))
                return "BŁĄD: Parametr 'appName' jest wymagany.";

            if (entitiesData == null || entitiesData.Count == 0)
                return "BŁĄD: Tablica 'entitiesData' jest pusta lub niepoprawna.";

            Database db = doc.Database;

            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                using (DocumentLock docLock = doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    RegAppTable rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
                    if (!rat.Has(appName))
                    {
                        rat.UpgradeOpen();
                        RegAppTableRecord ratr = new RegAppTableRecord();
                        ratr.Name = appName;
                        rat.Add(ratr);
                        tr.AddNewlyCreatedDBObject(ratr, true);
                    }

                    int successCount = 0;
                    var errors = new List<string>();

                    foreach (var itemToken in entitiesData)
                    {
                        JObject item = itemToken as JObject;
                        if (item == null)
                        {
                            errors.Add("Pominięto wpis: nieprawidłowy element (nie-obiekt).");
                            continue;
                        }

                        string handleStr = item["Handle"]?.ToString();
                        JObject attrsToken = item["Attributes"] as JObject;

                        if (string.IsNullOrWhiteSpace(handleStr) || attrsToken == null)
                        {
                            errors.Add($"Pominięto wpis: brak 'Handle' lub 'Attributes' (Handle='{handleStr}').");
                            continue;
                        }

                        if (!long.TryParse(handleStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long handleVal))
                        {
                            errors.Add($"Niepoprawny format Handle: '{handleStr}'.");
                            continue;
                        }

                        Handle handle = new Handle(handleVal);
                        ObjectId objId = db.GetObjectId(false, handle, 0);
                        if (objId.IsNull || objId.IsErased)
                        {
                            errors.Add($"Obiekt o Handle '{handleStr}' nie istnieje lub jest usunięty.");
                            continue;
                        }

                        Entity ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                        if (ent == null)
                        {
                            errors.Add($"Handle '{handleStr}' nie wskazuje na Entity.");
                            continue;
                        }

                        ResultBuffer rb = new ResultBuffer();
                        rb.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, appName));

                        foreach (var prop in attrsToken.Properties())
                        {
                            rb.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, prop.Name));
                            rb.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, prop.Value?.ToString() ?? ""));
                        }

                        ent.XData = rb;
                        successCount++;
                    }

                    if (successCount == 0)
                    {
                        return $"BŁĄD: Nie udało się zapisać XData do żadnego obiektu. Błędy: {string.Join("; ", errors)}";
                    }

                    tr.Commit();

                    string errSummary = "";
                    if (errors.Count > 0)
                    {
                        var firstErrors = errors.Take(3);
                        errSummary = $" (Pominięto: {errors.Count}/{entitiesData.Count}, m.in.: {string.Join("; ", firstErrors)})";
                    }

                    return $"SUKCES: Zapisano XData (AppName='{appName}') do {successCount}/{entitiesData.Count} obiektów.{errSummary}";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD MASOWEGO ZAPISU XDATA: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        public List<string> Examples => new List<string>
        {
            "{\"appName\":\"BIM_ROOM_DATA\",\"entitiesData\":[{\"Handle\":\"1A\",\"Attributes\":{\"Numer\":\"01\"}}]}",
            "{\"appName\":\"BIM_ROOM_DATA\",\"entitiesData\":[{\"Handle\":\"1A\",\"Attributes\":{\"Numer\":\"01\",\"Powierzchnia\":\"15.5\"}},{\"Handle\":\"2B\",\"Attributes\":{\"Numer\":\"02\",\"Powierzchnia\":\"20.0\"}}]}"
        };
    }
}
