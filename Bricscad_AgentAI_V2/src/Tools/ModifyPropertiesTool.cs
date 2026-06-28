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
                    Description = "Modyfikuje wlasciwosci systemowe zaznaczonych obiektow. Obsluguje wspolne wlasciwosci graficzne (Layer, Color, Linetype, LineWeight, Transparency) oraz wlasciwosci tekstow DBText/MText: TextStyleName, Justify, Attachment, Rotation, Height/TextHeight, Width, WidthFactor, Oblique, Position/Location, Annotative. NIE sluzy do zmiany tresci tekstu (Contents/Text/TextString) ani tresci wymiarow.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Modifications", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Lista docelowych modyfikacji. Format np.: [{\"Prop\": \"Layer\", \"Val\": \"OSIE\"}, {\"Prop\": \"Radius\", \"Val\": \"MATH: $OLD_RADIUS + 5\"}]"
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
                            string rP = ResolvePropertyAlias(ent, mod.Prop);

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

                            if (TryApplyTextProperty(doc.Database, tr, ent, rP, newVal, ostrzezenia, ref odrzucone, ref czyObiektZmodyfikowany))
                            {
                                continue;
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

                            // Przetworzenie MATH / RPN dla wartości numerycznych 
                            if (newVal.ToUpper().Contains("MATH:") || newVal.ToUpper().Contains("RPN:") || newVal.Contains("{MATH:"))
                            {
                                newVal = RpnCalculator.ProcessMathTemplates(newVal);
                            }

                            Type targetType = propInfo.PropertyType;

                            try
                            {
                                if (targetType == typeof(string)) wartoscDoZapisania = newVal;
                                else if (targetType == typeof(int)) wartoscDoZapisania = int.Parse(newVal, CultureInfo.InvariantCulture);
                                else if (targetType == typeof(double)) wartoscDoZapisania = double.Parse(newVal.Replace(",", "."), CultureInfo.InvariantCulture);
                                else if (targetType == typeof(bool)) wartoscDoZapisania = bool.Parse(newVal);
                                // KROK-CadTextProfile.16: Konwersja enum z nazwy (np. "MiddleCenter"
                                // dla AttachmentPoint) na wartosc enuma. Bez tego LLM podawal
                                // "MiddleCenter" jako string, reflection rzucal wyjatek,
                                // a PropertyValidator akceptowal (bo Attachment jest w API).
                                // Wspiera enum-y BricsCAD: AttachmentPoint, LineWeight,
                                // TransparencyMethod, LineWeight, DrawOrderType itd.
                                else if (targetType.IsEnum)
                                {
                                    // Parsowanie enum z nazwy (np. "MiddleCenter") - .NET Framework 4.8
                                    // nie ma Enum.TryParse(Type, string, out object), wiec uzywamy
                                    // refleksji do wywolania generycznej Enum.TryParse<T>(string, out T).
                                    // Fallbacki: PascalCase, case-insensitive match po nazwie, liczba.
                                    bool enumParsed = false;
                                    object enumValue = null;
                                    try
                                    {
                                        // Generyczna Enum.TryParse<TEnum>(string, bool ignoreCase, out TEnum result)
                                        var tryParseGeneric = typeof(Enum)
                                            .GetMethod("TryParse", new[] { typeof(string), typeof(bool), typeof(object).MakeByRefType() });
                                        // Nie mozna uzyc generycznej Enum.TryParse<TEnum>(string, out TEnum) -
                                        // wymaga typeof(TEnum) ktore znamy dopiero po typeof(targetType).
                                        // Probujemy wiec przez refleksje z object[].
                                        object[] tryParseArgs = new object[] { newVal, true, null };
                                        bool tpResult = (bool)tryParseGeneric
                                            .MakeGenericMethod(targetType)
                                            .Invoke(null, tryParseArgs);
                                        if (tpResult)
                                        {
                                            enumValue = tryParseArgs[2];
                                            enumParsed = true;
                                        }
                                    }
                                    catch
                                    {
                                        // Fallback: proste Enum.Parse (case-sensitive)
                                        try
                                        {
                                            enumValue = Enum.Parse(targetType, newVal);
                                            enumParsed = true;
                                        }
                                        catch { }
                                    }
                                    if (!enumParsed)
                                    {
                                        // Fallback: case-insensitive match po nazwie
                                        foreach (string name in Enum.GetNames(targetType))
                                        {
                                            if (string.Equals(name, newVal, StringComparison.OrdinalIgnoreCase))
                                            {
                                                enumValue = Enum.Parse(targetType, name);
                                                enumParsed = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (enumParsed)
                                    {
                                        wartoscDoZapisania = enumValue;
                                    }
                                    // Fallback: liczba (np. "5")
                                    else if (int.TryParse(newVal, out int enumInt) && Enum.IsDefined(targetType, enumInt))
                                    {
                                        wartoscDoZapisania = Enum.ToObject(targetType, enumInt);
                                    }
                                    else
                                    {
                                        var validValues = string.Join(", ", Enum.GetNames(targetType));
                                        ostrzezenia.Add($"[BŁĄD ENUM]: Nie mozna sparsowac '{newVal}' jako {targetType.Name}. " +
                                                         $"Dopuszczalne wartosci: {validValues}");
                                    }
                                }
                                else if (targetType == typeof(Teigha.Colors.Color))
                                {
                                    if (TryParseColor(newVal, out Teigha.Colors.Color color))
                                        wartoscDoZapisania = color;
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

        private static bool TryApplyTextProperty(
            Database db,
            Transaction tr,
            Entity ent,
            string propName,
            string value,
            HashSet<string> warnings,
            ref int rejected,
            ref bool modified)
        {
            string prop = NormalizePropertyName(propName);

            if (prop == "TEXTSTYLENAME" || prop == "TEXTSTYLE")
            {
                if (!(ent is DBText) && !(ent is MText)) return false;

                ObjectId styleId;
                if (!TryResolveTextStyleId(db, tr, value, out styleId))
                {
                    warnings.Add($"[BŁĄD STYLU]: Nie znaleziono stylu tekstu '{value}' w bieżącym rysunku.");
                    rejected++;
                    return true;
                }

                if (ent is DBText dbText)
                {
                    dbText.TextStyleId = styleId;
                    modified = true;
                    return true;
                }

                if (ent is MText mText)
                {
                    mText.TextStyleId = styleId;
                    modified = true;
                    return true;
                }
            }

            if (prop == "ATTACHMENT")
            {
                if (!(ent is MText mText)) return false;

                AttachmentPoint attachment;
                if (!TryParseEnumFlexible(value, out attachment))
                {
                    warnings.Add($"[BŁĄD ENUM]: Nie można sparsować '{value}' jako AttachmentPoint. Dopuszczalne wartości: {string.Join(", ", Enum.GetNames(typeof(AttachmentPoint)))}");
                    rejected++;
                    return true;
                }

                mText.Attachment = attachment;
                modified = true;
                return true;
            }

            if (prop == "JUSTIFY")
            {
                if (!(ent is DBText dbText)) return false;

                PropertyInfo justifyProperty = dbText.GetType().GetProperty("Justify", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (justifyProperty == null || !justifyProperty.CanWrite)
                {
                    warnings.Add("[OSTRZEŻENIE]: Nie można zapisać właściwości 'Justify' dla DBText (brak dostępu lub błąd refleksji).");
                    rejected++;
                    return true;
                }

                object justify;
                if (!TryParseEnumFlexible(justifyProperty.PropertyType, value, out justify))
                {
                    warnings.Add($"[BŁĄD ENUM]: Nie można sparsować '{value}' jako {justifyProperty.PropertyType.Name}. Dopuszczalne wartości: {string.Join(", ", Enum.GetNames(justifyProperty.PropertyType))}");
                    rejected++;
                    return true;
                }

                justifyProperty.SetValue(dbText, justify, null);
                try { dbText.AdjustAlignment(db); } catch { }
                modified = true;
                return true;
            }

            if (prop == "ANNOTATIVE" || prop == "OPISOWY" || prop == "ISANNOTATIVE")
            {
                bool enabled;
                if (!TryParseBoolFlexible(value, out enabled))
                {
                    warnings.Add($"[BŁĄD BOOL]: Nie można sparsować '{value}' jako wartości logicznej dla Annotative/Opisowy.");
                    rejected++;
                    return true;
                }

                ent.Annotative = enabled ? AnnotativeStates.True : AnnotativeStates.False;
                modified = true;
                return true;
            }

            return false;
        }

        private static string ResolvePropertyAlias(Entity ent, string propName)
        {
            string prop = NormalizePropertyName(propName);

            if (ent is MText)
            {
                if (prop == "MASKATLA" || prop == "BACKGROUNDMASK" || prop == "MASK" ||
                    prop == "TLO" || prop == "BACKGROUNDFILL")
                {
                    return "BackgroundFill";
                }

                if (prop == "RAMKA" || prop == "RAMKATEKSTU" || prop == "TEXTFRAME" ||
                    prop == "FRAME" || prop == "BORDER" || prop == "SHOWBORDERS")
                {
                    return "ShowBorders";
                }

                if (prop == "KOLORMASKI" || prop == "BACKGROUNDCOLOR" || prop == "MASKCOLOR" ||
                    prop == "BACKGROUNDFILLCOLOR")
                {
                    return "BackgroundFillColor";
                }

                if (prop == "KOLORTLARYSUNKU" || prop == "UZYJKOLORUTLARYSUNKU" ||
                    prop == "USEBACKGROUND" || prop == "USEBACKGROUNDCOLOR" ||
                    prop == "DRAWINGBACKGROUNDCOLOR")
                {
                    return "UseBackgroundColor";
                }

                if (prop == "MARGINESMASKI" || prop == "MASKSCALE" || prop == "BACKGROUNDSCALE" ||
                    prop == "BACKGROUNDSCALEFACTOR")
                {
                    return "BackgroundScaleFactor";
                }
            }

            return propName;
        }

        private static bool TryResolveTextStyleId(Database db, Transaction tr, string styleName, out ObjectId styleId)
        {
            styleId = ObjectId.Null;
            if (string.IsNullOrWhiteSpace(styleName)) return false;

            var table = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!table.Has(styleName)) return false;

            styleId = table[styleName];
            return !styleId.IsNull;
        }

        private static bool TryParseBoolFlexible(string value, out bool result)
        {
            result = false;
            if (bool.TryParse(value, out result)) return true;

            string normalized = (value ?? "").Trim().ToLowerInvariant();
            if (normalized == "1" || normalized == "tak" || normalized == "yes" ||
                normalized == "on" || normalized == "wlacz" || normalized == "włącz" ||
                normalized == "true")
            {
                result = true;
                return true;
            }

            if (normalized == "0" || normalized == "nie" || normalized == "no" ||
                normalized == "off" || normalized == "wylacz" || normalized == "wyłącz" ||
                normalized == "false")
            {
                result = false;
                return true;
            }

            return false;
        }

        private static bool TryParseColor(string value, out Teigha.Colors.Color color)
        {
            color = null;
            string normalized = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalized)) return false;

            if (normalized.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByLayer, 256);
                return true;
            }

            if (normalized.Equals("ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByBlock, 0);
                return true;
            }

            if (int.TryParse(normalized, out int colorIndex))
            {
                color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, (short)colorIndex);
                return true;
            }

            switch (NormalizePropertyName(normalized))
            {
                case "CZERWONY":
                case "RED":
                    color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1);
                    return true;
                case "ZOLTY":
                case "YELLOW":
                    color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 2);
                    return true;
                case "ZIELONY":
                case "GREEN":
                    color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 3);
                    return true;
                case "CYJAN":
                case "CYAN":
                    color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 4);
                    return true;
                case "NIEBIESKI":
                case "BLUE":
                    color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 5);
                    return true;
                case "MAGENTA":
                case "FIOLETOWY":
                    color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 6);
                    return true;
                case "BIALY":
                case "WHITE":
                    color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 7);
                    return true;
            }

            return false;
        }

        private static bool TryParseEnumFlexible<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            if (Enum.TryParse(value, true, out result)) return true;

            string normalizedValue = NormalizePropertyName(value);
            foreach (string name in Enum.GetNames(typeof(TEnum)))
            {
                if (NormalizePropertyName(name).Equals(normalizedValue, StringComparison.OrdinalIgnoreCase))
                {
                    result = (TEnum)Enum.Parse(typeof(TEnum), name);
                    return true;
                }
            }

            int numeric;
            if (int.TryParse(value, out numeric) && Enum.IsDefined(typeof(TEnum), numeric))
            {
                result = (TEnum)Enum.ToObject(typeof(TEnum), numeric);
                return true;
            }

            result = default(TEnum);
            return false;
        }

        private static bool TryParseEnumFlexible(Type enumType, string value, out object result)
        {
            try
            {
                result = Enum.Parse(enumType, value, true);
                return true;
            }
            catch
            {
                string normalizedValue = NormalizePropertyName(value);
                foreach (string name in Enum.GetNames(enumType))
                {
                    if (NormalizePropertyName(name).Equals(normalizedValue, StringComparison.OrdinalIgnoreCase))
                    {
                        result = Enum.Parse(enumType, name);
                        return true;
                    }
                }

                int numeric;
                if (int.TryParse(value, out numeric) && Enum.IsDefined(enumType, numeric))
                {
                    result = Enum.ToObject(enumType, numeric);
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static string NormalizePropertyName(string value)
        {
            return new string((value ?? "")
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        public List<string> Examples => null;
    }
}

