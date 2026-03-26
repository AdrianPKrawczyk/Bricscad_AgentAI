using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using Teigha.DatabaseServices;
using Teigha.Runtime;

namespace BricsCAD_Agent
{
    public class TrainingStudio
    {
        // Globalna zmienna przechowująca ścieżkę. Agent zapamięta Twój wybór podczas jednej sesji!
        public static string AktywnyPlikTreningowy = @"D:\GitHub\Bricscad_AgentAI\Bricscad_AgentAI\Agent_Training_Data.jsonl";

        [CommandMethod("AGENT_BUILD_TAG", CommandFlags.UsePickSet)]
        public void BuildTagCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // --- Lista przechowująca wszystkie kroki aktualnej sekwencji ---
            System.Collections.Generic.List<string> historiaSekwencji = new System.Collections.Generic.List<string>();

            try
            {
                PromptSelectionResult psr = ed.SelectImplied();
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    Komendy.AktywneZaznaczenie = psr.Value.GetObjectIds();
                    ed.WriteMessage($"\n[System] Przechwycono {Komendy.AktywneZaznaczenie.Length} zaznaczonych obiektów do pamięci Agenta.");
                }

                bool kontynuujSekwencje = true;

                // =======================================================
                // GŁÓWNA PĘTLA KREATORA SEKWENCJI
                // =======================================================
                while (kontynuujSekwencje)
                {
                    PromptKeywordOptions pko = new PromptKeywordOptions("\nWybierz tag (ENTER dla Select)");
                    pko.Keywords.Add("Select");
                    pko.Keywords.Add("BlockEdit");
                    pko.Keywords.Add("GetProperties");
                    pko.Keywords.Add("FormatMText");
                    pko.Keywords.Add("UpdateMText");
                    pko.Keywords.Add("EditText");
                    pko.Keywords.Default = "Select";

                    PromptResult pr = ed.GetKeywords(pko);
                    if (pr.Status != PromptStatus.OK) return;

                    string finalTag = "";

                    // --- [SELECT] ---
                    // --- [SELECT] ---
                    if (pr.StringResult == "Select")
                    {
                        PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nWybierz tryb zaznaczania [New/Add/Remove]: ");
                        pkoMode.Keywords.Add("New"); pkoMode.Keywords.Add("Add"); pkoMode.Keywords.Add("Remove");
                        pkoMode.Keywords.Default = "New";
                        string mode = ed.GetKeywords(pkoMode).StringResult;

                        // ... (Kod ładujący plik bazy danych BricsCAD_API_Quick.txt zostaje bez zmian) ...
                        string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        string folder = System.IO.Path.GetDirectoryName(assemblyPath);
                        string filePath = System.IO.Path.Combine(folder, "BricsCAD_API_Quick.txt");
                        System.Collections.Generic.Dictionary<string, string> bazyDict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        if (System.IO.File.Exists(filePath))
                        {
                            string[] lines = System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                            foreach (var line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line) || !line.Contains("|")) continue;
                                string[] parts = line.Split(new[] { '|' }, 2);
                                string entName = parts[0].Trim();
                                if (entName.Equals("dbtext", StringComparison.OrdinalIgnoreCase)) entName = "DBText";
                                else if (entName.Equals("mtext", StringComparison.OrdinalIgnoreCase)) entName = "MText";
                                else if (entName.Equals("solid3d", StringComparison.OrdinalIgnoreCase)) entName = "Solid3d";
                                else entName = char.ToUpper(entName[0]) + entName.Substring(1).ToLower();
                                bazyDict[entName] = parts[1];
                            }
                        }

                        string entType = "";
                        if (bazyDict.Count > 0)
                        {
                            PromptKeywordOptions pkoEnt = new PromptKeywordOptions("\nWybierz typ obiektu z bazy (ESC by wpisac recznie)");
                            ed.WriteMessage("\n\n--- DOSTĘPNE OBIEKTY W BAZIE WIEDZY ---");
                            string listaObiektow = "";
                            foreach (var key in bazyDict.Keys)
                            {
                                try { pkoEnt.Keywords.Add(key, key.ToUpper(), key.ToUpper()); listaObiektow += $"{key}, "; } catch { }
                            }
                            ed.WriteMessage($"\n{listaObiektow.TrimEnd(',', ' ')}\n---------------------------------------");
                            PromptResult prEnt = ed.GetKeywords(pkoEnt);
                            if (prEnt.Status == PromptStatus.OK && !string.IsNullOrEmpty(prEnt.StringResult)) entType = prEnt.StringResult;
                        }

