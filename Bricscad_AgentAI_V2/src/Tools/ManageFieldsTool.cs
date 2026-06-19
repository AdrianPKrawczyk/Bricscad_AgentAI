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
    /// <summary>
    /// Narzedzie do zarzadzania polami CAD (Field) w zaznaczonych obiektach tekstowych.
    /// Akcje: InsertField, RemoveField, ConvertToText, ReplaceFieldCode, EvaluateAll.
    /// Po kazdej mutacji automatycznie wysyla komende _.REGEN do BricsCAD,
    /// aby wymusic przeliczenie wartosci pol (w BricsCAD V22 nie ma
    /// publicznego EvaluateFields() na Database).
    /// </summary>
    public class ManageFieldsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageFields",
                    Description = "Zarzadzanie polami CAD w obiektach tekstowych. Akcje: InsertField (wstawia kod %<\\Ac...> do tekstu), " +
                                  "RemoveField (usuwa wskazane pole), ReplaceFieldCode (zamienia kod pola), " +
                                  "ConvertToText (zamienia wszystkie pola na zwykly tekst z zachowaniem rozwiazanej wartosci), " +
                                  "EvaluateAll (wymusza przeliczenie wszystkich pol w rysunku). " +
                                  "Skladnia kodu DWG: %<\\AcVar \"NAZWA_ZMIENNEJ\">, %<\\AcExpr wyrazenie>, %<\\AcDim \\Wlasciwosc>, " +
                                  "%<\\AcObjProp Object.\\Warstwa>, %<\\AcProp BlockRef.\\Atrybut>. " +
                                  "Auto-REGEN po kazdej mutacji.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Enum = new List<string>
                                    {
                                        "InsertField", "RemoveField", "ReplaceFieldCode",
                                        "ConvertToText", "EvaluateAll"
                                    },
                                    Description = "Akcja do wykonania. Wymagana."
                                }
                            },
                            {
                                "FieldCode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Kod DWG pola do wstawienia lub zamiany, np. \"%<\\AcVar \\\"DWGNAME\\\">\". " +
                                                  "Wymagany dla InsertField i ReplaceFieldCode."
                                }
                            },
                            {
                                "FieldName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa pola do usuniecia/zamiany (np. \"FIELD_0\"). " +
                                                  "Opcjonalny dla RemoveField/ReplaceFieldCode - jesli pominiety, operuje na wszystkich polach obiektu."
                                }
                            },
                            {
                                "Position", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Pozycja wstawienia pola. Wzory: \"Start\", \"End\", \"Replace\" (zastap cala tresc), " +
                                                  "\"Before:<szukanyTekst>\" (wstaw przed pierwszym wystapieniem), " +
                                                  "\"After:<szukanyTekst>\" (wstaw za pierwszym wystapieniem). Domyslnie \"End\".",
                                    Enum = new List<string> { "Start", "End", "Replace", "Before:<...>", "After:<...>" }
                                }
                            },
                            {
                                "Targets", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Zakres encji do modyfikacji. Domyslnie \"Selection\" (ActiveSelection). " +
                                                  "Inne opcje: \"AllMTextOnLayout\", \"AllDBTextOnLayout\", \"AllTextOnLayout\".",
                                    Enum = new List<string>
                                    {
                                        "Selection", "AllMTextOnLayout",
                                        "AllDBTextOnLayout", "AllTextOnLayout"
                                    }
                                }
                            },
                            {
                                "IncludeConstantAttributes", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy modyfikowac rowniez atrybuty stale (AttributeReference.Constant / AttributeDefinition.Constant). Domyslnie false."
                                }
                            },
                            {
                                "MatchText", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny filtr - obiekt (lub atrybut bloku) musi zawierac ten substring w tresci, " +
                                                  "aby zostal zmodyfikowany. Przydatne przy selekcji wielu obiektow, gdy zmiana " +
                                                  "ma dotyczyc tylko konkretnego tekstu (np. \"Wynik:\" albo \"Projekt:\"). " +
                                                  "Domyslnie brak filtra - wszystkie obiekty tekstowe w Targets sa modyfikowane."
                                }
                            },
                            {
                                "BlockNameFilter", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny filtr dla atrybutow blokow - modyfikuje tylko atrybuty w instancjach " +
                                                  "blokow o podanej nazwie (np. \"Bielik_Pole_Tytul\"). Bez podania - wszystkie " +
                                                  "zaznaczone BlockReference. Przydatne przy selekcji mieszanej."
                                }
                            },
                            {
                                "AttributeTagFilter", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny filtr dla atrybutow blokow - modyfikuje tylko atrybuty o podanym " +
                                                  "Tag (np. \"NUMER\", \"TYTUL\"). Bez podania - wszystkie atrybuty."
                                }
                            }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string action = args["Action"]?.ToString();
            if (string.IsNullOrWhiteSpace(action))
                return "BLAD: Brak wymaganego parametru Action.";

            switch (action.ToLowerInvariant())
            {
                case "insertfield": return ExecuteInsertField(doc, args);
                case "removefield": return ExecuteRemoveField(doc, args);
                case "replacefieldcode": return ExecuteReplaceFieldCode(doc, args);
                case "converttotext": return ExecuteConvertToText(doc, args);
                case "evaluateall": return ExecuteEvaluateAll(doc);
                default:
                    return $"BLAD: Nieznana akcja '{action}'. Dozwolone: InsertField, RemoveField, ReplaceFieldCode, ConvertToText, EvaluateAll.";
            }
        }

        // ---------- InsertField ----------
        private string ExecuteInsertField(Document doc, JObject args)
        {
            string fieldCode = args["FieldCode"]?.ToString();
            if (string.IsNullOrEmpty(fieldCode))
                return "BLAD: Dla Action='InsertField' wymagany jest parametr FieldCode z kodem DWG (np. \"%<\\AcVar \\\"DWGNAME\\\">\").";

            if (!LooksLikeFieldCode(fieldCode))
                return $"BLAD: Podany FieldCode '{fieldCode}' nie wyglada jak kod pola DWG (powinien zaczynac sie od '%<' i konczyc na '>').";

            string position = args["Position"]?.ToString() ?? "End";
            string targets = args["Targets"]?.ToString() ?? "Selection";

            return MutateEntities(doc, targets, args, (ent, warnings) =>
            {
                InsertFieldIntoEntity(ent, fieldCode, position, warnings);
            }, "Wstawiono pole");
        }

        // ---------- RemoveField ----------
        private string ExecuteRemoveField(Document doc, JObject args)
        {
            string fieldName = args["FieldName"]?.ToString();
            string targets = args["Targets"]?.ToString() ?? "Selection";

            return MutateEntities(doc, targets, args, (ent, warnings) =>
            {
                RemoveFieldsFromEntity(ent, fieldName, warnings);
            }, "Usunieto pole/pola");
        }

        // ---------- ReplaceFieldCode ----------
        private string ExecuteReplaceFieldCode(Document doc, JObject args)
        {
            string fieldCode = args["FieldCode"]?.ToString();
            string fieldName = args["FieldName"]?.ToString();
            if (string.IsNullOrEmpty(fieldCode))
                return "BLAD: Dla Action='ReplaceFieldCode' wymagany jest FieldCode z nowym kodem DWG.";

            if (!LooksLikeFieldCode(fieldCode))
                return $"BLAD: Podany FieldCode '{fieldCode}' nie wyglada jak kod pola DWG.";

            string targets = args["Targets"]?.ToString() ?? "Selection";

            // Strategia awaryjna: usun stare pole, wstaw nowe na koniec.
            return MutateEntities(doc, targets, args, (ent, warnings) =>
            {
                RemoveFieldsFromEntity(ent, fieldName, warnings);
                InsertFieldIntoEntity(ent, fieldCode, "End", warnings);
            }, "Zamieniono kod pola");
        }

        // ---------- ConvertToText ----------
        private string ExecuteConvertToText(Document doc, JObject args)
        {
            string targets = args["Targets"]?.ToString() ?? "Selection";

            return MutateEntities(doc, targets, args, (ent, warnings) =>
            {
                ConvertEntityFieldsToText(ent, warnings);
            }, "Zamieniono pola na tekst", forceRegen: false);
        }

        // ---------- EvaluateAll ----------
        private string ExecuteEvaluateAll(Document doc)
        {
            try
            {
                // W BricsCAD V22 nie ma publicznego API Database.EvaluateFields().
                // Najbardziej niezawodne: wyslac komende _.REGEN, ktora wymusza
                // przeliczenie wymiarow i pol.
                doc.SendStringToExecute("_.REGEN \n", true, false, false);
                return "WYNIK: Wyslano komende _.REGEN do wymuszenia przeliczenia pol. " +
                       "Sprawdz rysunek - wartosci pol powinny byc aktualne.";
            }
            catch (Exception ex)
            {
                return $"BLAD EVALUATEALL: {ex.Message}";
            }
        }

        // ---------- Helpers ----------

        private delegate void EntityMutator(Entity ent, HashSet<string> warnings);

        // Kontekst mutacji - przekazywany do mutatora i uzywany przez filtry.
        private sealed class MutateContext
        {
            public bool IncludeConstantAttributes;
            public string MatchText;
            public string BlockNameFilter;
            public string AttributeTagFilter;
            public EntityMutator Mutator;
            public HashSet<string> Warnings;
        }

        private string MutateEntities(
            Document doc,
            string targets,
            JObject args,
            EntityMutator mutator,
            string successLabel,
            bool forceRegen = true)
        {
            var objectIds = ResolveTargets(doc, targets);
            if (objectIds == null || objectIds.Count == 0)
                return $"BLAD: Targets='{targets}' nie zwrocil zadnych obiektow. " +
                       $"Dla 'Selection' uzyj najpierw SelectEntities. Dla 'AllTextOnLayout' sprawdz czy rysunek ma obiekty tekstowe.";

            var ctx = new MutateContext
            {
                IncludeConstantAttributes = args["IncludeConstantAttributes"]?.Value<bool>() ?? false,
                MatchText = args["MatchText"]?.ToString(),
                BlockNameFilter = args["BlockNameFilter"]?.ToString(),
                AttributeTagFilter = args["AttributeTagFilter"]?.ToString(),
                Mutator = mutator,
                Warnings = new HashSet<string>()
            };

            int modifiedCount = 0;
            int skippedCount = 0;
            int blockAttrsModified = 0;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in objectIds)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) { skippedCount++; continue; }

                        // Specjalna sciezka: BlockReference - skanuj jego atrybuty.
                        if (ent is BlockReference br)
                        {
                            int n = MutateBlockAttributes(br, ctx, tr);
                            blockAttrsModified += n;
                            continue;
                        }

                        if (!IsTextEntity(ent)) { skippedCount++; continue; }
                        if (!IsAttributeEligible(ent, ctx.IncludeConstantAttributes))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Filtr MatchText - tresc musi zawierac substring.
                        if (!string.IsNullOrEmpty(ctx.MatchText))
                        {
                            string content = ExtractEntityRawContent(ent);
                            if (string.IsNullOrEmpty(content) || !content.Contains(ctx.MatchText))
                            {
                                skippedCount++;
                                continue;
                            }
                        }

                        try
                        {
                            mutator(ent, ctx.Warnings);
                            modifiedCount++;
                        }
                        catch (Exception ex)
                        {
                            ctx.Warnings.Add($"Nie udalo sie przetworzyc [{ent.GetType().Name}#{id}]: {ex.Message}");
                            skippedCount++;
                        }
                    }
                    tr.Commit();
                }

                int totalModified = modifiedCount + blockAttrsModified;

                if (forceRegen && totalModified > 0)
                {
                    try
                    {
                        doc.SendStringToExecute("_.REGEN \n", true, false, false);
                    }
                    catch (Exception ex)
                    {
                        ctx.Warnings.Add($"Wyslanie _.REGEN nie powiodlo sie: {ex.Message}");
                    }
                }

                var sb = new StringBuilder();
                if (blockAttrsModified > 0 && modifiedCount > 0)
                {
                    sb.AppendLine($"WYNIK: {successLabel} w {modifiedCount} obiektach tekstowych " +
                                  $"i {blockAttrsModified} atrybutach blokow (pominieto {skippedCount}).");
                }
                else if (blockAttrsModified > 0)
                {
                    sb.AppendLine($"WYNIK: {successLabel} w {blockAttrsModified} atrybutach blokow (pominieto {skippedCount}).");
                }
                else
                {
                    sb.AppendLine($"WYNIK: {successLabel} w {modifiedCount} obiektach (pominieto {skippedCount}).");
                }

                if (!string.IsNullOrEmpty(ctx.MatchText))
                {
                    sb.AppendLine($"Filtrowano po MatchText='{ctx.MatchText}'.");
                }

                if (ctx.Warnings.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("UWAGI:");
                    foreach (var w in ctx.Warnings.Take(10))
                    {
                        sb.AppendLine($"- {w}");
                    }
                    if (ctx.Warnings.Count > 10)
                    {
                        sb.AppendLine($"- ... i {ctx.Warnings.Count - 10} innych.");
                    }
                }
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return $"BLAD KRYTYCZNY MANAGEFIELDS: {ex.Message}";
            }
        }

        // Mutuje atrybuty danego BlockReference z uwzglednieniem filtrow.
        // Zwraca liczbe zmodyfikowanych atrybutow.
        private int MutateBlockAttributes(BlockReference br, MutateContext ctx, Transaction tr)
        {
            int modified = 0;

            // Filtr nazwy bloku.
            if (!string.IsNullOrEmpty(ctx.BlockNameFilter))
            {
                if (!string.Equals(br.Name, ctx.BlockNameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }
            }

            foreach (ObjectId attId in br.AttributeCollection)
            {
                AttributeReference ar = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (ar == null || ar.IsErased) continue;

                // Filtr tagu atrybutu.
                if (!string.IsNullOrEmpty(ctx.AttributeTagFilter))
                {
                    if (!string.Equals(ar.Tag, ctx.AttributeTagFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                // Filtr stale atrybutu.
                if (!ctx.IncludeConstantAttributes && ar.IsConstant)
                {
                    continue;
                }

                // Filtr MatchText.
                if (!string.IsNullOrEmpty(ctx.MatchText))
                {
                    string content = ar.IsMTextAttribute && ar.MTextAttribute != null
                        ? ar.MTextAttribute.Contents
                        : ar.TextString;
                    if (string.IsNullOrEmpty(content) || !content.Contains(ctx.MatchText))
                    {
                        continue;
                    }
                }

                try
                {
                    ctx.Mutator(ar, ctx.Warnings);
                    modified++;
                }
                catch (Exception ex)
                {
                    ctx.Warnings.Add($"Nie udalo sie przetworzyc [AttributeReference {ar.Tag} w '{br.Name}']: {ex.Message}");
                }
            }

            return modified;
        }

        // Pomocnik: wyciaga surowa tresc encji tekstowej (Contents/TextString) - bez pol.
        // Uzywany przez filtr MatchText.
        private static string ExtractEntityRawContent(Entity ent)
        {
            try
            {
                if (ent is MText mt) return mt.Contents ?? "";
                if (ent is DBText dt) return dt.TextString ?? "";
                if (ent is AttributeReference ar)
                {
                    if (ar.IsMTextAttribute && ar.MTextAttribute != null)
                        return ar.MTextAttribute.Contents ?? "";
                    return ar.TextString ?? "";
                }
                if (ent is AttributeDefinition ad)
                {
                    if (ad.IsMTextAttributeDefinition && ad.MTextAttributeDefinition != null)
                        return ad.MTextAttributeDefinition.Contents ?? "";
                    return ad.TextString ?? "";
                }
            }
            catch { }
            return "";
        }

        private static List<ObjectId> ResolveTargets(Document doc, string targets)
        {
            var result = new List<ObjectId>();

            if (string.IsNullOrEmpty(targets) || targets.Equals("Selection", StringComparison.OrdinalIgnoreCase))
            {
                var sel = AgentMemoryState.ActiveSelection;
                if (sel == null) return result;
                return sel.ToList();
            }

            try
            {
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord modelSpace =
                        tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
                    if (modelSpace == null) { tr.Commit(); return result; }

                    foreach (ObjectId id in modelSpace)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        bool take = false;
                        switch (targets.ToLowerInvariant())
                        {
                            case "allmtextonlayout":
                                take = ent is MText;
                                break;
                            case "alldbtextonlayout":
                                take = ent is DBText;
                                break;
                            case "alltextonlayout":
                                take = ent is MText || ent is DBText;
                                break;
                        }
                        if (take) result.Add(id);
                    }
                    tr.Commit();
                }
            }
            catch
            {
                // Celowo polykamy - bledny targets traktujemy jako pusta selekcja.
            }
            return result;
        }

        private static bool IsTextEntity(Entity ent)
        {
            return ent is MText || ent is DBText
                || ent is AttributeReference || ent is AttributeDefinition;
        }

        private static bool IsAttributeEligible(Entity ent, bool includeConstant)
        {
            if (ent is AttributeReference ar)
            {
                if (!includeConstant && ar.IsConstant) return false;
                return true;
            }
            if (ent is AttributeDefinition ad)
            {
                if (!includeConstant && ad.Constant) return false;
                return true;
            }
            return true;
        }

        private static bool LooksLikeFieldCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            if (!code.StartsWith("%<") || !code.EndsWith(">")) return false;
            if (code.Length < 5) return false;
            return true;
        }

        private static void InsertFieldIntoEntity(Entity ent, string fieldCode, string position, HashSet<string> warnings)
        {
            // DBText jest jednoliniowy - nie obsluguje dobrze pol inline.
            // I tak wstawiamy kod, ale ostrzegamy agenta.
            bool dbTextWarning = false;

            if (ent is MText mt)
            {
                if (ent.IsReadEnabled)
                {
                    ent.UpgradeOpen();
                }
                mt.Contents = ApplyPosition(mt.Contents ?? "", fieldCode, position);
            }
            else if (ent is DBText dt)
            {
                if (ent.IsReadEnabled)
                {
                    ent.UpgradeOpen();
                }
                dt.TextString = ApplyPosition(dt.TextString ?? "", fieldCode, position);
                dbTextWarning = true;
            }
            else if (ent is AttributeReference ar)
            {
                if (ar.IsConstant)
                {
                    warnings.Add($"Atrybut stale (Constant=true) w [{ar.Tag}]: pominieto wstawienie pola.");
                    return;
                }
                if (ent.IsReadEnabled) ent.UpgradeOpen();
                if (ar.IsMTextAttribute && ar.MTextAttribute != null)
                {
                    MText sub = ar.MTextAttribute;
                    sub.Contents = ApplyPosition(sub.Contents ?? "", fieldCode, position);
                    ar.MTextAttribute = sub;
                }
                else
                {
                    ar.TextString = ApplyPosition(ar.TextString ?? "", fieldCode, position);
                    dbTextWarning = true;
                }
            }
            else if (ent is AttributeDefinition ad)
            {
                if (ad.Constant)
                {
                    warnings.Add($"Definicja atrybutu stalego (Constant=true) w [{ad.Tag}]: pominieto wstawienie pola.");
                    return;
                }
                if (ent.IsReadEnabled) ent.UpgradeOpen();
                if (ad.IsMTextAttributeDefinition && ad.MTextAttributeDefinition != null)
                {
                    MText sub = ad.MTextAttributeDefinition;
                    sub.Contents = ApplyPosition(sub.Contents ?? "", fieldCode, position);
                    ad.UpdateMTextAttributeDefinition();
                }
                else
                {
                    ad.TextString = ApplyPosition(ad.TextString ?? "", fieldCode, position);
                    dbTextWarning = true;
                }
            }
            else
            {
                warnings.Add($"[{ent.GetType().Name}#{ent.Id}] Nie obslugiwany typ encji - pominieto.");
                return;
            }

            if (dbTextWarning)
            {
                warnings.Add($"[{ent.GetType().Name}#{ent.Id}] Pole wstawione do jednoliniowego tekstu (DBText/atrybut DBText). " +
                             $"Pole bedzie obliczane, ale formatowanie RTF nie jest dostepne - rozważ migracje do MText.");
            }
        }

        private static void RemoveFieldsFromEntity(Entity ent, string fieldName, HashSet<string> warnings)
        {
            bool hasFields = false;
            try { hasFields = ent.HasFields; }
            catch { hasFields = false; }

            // Wyciagnij wszystkie kody pol z surowej tresci i usun je z tekstu.
            string raw = "";
            if (ent is MText mt) raw = SafeGetMTextCodes(mt);
            else if (ent is DBText dt) raw = SafeGetDBTextCodes(dt);
            else if (ent is AttributeReference ar)
            {
                raw = ar.IsMTextAttribute && ar.MTextAttribute != null
                    ? SafeGetMTextCodes(ar.MTextAttribute)
                    : SafeGetDBTextCodes(ar);
            }
            else if (ent is AttributeDefinition ad)
            {
                raw = ad.IsMTextAttributeDefinition && ad.MTextAttributeDefinition != null
                    ? SafeGetMTextCodes(ad.MTextAttributeDefinition)
                    : SafeGetDBTextCodes(ad);
            }

            if (string.IsNullOrEmpty(raw) || !raw.Contains("%<"))
            {
                if (hasFields)
                {
                    warnings.Add($"[{ent.GetType().Name}#{ent.Id}] Flaga HasFields=true, ale nie wykryto kodu %< w tresci - " +
                                 $"prawdopodobnie pole w formacie binarnym. Probuje wywolac ConvertToText jako fallback.");
                    ConvertEntityFieldsToText(ent, warnings);
                }
                return;
            }

            string cleaned = StripAllFieldMarkers(raw);

            if (ent.IsReadEnabled) ent.UpgradeOpen();

            if (ent is MText mtz) mtz.Contents = cleaned;
            else if (ent is DBText dtz) dtz.TextString = cleaned;
            else if (ent is AttributeReference arz)
            {
                if (arz.IsMTextAttribute && arz.MTextAttribute != null)
                {
                    MText sub = arz.MTextAttribute;
                    sub.Contents = cleaned;
                    arz.MTextAttribute = sub;
                }
                else
                {
                    arz.TextString = cleaned;
                }
            }
            else if (ent is AttributeDefinition adz)
            {
                if (adz.IsMTextAttributeDefinition && adz.MTextAttributeDefinition != null)
                {
                    MText sub = adz.MTextAttributeDefinition;
                    sub.Contents = cleaned;
                    adz.UpdateMTextAttributeDefinition();
                }
                else
                {
                    adz.TextString = cleaned;
                }
            }

            // Próbujemy rowniez wywolac RemoveField, zeby BricsCAD wyczyscil
            // wewnetrzne struktury pol, jesli jakies zostaly.
            TryRemoveAllNamedFields(ent, warnings);
        }

        private static void ConvertEntityFieldsToText(Entity ent, HashSet<string> warnings)
        {
            // Konwersja zamienia wszystkie pola na tekst z zachowaniem
            // rozwiązanej wartosci. Wywolujemy tylko jesli wykryto flage HasFields.
            bool hasFields = false;
            try { hasFields = ent.HasFields; }
            catch { hasFields = false; }

            if (!hasFields)
            {
                warnings.Add($"[{ent.GetType().Name}#{ent.Id}] Brak pol (HasFields=false) - konwersja pominieta.");
                return;
            }

            if (ent.IsReadEnabled) ent.UpgradeOpen();

            try
            {
                // ConvertFieldToText to metoda specyficzna dla MText/DBText/AttributeReference/AttributeDefinition.
                // W V22 nie ma jej na bazowym Entity - uzywamy refleksji, zeby nie wiazac sie z konkretnym overloadem.
                var method = ent.GetType().GetMethod("ConvertFieldToText",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(ent, null);
                }
                else
                {
                    warnings.Add($"[{ent.GetType().Name}#{ent.Id}] Brak publicznej metody ConvertFieldToText w {ent.GetType().Name}. " +
                                 $"Konwersja nie jest obslugiwana dla tego typu encji.");
                }
            }
            catch (System.Reflection.TargetInvocationException tex)
            {
                string inner = tex.InnerException != null ? tex.InnerException.Message : tex.Message;
                warnings.Add($"[{ent.GetType().Name}#{ent.Id}] ConvertFieldToText nie powiodlo sie: {inner}");
            }
            catch (Exception ex)
            {
                warnings.Add($"[{ent.GetType().Name}#{ent.Id}] ConvertFieldToText nie powiodlo sie: {ex.Message}");
            }
        }

        private static string SafeGetMTextCodes(MText mt)
        {
            try { return mt.getMTextWithFieldCodes() ?? mt.Contents ?? ""; }
            catch { return mt.Contents ?? ""; }
        }

        private static string SafeGetDBTextCodes(DBText dt)
        {
            try { return dt.getTextWithFieldCodes() ?? dt.TextString ?? ""; }
            catch { return dt.TextString ?? ""; }
        }

        private static string StripAllFieldMarkers(string raw)
        {
            // Usuwamy wszystkie wystapienia %<...>. Prosta maszyna stanow -
            // liczymy zagniezdzenia ">" w obrebie "%<...>".
            var sb = new StringBuilder();
            int i = 0;
            while (i < raw.Length)
            {
                if (i + 1 < raw.Length && raw[i] == '%' && raw[i + 1] == '<')
                {
                    int j = i + 2;
                    while (j < raw.Length && raw[j] != '>') j++;
                    if (j < raw.Length) j++; // przeskocz '>'
                    i = j;
                }
                else
                {
                    sb.Append(raw[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static void TryRemoveAllNamedFields(Entity ent, HashSet<string> warnings)
        {
            // Próba wywolania ent.RemoveField("FIELD_n") dla n=0..31.
            // W V22 sygnatura RemoveField(string) - probujemy wzorcowe nazwy.
            for (int i = 0; i < 32; i++)
            {
                try
                {
                    ObjectId fid = ent.GetField($"FIELD_{i}");
                    if (fid.IsNull) continue;
                    try
                    {
                        ent.RemoveField($"FIELD_{i}");
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"RemoveField(\"FIELD_{i}\") zwrocilo wyjatek: {ex.Message}");
                    }
                }
                catch
                {
                    break; // po pierwszym niepowodzeniu dalej juz nie ma pol
                }
            }
        }

        private static string ApplyPosition(string current, string fieldCode, string position)
        {
            if (string.IsNullOrEmpty(position)) position = "End";

            if (position.Equals("Start", StringComparison.OrdinalIgnoreCase))
            {
                return fieldCode + current;
            }
            if (position.Equals("End", StringComparison.OrdinalIgnoreCase))
            {
                return current + fieldCode;
            }
            if (position.Equals("Replace", StringComparison.OrdinalIgnoreCase))
            {
                return fieldCode;
            }
            if (position.StartsWith("Before:", StringComparison.OrdinalIgnoreCase))
            {
                string needle = position.Substring("Before:".Length);
                int idx = current.IndexOf(needle, StringComparison.Ordinal);
                if (idx < 0) return current + fieldCode;
                return current.Substring(0, idx) + fieldCode + current.Substring(idx);
            }
            if (position.StartsWith("After:", StringComparison.OrdinalIgnoreCase))
            {
                string needle = position.Substring("After:".Length);
                int idx = current.IndexOf(needle, StringComparison.Ordinal);
                if (idx < 0) return current + fieldCode;
                int insertAt = idx + needle.Length;
                return current.Substring(0, insertAt) + fieldCode + current.Substring(insertAt);
            }
            // Nieznany wzorzec - bezpieczny fallback na End.
            return current + fieldCode;
        }

        public List<string> Examples => new List<string>
        {
            "{\"Action\":\"InsertField\",\"FieldCode\":\"%<\\\\AcVar \\\"DWGNAME\\\">\",\"Position\":\"End\"}",
            "{\"Action\":\"InsertField\",\"FieldCode\":\"%<\\\\AcVar \\\"DWGNAME\\\">\",\"Position\":\"Start\",\"Targets\":\"Selection\"}",
            "{\"Action\":\"InsertField\",\"FieldCode\":\"%<\\\\AcFido \\\"SHEET\\\">\",\"Targets\":\"Selection\",\"AttributeTagFilter\":\"NUMER\",\"BlockNameFilter\":\"Bielik_Pole_Etykieta\"}",
            "{\"Action\":\"InsertField\",\"FieldCode\":\"%<\\\\AcDate \\\\yyyy-MM-dd>\",\"Targets\":\"Selection\",\"MatchText\":\"Wynik:\"}",
            "{\"Action\":\"ConvertToText\",\"Targets\":\"Selection\"}",
            "{\"Action\":\"RemoveField\",\"Targets\":\"AllTextOnLayout\"}",
            "{\"Action\":\"ReplaceFieldCode\",\"FieldName\":\"FIELD_0\",\"FieldCode\":\"%<\\\\AcVar \\\"SAVENAME\\\">\"}",
            "{\"Action\":\"EvaluateAll\"}"
        };

        // Lokalny using zeby nie smiecic góry pliku.
        private sealed class StringBuilder
        {
            private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder();
            public void Append(string s) => _sb.Append(s);
            public void Append(char c) => _sb.Append(c);
            public void AppendLine(string s) => _sb.AppendLine(s);
            public void AppendLine() => _sb.AppendLine();
            public override string ToString() => _sb.ToString();
        }
    }
}