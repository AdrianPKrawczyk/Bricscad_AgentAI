using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do zarządzania atrybutami (dynamicznymi tekstami) w konkretnych wystąpieniach bloków.
    /// Obsługuje odczyt (Read) oraz aktualizację (Update) wartości na podstawie tagów.
    /// </summary>
    public class EditAttributesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "EditAttributes",
                    Description = "Narzedzie do odczytu i edycji atrybutow w blokach CAD. ZASADA KRYTYCZNA: Jesli musisz zmienic wartosc atrybutu X tylko dla blokow, w ktorych atrybut Y ma konkretna wartosc, nie uzywaj IFTE w RPN. Zamiast tego wykonuj osobne wywolania Action: Update z parametrami FilterTag i FilterValue. Zmienna $OLD_VALUE w RPN zwraca zawsze wartosc edytowanego atrybutu, a nie atrybutu z filtra. Do wskazywania konkretnego elementu uzywaj stabilnego atrybutu identyfikujacego, a nie wartosci, ktora sama bedzie aktualizowana. Nie uzywaj atrybutu docelowego jako identyfikatora lancuchowych aktualizacji, jesli jego wartosc zmienia sie w trakcie sekwencji.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Enum = new List<string> { "Read", "Update" },
                                    Description = "Akcja do wykonania: 'Read' (pobranie wartości) lub 'Update' (zmiana wartości)."
                                }
                            },
                            {
                                "Attributes", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Lista wpisow atrybutow. Kazdy element tablicy musi byc plaskim obiektem w postaci {\"Tag\":\"...\",\"Value\":\"...\"}. Nie wolno tworzyc zagniezdzonego pola Attributes ani osobnego pola RPN. Jesli chcesz uzyc RPN, umiesc caly zapis w polu Value, np. {\"Tag\":\"DN_VAL\",\"Value\":\"RPN: $OLD_VALUE \\\"'\\\" \\\"\\\" REPLACE\"}. Dla odczytu mozna podac samo {\"Tag\":\"DN_VAL\"}. Jako Tag podawaj zawsze czysta nazwe etykiety, bez metadanych typu [Ukryty] lub [Wieloliniowy].",
                                    Items = JObject.Parse("{\"type\":\"object\",\"properties\":{\"Tag\":{\"type\":\"string\",\"description\":\"Czysta nazwa tagu atrybutu, np. DN_VAL.\"},\"Value\":{\"type\":\"string\",\"description\":\"Nowa wartosc albo transformacja RPN zapisana w calosci jako string, np. RPN: $OLD_VALUE \\\"'\\\" \\\"\\\" REPLACE.\"}},\"required\":[\"Tag\"]}")
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tylko dla Read: Nazwa zmiennej do zapisu zagregowanych wartości (bez @)."
                                }
                            },
                            {
                                "FilterTag", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalnie: Tag atrybutu sluzacy do filtrowania (np. ID, TYPE, NAME, VAL lub inny stabilny identyfikator wystepujacy w danym typie bloku). Nie uzywaj jako filtra atrybutu, ktory sam jest wlasnie zmieniany w sekwencji aktualizacji."
                                }
                            },
                            {
                                "FilterValue", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalnie: Wartosc atrybutu filtrujacego (np. konkretne oznaczenie, typ albo nazwa). Jesli aktualizujesz wiele roznych blokow, wykonuj osobne wywolania dla kazdego celu po stabilnym identyfikatorze."
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
            string saveAs = args["SaveAs"]?.ToString();
            string filterTag = args["FilterTag"]?.ToString();
            string filterValue = args["FilterValue"]?.ToString();
            JArray attrList = args["Attributes"] as JArray;

            if (attrList != null)
            {
                foreach (var token in attrList)
                {
                    JObject attrObj = token as JObject;
                    if (attrObj == null)
                    {
                        return "BLAD KRYTYCZNY EDYCJI ATRYBUTOW: Parametr Attributes musi byc tablica plaskich obiektow.";
                    }

                    if (attrObj["Attributes"] != null || attrObj["RPN"] != null)
                    {
                        return "BLAD KRYTYCZNY EDYCJI ATRYBUTOW: Niepoprawny ksztalt JSON. Kazdy element Attributes musi miec pola Tag oraz opcjonalnie Value. Nie uzywaj zagniezdzonego pola Attributes ani osobnego pola RPN.";
                    }

                    if (string.IsNullOrWhiteSpace(attrObj["Tag"]?.ToString()))
                    {
                        return "BLAD KRYTYCZNY EDYCJI ATRYBUTOW: Kazdy element Attributes musi zawierac pole Tag.";
                    }

                    if (string.Equals(action, "Update", StringComparison.OrdinalIgnoreCase) &&
                        attrObj["Value"] == null)
                    {
                        return "BLAD KRYTYCZNY EDYCJI ATRYBUTOW: Dla Action='Update' kazdy element Attributes musi zawierac pole Value.";
                    }
                }
            }

            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
                return "BŁĄD: Pamięć Agenta jest pusta. Zaznacz najpierw bloki (SelectEntities).";

            int updatedCount = 0;
            int blocksProcessed = 0;
            var readValues = new List<string>();
            var warnings = new HashSet<string>();

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        BlockReference br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        // Filtrowanie po atrybucie
                        if (!string.IsNullOrEmpty(filterTag))
                        {
                            bool isMatch = false;
                            foreach (ObjectId attId in br.AttributeCollection)
                            {
                                AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                                if (attRef != null && !attRef.IsErased && attRef.Tag.Equals(filterTag, StringComparison.OrdinalIgnoreCase))
                                {
                                    string val = attRef.IsMTextAttribute ? attRef.MTextAttribute.Text : attRef.TextString;
                                    if (val != null && val.Trim().Equals(filterValue?.Trim(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        isMatch = true;
                                        break;
                                    }
                                }
                            }
                            if (!isMatch) continue;
                        }

                        blocksProcessed++;
                        var processedTagsInThisBlock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            AttributeReference attRef = tr.GetObject(attId, action == "Update" ? OpenMode.ForWrite : OpenMode.ForRead) as AttributeReference;
                            if (attRef == null || attRef.IsErased) continue;

                            string currentTag = attRef.Tag;
                            
                            if (action == "Read")
                            {
                                bool shouldRead = true;
                                if (attrList != null && attrList.Count > 0)
                                {
                                    shouldRead = attrList.Any(a => a["Tag"]?.ToString().Equals(currentTag, StringComparison.OrdinalIgnoreCase) == true);
                                }

                                if (shouldRead)
                                {
                                    string val = attRef.IsMTextAttribute ? attRef.MTextAttribute.Text : attRef.TextString;

                                    // Budowanie pigułki informacyjnej dla LLM
                                    string meta = "";
                                    if (attRef.Invisible) meta += " [Ukryty]";
                                    if (attRef.IsMTextAttribute) meta += " [Wieloliniowy]";
                                    // Jeśli wyciągniesz Prompt z definicji bloku, można go tu dodać: meta += $" [Opis: {prompt}]"

                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        readValues.Add($"{currentTag}{meta}: {val}");
                                    }
                                }
                            }
                            else if (action == "Update" && attrList != null)
                            {
                                // Szukamy dopasowania, ale najpierw oczyszczamy to, co przysłał Agent (jeśli dodał nawiasy)
                                var match = attrList.FirstOrDefault(a => {
                                    string agentTag = a["Tag"]?.ToString() ?? "";
                                    if (agentTag.Contains("[")) agentTag = agentTag.Substring(0, agentTag.IndexOf('[')).Trim();
                                    return agentTag.Equals(currentTag, StringComparison.OrdinalIgnoreCase);
                                });

                                if (match != null)
                                {
                                    string rawVal = match["Value"]?.ToString() ?? "";

                                    // 1. Pobierz obecną wartość atrybutu
                                    string oldVal = attRef.IsMTextAttribute ? attRef.MTextAttribute.Text : attRef.TextString;

                                    // 2. Jeśli Agent próbuje operacji na starej wartości (np. przesyła RPN: "$OLD_VALUE 1 +")
                                    if (rawVal.Contains("$OLD_VALUE"))
                                    {
                                        // Ujednolicamy przecinki na kropki, żeby RPN i systemy liczące się nie gubiły
                                        oldVal = oldVal.Replace(",", ".");
                                        rawVal = rawVal.Replace("$OLD_VALUE", oldVal);
                                    }

                                    // 3. Wstrzykiwanie ewentualnych zmiennych z pamięci Agenta
                                    string finalVal = AgentMemoryState.InjectVariables(rawVal);

                                    // 4. Integracja z silnikiem matematycznym RPN (jeśli wykryto notację RPN)
                                    if (finalVal.StartsWith("RPN:"))
                                    {
                                        string rpnExpression = finalVal.Replace("RPN:", "").Trim();
                                        string normalizedRpn = rpnExpression.ToLowerInvariant();
                                        if (normalizedRpn.Contains("(replace") || normalizedRpn.Contains("replace("))
                                        {
                                            throw new Exception("Wykryto nieprawidlowa skladnie pseudo-RPN. Uzyj postfix, np. RPN: $OLD_VALUE \"'\" \"\" REPLACE");
                                        }

                                        bool looksLikeTextRpn =
                                            normalizedRpn.Contains("replace") ||
                                            normalizedRpn.Contains("concat") ||
                                            normalizedRpn.Contains("ifempty") ||
                                            normalizedRpn.Contains("split") ||
                                            normalizedRpn.Contains("num_add");

                                        bool looksLikeInfixMath =
                                            !looksLikeTextRpn &&
                                            (rpnExpression.Contains("(") ||
                                             rpnExpression.Contains(")") ||
                                             Regex.IsMatch(rpnExpression, @"\s[\+\-\*/\^]\s"));

                                        if (looksLikeInfixMath)
                                        {
                                            rpnExpression = Bricscad_AgentAI_V2.Core.RpnCalculator.ConvertInfixToRpn(rpnExpression);
                                        }

                                        try
                                        {
                                            // Wykorzystanie silnika RpnCalculator z Core (zwraca obliczony wynik jako string)
                                            finalVal = Bricscad_AgentAI_V2.Core.RpnCalculator.Evaluate(rpnExpression);
                                        }
                                        catch
                                        {
                                            // W razie bledu obliczen zostawiamy surowy tekst
                                        }
                                    }
                                    if (attRef.IsMTextAttribute)
                                    {
                                        MText mtxt = attRef.MTextAttribute;
                                        mtxt.Contents = finalVal;
                                        attRef.MTextAttribute = mtxt;
                                    }
                                    else
                                    {
                                        attRef.TextString = finalVal;
                                    }
                                    updatedCount++;
                                    processedTagsInThisBlock.Add(currentTag);
                                }
                            }
                        }

                        // Raportowanie brakujących tagów
                        if (action == "Update" && attrList != null)
                        {
                            foreach (var attr in attrList)
                            {
                                string targetTag = attr["Tag"]?.ToString();
                                if (!string.IsNullOrEmpty(targetTag) && !processedTagsInThisBlock.Contains(targetTag))
                                {
                                    warnings.Add($"Tag '{targetTag}' nie znaleziony w bloku '{br.Name}' (ID: {id}).");
                                }
                            }
                            // Odświeżenie instancji bloku po zmianie atrybutów
                            br.UpgradeOpen();
                            br.RecordGraphicsModified(true);
                        }
                    }
                    tr.Commit();
                }

                if (action == "Read")
                {
                    string resultVal = string.Join(" | ", readValues.Distinct());
                    if (!string.IsNullOrEmpty(saveAs))
                    {
                        AgentMemoryState.Variables[saveAs] = resultVal;
                    }
                    return $"WYNIK (Read): Odczytano {readValues.Count} wartości z {blocksProcessed} bloków.{(string.IsNullOrEmpty(saveAs) ? " Treść: " + resultVal : " Zapisano w @" + saveAs)}";
                }
                else
                {
                    string result = $"WYNIK (Update): Zaktualizowano {updatedCount} atrybutów w {blocksProcessed} blokach.";
                    if (warnings.Count > 0)
                    {
                        result += "\nUWAGI:\n- " + string.Join("\n- ", warnings.Take(5));
                        if (warnings.Count > 5) result += $"\n... i {warnings.Count - 5} innych.";
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY EDYCJI ATRYBUTÓW: {ex.Message}";
            }
        }
        public List<string> Examples => null;
    }
}