                        if (string.IsNullOrEmpty(entType))
                        {
                            PromptStringOptions psoType = new PromptStringOptions("\nPodaj typy obiektów po przecinku (np. Line, Circle): ");
                            psoType.AllowSpaces = true;
                            entType = ed.GetString(psoType).StringResult;
                            if (string.IsNullOrEmpty(entType)) entType = "Entity";
                        }

                        // --- NOWOŚĆ: PĘTLA ZBIERAJĄCA WIELE WARUNKÓW (LOGIKA AND) ---
                        System.Collections.Generic.List<string> warunkiList = new System.Collections.Generic.List<string>();
                        bool dodawajKolejneWarunki = true;
                        int licznikWarunkow = 1;

                        while (dodawajKolejneWarunki)
                        {
                            string prop = "";
                            System.Collections.Generic.Dictionary<string, string> propertiesMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                            if (bazyDict.Count > 0)
                            {
                                string pelnyOpis = "";
                                if (bazyDict.ContainsKey("Entity")) pelnyOpis += bazyDict["Entity"] + " ";
                                string glownaKlasa = entType.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                                if (bazyDict.ContainsKey(glownaKlasa) && !glownaKlasa.Equals("Entity", StringComparison.OrdinalIgnoreCase))
                                    pelnyOpis += bazyDict[glownaKlasa];

                                System.Text.RegularExpressions.MatchCollection matches = System.Text.RegularExpressions.Regex.Matches(pelnyOpis, @"\b([A-Z][A-Za-z0-9_]+)\s*\(([^)]+)\)");
                                foreach (System.Text.RegularExpressions.Match m in matches) propertiesMap[m.Groups[1].Value] = m.Groups[2].Value;

                                if (propertiesMap.Count > 0)
                                {
                                    PromptKeywordOptions pkoProp = new PromptKeywordOptions($"\nWybierz Wlasciwosc nr {licznikWarunkow} (ENTER by zakonczyc dodawanie warunkow)");
                                    pkoProp.AllowNone = true;
                                    ed.WriteMessage($"\n\n--- WŁAŚCIWOŚCI DLA: {glownaKlasa.ToUpper()} ---");

                                    // 1. Ładowanie standardowych właściwości z bazy
                                    foreach (var kvp in propertiesMap)
                                    {
                                        ed.WriteMessage($"\n [{kvp.Key}] - {kvp.Value}");
                                        try { pkoProp.Keywords.Add(kvp.Key, kvp.Key.ToUpper(), kvp.Key.ToUpper()); } catch { }
                                    }

                                    // --- 2. NOWOŚĆ: DODAJEMY WIRTUALNE WŁAŚCIWOŚCI WIZUALNE ---
                                    ed.WriteMessage($"\n\n--- WŁAŚCIWOŚCI WIZUALNE (Z uwzględnieniem 'Jak Warstwa') ---");
                                    string[] visualProps = { "VisualColor", "VisualLinetype", "VisualLineWeight", "VisualTransparency" };
                                    foreach (string vp in visualProps)
                                    {
                                        ed.WriteMessage($"\n [{vp}]");
                                        try { pkoProp.Keywords.Add(vp, vp.ToUpper(), vp.ToUpper()); } catch { }
                                    }

                                    ed.WriteMessage("\n-------------------------------------------");
                                    PromptResult prProp = ed.GetKeywords(pkoProp);
                                    if (prProp.Status == PromptStatus.OK && !string.IsNullOrEmpty(prProp.StringResult)) prop = prProp.StringResult;
                                }
                            }

                            if (string.IsNullOrEmpty(prop) && propertiesMap.Count == 0)
                            {
                                PromptStringOptions psoProp = new PromptStringOptions($"\nPodaj Właściwość nr {licznikWarunkow} (lub ENTER by zakonczyc warunki): ");
                                psoProp.AllowSpaces = false;
                                prop = ed.GetString(psoProp).StringResult;
                            }

                            // Jeśli wciśnięto ENTER (brak właściwości), przerywamy pętlę
                            if (string.IsNullOrEmpty(prop))
                            {
                                dodawajKolejneWarunki = false;
                                break;
                            }

                            PromptKeywordOptions pkoOp = new PromptKeywordOptions("\nWybierz operator [Rowne/Wieksze/Mniejsze/Zawiera]: ");
                            pkoOp.Keywords.Add("Rowne"); pkoOp.Keywords.Add("Wieksze"); pkoOp.Keywords.Add("Mniejsze"); pkoOp.Keywords.Add("Zawiera");
                            pkoOp.Keywords.Default = "Rowne";
                            string opWord = ed.GetKeywords(pkoOp).StringResult;
                            string opSign = "==";
                            if (opWord == "Wieksze") opSign = ">"; if (opWord == "Mniejsze") opSign = "<"; if (opWord == "Zawiera") opSign = "Contains";

                            PromptStringOptions psoVal = new PromptStringOptions($"\nPodaj szukaną wartość dla {prop}: ");
                            psoVal.AllowSpaces = true;
                            string val = ed.GetString(psoVal).StringResult;
                            if (!double.TryParse(val.Replace(",", "."), out _) && val != "true" && val != "false") val = $"\"{val}\"";

                            // Zapisujemy ten pojedynczy warunek do listy
                            warunkiList.Add($"{{\"Property\": \"{prop}\", \"Operator\": \"{opSign}\", \"Value\": {val}}}");
                            licznikWarunkow++;

                            // Pytamy czy chcemy dodać logiczne AND (kolejny warunek)
                            PromptKeywordOptions pkoJeszcze = new PromptKeywordOptions("\nCzy chcesz dodać KOLEJNY warunek (logika AND)? [Tak/Nie]: ");
                            pkoJeszcze.Keywords.Add("Tak"); pkoJeszcze.Keywords.Add("Nie");
                            pkoJeszcze.Keywords.Default = "Nie";
                            if (ed.GetKeywords(pkoJeszcze).StringResult != "Tak")
                            {
                                dodawajKolejneWarunki = false;
                            }
                        }

