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
                    pko.Keywords.Add("CreateObj");
                    pko.Keywords.Add("CREATEBlock");
                    pko.Keywords.Add("INSERTBlock");
                    pko.Keywords.Add("SETProps");
                    pko.Keywords.Add("BlockEdit");
                    pko.Keywords.Add("ListBlocks");
                    pko.Keywords.Add("GetPropsLite");
                    pko.Keywords.Add("ReadProp");
                    pko.Keywords.Add("LISTUnique");
                    pko.Keywords.Add("FULLGETProps");
                    pko.Keywords.Add("FormatMText");
                    pko.Keywords.Add("UpdateMText");
                    pko.Keywords.Add("EditText");
                    pko.Keywords.Add("ModifyGeom");
                    pko.Keywords.Add("AnnoScale");
                    pko.Keywords.Add("READScales");
                    pko.Keywords.Add("REMOVEScale");
                    pko.Keywords.Add("ASKUser");
                    pko.Keywords.Add("USERInput");
                    pko.Keywords.Add("MANAGELayers");
                    pko.Keywords.Add("SEARCHLayers");
                    pko.Keywords.Default = "Select";

                    PromptResult pr = ed.GetKeywords(pko);
                    if (pr.Status != PromptStatus.OK) return;

                    string finalTag = "";

                    // --- [CREATE_OBJECT] ---
                    if (pr.StringResult == "CreateObj")
                    {
                        PromptKeywordOptions pkoType = new PromptKeywordOptions("\nWybierz typ obiektu do utworzenia [Line/Circle/DBText]: ");
                        pkoType.Keywords.Add("Line"); pkoType.Keywords.Add("Circle"); pkoType.Keywords.Add("DBText");
                        pkoType.Keywords.Default = "Line";
                        string entityType = ed.GetKeywords(pkoType).StringResult;

                        System.Collections.Generic.List<string> argsList = new System.Collections.Generic.List<string> { $"\"EntityType\": \"{entityType}\"" };

                        if (entityType == "Line")
                        {
                            PromptKeywordOptions pkoSP = new PromptKeywordOptions("\nStartPoint [Wpisz/AskUser]: ");
                            pkoSP.Keywords.Add("Wpisz"); pkoSP.Keywords.Add("AskUser"); pkoSP.Keywords.Default = "AskUser";
                            if (ed.GetKeywords(pkoSP).StringResult == "AskUser") argsList.Add("\"StartPoint\": \"AskUser\"");
                            else { PromptStringOptions pso = new PromptStringOptions("\nPodaj (X,Y,Z): "); argsList.Add($"\"StartPoint\": \"({ed.GetString(pso).StringResult})\""); }

                            PromptKeywordOptions pkoEP = new PromptKeywordOptions("\nEndPoint [Wpisz/AskUser]: ");
                            pkoEP.Keywords.Add("Wpisz"); pkoEP.Keywords.Add("AskUser"); pkoEP.Keywords.Default = "AskUser";
                            if (ed.GetKeywords(pkoEP).StringResult == "AskUser") argsList.Add("\"EndPoint\": \"AskUser\"");
                            else { PromptStringOptions pso = new PromptStringOptions("\nPodaj (X,Y,Z): "); argsList.Add($"\"EndPoint\": \"({ed.GetString(pso).StringResult})\""); }
                        }
                        else if (entityType == "Circle")
                        {
                            PromptKeywordOptions pkoCen = new PromptKeywordOptions("\nCenter [Wpisz/AskUser]: ");
                            pkoCen.Keywords.Add("Wpisz"); pkoCen.Keywords.Add("AskUser"); pkoCen.Keywords.Default = "AskUser";
                            if (ed.GetKeywords(pkoCen).StringResult == "AskUser") argsList.Add("\"Center\": \"AskUser\"");
                            else { PromptStringOptions pso = new PromptStringOptions("\nPodaj (X,Y,Z): "); argsList.Add($"\"Center\": \"({ed.GetString(pso).StringResult})\""); }

                            // ZMIANA Z RADIUS NA DIAMETER
                            PromptKeywordOptions pkoDia = new PromptKeywordOptions("\nDiameter (Srednica) [Wpisz/AskUser]: ");
                            pkoDia.Keywords.Add("Wpisz"); pkoDia.Keywords.Add("AskUser"); pkoDia.Keywords.Default = "AskUser";
                            if (ed.GetKeywords(pkoDia).StringResult == "AskUser") argsList.Add("\"Diameter\": \"AskUser\"");
                            else { PromptStringOptions pso = new PromptStringOptions("\nPodaj Srednice: "); argsList.Add($"\"Diameter\": {ed.GetString(pso).StringResult.Replace(",", ".")}"); }
                        }
                        else if (entityType == "DBText")
                        {
                            PromptKeywordOptions pkoPos = new PromptKeywordOptions("\nPosition [Wpisz/AskUser]: ");
                            pkoPos.Keywords.Add("Wpisz"); pkoPos.Keywords.Add("AskUser"); pkoPos.Keywords.Default = "AskUser";
                            if (ed.GetKeywords(pkoPos).StringResult == "AskUser") argsList.Add("\"Position\": \"AskUser\"");
                            else { PromptStringOptions pso = new PromptStringOptions("\nPodaj (X,Y,Z): "); argsList.Add($"\"Position\": \"({ed.GetString(pso).StringResult})\""); }

                            PromptKeywordOptions pkoTxt = new PromptKeywordOptions("\nText [Wpisz/AskUser]: ");
                            pkoTxt.Keywords.Add("Wpisz"); pkoTxt.Keywords.Add("AskUser"); pkoTxt.Keywords.Default = "AskUser";
                            if (ed.GetKeywords(pkoTxt).StringResult == "AskUser") argsList.Add("\"Text\": \"AskUser\"");
                            else { PromptStringOptions pso = new PromptStringOptions("\nPodaj treść: "); pso.AllowSpaces = true; argsList.Add($"\"Text\": \"{ed.GetString(pso).StringResult}\""); }

                            PromptKeywordOptions pkoH = new PromptKeywordOptions("\nHeight [Wpisz/AskUser/Pomin]: ");
                            pkoH.Keywords.Add("Wpisz"); pkoH.Keywords.Add("AskUser"); pkoH.Keywords.Add("Pomin"); pkoH.Keywords.Default = "AskUser";
                            string hOpt = ed.GetKeywords(pkoH).StringResult;
                            if (hOpt == "AskUser") argsList.Add("\"Height\": \"AskUser\"");
                            else if (hOpt == "Wpisz") { PromptStringOptions pso = new PromptStringOptions("\nWysokość: "); argsList.Add($"\"Height\": {ed.GetString(pso).StringResult.Replace(",", ".")}"); }
                        }

                        // Wspólne, opcjonalne parametry
                        PromptKeywordOptions pkoLay = new PromptKeywordOptions("\nLayer [Wpisz/AskUser/Pomin]: ");
                        pkoLay.Keywords.Add("Wpisz"); pkoLay.Keywords.Add("AskUser"); pkoLay.Keywords.Add("Pomin"); pkoLay.Keywords.Default = "AskUser";
                        string layOpt = ed.GetKeywords(pkoLay).StringResult;
                        if (layOpt == "AskUser") argsList.Add("\"Layer\": \"AskUser\"");
                        else if (layOpt == "Wpisz") { PromptStringOptions pso = new PromptStringOptions("\nNazwa Warstwy: "); pso.AllowSpaces = true; argsList.Add($"\"Layer\": \"{ed.GetString(pso).StringResult}\""); }

                        PromptKeywordOptions pkoCol = new PromptKeywordOptions("\nColor [Wpisz/AskUser/Pomin]: ");
                        pkoCol.Keywords.Add("Wpisz"); pkoCol.Keywords.Add("AskUser"); pkoCol.Keywords.Add("Pomin"); pkoCol.Keywords.Default = "AskUser";
                        string colOpt = ed.GetKeywords(pkoCol).StringResult;
                        if (colOpt == "AskUser") argsList.Add("\"Color\": \"AskUser\"");
                        else if (colOpt == "Wpisz") { PromptStringOptions pso = new PromptStringOptions("\nNumer koloru: "); argsList.Add($"\"Color\": {ed.GetString(pso).StringResult}"); }

                        // === NOWOŚĆ: Zaznaczanie utworzonego obiektu ===
                        PromptKeywordOptions pkoSel = new PromptKeywordOptions("\nCzy ZAZNACZYĆ ten obiekt po utworzeniu? [Tak/Nie]: ");
                        pkoSel.Keywords.Add("Tak"); pkoSel.Keywords.Add("Nie"); pkoSel.Keywords.Default = "Tak";
                        if (ed.GetKeywords(pkoSel).StringResult == "Tak") argsList.Add("\"SelectObject\": true");
                        // ===============================================

                        finalTag = $"[ACTION:CREATE_OBJECT {{{string.Join(", ", argsList)}}}]";
                    }

                    // --- [CREATE_BLOCK] ---
                    else if (pr.StringResult == "CREATEBlock")
                    {
                        System.Collections.Generic.List<string> argsList = new System.Collections.Generic.List<string>();

                        PromptKeywordOptions pkoName = new PromptKeywordOptions("\nSkąd wziąć nazwę bloku? [Wpisz_z_palca/AskUser]: ");
                        pkoName.Keywords.Add("Wpisz_z_palca"); pkoName.Keywords.Add("AskUser"); pkoName.Keywords.Default = "AskUser";

                        if (ed.GetKeywords(pkoName).StringResult == "AskUser")
                        {
                            argsList.Add("\"Name\": \"AskUser\"");
                        }
                        else
                        {
                            PromptStringOptions psoName = new PromptStringOptions("\nPodaj nazwę dla bloku: ");
                            argsList.Add($"\"Name\": \"{ed.GetString(psoName).StringResult}\"");
                        }

                        PromptKeywordOptions pkoPt = new PromptKeywordOptions("\nJak ustalić punkt bazowy bloku? [Wpisz_XYZ/AskUser]: ");
                        pkoPt.Keywords.Add("Wpisz_XYZ"); pkoPt.Keywords.Add("AskUser"); pkoPt.Keywords.Default = "AskUser";

                        if (ed.GetKeywords(pkoPt).StringResult == "AskUser")
                        {
                            argsList.Add("\"BasePoint\": \"AskUser\"");
                        }
                        else
                        {
                            PromptStringOptions psoPt = new PromptStringOptions("\nPodaj współrzędne (X,Y,Z): ");
                            argsList.Add($"\"BasePoint\": \"({ed.GetString(psoPt).StringResult})\"");
                        }

                        finalTag = $"[ACTION:CREATE_BLOCK {{{string.Join(", ", argsList)}}}]";
                    }

                    // --- [INSERT_BLOCK] ---
                    else if (pr.StringResult == "INSERTBlock")
                    {
                        System.Collections.Generic.List<string> argsList = new System.Collections.Generic.List<string>();

                        PromptKeywordOptions pkoName = new PromptKeywordOptions("\nSkąd wziąć nazwę bloku? [Wpisz_z_palca/Pobierz_z_rysunku/AskUser]: ");
                        pkoName.Keywords.Add("Wpisz_z_palca");
                        pkoName.Keywords.Add("Pobierz_z_rysunku");
                        pkoName.Keywords.Add("AskUser");
                        pkoName.Keywords.Default = "Pobierz_z_rysunku";

                        string nameMode = ed.GetKeywords(pkoName).StringResult;

                        if (nameMode == "AskUser")
                        {
                            argsList.Add("\"Name\": \"AskUser\"");
                        }
                        else if (nameMode == "Pobierz_z_rysunku")
                        {
                            // Genialne zastosowanie USER_CHOICE do wyciągnięcia listy dostępnych bloków!
                            string ucTag = $"[ACTION:USER_CHOICE {{\"Question\": \"Wybierz blok do wstawienia:\", \"FetchTarget\": \"Property\", \"FetchScope\": \"Database\", \"FetchProperty\": \"Name\"}}]";
                            
                            ed.WriteMessage($"\n\n[System] --- WSTRZYKIWANIE KROKU POŚREDNIEGO (Skanowanie rysunku w poszukiwaniu bloków) ---");
                            string wynikUC = WykonywaczTagow(doc, ucTag);

                            if (wynikUC.Contains("anulował"))
                            {
                                finalTag = "ABORT";
                            }
                            else
                            {
                                // Zapisujemy ten krok do logów nauki, by Agent widział tok myślenia
                                historiaSekwencji.Add($"{{\"role\": \"assistant\", \"content\": \"{Komendy.SafeJson(ucTag)}\"}}");
                                string sysFeedback = $"Oto dane z narzędzia:\n{wynikUC}\n\nKontynuuj zadanie. UŻYJ TAGU [ACTION:INSERT_BLOCK ].";
                                historiaSekwencji.Add($"{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(sysFeedback)}\"}}");

                                int lastColonActual = wynikUC.LastIndexOf(':');
                                if (lastColonActual != -1)
                                {
                                    string wytypowanaNazwa = wynikUC.Substring(lastColonActual + 1).Trim();
                                    argsList.Add($"\"Name\": \"{wytypowanaNazwa}\"");
                                }
                            }
                        }
                        else
                        {
                            PromptStringOptions pso = new PromptStringOptions("\nPodaj nazwę bloku: ");
                            argsList.Add($"\"Name\": \"{ed.GetString(pso).StringResult}\"");
                        }

                        // Jeśli użytkownik nie wcisnął ESC, kontynuujemy budowanie tagu
                        if (finalTag != "ABORT")
                        {
                            PromptKeywordOptions pkoPt = new PromptKeywordOptions("\nJak ustalić punkt wstawienia? [Wpisz_XYZ/AskUser]: ");
                            pkoPt.Keywords.Add("Wpisz_XYZ"); pkoPt.Keywords.Add("AskUser"); pkoPt.Keywords.Default = "AskUser";

                            if (ed.GetKeywords(pkoPt).StringResult == "AskUser") argsList.Add("\"Position\": \"AskUser\"");
                            else { PromptStringOptions pso = new PromptStringOptions("\nPodaj współrzędne (X,Y,Z): "); argsList.Add($"\"Position\": \"({ed.GetString(pso).StringResult})\""); }

                            string sc = ed.GetString(new PromptStringOptions("\nSkala (domyślnie 1.0, ENTER by pominąć): ")).StringResult;
                            if (!string.IsNullOrWhiteSpace(sc)) argsList.Add($"\"Scale\": {sc.Replace(",", ".")}");

                            string rot = ed.GetString(new PromptStringOptions("\nObrót w stopniach (domyślnie 0, ENTER by pominąć): ")).StringResult;
                            if (!string.IsNullOrWhiteSpace(rot)) argsList.Add($"\"Rotation\": {rot.Replace(",", ".")}");

                            PromptKeywordOptions pkoSel = new PromptKeywordOptions("\nCzy ZAZNACZYĆ ten blok po wstawieniu? [Tak/Nie]: ");
                            pkoSel.Keywords.Add("Tak"); pkoSel.Keywords.Add("Nie"); pkoSel.Keywords.Default = "Tak";
                            if (ed.GetKeywords(pkoSel).StringResult == "Tak") argsList.Add("\"SelectObject\": true");

                            finalTag = $"[ACTION:INSERT_BLOCK {{{string.Join(", ", argsList)}}}]";
                        }
                    }

                    // --- [SELECT] ---
                    else if (pr.StringResult == "Select")
                    {
                        PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nWybierz tryb zaznaczania [New/Add/Remove]: ");
                        pkoMode.Keywords.Add("New"); pkoMode.Keywords.Add("Add"); pkoMode.Keywords.Add("Remove");
                        pkoMode.Keywords.Default = "New";
                        string mode = ed.GetKeywords(pkoMode).StringResult;

                        PromptKeywordOptions pkoScope = new PromptKeywordOptions("\nWybierz obszar wyszukiwania (Scope) [Model/Blocks]: ");
                        pkoScope.Keywords.Add("Model");
                        pkoScope.Keywords.Add("Blocks");
                        pkoScope.Keywords.Default = "Model";
                        string scope = ed.GetKeywords(pkoScope).StringResult;

                        PromptKeywordOptions pkoBaza = new PromptKeywordOptions("\nZ jakiej bazy wiedzy załadować podpowiedzi? [Quick/Full/AskUser]: ");
                        pkoBaza.Keywords.Add("Quick");
                        pkoBaza.Keywords.Add("Full");
                        pkoBaza.Keywords.Add("AskUser");
                        pkoBaza.Keywords.Default = "Quick";
                        string trybBazy = ed.GetKeywords(pkoBaza).StringResult;

                        // Wczytywanie słownika zawsze (nawet dla AskUser), żeby był dostępny w późniejszej pętli Właściwości
                        string nazwaBazy = (trybBazy == "Full") ? "BricsCAD_API_V22.txt" : "BricsCAD_API_Quick.txt";
                        string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        string folderDir = System.IO.Path.GetDirectoryName(assemblyPath);
                        string filePath = System.IO.Path.Combine(folderDir, nazwaBazy);
                        System.Collections.Generic.Dictionary<string, string> bazyDict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        if (System.IO.File.Exists(filePath))
                        {
                            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                            var matchesClasses = System.Text.RegularExpressions.Regex.Matches(content, @"\b([a-zA-Z0-9_]+)\|(.*?)(?=\b[a-zA-Z0-9_]+\||$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                            foreach (System.Text.RegularExpressions.Match m in matchesClasses)
                            {
                                string entName = m.Groups[1].Value.Trim();
                                if (entName.Contains(" ") || entName.Length > 35) continue;

                                if (entName.Equals("dbtext", StringComparison.OrdinalIgnoreCase)) entName = "DBText";
                                else if (entName.Equals("mtext", StringComparison.OrdinalIgnoreCase)) entName = "MText";
                                else if (entName.Equals("solid3d", StringComparison.OrdinalIgnoreCase)) entName = "Solid3d";
                                else entName = char.ToUpper(entName[0]) + entName.Substring(1).ToLower();

                                bazyDict[entName] = m.Groups[2].Value.Trim();
                            }
                        }

                        string entType = "";
                        bool abortSelect = false;

                        // --- MAGIA NR 1: Pytanie o Klasę Obiektu ---
                        if (trybBazy == "AskUser")
                        {
                            string ucTag = $"[ACTION:USER_CHOICE {{\"Question\": \"Wybierz typ obiektu (Klasę) z listy:\", \"FetchTarget\": \"Class\", \"FetchScope\": \"{scope}\"}}]";

                            ed.WriteMessage($"\n\n[System] --- WSTRZYKIWANIE KROKU POŚREDNIEGO (Myślenie Agenta) ---");
                            string wynikUC = WykonywaczTagow(doc, ucTag);
                            ed.WriteMessage($"\n[WYNIK]: {wynikUC}\n");

                            if (wynikUC.Contains("anulował"))
                            {
                                abortSelect = true;
                                finalTag = "ABORT";
                            }
                            else
                            {
                                // Wstrzykiwanie kroków do pamięci treningowej "w locie"
                                historiaSekwencji.Add($"{{\"role\": \"assistant\", \"content\": \"{Komendy.SafeJson(ucTag)}\"}}");
                                string sysFeedback = $"Oto dane z narzędzia:\n{wynikUC}\n\nKontynuuj zadanie. UŻYJ TAGU [SELECT: ].";
                                historiaSekwencji.Add($"{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(sysFeedback)}\"}}");

                                // Wyciągnięcie odpowiedzi i przypisanie do zmiennej entType
                                int lastColonActual = wynikUC.LastIndexOf(':');
                                if (lastColonActual != -1) entType = wynikUC.Substring(lastColonActual + 1).Trim();
                            }
                        }
                        else
                        {
                            if (bazyDict.Count > 0)
                            {
                                PromptKeywordOptions pkoEnt = new PromptKeywordOptions("\nWybierz typ obiektu z bazy (ESC by wpisac recznie)");
                                ed.WriteMessage("\n\n--- DOSTĘPNE OBIEKTY W BAZIE WIEDZY ---");

                                System.Collections.Generic.List<string> posortowaneKlucze = new System.Collections.Generic.List<string>(bazyDict.Keys);
                                posortowaneKlucze.Sort();

                                string listaObiektow = "\n";
                                int kolumna = 0;

                                foreach (var key in posortowaneKlucze)
                                {
                                    try
                                    {
                                        pkoEnt.Keywords.Add(key, key.ToUpper(), key.ToUpper());
                                        listaObiektow += string.Format("{0,-26}", key);
                                        kolumna++;
                                        if (kolumna >= 3)
                                        {
                                            listaObiektow += "\n";
                                            kolumna = 0;
                                        }
                                    }
                                    catch { }
                                }

                                try { pkoEnt.Keywords.Default = "Entity"; } catch { }

                                ed.WriteMessage($"\n{listaObiektow}\n---------------------------------------");

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
                        }

                        if (!abortSelect)
                        {
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

                                    if (nazwaBazy == "BricsCAD_API_V22.txt")
                                    {
                                        System.Text.RegularExpressions.MatchCollection matchesFull = System.Text.RegularExpressions.Regex.Matches(pelnyOpis, @"Właściwości \(Properties\):\s*(.*?)(?=\.\s*[A-Z]|\.$|$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                        foreach (System.Text.RegularExpressions.Match mFull in matchesFull)
                                        {
                                            string[] props = mFull.Groups[1].Value.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                                            foreach (string p in props)
                                            {
                                                string cleanProp = p.Trim().Split(new char[] { ' ', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)[0];
                                                if (cleanProp.Length > 1 && char.IsUpper(cleanProp[0]))
                                                {
                                                    propertiesMap[cleanProp] = "V22_API";
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        System.Text.RegularExpressions.MatchCollection matchesQuick = System.Text.RegularExpressions.Regex.Matches(pelnyOpis, @"\b([A-Z][A-Za-z0-9_]+)\s*\(([^)]+)\)");
                                        foreach (System.Text.RegularExpressions.Match m in matchesQuick) propertiesMap[m.Groups[1].Value] = m.Groups[2].Value;
                                    }

                                    if (propertiesMap.Count > 0)
                                    {
                                        PromptKeywordOptions pkoProp = new PromptKeywordOptions($"\nWybierz Wlasciwosc nr {licznikWarunkow} (ENTER by zakonczyc dodawanie warunkow)");
                                        pkoProp.AllowNone = true;
                                        ed.WriteMessage($"\n\n--- WŁAŚCIWOŚCI DLA: {glownaKlasa.ToUpper()} ---");

                                        System.Collections.Generic.List<string> posortowaneWlasciwosci = new System.Collections.Generic.List<string>(propertiesMap.Keys);
                                        posortowaneWlasciwosci.Sort();

                                        foreach (var klucz in posortowaneWlasciwosci)
                                        {
                                            ed.WriteMessage($"\n [{klucz}] - {propertiesMap[klucz]}");
                                            try { pkoProp.Keywords.Add(klucz, klucz.ToUpper(), klucz.ToUpper()); } catch { }
                                        }

                                        ed.WriteMessage($"\n\n--- WŁAŚCIWOŚCI WIZUALNE (Z uwzględnieniem 'Jak Warstwa') ---");
                                        string[] visualProps = { "VisualColor", "VisualLinetype", "VisualLineWeight", "VisualTransparency" };
                                        foreach (string vp in visualProps)
                                        {
                                            ed.WriteMessage($"\n [{vp}]");
                                            try { pkoProp.Keywords.Add(vp, vp.ToUpper(), vp.ToUpper()); } catch { }
                                        }

                                        ed.WriteMessage($"\n\n [WLASNA] - Wpisz ręcznie (np. z kropką Center.Z)");
                                        try { pkoProp.Keywords.Add("WLASNA", "WLASNA", "WLASNA"); } catch { }

                                        ed.WriteMessage("\n-------------------------------------------");
                                        PromptResult prProp = ed.GetKeywords(pkoProp);

                                        if (prProp.Status == PromptStatus.OK && !string.IsNullOrEmpty(prProp.StringResult))
                                        {
                                            if (prProp.StringResult == "WLASNA")
                                            {
                                                PromptStringOptions psoWlasna = new PromptStringOptions($"\nWpisz ręcznie nazwę właściwości (np. Layer): ");
                                                psoWlasna.AllowSpaces = false;
                                                prop = ed.GetString(psoWlasna).StringResult;
                                            }
                                            else
                                            {
                                                prop = prProp.StringResult;
                                            }
                                        }
                                    }
                                }

                                if (string.IsNullOrEmpty(prop) && propertiesMap.Count == 0)
                                {
                                    PromptStringOptions psoProp = new PromptStringOptions($"\nPodaj Właściwość nr {licznikWarunkow} (lub ENTER by zakonczyc warunki): ");
                                    psoProp.AllowSpaces = false;
                                    prop = ed.GetString(psoProp).StringResult;
                                }

                                if (string.IsNullOrEmpty(prop))
                                {
                                    dodawajKolejneWarunki = false;
                                    break;
                                }

                                PromptKeywordOptions pkoOp = new PromptKeywordOptions("\nWybierz operator [Rowne/Nierowne/Wieksze/Mniejsze/Zawiera/AskUser]: ");
                                pkoOp.Keywords.Add("Rowne");
                                pkoOp.Keywords.Add("Nierowne");
                                pkoOp.Keywords.Add("Wieksze");
                                pkoOp.Keywords.Add("Mniejsze");
                                pkoOp.Keywords.Add("Zawiera");
                                pkoOp.Keywords.Add("AskUser");
                                pkoOp.Keywords.Default = "Rowne";

                                string opWord = ed.GetKeywords(pkoOp).StringResult;

                                // --- MAGIA NR 2: Pytanie o Wartość danej Właściwości ---
                                if (opWord == "AskUser")
                                {
                                    PromptKeywordOptions pkoMulti = new PromptKeywordOptions("\nCzy wybór ma być wielokrotny (Checkboxy)? [Tak/Nie]: ");
                                    pkoMulti.Keywords.Add("Tak"); pkoMulti.Keywords.Add("Nie"); pkoMulti.Keywords.Default = "Nie";
                                    bool isMulti = ed.GetKeywords(pkoMulti).StringResult == "Tak";

                                    string multiParam = isMulti ? ", \"MultiSelect\": true" : "";
                                    string ucTag = $"[ACTION:USER_CHOICE {{\"Question\": \"Wybierz wartość dla '{prop}':\", \"FetchTarget\": \"Property\", \"FetchScope\": \"{scope}\", \"FetchProperty\": \"{prop}\"{multiParam}}}]";

                                    ed.WriteMessage($"\n\n[System] --- WSTRZYKIWANIE KROKU POŚREDNIEGO (Myślenie Agenta) ---");
                                    string wynikUC = WykonywaczTagow(doc, ucTag);
                                    ed.WriteMessage($"\n[WYNIK]: {wynikUC}\n");

                                    if (wynikUC.Contains("anulował"))
                                    {
                                        abortSelect = true;
                                        finalTag = "ABORT";
                                        break;
                                    }

                                    historiaSekwencji.Add($"{{\"role\": \"assistant\", \"content\": \"{Komendy.SafeJson(ucTag)}\"}}");
                                    string sysFeedback = $"Oto dane z narzędzia:\n{wynikUC}\n\nKontynuuj zadanie. UŻYJ TAGU [SELECT: ].";
                                    historiaSekwencji.Add($"{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(sysFeedback)}\"}}");

                                    string val = "";
                                    int lastColonActual = wynikUC.LastIndexOf(':');
                                    if (lastColonActual != -1) val = wynikUC.Substring(lastColonActual + 1).Trim();

                                    // Jeśli wybrano multiSelect, zmieniamy operator na "IN" i zostawiamy CSV w jednym cudzysłowie!
                                    string multiOpSign = isMulti ? "IN" : "==";

                                    if (isMulti)
                                    {
                                        val = $"\"{val}\"";
                                    }
                                    else
                                    {
                                        if (!double.TryParse(val.Replace(",", "."), out _) && val.ToLower() != "true" && val.ToLower() != "false") val = $"\"{val}\"";
                                    }

                                    warunkiList.Add($"{{\"Property\": \"{prop}\", \"Operator\": \"{multiOpSign}\", \"Value\": {val}}}");
                                    licznikWarunkow++;

                                    PromptKeywordOptions pkoJeszcze = new PromptKeywordOptions("\nCzy chcesz dodać KOLEJNY warunek (logika AND)? [Tak/Nie]: ");
                                    pkoJeszcze.Keywords.Add("Tak"); pkoJeszcze.Keywords.Add("Nie");
                                    pkoJeszcze.Keywords.Default = "Nie";
                                    if (ed.GetKeywords(pkoJeszcze).StringResult != "Tak") dodawajKolejneWarunki = false;

                                    continue;
                                }

                                string opSign = "==";
                                if (opWord == "Nierowne") opSign = "!=";
                                if (opWord == "Wieksze") opSign = ">";
                                if (opWord == "Mniejsze") opSign = "<";
                                if (opWord == "Zawiera") opSign = "Contains";

                                PromptStringOptions psoVal = new PromptStringOptions($"\nPodaj szukaną wartość dla {prop}: ");
                                psoVal.AllowSpaces = true;
                                string valNormal = ed.GetString(psoVal).StringResult;
                                if (!double.TryParse(valNormal.Replace(",", "."), out _) && valNormal.ToLower() != "true" && valNormal.ToLower() != "false") valNormal = $"\"{valNormal}\"";

                                warunkiList.Add($"{{\"Property\": \"{prop}\", \"Operator\": \"{opSign}\", \"Value\": {valNormal}}}");
                                licznikWarunkow++;

                                PromptKeywordOptions pkoJeszcze2 = new PromptKeywordOptions("\nCzy chcesz dodać KOLEJNY warunek (logika AND)? [Tak/Nie]: ");
                                pkoJeszcze2.Keywords.Add("Tak"); pkoJeszcze2.Keywords.Add("Nie");
                                pkoJeszcze2.Keywords.Default = "Nie";
                                if (ed.GetKeywords(pkoJeszcze2).StringResult != "Tak")
                                {
                                    dodawajKolejneWarunki = false;
                                }
                            }

                            // Złożenie finałowego Tagu SELECT, dziedziczącego wszystkie decyzje z okienek
                            if (!abortSelect && string.IsNullOrEmpty(finalTag))
                            {
                                string wszystkieWarunkiJson = string.Join(", ", warunkiList);
                                finalTag = $"[SELECT: {{\"Mode\": \"{mode}\", \"Scope\": \"{scope}\", \"EntityType\": \"{entType}\", \"Conditions\": [{wszystkieWarunkiJson}]}}]";
                            }
                        }
                    }

                    // --- [READ_PROPERTY] ---
                    else if (pr.StringResult == "ReadProp")
                    {
                        // 1. Ładowanie bazy wiedzy 
                        PromptKeywordOptions pkoBaza = new PromptKeywordOptions("\nZ jakiej bazy wiedzy załadować podpowiedzi? [Quick/Full]: ");
                        pkoBaza.Keywords.Add("Quick");
                        pkoBaza.Keywords.Add("Full");
                        pkoBaza.Keywords.Default = "Quick";
                        string trybBazy = ed.GetKeywords(pkoBaza).StringResult;

                        string nazwaBazy = (trybBazy == "Full") ? "BricsCAD_API_V22.txt" : "BricsCAD_API_Quick.txt";

                        string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        string folderDir = System.IO.Path.GetDirectoryName(assemblyPath);
                        string filePath = System.IO.Path.Combine(folderDir, nazwaBazy);
                        System.Collections.Generic.Dictionary<string, string> bazyDict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        if (System.IO.File.Exists(filePath))
                        {
                            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                            var matchesClasses = System.Text.RegularExpressions.Regex.Matches(content, @"\b([a-zA-Z0-9_]+)\|(.*?)(?=\b[a-zA-Z0-9_]+\||$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                            foreach (System.Text.RegularExpressions.Match m in matchesClasses)
                            {
                                string entName = m.Groups[1].Value.Trim();
                                if (entName.Contains(" ") || entName.Length > 35) continue;

                                if (entName.Equals("dbtext", StringComparison.OrdinalIgnoreCase)) entName = "DBText";
                                else if (entName.Equals("mtext", StringComparison.OrdinalIgnoreCase)) entName = "MText";
                                else if (entName.Equals("solid3d", StringComparison.OrdinalIgnoreCase)) entName = "Solid3d";
                                else entName = char.ToUpper(entName[0]) + entName.Substring(1).ToLower();

                                bazyDict[entName] = m.Groups[2].Value.Trim();
                            }
                        }

                        string entType = "Entity";
                        bool anulowano = false;
                        if (bazyDict.Count > 0)
                        {
                            PromptKeywordOptions pkoEnt = new PromptKeywordOptions($"\nZ jakiej klasy API wyświetlić podpowiedzi dla właściwości? (ENTER dla Entity, ESC by ZAKOŃCZYĆ)");
                            pkoEnt.AllowNone = true;

                            ed.WriteMessage("\n\n--- DOSTĘPNE OBIEKTY W BAZIE WIEDZY ---");
                            System.Collections.Generic.List<string> posortowaneKlucze = new System.Collections.Generic.List<string>(bazyDict.Keys);
                            posortowaneKlucze.Sort();

                            string listaObiektow = "\n";
                            int kolumna = 0;

                            foreach (var key in posortowaneKlucze)
                            {
                                try
                                {
                                    pkoEnt.Keywords.Add(key, key.ToUpper(), key.ToUpper());
                                    listaObiektow += string.Format("{0,-26}", key);
                                    kolumna++;
                                    if (kolumna >= 3)
                                    {
                                        listaObiektow += "\n";
                                        kolumna = 0;
                                    }
                                }
                                catch { }
                            }

                            try { pkoEnt.Keywords.Default = "Entity"; } catch { }
                            ed.WriteMessage($"\n{listaObiektow}\n---------------------------------------");

                            PromptResult prEnt = ed.GetKeywords(pkoEnt);

                            if (prEnt.Status == PromptStatus.Cancel) anulowano = true;
                            if (prEnt.Status == PromptStatus.OK && !string.IsNullOrEmpty(prEnt.StringResult))
                                entType = prEnt.StringResult;
                        }

                        if (!anulowano)
                        {
                            string propName = "";
                            System.Collections.Generic.Dictionary<string, string> propertiesMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                            if (bazyDict.Count > 0)
                            {
                                string pelnyOpis = "";
                                if (bazyDict.ContainsKey("Entity")) pelnyOpis += bazyDict["Entity"] + " ";
                                string glownaKlasa = entType.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                                if (bazyDict.ContainsKey(glownaKlasa) && !glownaKlasa.Equals("Entity", StringComparison.OrdinalIgnoreCase))
                                    pelnyOpis += bazyDict[glownaKlasa];

                                if (nazwaBazy == "BricsCAD_API_V22.txt")
                                {
                                    System.Text.RegularExpressions.MatchCollection matchesFull = System.Text.RegularExpressions.Regex.Matches(pelnyOpis, @"Właściwości \(Properties\):\s*(.*?)(?=\.\s*[A-Z]|\.$|$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    foreach (System.Text.RegularExpressions.Match mFull in matchesFull)
                                    {
                                        string[] props = mFull.Groups[1].Value.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                                        foreach (string p in props)
                                        {
                                            string cleanProp = p.Trim().Split(new char[] { ' ', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)[0];
                                            if (cleanProp.Length > 1 && char.IsUpper(cleanProp[0]))
                                            {
                                                propertiesMap[cleanProp] = "V22_API";
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    System.Text.RegularExpressions.MatchCollection matchesQuick = System.Text.RegularExpressions.Regex.Matches(pelnyOpis, @"\b([A-Z][A-Za-z0-9_]+)\s*\(([^)]+)\)");
                                    foreach (System.Text.RegularExpressions.Match m in matchesQuick) propertiesMap[m.Groups[1].Value] = m.Groups[2].Value;
                                }

                                if (propertiesMap.Count > 0)
                                {
                                    PromptKeywordOptions pkoProp = new PromptKeywordOptions($"\nWybierz Wlasciwosc do odczytania (ENTER by zakonczyc)");
                                    pkoProp.AllowNone = true;
                                    ed.WriteMessage($"\n\n--- WŁAŚCIWOŚCI DLA: {glownaKlasa.ToUpper()} ---");

                                    System.Collections.Generic.List<string> posortowaneWlasciwosci = new System.Collections.Generic.List<string>(propertiesMap.Keys);
                                    posortowaneWlasciwosci.Sort();

                                    foreach (var klucz in posortowaneWlasciwosci)
                                    {
                                        // Dla ReadProp celowo ukrywamy "wirtualne" właściwości Visual (są dobre do SELECT, ale nie do odczytu twardej geometrii)
                                        if (klucz.StartsWith("Visual", StringComparison.OrdinalIgnoreCase)) continue;
                                        ed.WriteMessage($"\n [{klucz}] - {propertiesMap[klucz]}");
                                        try { pkoProp.Keywords.Add(klucz, klucz.ToUpper(), klucz.ToUpper()); } catch { }
                                    }

                                    ed.WriteMessage($"\n\n [WLASNA] - Wpisz ręcznie (np. z kropką Center.Z)");
                                    try { pkoProp.Keywords.Add("WLASNA", "WLASNA", "WLASNA"); } catch { }

                                    ed.WriteMessage("\n-------------------------------------------");
                                    PromptResult prProp = ed.GetKeywords(pkoProp);

                                    if (prProp.Status == PromptStatus.Cancel) anulowano = true;
                                    if (prProp.Status == PromptStatus.OK && !string.IsNullOrEmpty(prProp.StringResult))
                                    {
                                        if (prProp.StringResult == "WLASNA")
                                        {
                                            PromptStringOptions psoWlasna = new PromptStringOptions($"\nWpisz ręcznie nazwę właściwości (np. Position.Z): ");
                                            psoWlasna.AllowSpaces = false;
                                            propName = ed.GetString(psoWlasna).StringResult;
                                        }
                                        else
                                        {
                                            propName = prProp.StringResult;
                                        }
                                    }
                                }
                            }

                            if (!anulowano)
                            {
                                if (string.IsNullOrEmpty(propName) && propertiesMap.Count == 0)
                                {
                                    PromptStringOptions psoName = new PromptStringOptions($"\nPodaj nazwę właściwości do odczytu (np. Layer, Center) lub ENTER by zakończyć: ");
                                    psoName.AllowSpaces = false;
                                    propName = ed.GetString(psoName).StringResult;
                                }

                                if (!string.IsNullOrEmpty(propName))
                                {
                                    finalTag = $"[ACTION:READ_PROPERTY {{\"Property\": \"{propName}\"}}]";
                                }
                            }
                        }
                    }


                    // --- [MODIFY GEOMETRY] ---
                    else if (pr.StringResult == "ModifyGeom")
                    {
                        PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nWybierz tryb edycji [Erase/Move/Copy/Rotate/Scale]: ");
                        pkoMode.Keywords.Add("Erase"); pkoMode.Keywords.Add("Move"); pkoMode.Keywords.Add("Copy");
                        pkoMode.Keywords.Add("Rotate"); pkoMode.Keywords.Add("Scale");
                        pkoMode.Keywords.Default = "Move";
                        string mode = ed.GetKeywords(pkoMode).StringResult;

                        System.Collections.Generic.List<string> argsList = new System.Collections.Generic.List<string> { $"\"Mode\": \"{mode}\"" };

                        if (mode == "Move" || mode == "Copy")
                        {
                            PromptStringOptions psoVec = new PromptStringOptions($"\nPodaj Wektor przesunięcia (X,Y,Z) dla {mode}: ");
                            psoVec.AllowSpaces = false;
                            string vec = ed.GetString(psoVec).StringResult;
                            argsList.Add($"\"Vector\": \"({vec})\"");
                        }
                        else if (mode == "Rotate" || mode == "Scale")
                        {
                            PromptStringOptions psoBase = new PromptStringOptions("\nPodaj Punkt Bazowy (X,Y,Z): ");
                            psoBase.AllowSpaces = false;
                            argsList.Add($"\"BasePoint\": \"({ed.GetString(psoBase).StringResult})\"");

                            if (mode == "Rotate")
                            {
                                PromptStringOptions psoAngle = new PromptStringOptions("\nPodaj kąt obrotu (w stopniach): ");
                                argsList.Add($"\"Angle\": {ed.GetString(psoAngle).StringResult.Replace(",", ".")}");
                            }
                            else if (mode == "Scale")
                            {
                                PromptStringOptions psoScale = new PromptStringOptions("\nPodaj mnożnik Skali (np. 0.5 lub 2): ");
                                argsList.Add($"\"Factor\": {ed.GetString(psoScale).StringResult.Replace(",", ".")}");
                            }
                        }

                        finalTag = $"[ACTION:MODIFY_GEOMETRY {{{string.Join(", ", argsList)}}}]";
                    }


                    // --- [SET_PROPERTIES] ---
                    else if (pr.StringResult == "SETProps")
                    {
                        // 1. Ładowanie bazy wiedzy 
                        // --- NOWOŚĆ: WYBÓR BAZY API W LOCIE ---
                        PromptKeywordOptions pkoBaza = new PromptKeywordOptions("\nZ jakiej bazy wiedzy załadować podpowiedzi? [Quick/Full]: ");
                        pkoBaza.Keywords.Add("Quick");
                        pkoBaza.Keywords.Add("Full");
                        pkoBaza.Keywords.Default = "Quick";
                        string trybBazy = ed.GetKeywords(pkoBaza).StringResult;

                        string nazwaBazy = (trybBazy == "Full") ? "BricsCAD_API_V22.txt" : "BricsCAD_API_Quick.txt";

                        string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        string folderDir = System.IO.Path.GetDirectoryName(assemblyPath);
                        string filePath = System.IO.Path.Combine(folderDir, nazwaBazy);
                        // ----------------------------------------
                        System.Collections.Generic.Dictionary<string, string> bazyDict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        if (System.IO.File.Exists(filePath))
                        {
                            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                            var matchesClasses = System.Text.RegularExpressions.Regex.Matches(content, @"\b([a-zA-Z0-9_]+)\|(.*?)(?=\b[a-zA-Z0-9_]+\||$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                            foreach (System.Text.RegularExpressions.Match m in matchesClasses)
                            {
                                string entName = m.Groups[1].Value.Trim();
                                if (entName.Contains(" ") || entName.Length > 35) continue;

                                if (entName.Equals("dbtext", StringComparison.OrdinalIgnoreCase)) entName = "DBText";
                                else if (entName.Equals("mtext", StringComparison.OrdinalIgnoreCase)) entName = "MText";
                                else if (entName.Equals("solid3d", StringComparison.OrdinalIgnoreCase)) entName = "Solid3d";
                                else entName = char.ToUpper(entName[0]) + entName.Substring(1).ToLower();

                                // Wczytujemy wszystko jak leci!
                                bazyDict[entName] = m.Groups[2].Value.Trim();
                            }
                        }

                        // 2. Pętla dodawania modyfikatorów
                        System.Collections.Generic.List<string> propsList = new System.Collections.Generic.List<string>();
                        bool dodawajKolejnePropsy = true;
                        int licznikPropsow = 1;

                        ed.WriteMessage("\n\n--- KREATOR ZMIANY WŁAŚCIWOŚCI ---");
                        while (dodawajKolejnePropsy)
                        {
                            // --- NOWOŚĆ: Pytamy o klasę W KAŻDYM OBROCIE PĘTLI ---
                            string entType = "Entity";
                            if (bazyDict.Count > 0)
                            {
                                PromptKeywordOptions pkoEnt = new PromptKeywordOptions($"\nZ jakiej klasy API wyświetlić podpowiedzi dla właściwości nr {licznikPropsow}? (ENTER dla Entity, ESC by ZAKOŃCZYĆ)");
                                pkoEnt.AllowNone = true;

                                ed.WriteMessage("\n\n--- DOSTĘPNE OBIEKTY W BAZIE WIEDZY ---");

                                // --- NOWOŚĆ: SORTOWANIE ALFABETYCZNE ---
                                System.Collections.Generic.List<string> posortowaneKlucze = new System.Collections.Generic.List<string>(bazyDict.Keys);
                                posortowaneKlucze.Sort(); // C# automatycznie sortuje stringi od A do Z
                                                          // ---------------------------------------

                                string listaObiektow = "\n";
                                int kolumna = 0;

                                // Zmieniamy pętlę, by iterowała po POSORTOWANEJ liście
                                foreach (var key in posortowaneKlucze)
                                {
                                    try
                                    {
                                        pkoEnt.Keywords.Add(key, key.ToUpper(), key.ToUpper());

                                        // Magia formatowania: dopełnia spacjami do 26 znaków, żeby zrobić równą tabelę
                                        listaObiektow += string.Format("{0,-26}", key);
                                        kolumna++;

                                        // Kiedy mamy 3 elementy w wierszu, przechodzimy do nowej linijki
                                        if (kolumna >= 3)
                                        {
                                            listaObiektow += "\n";
                                            kolumna = 0;
                                        }
                                    }
                                    catch { }
                                }

                                try { pkoEnt.Keywords.Default = "Entity"; } catch { }

                                // Wyświetlamy zbudowaną, posortowaną tabelę
                                ed.WriteMessage($"\n{listaObiektow}\n---------------------------------------");

                                PromptResult prEnt = ed.GetKeywords(pkoEnt);

                                if (prEnt.Status == PromptStatus.Cancel) break; // Wciśnięto ESC -> kończymy dodawanie
                                if (prEnt.Status == PromptStatus.OK && !string.IsNullOrEmpty(prEnt.StringResult))
                                    entType = prEnt.StringResult;
                            }

                            string propName = "";
                            System.Collections.Generic.Dictionary<string, string> propertiesMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                            if (bazyDict.Count > 0)
                            {
                                string pelnyOpis = "";
                                if (bazyDict.ContainsKey("Entity")) pelnyOpis += bazyDict["Entity"] + " ";
                                string glownaKlasa = entType.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                                if (bazyDict.ContainsKey(glownaKlasa) && !glownaKlasa.Equals("Entity", StringComparison.OrdinalIgnoreCase))
                                    pelnyOpis += bazyDict[glownaKlasa];

                                // Jeśli użytkownik wybrał potężną bazę V22
                                if (nazwaBazy == "BricsCAD_API_V22.txt")
                                {
                                    System.Text.RegularExpressions.MatchCollection matchesFull = System.Text.RegularExpressions.Regex.Matches(pelnyOpis, @"Właściwości \(Properties\):\s*(.*?)(?=\.\s*[A-Z]|\.$|$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    foreach (System.Text.RegularExpressions.Match mFull in matchesFull)
                                    {
                                        string[] props = mFull.Groups[1].Value.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                                        foreach (string p in props)
                                        {
                                            string cleanProp = p.Trim().Split(new char[] { ' ', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)[0];
                                            if (cleanProp.Length > 1 && char.IsUpper(cleanProp[0]))
                                            {
                                                propertiesMap[cleanProp] = "V22_API";
                                            }
                                        }
                                    }
                                }
                                else // Jeśli to stara, dobra baza Quick z nawiasami
                                {
                                    System.Text.RegularExpressions.MatchCollection matchesQuick = System.Text.RegularExpressions.Regex.Matches(pelnyOpis, @"\b([A-Z][A-Za-z0-9_]+)\s*\(([^)]+)\)");
                                    foreach (System.Text.RegularExpressions.Match m in matchesQuick) propertiesMap[m.Groups[1].Value] = m.Groups[2].Value;
                                }

                                if (propertiesMap.Count > 0)
                                {
                                    PromptKeywordOptions pkoProp = new PromptKeywordOptions($"\nWybierz Wlasciwosc nr {licznikPropsow} (ENTER by zakonczyc dodawanie)");
                                    pkoProp.AllowNone = true;
                                    ed.WriteMessage($"\n\n--- WŁAŚCIWOŚCI DLA: {glownaKlasa.ToUpper()} ---");

                                    // --- NOWOŚĆ: SORTOWANIE WŁAŚCIWOŚCI ---
                                    System.Collections.Generic.List<string> posortowaneWlasciwosci = new System.Collections.Generic.List<string>(propertiesMap.Keys);
                                    posortowaneWlasciwosci.Sort();

                                    foreach (var klucz in posortowaneWlasciwosci)
                                    {
                                        // UWAGA: Ukrywamy opcje "Visual", bo nie można nadpisać wirtualnej właściwości!
                                        if (klucz.StartsWith("Visual", StringComparison.OrdinalIgnoreCase)) continue;

                                        ed.WriteMessage($"\n [{klucz}] - {propertiesMap[klucz]}");
                                        try { pkoProp.Keywords.Add(klucz, klucz.ToUpper(), klucz.ToUpper()); } catch { }
                                    }

                                    // --- NOWOŚĆ: Dodajemy opcję wpisania własnej właściwości z kropką ---
                                    ed.WriteMessage($"\n\n [WLASNA] - Wpisz ręcznie (np. z kropką Position.Z)");
                                    try { pkoProp.Keywords.Add("WLASNA", "WLASNA", "WLASNA"); } catch { }

                                    ed.WriteMessage("\n-------------------------------------------");
                                    PromptResult prProp = ed.GetKeywords(pkoProp);

                                    if (prProp.Status == PromptStatus.Cancel) break; // ESC w drugim kroku też kończy
                                    if (prProp.Status == PromptStatus.OK && !string.IsNullOrEmpty(prProp.StringResult))
                                    {
                                        if (prProp.StringResult == "WLASNA")
                                        {
                                            PromptStringOptions psoWlasna = new PromptStringOptions($"\nWpisz ręcznie nazwę właściwości (np. Position.Z): ");
                                            psoWlasna.AllowSpaces = false;
                                            propName = ed.GetString(psoWlasna).StringResult;
                                        }
                                        else
                                        {
                                            propName = prProp.StringResult;
                                        }
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(propName) && propertiesMap.Count == 0)
                            {
                                PromptStringOptions psoName = new PromptStringOptions($"\nPodaj nazwę właściwości nr {licznikPropsow} (np. Layer, Color) lub ENTER by zakończyć: ");
                                psoName.AllowSpaces = false;
                                propName = ed.GetString(psoName).StringResult;
                            }

                            if (string.IsNullOrEmpty(propName)) break; // Pusty wpis = koniec dodawania

                            PromptKeywordOptions pkoOp = new PromptKeywordOptions($"\nWybierz Działanie dla {propName} [Zmien/Dodaj/Odejmij/Pomnoz/RPN]: ");
                            pkoOp.Keywords.Add("Zmien"); pkoOp.Keywords.Add("Dodaj"); pkoOp.Keywords.Add("Odejmij"); pkoOp.Keywords.Add("Pomnoz"); pkoOp.Keywords.Add("RPN");
                            pkoOp.Keywords.Default = "Zmien";
                            string opWord = ed.GetKeywords(pkoOp).StringResult;

                            string opSign = "=";
                            if (opWord == "Dodaj") opSign = "+"; if (opWord == "Odejmij") opSign = "-"; if (opWord == "Pomnoz") opSign = "*"; if (opWord == "RPN") opSign = "RPN";

                            // --- DODANA ŚCIĄGA RPN ---
                            if (opSign == "RPN")
                            {
                                ed.WriteMessage("\n\n--- ŚCIĄGA KALKULATORA RPN (G50) ---");
                                ed.WriteMessage("\n [STOS]   SWAP (zamień), DUP (kopiuj), DROP (usuń)");
                                ed.WriteMessage("\n [TEKST]  CONCAT (Łączy dwa elementy w jeden. (np. 'A' 'B' CONCAT -> 'AB'), REPLACE (Zamienia fragment tekstu. Wymaga 3 elementów: [Tekst_główny, Co_szukać, Na_co_zamienić]. (np. 'Woda' 'Nowa' REPLACE, SUBSTR (Wycina tekst. Wymaga 3 elementów: [Tekst_główny, Indeks_startu, Długość)");
                                ed.WriteMessage("\n [TEKST]  UPPER/LOWER (wielkość), TRIM (Usuwa przypadkowe spacje na początku i końcu tekstu.), LEN (Zwraca liczbę znaków w tekście)");
                                ed.WriteMessage("\n [TEKST]  FIND (Szuka fragmentu i zwraca jego pozycję (liczbę). Jeśli nie znajdzie, zwraca -1), SPLIT (Dzieli tekst po separatorze i wyciąga konkretny kawałek. Wymaga 3 elementów: [Tekst, Separator, Indeks]. (np. dla nazwy A_B_C, komenda '_' 1 SPLIT zwróci B)");
                                ed.WriteMessage("\n [MATEMA] +, -, *, /, ^, SQRT, SIN, COS, ROUND");
                                ed.WriteMessage("\n [LOGIKA] ==, !=, >, < (Zwracają 1 lub 0)");
                                ed.WriteMessage("\n [WARUNEK] IFTE (Stos musi wyglądać tak: [Warunek] [Gdy_Prawda] [Gdy_Fałsz] IFTE)");
                                ed.WriteMessage("\n UWAGA: Własne teksty wpisuj w apostrofach, np. 'prefiks_' !");
                                ed.WriteMessage("\n------------------------------------\n");
                            }
                            // -------------------------

                            string promptText = opSign == "RPN" ? $"\nPodaj wyrażenie RPN (Obecna wartość jest na dnie stosu! Np: 2 * 50 +): " : $"\nPodaj wartość dla [{propName}]: ";
                            PromptStringOptions psoVal = new PromptStringOptions(promptText);
                            psoVal.AllowSpaces = true; // Konieczne dla wyrażeń RPN!
                            string valStr = ed.GetString(psoVal).StringResult;

                            if (opSign != "RPN" && !double.TryParse(valStr.Replace(",", "."), out _) && valStr.ToLower() != "true" && valStr.ToLower() != "false") valStr = $"\"{valStr}\"";
                            else if (opSign == "RPN") valStr = $"\"{valStr}\""; // RPN musi być jako string w JSON

                            if (opSign == "=") propsList.Add($"{{\"Property\": \"{propName}\", \"Value\": {valStr}}}");
                            else propsList.Add($"{{\"Property\": \"{propName}\", \"Operator\": \"{opSign}\", \"Value\": {valStr}}}");
                            licznikPropsow++;
                        }

                        if (propsList.Count > 0)
                        {
                            string polaczonePropsy = string.Join(", ", propsList);
                            finalTag = $"[ACTION:SET_PROPERTIES {{\"Properties\": [{polaczonePropsy}]}}]";
                        }
                    }

                    // --- [EDIT_BLOCK] ---
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
                        // --- NOWOŚĆ: Pytanie o usuwanie wymiarów ---
                        PromptKeywordOptions pkoRemoveDim = new PromptKeywordOptions("\nCzy usunąć wszystkie wymiary (Dimension) z wnętrza bloku? [Tak/Nie]: ");
                        pkoRemoveDim.Keywords.Add("Tak");
                        pkoRemoveDim.Keywords.Add("Nie");
                        pkoRemoveDim.Keywords.Default = "Nie";
                        if (ed.GetKeywords(pkoRemoveDim).StringResult == "Tak")
                        {
                            argsList.Add("\"RemoveDimensions\": true");
                        }

                        finalTag = $"[ACTION:EDIT_BLOCK {{{string.Join(", ", argsList)}}}]";
                    }
                    // --- [GET_PROPERTIES_LITE] ---
                    else if (pr.StringResult == "GetPropsLite")
                    {
                        finalTag = "[ACTION:GET_PROPERTIES_LITE]";
                    }
                    // --- [GET_PROPERTIES FULL] ---
                    else if (pr.StringResult == "FULLGETProps")
                    {
                        finalTag = "[ACTION:GET_PROPERTIES]";
                    }

                    // --- [LIST_BLOCKS] ---
                    else if (pr.StringResult == "ListBlocks")
                    {
                        finalTag = "[ACTION:LIST_BLOCKS]";
                    }

                    // --- [ADD_ANNO_SCALE] ---
                    else if (pr.StringResult == "AnnoScale")
                    {
                        PromptStringOptions psoScale = new PromptStringOptions("\nPodaj dokładną nazwę skali do dodania (np. 1:50, 1:100): ");
                        psoScale.AllowSpaces = true;
                        string scale = ed.GetString(psoScale).StringResult;

                        finalTag = $"[ACTION:ADD_ANNO_SCALE {{\"Scale\": \"{scale}\"}}]";
                    }

                    // --- [READ_ANNO_SCALES] ---
                    else if (pr.StringResult.Equals("READScales", StringComparison.OrdinalIgnoreCase) || pr.StringResult == "ReadScales")
                    {
                        PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nWybierz tryb odczytu [Summary/Detailed]: ");
                        pkoMode.Keywords.Add("Summary");
                        pkoMode.Keywords.Add("Detailed");
                        pkoMode.Keywords.Default = "Summary";
                        string mode = ed.GetKeywords(pkoMode).StringResult;

                        finalTag = $"[ACTION:READ_ANNO_SCALES {{\"Mode\": \"{mode}\"}}]";
                    }

                    // --- [LIST_UNIQUE] ---
                    else if (pr.StringResult.Equals("LISTUnique", StringComparison.OrdinalIgnoreCase))
                    {
                        PromptKeywordOptions pkoTarget = new PromptKeywordOptions("\nWybierz cel analizy (Target) [Class/Property]: ");
                        pkoTarget.Keywords.Add("Class");
                        pkoTarget.Keywords.Add("Property");
                        pkoTarget.Keywords.Default = "Class";
                        string target = ed.GetKeywords(pkoTarget).StringResult;

                        PromptKeywordOptions pkoScope = new PromptKeywordOptions("\nWybierz zakres przeszukiwania (Scope) [Selection/Model/Blocks]: ");
                        pkoScope.Keywords.Add("Selection");
                        pkoScope.Keywords.Add("Model");
                        pkoScope.Keywords.Add("Blocks");
                        pkoScope.Keywords.Default = "Selection";
                        string scope = ed.GetKeywords(pkoScope).StringResult;

                        if (target == "Property")
                        {
                            PromptStringOptions psoProp = new PromptStringOptions("\nPodaj nazwę właściwości do zgrupowania (np. Name, Layer, Color): ");
                            psoProp.AllowSpaces = false;
                            string prop = ed.GetString(psoProp).StringResult;
                            finalTag = $"[ACTION:LIST_UNIQUE {{\"Target\": \"Property\", \"Scope\": \"{scope}\", \"Property\": \"{prop}\"}}]";
                        }
                        else
                        {
                            finalTag = $"[ACTION:LIST_UNIQUE {{\"Target\": \"Class\", \"Scope\": \"{scope}\"}}]";
                        }
                    }

                    // --- [USER_CHOICE] (Główne menu) ---
                    else if (pr.StringResult.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                    {
                        PromptStringOptions psoQ = new PromptStringOptions("\nPodaj treść pytania dla użytkownika (np. Którą warstwę zaktualizować?): ");
                        psoQ.AllowSpaces = true;
                        string question = ed.GetString(psoQ).StringResult;

                        // --- NOWOŚĆ: Pytanie o tryb wielokrotnego wyboru ---
                        PromptKeywordOptions pkoMulti = new PromptKeywordOptions("\nCzy wybór ma być wielokrotny (Checkboxy)? [Tak/Nie]: ");
                        pkoMulti.Keywords.Add("Tak"); pkoMulti.Keywords.Add("Nie"); pkoMulti.Keywords.Default = "Nie";
                        bool isMulti = ed.GetKeywords(pkoMulti).StringResult == "Tak";
                        string multiParam = isMulti ? ", \"MultiSelect\": true" : "";

                        PromptKeywordOptions pkoMethod = new PromptKeywordOptions("\nJak chcesz wprowadzić opcje? [Recznie/Pobierz_z_rysunku]: ");
                        pkoMethod.Keywords.Add("Recznie");
                        pkoMethod.Keywords.Add("Pobierz_z_rysunku");
                        pkoMethod.Keywords.Default = "Pobierz_z_rysunku";
                        string method = ed.GetKeywords(pkoMethod).StringResult;

                        System.Collections.Generic.List<string> optionsList = new System.Collections.Generic.List<string>();

                        if (method == "Pobierz_z_rysunku")
                        {
                            PromptKeywordOptions pkoTarget = new PromptKeywordOptions("\nWybierz cel analizy (Target) [Class/Property]: ");
                            pkoTarget.Keywords.Add("Class"); pkoTarget.Keywords.Add("Property"); pkoTarget.Keywords.Default = "Class";
                            string target = ed.GetKeywords(pkoTarget).StringResult;

                            PromptKeywordOptions pkoScope = new PromptKeywordOptions("\nWybierz zakres przeszukiwania (Scope) [Selection/Model/Blocks]: ");
                            pkoScope.Keywords.Add("Selection"); pkoScope.Keywords.Add("Model"); pkoScope.Keywords.Add("Blocks");
                            pkoScope.Keywords.Default = "Selection";
                            string scope = ed.GetKeywords(pkoScope).StringResult;

                            string prop = "";
                            if (target == "Property")
                            {
                                PromptStringOptions psoProp = new PromptStringOptions("\nPodaj nazwę właściwości do pobrania (np. Name, Layer, Color): ");
                                psoProp.AllowSpaces = false;
                                prop = ed.GetString(psoProp).StringResult;

                                // Wstrzykujemy multiParam do tagu FetchProperty
                                finalTag = $"[ACTION:USER_CHOICE {{\"Question\": \"{question}\", \"FetchTarget\": \"Property\", \"FetchScope\": \"{scope}\", \"FetchProperty\": \"{prop}\"{multiParam}}}]";
                            }
                            else
                            {
                                // Wstrzykujemy multiParam do tagu FetchClass
                                finalTag = $"[ACTION:USER_CHOICE {{\"Question\": \"{question}\", \"FetchTarget\": \"Class\", \"FetchScope\": \"{scope}\"{multiParam}}}]";
                            }
                        }
                        else
                        {
                            // Stary kod - Ręczne wpisywanie lub wklejanie listy
                            bool addingOptions = true;
                            int optCount = 1;
                            ed.WriteMessage("\n[INFO]: Możesz wpisywać opcje pojedynczo LUB wkleić całą listę po przecinku!");

                            while (addingOptions)
                            {
                                PromptStringOptions psoOpt = new PromptStringOptions($"\nPodaj opcję nr {optCount} (lub wklej listę, ENTER by zakończyć): ");
                                psoOpt.AllowSpaces = true;
                                string opt = ed.GetString(psoOpt).StringResult;

                                if (string.IsNullOrEmpty(opt))
                                {
                                    addingOptions = false;
                                }
                                else
                                {
                                    if (opt.Contains(","))
                                    {
                                        string[] elementy = opt.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                        foreach (string el in elementy)
                                        {
                                            string czystyEl = el.Trim();
                                            if (!string.IsNullOrEmpty(czystyEl))
                                            {
                                                optionsList.Add($"\"{czystyEl}\"");
                                                optCount++;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        optionsList.Add($"\"{opt.Trim()}\"");
                                        optCount++;
                                    }
                                }
                            }

                            // Wstrzykujemy multiParam do klasycznego tagu z tablicą Options
                            string arrayString = string.Join(", ", optionsList);
                            finalTag = $"[ACTION:USER_CHOICE {{\"Question\": \"{question}\", \"Options\": [{arrayString}]{multiParam}}}]";
                        }
                    }

                    // --- [USER_INPUT] ---
                    else if (pr.StringResult.Equals("USERInput", StringComparison.OrdinalIgnoreCase))
                    {
                        PromptKeywordOptions pkoType = new PromptKeywordOptions("\nWybierz typ pobieranych danych [String/Point/Points]: ");
                        pkoType.Keywords.Add("String"); pkoType.Keywords.Add("Point"); pkoType.Keywords.Add("Points");
                        pkoType.Keywords.Default = "String";
                        string type = ed.GetKeywords(pkoType).StringResult;

                        PromptStringOptions psoPrompt = new PromptStringOptions("\nPodaj treść prośby do użytkownika (np. Wskaż nowy środek): ");
                        psoPrompt.AllowSpaces = true;
                        string promptMsg = ed.GetString(psoPrompt).StringResult;

                        finalTag = $"[ACTION:USER_INPUT {{\"Type\": \"{type}\", \"Prompt\": \"{promptMsg}\"}}]";
                    }

                    // --- [REMOVE_ANNO_SCALE] ---
                    else if (pr.StringResult.Equals("REMOVEScale", StringComparison.OrdinalIgnoreCase) || pr.StringResult == "RemoveScale")
                    {
                        PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nCo chcesz zrobić? [UsunKonkretna/WylaczCalkowicie]: ");
                        pkoMode.Keywords.Add("UsunKonkretna");
                        pkoMode.Keywords.Add("WylaczCalkowicie");
                        pkoMode.Keywords.Default = "UsunKonkretna";
                        string mode = ed.GetKeywords(pkoMode).StringResult;

                        if (mode == "WylaczCalkowicie")
                        {
                            finalTag = "[ACTION:REMOVE_ANNO_SCALE {\"Mode\": \"RemoveAll\"}]";
                        }
                        else
                        {
                            PromptStringOptions psoScale = new PromptStringOptions("\nPodaj dokładną nazwę skali do usunięcia (np. 1:50): ");
                            psoScale.AllowSpaces = true;
                            string scale = ed.GetString(psoScale).StringResult;
                            finalTag = $"[ACTION:REMOVE_ANNO_SCALE {{\"Scale\": \"{scale}\"}}]";
                        }
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

                    // --- [SEARCH_LAYERS] ---
                    else if (pr.StringResult.Equals("SEARCHLayers", StringComparison.OrdinalIgnoreCase))
                    {
                        ed.WriteMessage("\n\n--- DOSTĘPNE METODY WYSZUKIWANIA ---");
                        ed.WriteMessage("\n [CONTAINS]   - Zawiera (szukany tekst jest gdziekolwiek w nazwie)");
                        ed.WriteMessage("\n [STARTSWITH] - Zaczyna się od (szukany tekst jest na samym początku)");
                        ed.WriteMessage("\n [ENDSWITH]   - Kończy się na (szukany tekst jest na samym końcu)");
                        ed.WriteMessage("\n [EQUALS]     - Równe (dokładnie taka sama nazwa)");
                        ed.WriteMessage("\n------------------------------------");

                        PromptKeywordOptions pkoCond = new PromptKeywordOptions("\nWybierz warunek filtrowania [CONTAINS/STARTSWITH/ENDSWITH/EQUALS]: ");
                        pkoCond.Keywords.Add("CONTAINS", "CONTAINS", "CONTAINS");
                        pkoCond.Keywords.Add("STARTSWITH", "STARTSWITH", "STARTSWITH");
                        pkoCond.Keywords.Add("ENDSWITH", "ENDSWITH", "ENDSWITH");
                        pkoCond.Keywords.Add("EQUALS", "EQUALS", "EQUALS");
                        pkoCond.Keywords.Default = "CONTAINS";

                        string condition = ed.GetKeywords(pkoCond).StringResult;

                        // Formatyzujemy ładnie dla JSONa (np. "Contains")
                        if (condition == "CONTAINS") condition = "Contains";
                        else if (condition == "STARTSWITH") condition = "StartsWith";
                        else if (condition == "ENDSWITH") condition = "EndsWith";
                        else if (condition == "EQUALS") condition = "Equals";

                        PromptStringOptions psoVal = new PromptStringOptions($"\nPodaj szukany ciąg znaków (np. kanalizacja): ");
                        psoVal.AllowSpaces = true;
                        string val = ed.GetString(psoVal).StringResult;

                        finalTag = $"[ACTION:SEARCH_LAYERS {{\"Condition\": \"{condition}\", \"Value\": \"{val}\"}}]";
                    }

                    // --- [MANAGE_LAYERS] ---
                    else if (pr.StringResult.Equals("ManageLayers", StringComparison.OrdinalIgnoreCase))
                    {
                        // Używamy Wielkich Liter dla perfekcyjnego linkowania!
                        PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nWybierz tryb operacji na warstwach [CREATE/MODIFY/DELETE/MERGE/PURGE]: ");
                        pkoMode.Keywords.Add("CREATE"); pkoMode.Keywords.Add("MODIFY");
                        pkoMode.Keywords.Add("DELETE"); pkoMode.Keywords.Add("MERGE"); pkoMode.Keywords.Add("PURGE");
                        pkoMode.Keywords.Default = "MODIFY";
                        string mode = ed.GetKeywords(pkoMode).StringResult;

                        // Przywracamy ładny Capitalize dla JSONa
                        string modeJson = char.ToUpper(mode[0]) + mode.Substring(1).ToLower();

                        if (mode == "PURGE")
                        {
                            finalTag = $"[ACTION:MANAGE_LAYERS {{\"Mode\": \"Purge\"}}]";
                        }
                        else if (mode == "DELETE" || mode == "MERGE")
                        {
                            PromptKeywordOptions pkoSrc = new PromptKeywordOptions("\nJak wskazać warstwy ŹRÓDŁOWE? [Wpisz/AskUser]: ");
                            pkoSrc.Keywords.Add("Wpisz"); pkoSrc.Keywords.Add("AskUser");
                            pkoSrc.Keywords.Default = "AskUser";
                            string srcMode = ed.GetKeywords(pkoSrc).StringResult;

                            string srcJsonArray = "";
                            bool abort = false;

                            if (srcMode == "AskUser")
                            {
                                string ucTag = $"[ACTION:USER_CHOICE {{\"Question\": \"Wybierz warstwy do operacji:\", \"FetchTarget\": \"Property\", \"FetchScope\": \"Database\", \"FetchProperty\": \"Layer\", \"MultiSelect\": true}}]"; ed.WriteMessage($"\n\n[System] --- WSTRZYKIWANIE KROKU (Myślenie Agenta) ---");
                                string wynikUC = WykonywaczTagow(doc, ucTag);

                                if (wynikUC.Contains("anulował")) { abort = true; finalTag = "ABORT"; }
                                else
                                {
                                    historiaSekwencji.Add($"{{\"role\": \"assistant\", \"content\": \"{Komendy.SafeJson(ucTag)}\"}}");
                                    string sysFeedback = $"Oto dane z narzędzia:\n{wynikUC}\n\nKontynuuj zadanie. UŻYJ TAGU [ACTION:MANAGE_LAYERS ].";
                                    historiaSekwencji.Add($"{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(sysFeedback)}\"}}");

                                    string val = "";
                                    int lastColonActual = wynikUC.LastIndexOf(':');
                                    if (lastColonActual != -1) val = wynikUC.Substring(lastColonActual + 1).Trim();

                                    string[] elements = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    System.Collections.Generic.List<string> quotes = new System.Collections.Generic.List<string>();
                                    foreach (string el in elements) quotes.Add($"\"{el.Trim()}\"");
                                    srcJsonArray = string.Join(", ", quotes);
                                }
                            }
                            else
                            {
                                PromptStringOptions psoSrc = new PromptStringOptions("\nPodaj nazwy warstw po przecinku: ");
                                psoSrc.AllowSpaces = true;
                                string srcLayers = ed.GetString(psoSrc).StringResult;
                                string[] elements = srcLayers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                System.Collections.Generic.List<string> quotes = new System.Collections.Generic.List<string>();
                                foreach (string el in elements) quotes.Add($"\"{el.Trim()}\"");
                                srcJsonArray = string.Join(", ", quotes);
                            }

                            if (!abort)
                            {
                                if (mode == "MERGE")
                                {
                                    PromptKeywordOptions pkoTarget = new PromptKeywordOptions("\nWarstwa DOCELOWA [Nowa/Istniejaca/AskUser]: ");
                                    pkoTarget.Keywords.Add("Nowa"); pkoTarget.Keywords.Add("Istniejaca"); pkoTarget.Keywords.Add("AskUser");
                                    pkoTarget.Keywords.Default = "AskUser";
                                    string targetMode = ed.GetKeywords(pkoTarget).StringResult;

                                    string targetLayer = "";

                                    if (targetMode == "AskUser")
                                    {
                                        string ucTag = $"[ACTION:USER_CHOICE {{\"Question\": \"Wybierz istniejącą warstwę docelową:\", \"FetchTarget\": \"Property\", \"FetchScope\": \"Database\", \"FetchProperty\": \"Layer\"}}]"; string wynikUC = WykonywaczTagow(doc, ucTag);
                                        if (wynikUC.Contains("anulował")) { finalTag = "ABORT"; }
                                        else
                                        {
                                            historiaSekwencji.Add($"{{\"role\": \"assistant\", \"content\": \"{Komendy.SafeJson(ucTag)}\"}}");
                                            string sysFeedback = $"Oto dane z narzędzia:\n{wynikUC}\n\nKontynuuj zadanie. UŻYJ TAGU [ACTION:MANAGE_LAYERS ].";
                                            historiaSekwencji.Add($"{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(sysFeedback)}\"}}");

                                            int lastColonActual = wynikUC.LastIndexOf(':');
                                            if (lastColonActual != -1) targetLayer = wynikUC.Substring(lastColonActual + 1).Trim();
                                        }
                                    }
                                    else if (targetMode == "Nowa")
                                    {
                                        PromptStringOptions psoTarget = new PromptStringOptions("\nPodaj nazwę CAŁKIEM NOWEJ warstwy: ");
                                        targetLayer = ed.GetString(psoTarget).StringResult;
                                    }
                                    else
                                    {
                                        PromptStringOptions psoTarget = new PromptStringOptions("\nWpisz nazwę ISTNIEJĄCEJ warstwy: ");
                                        targetLayer = ed.GetString(psoTarget).StringResult;
                                    }

                                    if (finalTag != "ABORT")
                                        finalTag = $"[ACTION:MANAGE_LAYERS {{\"Mode\": \"Merge\", \"SourceLayers\": [{srcJsonArray}], \"TargetLayer\": \"{targetLayer}\"}}]";
                                }
                                else
                                {
                                    finalTag = $"[ACTION:MANAGE_LAYERS {{\"Mode\": \"Delete\", \"SourceLayers\": [{srcJsonArray}]}}]";
                                }
                            }
                        }
                        else // CREATE / MODIFY
                        {
                            string layerName = "";
                            bool abort = false;

                            if (mode == "MODIFY")
                            {
                                PromptKeywordOptions pkoLayer = new PromptKeywordOptions("\nJak wskazać warstwy do modyfikacji? [Wpisz/AskUser]: ");
                                pkoLayer.Keywords.Add("Wpisz"); pkoLayer.Keywords.Add("AskUser");
                                pkoLayer.Keywords.Default = "AskUser";
                                if (ed.GetKeywords(pkoLayer).StringResult == "AskUser")
                                {
                                    // Uruchamiamy Checkboxy dla trybu Modify!
                                    string ucTag = $"[ACTION:USER_CHOICE {{\"Question\": \"Wybierz warstwy do modyfikacji:\", \"FetchTarget\": \"Property\", \"FetchScope\": \"Database\", \"FetchProperty\": \"Layer\", \"MultiSelect\": true}}]";
                                    string wynikUC = WykonywaczTagow(doc, ucTag);
                                    if (wynikUC.Contains("anulował")) { abort = true; finalTag = "ABORT"; }
                                    else
                                    {
                                        historiaSekwencji.Add($"{{\"role\": \"assistant\", \"content\": \"{Komendy.SafeJson(ucTag)}\"}}");
                                        string sysFeedback = $"Oto dane z narzędzia:\n{wynikUC}\n\nKontynuuj zadanie. UŻYJ TAGU [ACTION:MANAGE_LAYERS ].";
                                        historiaSekwencji.Add($"{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(sysFeedback)}\"}}");

                                        int lastColonActual = wynikUC.LastIndexOf(':');
                                        if (lastColonActual != -1) layerName = wynikUC.Substring(lastColonActual + 1).Trim();
                                    }
                                }
                                else
                                {
                                    // --- NAPRAWA: Zezwolenie na spacje w nazwach warstw! ---
                                    PromptStringOptions psoMod = new PromptStringOptions("\nPodaj nazwę warstwy (możesz oddzielić przecinkami): ");
                                    psoMod.AllowSpaces = true;
                                    layerName = ed.GetString(psoMod).StringResult;
                                }
                            }
                            else // CREATE
                            {
                                PromptKeywordOptions pkoLayerC = new PromptKeywordOptions("\nJak podać nazwę nowej warstwy? [Wpisz/UserInput]: ");
                                pkoLayerC.Keywords.Add("Wpisz"); pkoLayerC.Keywords.Add("UserInput");
                                pkoLayerC.Keywords.Default = "UserInput";

                                if (ed.GetKeywords(pkoLayerC).StringResult == "UserInput")
                                {
                                    string uiTag = $"[ACTION:USER_INPUT {{\"Type\": \"String\", \"Prompt\": \"Podaj nazwę dla nowej warstwy:\"}}]";
                                    ed.WriteMessage($"\n\n[System] --- WSTRZYKIWANIE ZAPYTANIA DO UŻYTKOWNIKA ---");
                                    string wynikUI = WykonywaczTagow(doc, uiTag);

                                    if (wynikUI.Contains("anulował")) { abort = true; finalTag = "ABORT"; }
                                    else
                                    {
                                        historiaSekwencji.Add($"{{\"role\": \"assistant\", \"content\": \"{Komendy.SafeJson(uiTag)}\"}}");
                                        string sysFeedback = $"Oto dane z narzędzia:\n{wynikUI}\n\nKontynuuj zadanie. UŻYJ TAGU [ACTION:MANAGE_LAYERS ].";
                                        historiaSekwencji.Add($"{{\"role\": \"user\", \"content\": \"{Komendy.SafeJson(sysFeedback)}\"}}");

                                        int lastColonActual = wynikUI.LastIndexOf(':');
                                        if (lastColonActual != -1) layerName = wynikUI.Substring(lastColonActual + 1).Trim();
                                    }
                                }
                                else
                                {
                                    // --- NAPRAWA: Zezwolenie na spacje w nazwach nowej warstwy! ---
                                    PromptStringOptions psoCre = new PromptStringOptions("\nPodaj nazwę NOWEJ warstwy: ");
                                    psoCre.AllowSpaces = true;
                                    layerName = ed.GetString(psoCre).StringResult;
                                }
                            }

                            if (!abort)
                            {
                                System.Collections.Generic.List<string> props = new System.Collections.Generic.List<string>();
                                props.Add($"\"Mode\": \"{modeJson}\"");
                                props.Add($"\"Layer\": \"{layerName}\"");

                                bool selectingProps = true;
                                while (selectingProps)
                                {
                                    PromptKeywordOptions pkoProp = new PromptKeywordOptions("\nWybierz właściwość do ustawienia ");
                                    pkoProp.Keywords.Add("Kolor", "Kolor", "Kolor");
                                    pkoProp.Keywords.Add("Grubosc", "Grubosc", "Grubosc");
                                    pkoProp.Keywords.Add("RodzajLinii", "RodzajLinii", "RodzajLinii");
                                    pkoProp.Keywords.Add("Przezroczystosc", "Przezroczystosc", "Przezroczystosc");
                                    pkoProp.Keywords.Add("OnOff", "OnOff", "OnOff");
                                    pkoProp.Keywords.Add("Zamrozenie", "Zamrozenie", "Zamrozenie");
                                    pkoProp.Keywords.Add("Blokada", "Blokada", "Blokada");
                                    pkoProp.Keywords.Add("Nazwa", "Nazwa", "Nazwa");
                                    pkoProp.Keywords.Add("Zakoncz", "Zakoncz", "Zakoncz");
                                    pkoProp.Keywords.Default = "Zakoncz";

                                    string propChoice = ed.GetKeywords(pkoProp).StringResult;

                                    if (propChoice == "Zakoncz" || string.IsNullOrEmpty(propChoice)) { selectingProps = false; break; }

                                    if (propChoice == "Kolor")
                                    {
                                        string c = ed.GetString(new PromptStringOptions("\nPodaj kolor (0-255): ")).StringResult;
                                        if (!string.IsNullOrEmpty(c)) props.Add($"\"Color\": {c}");
                                    }
                                    else if (propChoice == "Grubosc")
                                    {
                                        string lw = ed.GetString(new PromptStringOptions("\nGrubość linii (np. 25 dla 0.25mm, -3 domyślna): ")).StringResult;
                                        if (!string.IsNullOrEmpty(lw)) props.Add($"\"LineWeight\": {lw}");
                                    }
                                    else if (propChoice == "RodzajLinii")
                                    {
                                        string lt = ed.GetString(new PromptStringOptions("\nRodzaj linii (np. Continuous, Dashed): ")).StringResult;
                                        if (!string.IsNullOrEmpty(lt)) props.Add($"\"Linetype\": \"{lt}\"");
                                    }
                                    else if (propChoice == "Przezroczystosc")
                                    {
                                        string tr = ed.GetString(new PromptStringOptions("\nPrzezroczystość (0-90): ")).StringResult;
                                        if (!string.IsNullOrEmpty(tr)) props.Add($"\"Transparency\": {tr}");
                                    }
                                    else if (propChoice == "OnOff")
                                    {
                                        string off = ed.GetKeywords(new PromptKeywordOptions("\nWyłączona (IsOff)? [True/False]: ", "True False")).StringResult;
                                        if (!string.IsNullOrEmpty(off)) props.Add($"\"IsOff\": {off.ToLower()}");
                                    }
                                    else if (propChoice == "Zamrozenie")
                                    {
                                        string fr = ed.GetKeywords(new PromptKeywordOptions("\nZamrożona (IsFrozen)? [True/False]: ", "True False")).StringResult;
                                        if (!string.IsNullOrEmpty(fr)) props.Add($"\"IsFrozen\": {fr.ToLower()}");
                                    }
                                    else if (propChoice == "Blokada")
                                    {
                                        string lo = ed.GetKeywords(new PromptKeywordOptions("\nZablokowana (IsLocked)? [True/False]: ", "True False")).StringResult;
                                        if (!string.IsNullOrEmpty(lo)) props.Add($"\"IsLocked\": {lo.ToLower()}");
                                    }
                                    else if (propChoice == "Nazwa")
                                    {
                                        PromptStringOptions psoName = new PromptStringOptions("\nNowa nazwa warstwy: ");
                                        psoName.AllowSpaces = true;
                                        string nn = ed.GetString(psoName).StringResult;
                                        if (!string.IsNullOrEmpty(nn)) props.Add($"\"NewName\": \"{nn}\"");
                                    }
                                }

                                finalTag = $"[ACTION:MANAGE_LAYERS {{{string.Join(", ", props)}}}]";
                            }
                        }
                    }

                    if (finalTag == "ABORT")
                    {
                        ed.WriteMessage("\n[System] Operacja anulowana. Wracam do wyboru tagu.");
                        continue;
                    }
                    // === WSTRZYKIWANIE WEWNĘTRZNEGO KOMENTARZA AGENTA ===
                    if (!string.IsNullOrEmpty(finalTag))
                    {
                        PromptKeywordOptions pkoCmt = new PromptKeywordOptions("\nCzy dodać wewn. Komentarz (Myśl Agenta) do tego tagu? [Tak/Nie]: ");
                        pkoCmt.Keywords.Add("Tak"); pkoCmt.Keywords.Add("Nie"); pkoCmt.Keywords.Default = "Nie";
                        if (ed.GetKeywords(pkoCmt).StringResult == "Tak")
                        {
                            PromptStringOptions psoCmt = new PromptStringOptions("\nWpisz komentarz (np. Pytam o punkt, by móc przesunąć element): ");
                            psoCmt.AllowSpaces = true;
                            string cmt = ed.GetString(psoCmt).StringResult;

                            // Wyszukuje ostatni znak zamykający JSON-a w wygenerowanym tagu i "wpycha" tam komentarz
                            int lastBrace = finalTag.LastIndexOf('}');
                            if (lastBrace != -1)
                            {
                                finalTag = finalTag.Insert(lastBrace, $", \"Comment\": \"{cmt}\"");
                            }
                        }
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
                // ==========================================
                // WSTRZYKNIĘCIE PAMIĘCI AGENTA (ZMIENNE @)
                // ==========================================
                wklejonyTag = AgentMemory.InjectVariables(wklejonyTag);

                // ==========================================
                // NOWOŚĆ: BEZPOŚREDNIA OBSŁUGA LISP
                // ==========================================
                if (wklejonyTag.StartsWith("[LISP:"))
                {
                    int startIdx = wklejonyTag.IndexOf("[LISP:") + 6;
                    int endIdx = wklejonyTag.LastIndexOf("]");
                    if (endIdx > startIdx)
                    {
                        string lispCode = wklejonyTag.Substring(startIdx, endIdx - startIdx).Trim();

                        // Przesyłamy wyczyszczony skrypt do wiersza poleceń BricsCADa
                        doc.SendStringToExecute(lispCode + " ", true, false, false);

                        return "Wysłano skrypt LISP do wewnętrznego silnika BricsCAD.";
                    }
                    return "BŁĄD: Niepoprawny format tagu [LISP: ]";
                }
                // ==========================================

                if (wklejonyTag.Contains("[SELECT:"))
                {
                    // Tu odwołujemy się do metody z klasy Komendy:
                    int wynik = Komendy.WykonajInteligentneZaznaczenie(doc, wklejonyTag);
                    return $"Pomyślnie zaznaczono {wynik} obiekt(ów).";
                }

                // --- NOWOŚĆ: Reakcja na tag tworzenia obiektów ---
                else if (wklejonyTag.Contains("[ACTION:CREATE_OBJECT"))
                {
                    CreateObjectTool tool = new CreateObjectTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:CREATE_BLOCK"))
                {
                    CreateBlockTool tool = new CreateBlockTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:INSERT_BLOCK"))
                {
                    InsertBlockTool tool = new InsertBlockTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:SET_PROPERTIES"))
                {
                    SetPropertiesTool tool = new SetPropertiesTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:ADD_ANNO_SCALE"))
                {
                    AddAnnoScaleTool tool = new AddAnnoScaleTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:READ_ANNO_SCALES"))
                {
                    ReadAnnoScalesTool tool = new ReadAnnoScalesTool();
                    return tool.Execute(doc, wklejonyTag);
                }
                else if (wklejonyTag.Contains("[ACTION:REMOVE_ANNO_SCALE"))
                {
                    RemoveAnnoScaleTool tool = new RemoveAnnoScaleTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:READ_PROPERTY"))
                {
                    ReadPropertyTool tool = new ReadPropertyTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:USER_CHOICE"))
                {
                    UserChoiceTool tool = new UserChoiceTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:EDIT_BLOCK"))
                {
                    EditBlockTool tool = new EditBlockTool();
                    return tool.Execute(doc, wklejonyTag);
                }
                else if (wklejonyTag.Contains("[ACTION:GET_PROPERTIES_LITE]"))
                {
                    GetPropertiesToolLite tool = new GetPropertiesToolLite();
                    return tool.Execute(doc, wklejonyTag);
                }
                else if (wklejonyTag.Contains("[ACTION:GET_PROPERTIES]"))
                {
                    GetPropertiesTool tool = new GetPropertiesTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:LIST_BLOCKS]"))
                {
                    ListBlocksTool tool = new ListBlocksTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:LIST_UNIQUE"))
                {
                    ListUniqueTool tool = new ListUniqueTool();
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

                else if (wklejonyTag.Contains("[ACTION:MODIFY_GEOMETRY"))
                {
                    ModifyGeometryTool tool = new ModifyGeometryTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:USER_INPUT"))
                {
                    UserInputTool tool = new UserInputTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:TEXT_EDIT"))
                {
                    TextEditTool tool = new TextEditTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:MANAGE_LAYERS"))
                {
                    ManageLayersTool tool = new ManageLayersTool();
                    return tool.Execute(doc, wklejonyTag);
                }

                else if (wklejonyTag.Contains("[ACTION:SEARCH_LAYERS"))
                {
                    SearchLayersTool tool = new SearchLayersTool();
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