using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzedzie do inspekcji pol CAD (Field) w zaznaczonych obiektach tekstowych.
    /// Odczytuje kody pol, rozwiązane wartosci i heurystycznie wykrywa kategorie.
    /// Nie wymaga LockDocument - praca tylko w trybie odczytu.
    /// </summary>
    public class ReadFieldsTool : IToolV2
    {
        // Skladnia pola DWG moze przyjac dwie formy:
        //
        // A) Forma czytelna (po ewaluacji i zapisie jako tekst):
        //      %<\AcKat "argument">
        //    Kategorie: AcVar (SystemVariable), AcExpr (Expression),
        //    AcDim (DimensionProperty), AcObjProp (ObjectProperty),
        //    AcProp (BlockAttributeProperty), AcDate (DateTime),
        //    AcFido (SheetSet).
        //
        // B) Forma binarna (przed ewaluacja / zapisana w DWG):
        //      %<\_FldIdx HEX>
        //    Indeks pola w tablicy FieldReference, bez jawnej kategorii.
        //    Wystepuje gdy rysunek nie zostal przeliczony (_.REGEN) lub
        //    gdy pole ma niestandardowy ewaluator.
        //
        // UWAGA: regex w C# "\\Ac" oznacza dopasowanie "\Ac" jako literal.
        // Grupa przechwytuje "Var"/"Date"/"Expr"/... (bez prefiksu Ac).
        private static readonly Regex FieldReadableRegex =
            new Regex(@"%<\\Ac([A-Za-z][A-Za-z0-9_]*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FieldBinaryRegex =
            new Regex(@"%<\\\s*_FldIdx\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadFields",
                    Description = "Inspekcja pol CAD (Field) w zaznaczonych obiektach tekstowych (DBText, MText, AttributeReference, AttributeDefinition, MLeader). " +
                                  "Zwraca liste pol z kodem zrodlowym %<\\Ac...>, rozwiazana wartoscia oraz heurystycznie wykryta kategoria " +
                                  "(SystemVariable, Expression, DimensionProperty, ObjectProperty, BlockAttributeProperty, DateTime, SheetSet, Unknown). " +
                                  "ZAWSZE wywoluj to narzedzie przed edycja tekstu, ktory moze zawierac pola, aby nie zniszczyc mechanizmu dynamicznego.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zmiennej (bez @), pod ktora raport JSON zostanie zapisany w pamieci Agenta."
                                }
                            },
                            {
                                "IncludeRawCode", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy dolaczyc oryginalny kod pola %<\\Ac...> do raportu (domyslnie true)."
                                }
                            },
                            {
                                "FilterByCategory", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny filtr kategorii: SystemVariable, Expression, DimensionProperty, ObjectProperty, BlockAttributeProperty, DateTime, SheetSet, BinaryField (forma %<\\_FldIdx - pole przed ewaluacja), Unknown.",
                                    Enum = new List<string>
                                    {
                                        "SystemVariable", "Expression", "DimensionProperty",
                                        "ObjectProperty", "BlockAttributeProperty", "DateTime",
                                        "SheetSet", "BinaryField", "Unknown"
                                    }
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
                return "BLAD: Brak zaznaczonych obiektow w pamieci Agenta. Uzyj najpierw SelectEntities, aby wybrac obiekty tekstowe.";
            }

            string saveAs = args["SaveAs"]?.ToString();
            bool includeRawCode = args["IncludeRawCode"] == null
                ? true
                : args["IncludeRawCode"].Value<bool>();
            string filterCategory = args["FilterByCategory"]?.ToString();

            var reportEntries = new List<Dictionary<string, string>>();
            int entitiesWithFields = 0;
            int totalFieldsFound = 0;
            var skippedEntities = new List<string>();
            var warnings = new HashSet<string>();

            try
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null)
                        {
                            skippedEntities.Add($"[{id}] nie jest encja");
                            continue;
                        }

                        string entityType = ent.GetType().Name;

                        // Specjalny przypadek: BlockReference - skanujemy jego atrybuty.
                        if (ent is BlockReference br)
                        {
                            int blockFields = ScanBlockAttributes(br, filterCategory, includeRawCode, ref reportEntries, ref warnings);
                            if (blockFields > 0)
                            {
                                entitiesWithFields++;
                                totalFieldsFound += blockFields;
                            }
                            continue;
                        }

                        string rawContents = ExtractRawContents(ent, warnings);
                        string resolvedContents = ExtractResolvedContents(ent);
                        bool hasFieldsFlag = SafeHasFields(ent);

                        if (!hasFieldsFlag && !LooksLikeContainsField(rawContents))
                        {
                            continue;
                        }

                        entitiesWithFields++;
                        var foundInEntity = ExtractFieldsFromText(rawContents);
                        string resolvedStatus = DetectResolvedStatus(ent);

                        if (foundInEntity.Count == 0 && hasFieldsFlag)
                        {
                            // Flaga HasFields=true, ale nie udalo sie wyciagnac kodu -
                            // raportuj z placeholderem zeby Agent wiedzial, ze pole jest obecne.
                            foundInEntity.Add(new FieldInfo
                            {
                                Code = "<nie mozna odczytac kodu>",
                                Category = "Unknown",
                                ResolvedValue = resolvedContents ?? ""
                            });
                        }
                        else if (foundInEntity.Count == 0)
                        {
                            // Detectowano "%<" heurestycznie ale parser nic nie znalazl.
                            warnings.Add($"[{entityType}#{id}] Wykryto znacznik %< ale nie rozpoznano zadnej kategorii. Surowa tresc: '{Truncate(rawContents, 120)}'");
                            foundInEntity.Add(new FieldInfo
                            {
                                Code = Truncate(rawContents, 80),
                                Category = "Unknown",
                                ResolvedValue = resolvedContents ?? ""
                            });
                        }

                        foreach (var fi in foundInEntity)
                        {
                            totalFieldsFound++;
                            if (!string.IsNullOrEmpty(filterCategory)
                                && !fi.Category.Equals(filterCategory, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var entry = new Dictionary<string, string>
                            {
                                { "EntityType", entityType },
                                { "EntityId", id.ToString() },
                                { "Category", fi.Category },
                                { "ResolvedValue", fi.ResolvedValue ?? "" },
                                { "ResolvedStatus", resolvedStatus }
                            };
                            if (includeRawCode)
                            {
                                entry["Code"] = fi.Code ?? "";
                            }
                            reportEntries.Add(entry);
                        }
                    }
                    tr.Commit();
                }

                if (reportEntries.Count == 0)
                {
                    if (!string.IsNullOrEmpty(filterCategory))
                    {
                        return $"WYNIK: W zaznaczeniu nie znaleziono pol kategorii '{filterCategory}'. " +
                               $"Lacznie sprawdzono {ids.Length} obiektow tekstowych.";
                    }
                    return $"WYNIK: W zaznaczeniu ({ids.Length} obiektow) nie wykryto zadnych pol CAD " +
                           $"(brak '%<' w tresci oraz flaga HasFields=false na wszystkich obiektach tekstowych).";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"WYNIK: Znaleziono {totalFieldsFound} pol w {entitiesWithFields} obiektach " +
                              $"(sprawdzono {ids.Length}):");

                // Jesli wszystkie wykryte pola sa w formie binarnej (_FldIdx),
                // poinformuj o koniecznosci przeliczenia - kategorie nie beda dokladne.
                int binaryCount = reportEntries.Count(e =>
                    e.TryGetValue("Category", out var c) && c == "BinaryField");
                if (binaryCount > 0 && binaryCount == reportEntries.Count)
                {
                    sb.AppendLine();
                    sb.AppendLine("UWAGA: Wszystkie wykryte pola sa w formie binarnej (%<\\_FldIdx).");
                    sb.AppendLine("Prawdopodobnie rysunek nie zostal przeliczony poleceniem _.REGEN,");
                    sb.AppendLine("albo pola maja niestandardowe ewaluatory. Wywolaj");
                    sb.AppendLine("ManageFieldsTool(EvaluateAll) aby wymusic przeliczenie i ponow odczyt.");
                }

                // Statystyka statusu ewaluacji.
                int notEvaluated = reportEntries.Count(e =>
                    e.TryGetValue("ResolvedStatus", out var s) && s == "NotYetEvaluated");
                int evaluated = reportEntries.Count - notEvaluated;
                if (notEvaluated > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Status ewaluacji: {evaluated} przeliczonych, " +
                                  $"{notEvaluated} nieprzeliczonych (wymagane _.REGEN).");
                }

                foreach (var entry in reportEntries)
                {
                    string codeVal;
                    entry.TryGetValue("Code", out codeVal);
                    string codePart = includeRawCode ? $" kod='{codeVal}'" : "";
                    string entityType, entityId, category, resolvedValue;
                    entry.TryGetValue("EntityType", out entityType);
                    entry.TryGetValue("EntityId", out entityId);
                    entry.TryGetValue("Category", out category);
                    entry.TryGetValue("ResolvedValue", out resolvedValue);
                    entry.TryGetValue("ResolvedStatus", out string status);
                    entry.TryGetValue("Tag", out string attrTag);
                    entry.TryGetValue("ParentBlock", out string parentBlock);
                    string ctx = !string.IsNullOrEmpty(parentBlock)
                        ? $" blok={parentBlock}/Tag={attrTag}"
                        : "";
                    sb.AppendLine(
                        $"- [{entityType}#{entityId}]{ctx} " +
                        $"kategoria={category} status={status ?? "?"}{codePart} " +
                        $"wartosc='{Truncate(resolvedValue, 120)}'");
                }

                if (warnings.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("UWAGI:");
                    foreach (var w in warnings.Take(5))
                    {
                        sb.AppendLine($"- {w}");
                    }
                    if (warnings.Count > 5)
                    {
                        sb.AppendLine($"- ... i {warnings.Count - 5} innych.");
                    }
                }

                if (skippedEntities.Count > 0 && skippedEntities.Count <= 5)
                {
                    sb.AppendLine();
                    sb.AppendLine("Pominieto obiekty nie-tekstowe:");
                    foreach (var s in skippedEntities)
                    {
                        sb.AppendLine($"- {s}");
                    }
                }

                if (!string.IsNullOrEmpty(saveAs))
                {
                    AgentMemoryState.Variables[saveAs] = SerializeJson(reportEntries);
                    sb.AppendLine();
                    sb.AppendLine($"ZAPISANO W PAMIECI JAKO @{saveAs} (format JSON, {reportEntries.Count} wpisow).");
                }

                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return $"BLAD KRYTYCZNY ODCZYTU POL: {ex.Message}";
            }
        }

        private static bool LooksLikeContainsField(string contents)
        {
            return !string.IsNullOrEmpty(contents) && contents.Contains("%<");
        }

        private static bool SafeHasFields(Entity ent)
        {
            try
            {
                return ent.HasFields;
            }
            catch
            {
                return false;
            }
        }

        // Skanuje atrybuty BlockReference i raportuje pola znalezione w kazdym
        // atrybucie. Zwraca liczbe pol faktycznie dodanych do reportEntries.
        private static int ScanBlockAttributes(
            BlockReference br,
            string filterCategory,
            bool includeRawCode,
            ref List<Dictionary<string, string>> reportEntries,
            ref HashSet<string> warnings)
        {
            int added = 0;
            try
            {
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    AttributeReference ar = br.Database.TransactionManager.TopTransaction
                        .GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (ar == null || ar.IsErased) continue;

                    string raw = ar.IsMTextAttribute && ar.MTextAttribute != null
                        ? ar.MTextAttribute.Contents
                        : ar.TextString;
                    if (string.IsNullOrEmpty(raw)) continue;

                    bool hasFields = false;
                    try { hasFields = ar.HasFields; } catch { }
                    if (!hasFields && !LooksLikeContainsField(raw)) continue;

                    string resolved = ar.IsMTextAttribute && ar.MTextAttribute != null
                        ? (ar.MTextAttribute.Text ?? ar.MTextAttribute.Contents ?? "")
                        : (ar.TextString ?? "");
                    string status = string.IsNullOrEmpty(resolved) || resolved.Contains("%<")
                        ? "NotYetEvaluated"
                        : "Success";

                    var fields = ExtractFieldsFromText(raw);
                    if (fields.Count == 0 && hasFields)
                    {
                        fields.Add(new FieldInfo
                        {
                            Code = "<nie mozna odczytac kodu>",
                            Category = "Unknown",
                            ResolvedValue = resolved
                        });
                    }
                    else if (fields.Count == 0)
                    {
                        warnings.Add($"[AttributeReference#{attId}/Tag={ar.Tag}] Wykryto %< ale nie rozpoznano kategorii. Tresc: '{Truncate(raw, 120)}'");
                        fields.Add(new FieldInfo
                        {
                            Code = Truncate(raw, 80),
                            Category = "Unknown",
                            ResolvedValue = resolved
                        });
                    }

                    foreach (var fi in fields)
                    {
                        if (!string.IsNullOrEmpty(filterCategory)
                            && !fi.Category.Equals(filterCategory, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var entry = new Dictionary<string, string>
                        {
                            { "EntityType", "AttributeReference" },
                            { "EntityId", attId.ToString() },
                            { "ParentBlock", br.Name ?? "(null)" },
                            { "Tag", ar.Tag ?? "" },
                            { "Category", fi.Category },
                            { "ResolvedValue", fi.ResolvedValue ?? "" },
                            { "ResolvedStatus", status }
                        };
                        if (includeRawCode)
                        {
                            entry["Code"] = fi.Code ?? "";
                        }
                        reportEntries.Add(entry);
                        added++;
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Blad skanowania atrybutow bloku '{br.Name}': {ex.Message}");
            }
            return added;
        }

        private static string ExtractRawContents(Entity ent, HashSet<string> warnings)
        {
            try
            {
                if (ent is MText mt)
                {
                    // Preferuj Contents (zawiera %<\Ac...>) nad getMTextWithFieldCodes
                    // (ktory w V22 moze zwrocic skrocona forme %<\_FldIdx>).
                    return mt.Contents ?? mt.getMTextWithFieldCodes() ?? "";
                }
                if (ent is DBText dt)
                {
                    return dt.TextString ?? dt.getTextWithFieldCodes() ?? "";
                }
                if (ent is AttributeReference ar)
                {
                    if (ar.IsMTextAttribute && ar.MTextAttribute != null)
                        return ar.MTextAttribute.Contents
                            ?? ar.MTextAttribute.getMTextWithFieldCodes()
                            ?? "";
                    return ar.TextString ?? ar.getTextWithFieldCodes() ?? "";
                }
                if (ent is AttributeDefinition ad)
                {
                    if (ad.IsMTextAttributeDefinition && ad.MTextAttributeDefinition != null)
                        return ad.MTextAttributeDefinition.Contents
                            ?? ad.MTextAttributeDefinition.getMTextWithFieldCodes()
                            ?? "";
                    return ad.TextString ?? ad.getTextWithFieldCodes() ?? "";
                }
                if (ent is MLeader ml)
                {
                    if (ml.ContentType == ContentType.MTextContent && ml.MText != null)
                        return ml.MText.Contents
                            ?? ml.MText.getMTextWithFieldCodes()
                            ?? "";
                    return "";
                }
                return "";
            }
            catch (Exception ex)
            {
                warnings.Add($"Nie udalo sie odczytac surowej tresci [{ent.GetType().Name}#{ent.Id}]: {ex.Message}");
                return "";
            }
        }

        private static string ExtractResolvedContents(Entity ent)
        {
            try
            {
                if (ent is MText mt)
                {
                    // .Text zwraca wartosc rozwiazana, ale przed _.REGEN moze byc puste.
                    // W takim przypadku spadaj na Contents (zawiera kody pol, ale nie jest puste).
                    string resolved = mt.Text;
                    if (string.IsNullOrEmpty(resolved)) resolved = mt.Contents ?? "";
                    return resolved ?? "";
                }
                if (ent is DBText dt)
                {
                    // DBText.TextString jest po prostu zawartoscia - przed REGEN moze
                    // zawierac kody pol, ale zawsze niepuste.
                    return dt.TextString ?? "";
                }
                if (ent is AttributeReference ar)
                {
                    if (ar.IsMTextAttribute && ar.MTextAttribute != null)
                    {
                        string resolved = ar.MTextAttribute.Text;
                        if (string.IsNullOrEmpty(resolved))
                            resolved = ar.MTextAttribute.Contents ?? "";
                        return resolved ?? "";
                    }
                    return ar.TextString ?? "";
                }
                if (ent is AttributeDefinition ad)
                {
                    if (ad.IsMTextAttributeDefinition && ad.MTextAttributeDefinition != null)
                    {
                        string resolved = ad.MTextAttributeDefinition.Text;
                        if (string.IsNullOrEmpty(resolved))
                            resolved = ad.MTextAttributeDefinition.Contents ?? "";
                        return resolved ?? "";
                    }
                    return ad.TextString ?? "";
                }
                if (ent is MLeader ml)
                {
                    if (ml.ContentType == ContentType.MTextContent && ml.MText != null)
                    {
                        string resolved = ml.MText.Text;
                        if (string.IsNullOrEmpty(resolved)) resolved = ml.MText.Contents ?? "";
                        return resolved ?? "";
                    }
                    return "";
                }
                return "";
            }
            catch
            {
                return "";
            }
        }

        // Heurystycznie sprawdza, czy pole zostalo przeliczone (ma wartosc rozwiazana).
        // Zwraca "Success" gdy widac konkretna wartosc, "NotYetEvaluated" gdy
        // zawartosc == kod pola lub jest pusta.
        private static string DetectResolvedStatus(Entity ent)
        {
            string resolved = ExtractResolvedContents(ent);
            if (string.IsNullOrEmpty(resolved)) return "NotYetEvaluated";
            // Jesli zawiera marker pola, to na pewno nie przeliczone.
            if (resolved.Contains("%<")) return "NotYetEvaluated";
            return "Success";
        }

        private static List<FieldInfo> ExtractFieldsFromText(string rawContents)
        {
            var result = new List<FieldInfo>();
            if (string.IsNullOrEmpty(rawContents)) return result;

            // Skanuj caly tekst i wyciagaj zarowno forme czytelna %<\Ac...>
            // jak i forme binarna %<\_FldIdx ...>.
            int searchFrom = 0;
            while (searchFrom < rawContents.Length)
            {
                int start = rawContents.IndexOf("%<", searchFrom, StringComparison.Ordinal);
                if (start < 0) break;

                // Znajdz koniec pola - w formie czytelnej i binarnej jest to '>'
                // lub bialy znak / niealfanumeryczny znak (w formie _FldIdx
                // nie zawsze jest zamykajacy '>').
                int end = FindFieldEnd(rawContents, start + 2);

                string marker = rawContents.Substring(start, end - start);
                result.Add(new FieldInfo
                {
                    Code = marker,
                    Category = ClassifyFieldCode(marker),
                    ResolvedValue = null
                });

                searchFrom = end;
            }

            return result;
        }

        // Szuka konca pola zaczynajac od 'pos' (po "%<"). Zwraca indeks
        // pierwszego znaku NIE bedacego czescia pola (zazwyczaj '>' lub
        // bialy znak w formie _FldIdx).
        private static int FindFieldEnd(string s, int pos)
        {
            // Najpierw szukamy '>' - dziala dla %<\Ac...>.
            int gt = s.IndexOf('>', pos);
            if (gt >= 0) return gt + 1;

            // Brak '>' - prawdopodobnie forma _FldIdx.
            // Koniec pola to bialy znak, klamra, przecinek lub koniec tekstu.
            int i = pos;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n'
                    || c == '|' || c == ',' || c == ';' || c == '}')
                    return i;
                i++;
            }
            return i;
        }

        private static string ClassifyFieldCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return "Unknown";

            // Najpierw szukamy formy czytelnej %<\Ac...>.
            // Regex przechwytuje tylko sufiks po "Ac" - np. "Var", "Date", "Expr".
            var m = FieldReadableRegex.Match(code);
            if (m.Success)
            {
                string kw = m.Groups[1].Value.ToLowerInvariant();
                switch (kw)
                {
                    case "var": return "SystemVariable";
                    case "expr": return "Expression";
                    case "dim": return "DimensionProperty";
                    case "objprop": return "ObjectProperty";
                    case "prop": return "BlockAttributeProperty";
                    case "date": return "DateTime";
                    case "fido": return "SheetSet";
                    case "field": return "NestedField";
                    default: return "Unknown";
                }
            }

            // Forma binarna - brak kategorii bez dostepu do tablicy FieldReference.
            if (FieldBinaryRegex.IsMatch(code))
            {
                return "BinaryField";
            }

            return "Unknown";
        }

        private static string SerializeJson(List<Dictionary<string, string>> entries)
        {
            var arr = new JArray();
            foreach (var e in entries)
            {
                var jo = new JObject();
                foreach (var kv in e)
                {
                    jo[kv.Key] = kv.Value;
                }
                arr.Add(jo);
            }
            return arr.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "...";
        }

        private sealed class FieldInfo
        {
            public string Code { get; set; }
            public string Category { get; set; }
            public string ResolvedValue { get; set; }
        }

        public List<string> Examples => new List<string>
        {
            "{\"SaveAs\":\"FieldList\",\"IncludeRawCode\":true}",
            "{\"FilterByCategory\":\"DateTime\"}",
            "{\"SaveAs\":\"FieldsSummary\",\"IncludeRawCode\":false,\"FilterByCategory\":\"SystemVariable\"}"
        };
    }
}