                        // Składamy wszystkie warunki po przecinku w jedną dużą tablicę JSON
                        string wszystkieWarunkiJson = string.Join(", ", warunkiList);
                        finalTag = $"[SELECT: {{\"Mode\": \"{mode}\", \"EntityType\": \"{entType}\", \"Conditions\": [{wszystkieWarunkiJson}]}}]";


                    }                    // --- [EDIT_BLOCK] ---
                    else if (pr.StringResult == "BlockEdit")
                    {
                        System.Collections.Generic.List<string> argsList = new System.Collections.Generic.List<string>();
                        string col = ed.GetString(new PromptStringOptions("\nPodaj nowy Kolor (1-255, 0-ByBlock, 256-ByLayer) jaki mają przyjąć elementy w bloku lub ENTER by pominąć: ()")).StringResult;
                        if (!string.IsNullOrEmpty(col)) argsList.Add($"\"Color\": {col}");
                        string lay = ed.GetString(new PromptStringOptions("\nPodaj nową Warstwę, na którą zostaną zrzucone elementy wewnętrzne bloku lub ENTER by pominąć: ")).StringResult;
                        if (!string.IsNullOrEmpty(lay)) argsList.Add($"\"Layer\": \"{lay}\"");
                        string filt = ed.GetString(new PromptStringOptions("\nPodaj FilterColor (tylko obiekty w tym kolorze będą zmienione, Jeśli wpiszesz tu np. 2 (żółty), a w Kolor wpiszesz 1 (czerwony), to program wejdzie do bloku, ale zmieni kolor tylko tych elementów, które obecnie są żółte. Reszty nie ruszy) lub ENTER by pominąć: ")).StringResult;
                        if (!string.IsNullOrEmpty(filt)) argsList.Add($"\"FilterColor\": {filt}");
                        string findText = ed.GetString(new PromptStringOptions("\nPodaj tekst, który chcesz znaleźć wewnątrz bloku lub w jego atrybutach lub ENTER by pominąć: ")).StringResult;
                        if (!string.IsNullOrEmpty(findText))
                        {
                            string repText = ed.GetString(new PromptStringOptions("\nPodaj Tekst do podmiany, na które chcesz zamienić znaleziony tekst.: ")).StringResult;
                            argsList.Add($"\"FindText\": \"{findText}\""); argsList.Add($"\"ReplaceText\": \"{repText}\"");
                        }
                        finalTag = $"[ACTION:EDIT_BLOCK {{{string.Join(", ", argsList)}}}]";
                    }
                    // --- [GET_PROPERTIES] ---
                    else if (pr.StringResult == "GetProperties")
                    {
                        finalTag = "[ACTION:GET_PROPERTIES]";
                    }
                    // --- [MTEXT_FORMAT] ---
                    else if (pr.StringResult == "FormatMText")
                    {
                        // --- WYŚWIETLANIE INSTRUKCJI W KONSOLI ---
                    ed.WriteMessage("\n\n--- OPIS TRYBÓW NARZĘDZIA MTEXT_FORMAT ---");
                        ed.WriteMessage("\n[HighlightWord] - Wyróżnia tylko JEDNO słowo.");
                        ed.WriteMessage("\n  Szuka konkretnego słowa i zmienia formatowanie (kolor/pogrubienie) tylko dla niego, resztę zostawia bez zmian.");
                        ed.WriteMessage("\n  (Wymaga podania szukanego słowa).");

                        ed.WriteMessage("\n\n[FormatAll] - Formatuje CAŁY tekst.");
                        ed.WriteMessage("\n  Ignoruje pojedyncze słowa i narzuca nowe formatowanie na całą zawartość zaznaczonych tekstów.");

                        ed.WriteMessage("\n\n[ClearFormatting] - Czyszczenie (Narzędzie ratunkowe).");
                        ed.WriteMessage("\n  Kasuje ręczne zmiany koloru/czcionki w edytorze i przywraca tekst do domyślnego wyglądu ze Stylu i Warstwy.");
                        ed.WriteMessage("\n------------------------------------------\n");

                        PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nWybierz Mode [HighlightWord/FormatAll/ClearFormatting]: ");
                        pkoMode.Keywords.Add("HighlightWord"); pkoMode.Keywords.Add("FormatAll"); pkoMode.Keywords.Add("ClearFormatting");
                        pkoMode.Keywords.Default = "HighlightWord";
                        string mode = ed.GetKeywords(pkoMode).StringResult;
                        System.Collections.Generic.List<string> argsList = new System.Collections.Generic.List<string> { $"\"Mode\": \"{mode}\"" };

                        if (mode == "HighlightWord")
                        {
                            PromptStringOptions psoWord = new PromptStringOptions("\nPodaj szukane słowo (Word): ");
                            psoWord.AllowSpaces = true;
                            argsList.Add($"\"Word\": \"{ed.GetString(psoWord).StringResult}\"");
                        }

                        string col = ed.GetString(new PromptStringOptions("\nPodaj nowy Kolor (1-255) lub ENTER by pominąć: ")).StringResult;
                        if (!string.IsNullOrEmpty(col)) argsList.Add($"\"Color\": {col}");

                        PromptKeywordOptions pkoBold = new PromptKeywordOptions("\nPogrubienie (Bold)? [Tak/Nie/Pomin]: ");
                        pkoBold.Keywords.Add("Tak"); pkoBold.Keywords.Add("Nie"); pkoBold.Keywords.Add("Pomin"); pkoBold.Keywords.Default = "Pomin";
                        string bold = ed.GetKeywords(pkoBold).StringResult;
                        if (bold == "Tak") argsList.Add("\"Bold\": true"); else if (bold == "Nie") argsList.Add("\"Bold\": false");

                        finalTag = $"[ACTION:MTEXT_FORMAT {{{string.Join(", ", argsList)}}}]";
                    }
                    // --- [TEXT EDIT TOOLS] ---
                    else if (pr.StringResult == "UpdateMText" || pr.StringResult == "EditText")
                    {
                        string actionName = pr.StringResult == "UpdateMText" ? "MTEXT_EDIT" : "TEXT_EDIT";

                        // --- WYŚWIETLANIE INSTRUKCJI W KONSOLI ---
                        ed.WriteMessage($"\n\n--- OPIS TRYBÓW NARZĘDZIA {actionName} ---");
                        ed.WriteMessage("\n[Append] - Dopisywanie na końcu.");
                        ed.WriteMessage("\n  Dodaje Twój nowy tekst na samym końcu istniejącej zawartości zaznaczonych tekstów.");

                        ed.WriteMessage("\n\n[Prepend] - Dopisywanie na początku.");
                        ed.WriteMessage("\n  Wstawia Twój nowy tekst na samym początku istniejącej zawartości zaznaczonych tekstów.");

                        ed.WriteMessage("\n\n[Replace] - Zamiana tekstu (Znajdź i Zamień).");
                        ed.WriteMessage("\n  Szuka konkretnego słowa lub zdania (FindText) i podmienia je na nowy tekst.");
                        ed.WriteMessage("\n------------------------------------------\n");

                        PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nWybierz Mode [Append/Prepend/Replace]: ");
                        pkoMode.Keywords.Add("Append"); pkoMode.Keywords.Add("Prepend"); pkoMode.Keywords.Add("Replace");
                        pkoMode.Keywords.Default = "Append";
                        string mode = ed.GetKeywords(pkoMode).StringResult;
                        System.Collections.Generic.List<string> argsList = new System.Collections.Generic.List<string> { $"\"Mode\": \"{mode}\"" };

                        if (mode == "Replace")
                        {
                            PromptStringOptions psoFind = new PromptStringOptions("\nPodaj szukany tekst do podmiany (FindText): ");
                            psoFind.AllowSpaces = true;
                            argsList.Add($"\"FindText\": \"{ed.GetString(psoFind).StringResult}\"");
                        }

                        PromptStringOptions psoText = new PromptStringOptions($"\nPodaj nowy tekst (Text) dla trybu {mode}: ");
                        psoText.AllowSpaces = true;
                        argsList.Add($"\"Text\": \"{ed.GetString(psoText).StringResult}\"");

                        string col = ed.GetString(new PromptStringOptions("\nPodaj nowy Kolor (1-255) lub ENTER by pominąć: ")).StringResult;
                        if (!string.IsNullOrEmpty(col)) argsList.Add($"\"Color\": {col}");

                        if (actionName == "MTEXT_EDIT")
                        {
                            PromptKeywordOptions pkoFormat = new PromptKeywordOptions("\nCzy dodać formatowanie do dodawanego tekstu? [Tak/Nie]: ");
                            pkoFormat.Keywords.Add("Tak"); pkoFormat.Keywords.Add("Nie"); pkoFormat.Keywords.Default = "Nie";
                            if (ed.GetKeywords(pkoFormat).StringResult == "Tak")
                            {
                                PromptKeywordOptions pkoB = new PromptKeywordOptions("\nBold? [Tak/Nie/Pomin]: ");
                                pkoB.Keywords.Add("Tak"); pkoB.Keywords.Add("Nie"); pkoB.Keywords.Add("Pomin"); pkoB.Keywords.Default = "Pomin";
                                string b = ed.GetKeywords(pkoB).StringResult;
                                if (b == "Tak") argsList.Add("\"Bold\": true"); else if (b == "Nie") argsList.Add("\"Bold\": false");

                                PromptKeywordOptions pkoI = new PromptKeywordOptions("\nItalic? [Tak/Nie/Pomin]: ");
                                pkoI.Keywords.Add("Tak"); pkoI.Keywords.Add("Nie"); pkoI.Keywords.Add("Pomin"); pkoI.Keywords.Default = "Pomin";
                                string i = ed.GetKeywords(pkoI).StringResult;
                                if (i == "Tak") argsList.Add("\"Italic\": true"); else if (i == "Nie") argsList.Add("\"Italic\": false");

                                PromptKeywordOptions pkoU = new PromptKeywordOptions("\nUnderline? [Tak/Nie/Pomin]: ");
                                pkoU.Keywords.Add("Tak"); pkoU.Keywords.Add("Nie"); pkoU.Keywords.Add("Pomin"); pkoU.Keywords.Default = "Pomin";
                                string u = ed.GetKeywords(pkoU).StringResult;
                                if (u == "Tak") argsList.Add("\"Underline\": true"); else if (u == "Nie") argsList.Add("\"Underline\": false");
                            }
                        }
                        finalTag = $"[ACTION:{actionName} {{{string.Join(", ", argsList)}}}]";
                    }

