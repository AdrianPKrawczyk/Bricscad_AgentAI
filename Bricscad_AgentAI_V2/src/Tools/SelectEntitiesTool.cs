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
    public class SelectEntitiesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "SelectEntities",
                    Description = "Inteligentnie wyszukuje i izoluje graficzne obiekty w obszarze roboczym bazując na hierarchii klas i ich cechach (np. Długość, Kolor, Warstwa). Pozwala kumulować i odejmować obiekty od selekcji.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Mode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tryb operacji na stanie selekcji. 'New' = Kasuje stara logike i zastepuje nową selekcja. 'Add' = Wzbogaca kolekcje o ułamki, 'Remove' = Określa zbiór by odejmować, 'Clear' = Wyłącza wszystko. Domyślnie 'New'.",
                                    Enum = new List<string> { "New", "Add", "Remove", "Clear" }
                                }
                            },
                            {
                                "Scope", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Przestrzeń poszukiwań. 'Model' to cały rysunek (zakładając że znajdujemy się w nim we współrzędnych globalnych). 'Blocks' przeszukuje obiekty które aktualnie widnieją WYŁĄCZNIE W DOTYCHCZASOWYCH OBIEKTACH AgentMemoryState (filtrowanie schodkowe ukrytego wymiaru). Domyślnie 'Model'.",
                                    Enum = new List<string> { "Model", "Blocks" }
                                }
                            },
                            {
                                "EntityType", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Klasa obiektu CAD jakiego poszukujesz. Obsługuje filtry Wildcard zaczynające się gwiazdką np. '*Line' dopasuje zarowno Line jak i Polyline. Użyj '*' albo '*Entity' w poszukiwaniu wszystkich klas."
                                }
                            },
                            {
                                "Conditions", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Tablica słowników warunków. Przykładowa konwencja: [ { \"Prop\": \"Layer\", \"Op\": \"==\", \"Val\": \"ściany_nośne\" }, { \"Prop\": \"Length\", \"Op\": \">=\", \"Val\": \"25.0\" } ]. Operator op = \"in\" działa dla Val postaci po przecinku np. \"1,2,3\""
                                }
                            },
                            {
                                "AdvancedFilters", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Tablica zaawansowanych filtrów dla ukrytych właściwości (np. Transparency, TextOverride). Używaj, gdy potrzebne są operacje arytmetyczne lub przeszukiwanie tekstu.",
                                    Items = new JObject
                                    {
                                        ["type"] = "object",
                                        ["properties"] = new JObject
                                        {
                                            ["Prop"] = new JObject { ["type"] = "string", ["description"] = "Nazwa właściwości (np. 'Transparency', 'TextOverride', 'MText.Text')." },
                                            ["Op"] = new JObject { ["type"] = "string", ["description"] = "Operator: '>', '<', '>=', '<=', '==', '!=', 'Contains', 'NotContains'." },
                                            ["Val"] = new JObject { ["type"] = "string", ["description"] = "Wartość do porównania." }
                                        }
                                    }
                                }
                            }
                        },
                        Required = new List<string> { "EntityType" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var ed = doc.Editor;
            
            string entityTypeStr = args["EntityType"]?.ToString() ?? "Entity";
            if (entityTypeStr.Contains("*Entity") || entityTypeStr == "*") entityTypeStr = "Entity";
            
            string trybStr = args["Mode"]?.ToString() ?? "New";
            string scopeStr = args["Scope"]?.ToString() ?? "Model";
            
            var warunki = new List<(string Prop, string Op, string Val)>();
            
            if (args["Conditions"] is JArray conditionsArray)
            {
                foreach (JObject condition in conditionsArray)
                {
                    string prop = condition["Prop"]?.ToString()?.Trim();
                    string op = condition["Op"]?.ToString()?.Trim();
                    string val = condition["Val"]?.ToString()?.Trim();
                    
                    if (!string.IsNullOrEmpty(prop) && !string.IsNullOrEmpty(op))
                    {
                        warunki.Add((prop, op, val ?? ""));
                    }
                }
            }

            var advancedFilters = new List<(string Prop, string Op, string Val)>();
            if (args["AdvancedFilters"] is JArray advArray)
            {
                foreach (JObject filter in advArray)
                {
                    string prop = filter["Prop"]?.ToString()?.Trim();
                    string op = filter["Op"]?.ToString()?.Trim();
                    string val = filter["Val"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(prop) && !string.IsNullOrEmpty(op))
                    {
                        advancedFilters.Add((prop, op, val ?? ""));
                    }
                }
            }
            
            if (string.IsNullOrEmpty(entityTypeStr)) return "Błąd pusta encja filtra.";
            
            if (entityTypeStr.Equals("Clear", StringComparison.OrdinalIgnoreCase) || trybStr.Equals("Clear", StringComparison.OrdinalIgnoreCase))
            {
                ed.SetImpliedSelection(new ObjectId[0]);
                AgentMemoryState.Clear();
                return "SUKCES: Odznaczono wszystkie obiekty, wyczyszczono pamięć Agenta.";
            }

            string[] typyDoSzukania = entityTypeStr.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<ObjectId> znalezioneObiekty = new List<ObjectId>();
            
            try
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    List<ObjectId> blokiDoPrzeszukania = new List<ObjectId>();
                    
                    if (scopeStr.Equals("Blocks", StringComparison.OrdinalIgnoreCase) && AgentMemoryState.ActiveSelection.Length > 0)
                    {
                        foreach (ObjectId id in AgentMemoryState.ActiveSelection)
                        {
                            Entity zaznaczonyEnt = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (zaznaczonyEnt is BlockReference br && !blokiDoPrzeszukania.Contains(br.BlockTableRecord))
                                blokiDoPrzeszukania.Add(br.BlockTableRecord);
                        }

                        for (int i = 0; i < blokiDoPrzeszukania.Count; i++)
                        {
                            BlockTableRecord wewnetrznyBtr = (BlockTableRecord)tr.GetObject(blokiDoPrzeszukania[i], OpenMode.ForRead);
                            foreach (ObjectId wewnetrzneId in wewnetrznyBtr)
                            {
                                Entity wewnEnt = tr.GetObject(wewnetrzneId, OpenMode.ForRead) as Entity;
                                if (wewnEnt is BlockReference zagniezdzonyBr && !blokiDoPrzeszukania.Contains(zagniezdzonyBr.BlockTableRecord))
                                    blokiDoPrzeszukania.Add(zagniezdzonyBr.BlockTableRecord);
                            }
                        }
                    }
                    else
                    {
                        blokiDoPrzeszukania.Add(doc.Database.CurrentSpaceId);
                    }

                    foreach (ObjectId spaceId in blokiDoPrzeszukania)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);

                        foreach (ObjectId objId in btr)
                        {
                            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            string nazwaTypuEnt = ent.GetType().Name;
                            bool typPasuje = false;

                            foreach (var t in typyDoSzukania)
                            {
                                string szukanyTyp = t.Trim();
                                if (szukanyTyp.Equals("Entity", StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }
                                if (szukanyTyp.Equals("Text", StringComparison.OrdinalIgnoreCase) && ent is DBText) { typPasuje = true; break; }
                                if (szukanyTyp.Equals("Dimension", StringComparison.OrdinalIgnoreCase) && ent is Dimension) { typPasuje = true; break; }

                                if (szukanyTyp.StartsWith("*") && nazwaTypuEnt.EndsWith(szukanyTyp.Substring(1), StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }
                                if (nazwaTypuEnt.Equals(szukanyTyp, StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }
                            }
                            
                            if (!typPasuje) continue;

                            bool spelniaWszystkie = true;
                            foreach (var warunek in warunki)
                            {
                                string rzeczywistaWlasciwosc = warunek.Prop;
                                bool sprawdzajWizualnie = false;

                                if (rzeczywistaWlasciwosc.Equals("VisualColor", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "Color"; sprawdzajWizualnie = true; }
                                else if (rzeczywistaWlasciwosc.Equals("VisualLinetype", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "Linetype"; sprawdzajWizualnie = true; }
                                else if (rzeczywistaWlasciwosc.Equals("VisualLineWeight", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "LineWeight"; sprawdzajWizualnie = true; }
                                else if (rzeczywistaWlasciwosc.Equals("VisualTransparency", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "Transparency"; sprawdzajWizualnie = true; }
                                else if (ent is MText && rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase)) rzeczywistaWlasciwosc = "TextHeight";
                                else if (ent is Dimension && (rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase) || rzeczywistaWlasciwosc.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))) rzeczywistaWlasciwosc = "Dimtxt";
                                else if (rzeczywistaWlasciwosc.Equals("Value", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (ent is DBText) rzeczywistaWlasciwosc = "TextString";
                                    else if (ent is MText) rzeczywistaWlasciwosc = "Text";
                                }

                                string[] zagniezdzenia = rzeczywistaWlasciwosc.Split('.');
                                object wartoscObiektu = ent;
                                
                                try 
                                {
                                    foreach (string czesc in zagniezdzenia)
                                    {
                                        if (wartoscObiektu == null) break;
                                        var propInfo = wartoscObiektu.GetType().GetProperty(czesc, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                                        if (propInfo != null && propInfo.CanRead)
                                        {
                                            wartoscObiektu = propInfo.GetValue(wartoscObiektu);
                                        }
                                        else
                                        {
                                            wartoscObiektu = null;
                                            break;
                                        }
                                    }
                                } 
                                catch 
                                { 
                                    wartoscObiektu = null; 
                                }

                                if (wartoscObiektu == null) { spelniaWszystkie = false; break; }

                                string valStr = wartoscObiektu.ToString();

                                if (wartoscObiektu is Teigha.Colors.Color colorObj)
                                {
                                    if (colorObj.IsByLayer) valStr = "256";
                                    else if (colorObj.IsByBlock) valStr = "0";
                                    else if (colorObj.IsByColor) valStr = $"{colorObj.Red},{colorObj.Green},{colorObj.Blue}";
                                    else valStr = colorObj.ColorIndex.ToString();
                                }
                                else if (wartoscObiektu is Teigha.Colors.Transparency transp)
                                {
                                    if (transp.IsByLayer) valStr = "ByLayer";
                                    else if (transp.IsByBlock) valStr = "ByBlock";
                                    else 
                                    {
                                        long uiVal = (long)Math.Round(100.0 - (transp.Alpha * 100.0 / 255.0));
                                        valStr = uiVal.ToString();
                                    }
                                }
                                else if (wartoscObiektu is Teigha.DatabaseServices.AnnotativeStates annState)
                                {
                                    valStr = annState == Teigha.DatabaseServices.AnnotativeStates.True ? "True" : "False";
                                }
                                else if (wartoscObiektu is bool bVal)
                                {
                                    valStr = bVal ? "True" : "False";
                                }
                                else if (wartoscObiektu is Teigha.Geometry.Point3d pt)
                                {
                                    string ptX = Math.Round(pt.X, 4).ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    string ptY = Math.Round(pt.Y, 4).ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    string ptZ = Math.Round(pt.Z, 4).ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    valStr = $"({ptX},{ptY},{ptZ})";
                                }
                                else if (wartoscObiektu is Teigha.DatabaseServices.LineWeight lw)
                                {
                                    if (lw == Teigha.DatabaseServices.LineWeight.ByLayer) valStr = "-1";
                                    else if (lw == Teigha.DatabaseServices.LineWeight.ByBlock) valStr = "-2";
                                    else if (lw == Teigha.DatabaseServices.LineWeight.ByLineWeightDefault) valStr = "-3";
                                    else valStr = ((int)lw).ToString();

                                    if (sprawdzajWizualnie && lw == Teigha.DatabaseServices.LineWeight.ByLayer)
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null)
                                            {
                                                if (ltr.LineWeight == Teigha.DatabaseServices.LineWeight.ByLineWeightDefault) valStr = "-3";
                                                else if (ltr.LineWeight == Teigha.DatabaseServices.LineWeight.ByLayer) valStr = "-1";
                                                else valStr = ((int)ltr.LineWeight).ToString();
                                            }
                                        }
                                        catch { valStr = "-3"; }
                                    }
                                }

                                if (sprawdzajWizualnie)
                                {
                                    if (rzeczywistaWlasciwosc == "Color" && valStr == "256")
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null) 
                                            {
                                                if (ltr.Color.IsByColor) valStr = $"{ltr.Color.Red},{ltr.Color.Green},{ltr.Color.Blue}";
                                                else valStr = ltr.Color.ColorIndex.ToString();
                                            }
                                        }
                                        catch { }
                                    }
                                    else if (rzeczywistaWlasciwosc.Equals("Linetype", StringComparison.OrdinalIgnoreCase) && valStr.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null)
                                            {
                                                LinetypeTableRecord lttr = tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                                                if (lttr != null) valStr = lttr.Name;
                                            }
                                        }
                                        catch { }
                                    }
                                }

                                bool warunekSpelniony = ValidateLogicCondition(valStr, warunek.Op, warunek.Val);
                                
                                if (!warunekSpelniony) { spelniaWszystkie = false; break; }
                            }

                            if (spelniaWszystkie && advancedFilters.Count > 0)
                            {
                                foreach (var filter in advancedFilters)
                                {
                                    try 
                                    {
                                        object currentVal = ent;
                                        string[] parts = filter.Prop.Split('.');
                                        
                                        if (filter.Prop.Equals("TextOverride", StringComparison.OrdinalIgnoreCase) || filter.Prop.Equals("DimensionText", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (ent is Dimension dim) currentVal = dim.DimensionText;
                                            else if (ent is MText mt) currentVal = mt.Contents;
                                            else if (ent is DBText txt) currentVal = txt.TextString;
                                            else { spelniaWszystkie = false; break; }
                                        }
                                        else if (filter.Prop.Equals("Transparency", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // Konwersja Alpha (0-255) na Procenty (0-90) zgodnie z wymogiem architekta
                                            byte alpha = ent.Transparency.Alpha;
                                            double transPercent = Math.Round(100.0 - (alpha * 100.0 / 255.0)); 
                                            if (transPercent > 90) transPercent = 90.0;
                                            currentVal = transPercent;
                                        }
                                        else 
                                        {
                                            // [HOTFIX v2.14.0]: JAWNE RZUTOWANIE (Hard-cast fallback) dla problematycznych właściwości Teigha
                                            if (ent is Hatch hatch && filter.Prop.Equals("HatchObjectType", StringComparison.OrdinalIgnoreCase))
                                            {
                                                currentVal = (int)hatch.HatchObjectType;
                                            }
                                            else if (ent is Dimension dim && (filter.Prop.Equals("DimensionText", StringComparison.OrdinalIgnoreCase) || filter.Prop.Equals("TextOverride", StringComparison.OrdinalIgnoreCase)))
                                            {
                                                currentVal = dim.DimensionText;
                                            }
                                            else if (ent is MText mtext && (filter.Prop.Equals("TextOverride", StringComparison.OrdinalIgnoreCase) || filter.Prop.Equals("Contents", StringComparison.OrdinalIgnoreCase)))
                                            {
                                                currentVal = mtext.Contents;
                                            }
                                            else if (ent is DBText dbtext && (filter.Prop.Equals("TextOverride", StringComparison.OrdinalIgnoreCase) || filter.Prop.Equals("TextString", StringComparison.OrdinalIgnoreCase)))
                                            {
                                                currentVal = dbtext.TextString;
                                            }
                                            else
                                            {
                                                // Standardowy mechanizm C# Reflection
                                                foreach (string part in parts)
                                                {
                                                    if (currentVal == null) break;
                                                    var pInfo = currentVal.GetType().GetProperty(part, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                                                    if (pInfo != null) currentVal = pInfo.GetValue(currentVal);
                                                    else { currentVal = null; break; }
                                                }
                                            }
                                        }

                                        if (currentVal == null) { spelniaWszystkie = false; break; }
                                        if (!ValidateLogicCondition(currentVal.ToString(), filter.Op, filter.Val)) { spelniaWszystkie = false; break; }
                                    }
                                    catch { spelniaWszystkie = false; break; }
                                }
                            }
                            
                            if (spelniaWszystkie) znalezioneObiekty.Add(objId);
                        }
                    }

                    if (trybStr.Equals("Add", StringComparison.OrdinalIgnoreCase))
                    {
                        AgentMemoryState.Append(znalezioneObiekty.ToArray());
                    }
                    else if (trybStr.Equals("Remove", StringComparison.OrdinalIgnoreCase))
                    {
                        AgentMemoryState.Remove(znalezioneObiekty.ToArray());
                    }
                    else
                    {
                        AgentMemoryState.Update(znalezioneObiekty.Distinct().ToArray());
                    }

                    var iloscKoncowa = AgentMemoryState.ActiveSelection.Length;

                    if (iloscKoncowa > 0)
                    {
                        if (!scopeStr.Equals("Blocks", StringComparison.OrdinalIgnoreCase))
                        {
                            try { ed.SetImpliedSelection(AgentMemoryState.ActiveSelection); }
                            catch { }
                        }
                        return $"SUKCES: Aktywne zaznaczenie w pamięci (Twarda relacja '{trybStr}'): {iloscKoncowa} obiekt(ów).";
                    }
                    else
                    {
                        ed.SetImpliedSelection(new ObjectId[0]);
                        AgentMemoryState.Clear();
                        tr.Commit();
                        return "WYNIK: Niczego po tej modyfikacji nie ma w pamięci dla tej maski. Operacja bezskuteczna, pamięć ZEROWA.";
                    }
                }
            }
            catch (System.Exception ex)
            {
                return $"BŁĄD Zaznaczania z bazy danych: {ex.Message}";
            }
        }
        
        public static bool ValidateLogicCondition(string valStr, string op, string warVal)
        {
            if (valStr == null) return false;
            if (warVal == null) return false;
            
            bool warunekSpelniony = false;
            
            if (double.TryParse(valStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double valNum) &&
                double.TryParse(warVal.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double warNum))
            {
                switch (op)
                {
                    case "==": warunekSpelniony = Math.Abs(valNum - warNum) < 0.0001; break;
                    case "!=": warunekSpelniony = Math.Abs(valNum - warNum) >= 0.0001; break;
                    case ">": warunekSpelniony = valNum > warNum; break;
                    case "<": warunekSpelniony = valNum < warNum; break;
                    case ">=": warunekSpelniony = valNum >= warNum; break;
                    case "<=": warunekSpelniony = valNum <= warNum; break;
                }
            }
            else
            {
                switch (op.ToLower())
                {
                    case "==": warunekSpelniony = valStr.Replace(" ", "").Equals(warVal.Replace(" ", ""), StringComparison.OrdinalIgnoreCase); break;
                    case "!=": warunekSpelniony = !valStr.Replace(" ", "").Equals(warVal.Replace(" ", ""), StringComparison.OrdinalIgnoreCase); break;
                    case "contains": warunekSpelniony = valStr.IndexOf(warVal, StringComparison.OrdinalIgnoreCase) >= 0; break;
                    case "notcontains": warunekSpelniony = valStr.IndexOf(warVal, StringComparison.OrdinalIgnoreCase) < 0; break;
                    case "in":
                        string[] mozliweWartosci = warVal.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string mw in mozliweWartosci)
                        {
                            if (valStr.Equals(mw.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                warunekSpelniony = true;
                                break;
                            }
                        }
                        break;
                }
            }
            return warunekSpelniony;
        }
        public List<string> Examples => null;
    }
}

