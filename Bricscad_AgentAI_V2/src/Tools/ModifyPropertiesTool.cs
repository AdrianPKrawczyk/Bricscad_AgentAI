using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ModifyPropertiesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ModifyProperties",
                    Description = "Modyfikuje właściwości (Layer, Color, Linetype, LineWeight, TextString, TextHeight, itp.) obiektów znajdujących się w aktualnym zaznaczeniu pamięci.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Modifications", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Lista docelowych modyfikacji. Format np.: [{\"Prop\": \"Layer\", \"Val\": \"OSIE\"}, {\"Prop\": \"Radius\", \"Val\": \"RPN: $OLD_RADIUS 5 +\"}]"
                                }
                            }
                        },
                        Required = new List<string> { "Modifications" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            if (AgentMemoryState.ActiveSelection == null || AgentMemoryState.ActiveSelection.Length == 0)
            {
                return "BŁĄD: Pamięć Agenta jest pusta. Użyj najpierw SelectEntitiesTool lub CreateObjectTool, by zaznaczyć obiekty.";
            }

            var modyfikacje = new List<(string Prop, string Val)>();
            if (args["Modifications"] is JArray arr)
            {
                foreach (JObject item in arr)
                {
                    string prop = item["Prop"]?.ToString()?.Trim();
                    string val = item["Val"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(prop) && val != null)
                    {
                        modyfikacje.Add((prop, val));
                    }
                }
            }

            if (modyfikacje.Count == 0) return "BŁĄD: Brak zdefiniowanych modyfikacji.";

            int odrzucone = 0;
            int udane = 0;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in AgentMemoryState.ActiveSelection)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        bool czyObiektZmodyfikowany = false;

                        foreach (var mod in modyfikacje)
                        {
                            object wartoscDoZapisania = null;
                            string rP = mod.Prop;
                            string newVal = AgentMemoryState.InjectVariables(mod.Val); // Wstrzykiwanie zmiennych
                            
                            // Mapowanie wizualnych własności
                            if (rP.Equals("Color", StringComparison.OrdinalIgnoreCase) || rP.Equals("ColorIndex", StringComparison.OrdinalIgnoreCase)) rP = "ColorIndex";
                            if (ent is MText && rP.Equals("Height", StringComparison.OrdinalIgnoreCase)) rP = "TextHeight";
                            if (ent is Dimension && (rP.Equals("Height", StringComparison.OrdinalIgnoreCase) || rP.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))) rP = "Dimtxt";
                            if (rP.Equals("Value", StringComparison.OrdinalIgnoreCase))
                            {
                                if (ent is DBText) rP = "TextString";
                                else if (ent is MText) rP = "Text";
                            }

                            // Szukanie właściwości przez Reflection
                            PropertyInfo propInfo = ent.GetType().GetProperty(rP, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            
                            if (propInfo == null || !propInfo.CanWrite || !propInfo.CanRead)
                            {
                                odrzucone++;
                                continue;
                            }

                            // Pobranie starej wartości dla silnika RPN (np. do $OLD_RADIUS)
                            object oldValObj = propInfo.GetValue(ent, null);
                            if (oldValObj != null)
                            {
                                string oldKey = "OLD_" + rP.ToUpperInvariant();
                                AgentMemoryState.Variables[oldKey] = oldValObj.ToString();
                                newVal = AgentMemoryState.InjectVariables(newVal); // Podmiana $OLD_...
                            }

                            // Przetworzenie RPN dla wartości numerycznych 
                            if (newVal.StartsWith("RPN:", StringComparison.OrdinalIgnoreCase))
                            {
                                string wyrRpn = newVal.Substring(4).Trim();
                                newVal = RpnCalculator.Evaluate(wyrRpn);
                            }

                            Type targetType = propInfo.PropertyType;

                            try
                            {
                                if (targetType == typeof(string)) wartoscDoZapisania = newVal;
                                else if (targetType == typeof(int)) wartoscDoZapisania = int.Parse(newVal, CultureInfo.InvariantCulture);
                                else if (targetType == typeof(double)) wartoscDoZapisania = double.Parse(newVal.Replace(",", "."), CultureInfo.InvariantCulture);
                                else if (targetType == typeof(bool)) wartoscDoZapisania = bool.Parse(newVal);
                                else if (rP == "ColorIndex" && targetType == typeof(Teigha.Colors.Color))
                                {
                                    if (int.TryParse(newVal, out int cIndex))
                                        wartoscDoZapisania = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, (short)cIndex);
                                }

                                if (wartoscDoZapisania != null)
                                {
                                    propInfo.SetValue(ent, wartoscDoZapisania, null);
                                    czyObiektZmodyfikowany = true;
                                }
                                else
                                {
                                    odrzucone++;
                                }
                            }
                            catch
                            {
                                odrzucone++;
                            }
                        }

                        if (czyObiektZmodyfikowany) udane++;
                    }

                    tr.Commit();
                    
                    if (udane > 0)
                        return $"SUKCES: Zmodyfikowano obiektów: {udane}. Odrzucono atrybutów niedopasowanych do encji: {odrzucone}.";
                    else
                        return $"BŁĄD: Żaden obiekt nie przyjął nowych właściwości (np. zła nazwa atrybutu).";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD WEWNĘTRZNY CAD: {ex.Message}";
            }
        }
    }
}