                    ed.WriteMessage($"\n\n--- WYGENEROWANY TAG JSON ---\n{finalTag}\n-----------------------------\n");

                    // =======================================================
                    // TESTOWANIE I ZAPIS KROKU
                    // =======================================================
                    string komunikatZTestu = "";
                    PromptKeywordOptions pkoExec = new PromptKeywordOptions("\nCzy chcesz przetestować ten tag na rysunku? [Tak/Nie]: ");
                    pkoExec.Keywords.Add("Tak"); pkoExec.Keywords.Add("Nie"); pkoExec.Keywords.Default = "Tak";

                    if (ed.GetKeywords(pkoExec).StringResult == "Tak")
                    {
                        komunikatZTestu = WykonywaczTagow(doc, finalTag);
                        ed.WriteMessage($"\n[WYNIK TESTU]: {komunikatZTestu}");
                    }

                    // Tu używamy metody z klasy Komendy:
                    historiaSekwencji.Add($"{{\"role\": \"assistant\", \"content\": \"{Komendy.SafeJson(finalTag)}\"}}");

                    PromptKeywordOptions pkoDalej = new PromptKeywordOptions("\nCzy chcesz dodać KOLEJNY KROK do tego scenariusza? [Tak/Nie]: ");
                    pkoDalej.Keywords.Add("Tak"); pkoDalej.Keywords.Add("Nie"); pkoDalej.Keywords.Default = "Nie";

