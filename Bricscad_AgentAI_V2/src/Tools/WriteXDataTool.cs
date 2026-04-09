using System;
using System.Collections.Generic;
using System.Globalization;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class WriteXDataTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "WriteXData",
                    Description = "Zapisuje lub nadpisuje dane XData dla obiektów w aktywnym zaznaczeniu. Automatycznie rejestruje aplikację (RegApp), jeśli nie istnieje.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "AppName", new ToolParameter { Type = "string", Description = "Nazwa aplikacji (RegApp) do której przypisane będą dane." } },
                            { 
                                "Entries", new ToolParameter 
                                { 
                                    Type = "array", 
                                    Description = "Lista wpisów do zapisu. Każdy wpis powinien mieć 'Type' (String, Double, Int) i 'Value'.",
                                    Items = JObject.Parse(@"{ ""type"": ""object"", ""properties"": { ""Type"": { ""type"": ""string"", ""enum"": [""String"", ""Double"", ""Int""] }, ""Value"": { ""type"": ""string"" } } }")
                                } 
                            }
                        },
                        Required = new List<string> { "AppName", "Entries" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
                return "BŁĄD: Brak zaznaczonych obiektów. Użyj SelectEntities przed zapisem XData.";

            string appName = args["AppName"]?.ToString();
            var entries = args["Entries"] as JArray;

            if (string.IsNullOrEmpty(appName) || entries == null)
                return "BŁĄD: Brak nazwy aplikacji lub pusty zbiór danych (Entries).";

            Database db = doc.Database;

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 1. Rejestracja aplikacji (RegApp)
                    RegAppTable rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
                    if (!rat.Has(appName))
                    {
                        rat.UpgradeOpen();
                        RegAppTableRecord ratr = new RegAppTableRecord();
                        ratr.Name = appName;
                        rat.Add(ratr);
                        tr.AddNewlyCreatedDBObject(ratr, true);
                    }

                    // 2. Przygotowanie bufora danych (ResultBuffer)
                    // Zawsze zaczyna się od kodu 1001 (Nazwa aplikacji)
                    ResultBuffer rb = new ResultBuffer();
                    rb.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, appName));

                    foreach (var entry in entries)
                    {
                        string typeStr = entry["Type"]?.ToString().ToLower();
                        string valStr = entry["Value"]?.ToString();

                        if (typeStr == "string")
                        {
                            // Używamy 1000 (ExtendedDataAsciiString) zamiast 1002 (ControlString)
                            rb.Add(new TypedValue(1000, valStr));
                        }
                        else if (typeStr == "double")
                        {
                            if (double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                                rb.Add(new TypedValue((int)DxfCode.ExtendedDataReal, dVal));
                            else
                                return $"BŁĄD: Niepoprawny format liczby (Double): '{valStr}'";
                        }
                        else if (typeStr == "int")
                        {
                            if (int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iVal))
                                rb.Add(new TypedValue((int)DxfCode.ExtendedDataInteger32, iVal));
                            else
                                return $"BŁĄD: Niepoprawny format liczby całkowitej (Int): '{valStr}'";
                        }
                    }

                    // 3. Zastosowanie do zaznaczonych obiektów (Nadpisanie)
                    int count = 0;
                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent != null)
                        {
                            ent.XData = rb;
                            count++;
                        }
                    }

                    tr.Commit();
                    return $"SUKCES: Zapisano dane XData aplikacji '{appName}' w {count} obiektach.";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD ZAPISU XDATA: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>
        {
            "{\"AppName\": \"BIELIK_METADATA\", \"Entries\": [{\"Type\": \"String\", \"Value\": \"Draft\"}, {\"Type\": \"Int\", \"Value\": \"1\"}]}",
            "{\"AppName\": \"MY_APP\", \"Entries\": [{\"Type\": \"Double\", \"Value\": \"123.45\"}]}"
        };
    }
}