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
                    Description = "Służy WYŁĄCZNIE do modyfikacji podstawowych właściwości graficznych wspólnych dla wszystkich obiektów (Layer, Color, Linetype, Lineweight, Transparency). UWAGA: NIE UŻYWAJ tego narzędzia do edycji tekstów, wymiarów (Dimension), ani zawartości bloków. Do tych celów musisz załadować specjalistyczne kategorie narzędzi używając RequestAdditionalTools.",
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
            var ostrzezenia = new HashSet<string>();

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
                        string className = ent.GetType().Name;

                        foreach (var mod in modyfikacje)
                        {
                            string propToCheck = mod.Prop;
                            string[] forbiddenProps = { "Text", "TextOverride", "DimensionText", "Contents", "Dimscale", "Dimblk" };

                            if (forbiddenProps.Any(p => p.Equals(propToCheck, StringComparison.OrdinalIgnoreCase)))
                            {
                                return $"BŁĄD: Narzędzie ModifyPropertiesTool nie obsługuje właściwości '{propToCheck}'. Aby edytować teksty lub wymiary, użyj specjalistycznych narzędzi (np. DimensionEditTool).";
                            }

                            object wartoscDoZapisania = null;
                            string rP = mod.Prop;

                            // TARCZA ANTY-HALUCYNACYJNA V2
                            if (!Bricscad_AgentAI_V2.Core.PropertyValidator.IsPropertyValid(className, rP))
                            {
                                ostrzezenia.Add($"[OSTRZEŻENIE]: Pominięto właściwość '{rP}', ponieważ obiekt klasy '{className}' jej nie posiada.");
                                odrzucone++;
                                continue;
                            }

                            string newVal = AgentMemoryState.InjectVariables(mod.Val); // Wstrzykiwanie zmiennych
                            
                            // Mapowanie wizualnych własności (Normalizacja nazw)
                            string targetPropName = rP;
                            if (targetPropName.Equals("Color", StringComparison.OrdinalIgnoreCase) || targetPropName.Equals("ColorIndex", StringComparison.OrdinalIgnoreCase)) targetPropName = "ColorIndex";
                            if (ent is MText && targetPropName.Equals("Height", StringComparison.OrdinalIgnoreCase)) targetPropName = "TextHeight";
                            if (ent is Dimension && (targetPropName.Equals("Height", StringComparison.OrdinalIgnoreCase) || targetPropName.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))) targetPropName = "Dimtxt";
                            if (targetPropName.Equals("Value", StringComparison.OrdinalIgnoreCase))
                            {
                                if (ent is DBText) targetPropName = "TextString";
                                else if (ent is MText) targetPropName = "Text";
                            }

                            // Ręczna obsługa Transparency (Konwersja 0-90 UI -> Alpha)
                            if (targetPropName.Equals("Transparency", StringComparison.OrdinalIgnoreCase))
                            {
                                if (newVal.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)) ent.Transparency = new Teigha.Colors.Transparency(Teigha.Colors.TransparencyMethod.ByLayer);
                                else if (newVal.Equals("ByBlock", StringComparison.OrdinalIgnoreCase)) ent.Transparency = new Teigha.Colors.Transparency(Teigha.Colors.TransparencyMethod.ByBlock);
                                else if (byte.TryParse(newVal, out byte uiAlpha))
                                {
                                    // Konwersja UI (0-100) na Alpha (255-0)
                                    byte realAlpha = (byte)Math.Round(255.0 * (100.0 - uiAlpha) / 100.0);
                                    ent.Transparency = new Teigha.Colors.Transparency(realAlpha);
                                }
                                czyObiektZmodyfikowany = true;
                                continue; 
                            }

                            // Szukanie właściwości przez Reflection
                            PropertyInfo propInfo = ent.GetType().GetProperty(targetPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            
                            if (propInfo == null || !propInfo.CanWrite || !propInfo.CanRead)
                            {
                                ostrzezenia.Add($"[OSTRZEŻENIE]: Nie można zapisać właściwości '{rP}' dla {className} (brak dostępu lub błąd refleksji).");
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
                                else if (targetPropName == "ColorIndex" && targetType == typeof(Teigha.Colors.Color))
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
                            catch (Exception)
                            {
                                ostrzezenia.Add($"[BŁĄD]: Nie udało się przekonwertować wartości '{newVal}' na typ {targetType.Name} dla właściwości {rP}.");
                                odrzucone++;
                            }
                        }

                        if (czyObiektZmodyfikowany) udane++;
                    }

                    tr.Commit();
                }

                string raport = udane > 0 
                    ? $"SUKCES: Zmodyfikowano obiektów: {udane}. Odrzucono atrybutów: {odrzucone}."
                    : $"BŁĄD: Żaden obiekt nie został zmodyfikowany.";

                if (ostrzezenia.Count > 0)
                {
                    raport += "\n\nLOGI WALIDATORA:\n" + string.Join("\n", ostrzezenia);
                }

                return raport;
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY CAD: {ex.Message}";
            }
        }
        public List<string> Examples => null;
    }
}