                    if (ed.GetKeywords(pkoDalej).StringResult == "Tak")
                    {
                        string systemFeedback = "";
                        if (string.IsNullOrEmpty(komunikatZTestu)) komunikatZTestu = "Wykonano pomyślnie.";

                        if (finalTag.Contains("[SELECT:"))
                            systemFeedback = $"[SYSTEM]: {komunikatZTestu} Jeśli masz wykonać akcję na zaznaczeniu użyj [ACTION: ], w przeciwnym razie opisz wynik za pomocą [MSG: ].";
                        else
                            systemFeedback = $"Oto dane z narzędzia:\n{komunikatZTestu}\n\nKontynuuj zadanie. UŻYJ TAGU [MSG: twoja odpowiedź].";

                        historiaSekwencji.Add($"{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(systemFeedback)}\"}}");
                        ed.WriteMessage("\n--- KONTYNUACJA SEKWENCJI ---\n");
                    }
                    else
                    {
                        kontynuujSekwencje = false;
                    }
                } // Koniec pętli while

                // =======================================================
                // OSTATECZNY ZAPIS DO PLIKU Z WYBOREM NAZWY
                // =======================================================
                string nazwaPliku = System.IO.Path.GetFileName(AktywnyPlikTreningowy);
                PromptKeywordOptions pkoSave = new PromptKeywordOptions($"\nZapisać CAŁĄ sekwencję do [{nazwaPliku}]? [Tak/Zmien/Nie]: ");
                pkoSave.Keywords.Add("Tak");
                pkoSave.Keywords.Add("Zmien");
                pkoSave.Keywords.Add("Nie");
                pkoSave.Keywords.Default = "Tak";

                string decyzjaZapisu = ed.GetKeywords(pkoSave).StringResult;

                if (decyzjaZapisu == "Zmien")
                {
                    string folderPath = System.IO.Path.GetDirectoryName(AktywnyPlikTreningowy);
                    if (!System.IO.Directory.Exists(folderPath)) System.IO.Directory.CreateDirectory(folderPath);

                    string[] files = System.IO.Directory.GetFiles(folderPath, "*.jsonl");
                    ed.WriteMessage($"\n\n--- DOSTĘPNE PLIKI JSONL W FOLDERZE ---");
                    for (int i = 0; i < files.Length; i++)
                    {
                        ed.WriteMessage($"\n [{i + 1}] {System.IO.Path.GetFileName(files[i])}");
                    }
                    ed.WriteMessage("\n [N] - Utwórz NOWY plik");
                    ed.WriteMessage("\n---------------------------------------");

                    PromptStringOptions psoFile = new PromptStringOptions("\nWybierz numer pliku lub wpisz 'N' dla nowego: ");
                    string fileChoice = ed.GetString(psoFile).StringResult.Trim().ToUpper();

                    if (fileChoice == "N")
                    {
                        PromptStringOptions psoNew = new PromptStringOptions("\nPodaj nazwę nowego pliku (bez .jsonl): ");
                        string newName = ed.GetString(psoNew).StringResult.Trim();
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            AktywnyPlikTreningowy = System.IO.Path.Combine(folderPath, newName + ".jsonl");
                            ed.WriteMessage($"\n[System] Ustawiono jako aktywny nowy plik: {AktywnyPlikTreningowy}");
                            decyzjaZapisu = "Tak"; // Wymuszamy kontynuację zapisu po stworzeniu
                        }
                    }
                    else if (int.TryParse(fileChoice, out int idx) && idx > 0 && idx <= files.Length)
                    {
                        AktywnyPlikTreningowy = files[idx - 1];
                        ed.WriteMessage($"\n[System] Zmieniono aktywny plik na: {AktywnyPlikTreningowy}");
                        decyzjaZapisu = "Tak"; // Wymuszamy kontynuację zapisu po wybraniu
                    }
                    else
                    {
                        ed.WriteMessage("\n[Błąd] Nieprawidłowy wybór. Anulowano zapisywanie.");
                        decyzjaZapisu = "Nie";
                    }
                }

                // Właściwy zapis do wybranego/aktywnego pliku
                if (decyzjaZapisu == "Tak")
                {
                    PromptStringOptions psoPrompt = new PromptStringOptions("\nWpisz ludzkie polecenie DLA CAŁEJ sekwencji (np. Zaznacz teksty i dopisz X): ");
                    psoPrompt.AllowSpaces = true;
                    string userPrompt = ed.GetString(psoPrompt).StringResult;

                    if (!string.IsNullOrWhiteSpace(userPrompt))
                    {
                        string jsonLine = $"{{\"messages\": [{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(userPrompt)}\"}}, ";
                        jsonLine += string.Join(", ", historiaSekwencji);
                        jsonLine += "]}\n";

                        try
                        {
                            System.IO.File.AppendAllText(AktywnyPlikTreningowy, jsonLine, System.Text.Encoding.UTF8);
                            ed.WriteMessage($"\n[SUKCES! ZŁOTY STANDARD ZAPISANY] Zapisano do: {System.IO.Path.GetFileName(AktywnyPlikTreningowy)}");
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n[BŁĄD ZAPISU DO PLIKU]: {ex.Message}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[BŁĄD KREATORA]: {ex.Message}");
            }
        }

        public static string WykonywaczTagow(Document doc, string wklejonyTag)
        {
            try
            {
                if (wklejonyTag.Contains("[SELECT:"))
                {
                    // Tu odwołujemy się do metody z klasy Komendy:
                    int wynik = Komendy.WykonajInteligentneZaznaczenie(doc, wklejonyTag);
                    return $"Pomyślnie zaznaczono {wynik} obiekt(ów).";
                }
                else if (wklejonyTag.Contains("[ACTION:EDIT_BLOCK"))
                {
                    EditBlockTool tool = new EditBlockTool();
                    return tool.Execute(doc, wklejonyTag);
                }
                else if (wklejonyTag.Contains("[ACTION:GET_PROPERTIES"))
                {
                    GetPropertiesTool tool = new GetPropertiesTool();
                    return tool.Execute(doc, wklejonyTag);
                }
                else if (wklejonyTag.Contains("[ACTION:MTEXT_FORMAT"))
                {
                    MTextFormatTool tool = new MTextFormatTool();
                    return tool.Execute(doc, wklejonyTag);
                }
                else if (wklejonyTag.Contains("[ACTION:MTEXT_EDIT"))
                {
                    MTextEditTool tool = new MTextEditTool();
                    return tool.Execute(doc, wklejonyTag);
                }
                else if (wklejonyTag.Contains("[ACTION:TEXT_EDIT"))
                {
                    TextEditTool tool = new TextEditTool();
                    return tool.Execute(doc, wklejonyTag);
                }
                return "Brak rozpoznanego tagu narzędzia w wygenerowanym stringu.";
            }
            catch (System.Exception ex)
            {
                return $"BŁĄD WYKONANIA: {ex.Message}";
            }
        }

        private static Bricscad.Windows.PaletteSet dbManagerPalette = null;

        [CommandMethod("AGENT_DB_MANAGER")]
        public void UruchomDBManager()
        {
            if (dbManagerPalette == null)
            {
                dbManagerPalette = new Bricscad.Windows.PaletteSet("Bielik DB Manager");
                dbManagerPalette.Style = Bricscad.Windows.PaletteSetStyles.ShowCloseButton | Bricscad.Windows.PaletteSetStyles.ShowPropertiesMenu;
                dbManagerPalette.MinimumSize = new System.Drawing.Size(400, 600);

                DatasetManagerControl interfejs = new DatasetManagerControl();
                dbManagerPalette.Add("Zarządzanie Datasetem", interfejs);
            }
            dbManagerPalette.Visible = true;
        }

    }
}