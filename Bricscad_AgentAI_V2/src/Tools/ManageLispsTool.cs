using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ManageLispsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "manage_lisps",
                    Description = "Zarządza skryptami LISP. Pozwala na tworzenie, edycję, czytanie i usuwanie skryptów. Skrypty LISP używane są przez profil do zadań związanych z automatyzacją.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Action", new ToolParameter { Type = "string", Enum = new List<string> { "save_lisp", "read_lisp", "execute_lisp", "delete_lisp", "list_lisps", "generate_lisp" }, Description = "Akcja do wykonania. 'generate_lisp' to AWARYJNE narzedzie (v2.35.3 / BUG #7): gdy SelectEntities/Foreach nie wystarczaja (np. setki obiektow), wygeneruj LISP z odpowiednim szablonem i wykonaj go." } },
                            { "LispId", new ToolParameter { Type = "string", Description = "Unikalny identyfikator skryptu (np. 'c:prostokat' lub 'moj_test'). Bez spacji, z użyciem podkreśleń. Opcjonalne tylko dla list_lisps." } },
                            { "Category", new ToolParameter { Type = "string", Description = "Kategoria skryptu LISP. Wymagane przy zapisie. Przykłady: 'Geometria', 'Narzędzia', 'Testy'." } },
                            { "Description", new ToolParameter { Type = "string", Description = "Krótki opis co robi skrypt. Wymagane przy zapisie." } },
                            { "LispCode", new ToolParameter { Type = "string", Description = "Kod źródłowy LISP. Wymagane przy zapisie." } },
                            { "Template", new ToolParameter { Type = "string", Enum = new List<string> { "replace_text_in_layers" }, Description = "Szablon LISP dla generate_lisp. 'replace_text_in_layers' - zamienia tekst w obiektach TEXT/MTEXT na podanych warstwach." } },
                            { "Layers", new ToolParameter { Type = "string", Description = "Lista warstw oddzielonych przecinkami (wymagane dla Template=replace_text_in_layers)." } },
                            { "FindText", new ToolParameter { Type = "string", Description = "Tekst do znalezienia (wymagane dla Template=replace_text_in_layers)." } },
                            { "ReplaceWith", new ToolParameter { Type = "string", Description = "Nowy tekst (wymagane dla Template=replace_text_in_layers)." } },
                            { "MatchMode", new ToolParameter { Type = "string", Enum = new List<string> { "exact", "contains" }, Description = "Tryb dopasowania: 'exact' = cale wyrazenie, 'contains' = tekst zawiera FindText (v2.35.3)." } }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Action\": \"list_lisps\" }",
            "{ \"Action\": \"save_lisp\", \"LispId\": \"test_srodowiska\", \"Category\": \"Testy\", \"Description\": \"Testowy skrypt\", \"LispCode\": \"(defun c:test_srodowiska ... )\" }",
            "{ \"Action\": \"read_lisp\", \"LispId\": \"test_srodowiska\" }",
            "{ \"Action\": \"execute_lisp\", \"LispId\": \"test_srodowiska\" }",
            "{ \"Action\": \"delete_lisp\", \"LispId\": \"test_srodowiska\" }"
        };

        public string Execute(Document doc, JObject args)
        {
            if (args["Action"] == null)
                return "Błąd: Brak parametru Action.";
            
            string action = args["Action"].ToString();

            if (action == "list_lisps")
            {
                var lisps = LispManager.LoadAllLisps();
                if (lisps == null) return "Brak zapisanych skryptów LISP.";
                var list = new List<string>();
                foreach (var l in lisps)
                {
                    list.Add($"- [{l.Category}] {l.LispId}: {l.Description}");
                }
                if (list.Count == 0) return "Baza skryptów LISP jest pusta.";
                return "Dostępne skrypty LISP:\n" + string.Join("\n", list);
            }

            if (args["LispId"] == null)
                return "Błąd: Brak parametru LispId.";

            string lispId = args["LispId"].ToString();

            try
            {
                if (action == "save_lisp")
                {
                    if (args["LispCode"] == null) return "Błąd: Brak parametru LispCode.";
                    if (args["Category"] == null) return "Błąd: Brak parametru Category.";
                    if (args["Description"] == null) return "Błąd: Brak parametru Description.";

                    var metadata = new LispMetadata
                    {
                        LispId = lispId,
                        Category = args["Category"].ToString(),
                        Description = args["Description"].ToString(),
                        CreatedAt = DateTime.Now
                    };

                    LispManager.SaveLisp(metadata, args["LispCode"].ToString());
                    return $"[SUKCES] Skrypt LISP '{lispId}' został zapisany w Bazie Wiedzy.";
                }
                else if (action == "read_lisp")
                {
                    string code = LispManager.GetLispCode(lispId);
                    if (string.IsNullOrEmpty(code))
                        return $"[BŁĄD] Nie znaleziono skryptu LISP o identyfikatorze '{lispId}' w Bazie Wiedzy.";
                    
                    return $"Skrypt LISP '{lispId}':\n```lisp\n{code}\n```";
                }
                else if (action == "execute_lisp")
                {
                    string code = LispManager.GetLispCode(lispId);
                    if (string.IsNullOrEmpty(code))
                        return $"[BŁĄD] Nie znaleziono skryptu LISP o identyfikatorze '{lispId}' w Bazie Wiedzy do wykonania.";

                    LispManager.TriggerLispExecution(lispId, code);

                    return $"[SUKCES] Polecenie uruchomienia skryptu '{lispId}' w BricsCAD zostało przekazane i jest wykonywane.";
                }
                else if (action == "generate_lisp")
                {
                    // Fix v2.35.3 (BUG #7): AWARYJNY LISP FALLBACK.
                    // Gdy SelectEntities + ForeachTool nie wystarczaja (np. 1000+ obiektow,
                    // LLM wpadl w petle wywolan), generujemy LISP-a z odpowiednim szablonem
                    // i wykonujemy go inline. LISP wykonuje sie JEDNYM wywolaniem - nie ma
                    // limitu iteracji.
                    string template = args["Template"]?.ToString();
                    if (string.IsNullOrEmpty(template))
                        return "[BŁĄD] Brak parametru Template dla generate_lisp. Dostępne: replace_text_in_layers";

                    if (template == "replace_text_in_layers")
                    {
                        string layers = args["Layers"]?.ToString();
                        string findText = args["FindText"]?.ToString();
                        string replaceWith = args["ReplaceWith"]?.ToString();
                        string matchMode = args["MatchMode"]?.ToString() ?? "contains";

                        // Fix v2.35.4: Precyzyjny komunikat brakujacych pol (pomaga LLM
                        // poprawic wywolanie w nastepnej iteracji zamiast zgadywac).
                        if (string.IsNullOrEmpty(layers) || string.IsNullOrEmpty(findText) || string.IsNullOrEmpty(replaceWith))
                        {
                            var missing = new List<string>();
                            if (string.IsNullOrEmpty(layers)) missing.Add("Layers");
                            if (string.IsNullOrEmpty(findText)) missing.Add("FindText");
                            if (string.IsNullOrEmpty(replaceWith)) missing.Add("ReplaceWith");
                            return $"[BŁĄD] Brak wymaganych parametrow dla Template=replace_text_in_layers: " +
                                   $"{string.Join(", ", missing)}. " +
                                   $"Musisz podac WSZYSTKIE trzy parametry w nastepnej iteracji. " +
                                   $"PAMIĘTAJ: FindText to tekst DO ZNALEZIENIA w obiektach (np. 'Stal oc. DN15'), " +
                                   $"ReplaceWith to tekst DOCELOWY (np. 'PP-stabi PN20 %%C25').";
                        }

                        string lispCode = GenerateReplaceTextInLayersLisp(layers, findText, replaceWith, matchMode);
                        // Fix v2.35.4 (BUG #8): tmpLispId MUSI miec dwukropek (notacja LISP),
                        // ale plik na dysku zamieni ':' na '_' (Windows). To dziala poprawnie
                        // dzieki poprawce w LispManager.SaveLisp.
                        string tmpLispId = $"c:agent_replace_{DateTime.Now:HHmmssfff}";

                        // Fix v2.36.0 (BUG agent_bug_01): Snapshot licznika mutacji PRZED
                        // uruchomieniem LISP, zeby po nim odczytac DELTE (EngineTracer
                        // subskrybuje ObjectModified - jesli EngineTracer wlaczony).
                        int mutationsBeforeLisp = AgentMemoryState.MutationCount;
                        int modelSpaceBeforeLisp = EngineTracer.CountObjectsInModelSpace();

                        // Zapisz i wykonaj
                        LispManager.SaveLisp(
                            new LispMetadata { LispId = tmpLispId, Category = "AgentGenerated", Description = $"Auto-gen: replace '{findText}' in {layers}", CreatedAt = DateTime.Now },
                            lispCode);
                        LispManager.TriggerLispExecution(tmpLispId, lispCode);

                        // Fix v2.36.0 (BUG agent_bug_01): SendStringToExecute jest
                        // ASYNCHRONICZNY w Teigha - LISP wykonuje sie dopiero PO zakonczeniu
                        // naszego watku, wiec EngineTracer.MutationCount przy zwrocie z
                        // manage_lisps == 0 (falszywy alarm "BRAK MUTACJI").
                        // Czekamy na stabilizacje licznika (brak zmian przez 300ms) lub
                        // na timeout 8s - w zaleznosci co nastapi wczesniej.
                        int observedMutationsDelta = 0;
                        int observedModelSpaceDelta = 0;
                        try
                        {
                            const int maxWaitMs = 8000;
                            const int stableWindowMs = 300;
                            const int pollIntervalMs = 50;
                            int elapsed = 0;
                            int prevMutations = mutationsBeforeLisp;
                            int prevModelSpace = modelSpaceBeforeLisp;
                            int stableSince = 0;
                            bool stabilized = false;
                            while (elapsed < maxWaitMs)
                            {
                                System.Threading.Thread.Sleep(pollIntervalMs);
                                elapsed += pollIntervalMs;
                                int curMutations = AgentMemoryState.MutationCount;
                                int curModelSpace = EngineTracer.CountObjectsInModelSpace();
                                if (curMutations == prevMutations && curModelSpace == prevModelSpace)
                                {
                                    stableSince += pollIntervalMs;
                                    if (stableSince >= stableWindowMs)
                                    {
                                        stabilized = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    stableSince = 0;
                                    prevMutations = curMutations;
                                    prevModelSpace = curModelSpace;
                                }
                            }
                            observedMutationsDelta = AgentMemoryState.MutationCount - mutationsBeforeLisp;
                            observedModelSpaceDelta = EngineTracer.CountObjectsInModelSpace() - modelSpaceBeforeLisp;
                            // Wymusz refresh ekranu po LISP - niektore mutacje moga byc
                            // widoczne dopiero po regen (np. zmiana TextString nie zawsze
                            // triggeruje ObjectModified bezposrednio, dopiero po REDRAW).
                            try { doc.SendStringToExecute("_.REGEN\n", true, false, false); } catch { }
                            System.Threading.Thread.Sleep(200);
                        }
                        catch (Exception waitEx)
                        {
                            // Nie blokuj sesji jesli polling sie nie uda - LLM moze
                            // kontynuowac. Logujemy ostrzezenie.
                            System.Diagnostics.Debug.WriteLine($"[ManageLispsTool] Polling mutacji nie udany: {waitEx.Message}");
                        }

                        return $"[SUKCES] Wygenerowano i uruchomiono LISP '{tmpLispId}'.\n" +
                               $"Szablon: {template}\n" +
                               $"Warstwy: {layers}\n" +
                               $"Znajdz: {findText}\n" +
                               $"Zamien na: {replaceWith}\n" +
                               $"Tryb dopasowania: {matchMode}\n" +
                               $"LISP wykonuje sie bezposrednio w BricsCAD (jeden krok, bez limitu iteracji).\n" +
                               $"Zaobserwowane mutacje: {observedMutationsDelta} (EngineTracer), ModelSpace delta: {observedModelSpaceDelta}.\n\n" +
                               $"Kod LISP:\n{lispCode}";
                    }
                    return $"[BŁĄD] Nieznany Template '{template}'. Dostępne: replace_text_in_layers";
                }
                else if (action == "delete_lisp")
                {
                    LispManager.DeleteLisp(lispId);
                    return $"[SUKCES] Skrypt LISP '{lispId}' został usunięty z Bazy Wiedzy.";
                }
                else
                {
                    return $"Błąd: Nieznana akcja '{action}'. Dozwolone to: save_lisp, read_lisp, execute_lisp, delete_lisp, list_lisps.";
                }
            }
            catch (Exception ex)
            {
                return $"[BŁĄD] Podczas obsługi skryptu LISP wystąpił wyjątek: {ex.Message}";
            }
        }

        /// <summary>
        /// Fix v2.35.3 (BUG #7): Generuje LISP, ktory zamienia tekst w obiektach TEXT/MTEXT
        /// na podanych warstwach. Dziala w jednym wywolaniu SendCommand - bez limitu iteracji.
        /// Uzywany jako AWARYJNY FALLBACK gdy SelectEntities/ForeachTool nie wystarczaja.
        ///
        /// Wymagania:
        /// - findText/replaceWith: zamieniane 1:1 (bez regex). Znaki specjalne LISP
        ///   (backslash, cudzyslow) sa automatycznie escapowane.
        /// - matchMode=exact: cale wyrazenie musi sie zgadzac (vl-string-mismatch = 0).
        /// - matchMode=contains: wyrazenie musi zawierac FindText (vl-string-search).
        ///
        /// Warstwy sa przekazywane jako lista rozdzielana przecinkami. LISP iteruje po
        /// wszystkich obiektach w Model Space i przetwarza tylko te, ktore:
        /// 1) naleza do jednej z podanych warstw,
        /// 2) sa typu TEXT lub MTEXT,
        /// 3) spelniaja kryterium matchMode.
        /// </summary>
        private static string GenerateReplaceTextInLayersLisp(
            string layers, string findText, string replaceWith, string matchMode)
        {
            // Escapowanie znakow specjalnych LISP
            string escFind = findText.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string escReplace = replaceWith.Replace("\\", "\\\\").Replace("\"", "\\\"");

            // Lista warstw jako warunek LISP (porownanie z (assoc 8 ...))
            string[] layerArr = layers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var layerConds = new List<string>();
            foreach (var l in layerArr)
            {
                string el = l.Trim();
                if (string.IsNullOrEmpty(el)) continue;
                // BricsCAD LISP: porownanie nazwy warstwy
                layerConds.Add($"(= (cdr (assoc 8 entdata)) \"{EscapeLisp(el)}\")");
            }
            string layerOr = string.Join(" ", layerConds);

            // Tryb dopasowania
            string matchCond;
            if (matchMode.Equals("exact", StringComparison.OrdinalIgnoreCase))
            {
                // Exact match: (vl-string-mismatch "find" text) = 0 oznacza identycznosc
                matchCond = $"(zerop (vl-string-mismatch \"{escFind}\" textstr))";
            }
            else
            {
                // Contains: (vl-string-search "find" text) != nil
                matchCond = $"(vl-string-search \"{escFind}\" textstr)";
            }

            return $@"(defun c:agent_replace_{DateTime.Now:HHmmssfff} (/ ss i ent entdata obj textstr layercond result)
  (setq ss (ssget ""X"" '((0 . ""TEXT,MTEXT""))))
  (if ss
    (progn
      (setq i 0 result 0)
      (repeat (sslength ss)
        (setq ent (ssname ss i))
        (setq entdata (entget ent))
        (setq obj (vlax-ename->vla-object ent))
        (if (and (or {layerOr})
                 (vlax-property-available-p obj 'TextString))
          (progn
            (setq textstr (vlax-get-property obj 'TextString))
            (if {matchCond}
              (progn
                (vlax-put-property obj 'TextString ""{escReplace}"")
                (setq result (1+ result)))))))
        (setq i (1+ i)))
      (princ (strcat ""\nZamieniono tekst w "" (itoa result) "" obiektach na warstwach: "" ""{EscapeLisp(layers)}""))
      result)
    (princ ""\nNie znaleziono obiektow TEXT/MTEXT.""))
  (princ))";
        }

        private static string EscapeLisp(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
