using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Teigha.Runtime;

namespace BricsCAD_Agent
{
    public class AgentStartupAndCLI : IExtensionApplication
    {
        public void Initialize()
        {
            Task.Run(() => RozgrzejModelAsync());
        }

        public void Terminate() { }

        private async Task RozgrzejModelAsync()
        {
            try
            {
                await Task.Delay(2000);

                Document doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage("\n[Bielik]: Rozpoczynam ciche ładowanie modelu AI do pamięci karty graficznej...");

                using (var client = new HttpClient())
                {
                    string payload = "{\"model\": \"qwen2.5-coder-14b\", \"messages\": [{\"role\": \"user\", \"content\": \"Rozgrzewka\"}], \"max_tokens\": 2}";
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    await client.PostAsync("http://127.0.0.1:1234/v1/chat/completions", content);
                }

                doc?.Editor.WriteMessage("\n[Bielik]: Model AI jest rozgrzany i gotowy do błyskawicznej pracy!");
            }
            catch { }
        }

        // ==============================================================
        // DODANO FLAGĘ "Transparent" - pozwala na wywołanie polecenia 
        // wewnątrz innego za pomocą apostrofu: 'AI
        // ==============================================================
        [CommandMethod("AI", CommandFlags.Transparent)]
        public async void CommandAI()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptStringOptions pso = new PromptStringOptions("\n[Bielik] Wpisz zapytanie: ");
            pso.AllowSpaces = true;
            PromptResult pr = ed.GetString(pso);

            if (pr.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(pr.StringResult))
            {
                string prompt = pr.StringResult.Trim();

                // Jeśli okno jest zamknięte, otwieramy je bezpośrednio przez C# (omijając wstrzykiwanie tekstu do konsoli CAD!)
                if (AgentControl.Instance == null)
                {
                    try
                    {
                        // Bezpośrednie wywołanie funkcji z pliku AgentCommand.cs
                        new Komendy().UruchomInterfejsAgenta();
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n[Błąd ładowania okna]: {ex.Message}");
                    }

                    // Czekaj max 3 sekundy aż okienko się załaduje do pamięci
                    int retries = 0;
                    while (AgentControl.Instance == null && retries < 30)
                    {
                        await Task.Delay(100);
                        retries++;
                    }
                }

                // Jeśli okno już jest (lub właśnie się otworzyło), wstrzykujemy pytanie
                if (AgentControl.Instance != null)
                {
                    AgentControl.Instance.WyslijZapytanieZKonsoli(prompt);
                }
                else
                {
                    ed.WriteMessage("\n[Błąd]: Okno Agenta nie otworzyło się na czas.");
                }
            }
        }

        // ==============================================================
        // INTERAKTYWNY KALKULATOR RPN (WSTRZYKUJĄCY)
        // ==============================================================
        [CommandMethod("RPN", CommandFlags.Transparent)]
        public void CommandRPN()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n\n--- INTERAKTYWNY KALKULATOR RPN ---");
            ed.WriteMessage("\nWpisuj liczby/operatory i zatwierdzaj [ENTER]. Zostaną na stosie.");
            ed.WriteMessage("\nWciśnij pusty [ENTER] lub wpisz '=', aby WSTRZYKNĄĆ wynik i zamknąć.");
            ed.WriteMessage("\n-----------------------------------\n");

            // --- NOWOŚĆ: Próba wczytania stosu przypisanego do tego pliku ---
            RpnCalculator.LoadFromDWG(doc);
            while (true)
            {
                if (RpnCalculator.AutoPreview)
                {
                    ed.WriteMessage("\n========================");
                    ed.WriteMessage("\n" + RpnCalculator.GetHPStackView(6));
                    ed.WriteMessage("\n========================");
                }

                PromptStringOptions pso = new PromptStringOptions("\n[RPN] Wejście (lub '=' / [ENTER] by zakończyć, '?' pomoc): ");
                pso.AllowSpaces = true;
                PromptResult pr = ed.GetString(pso);

                // Reakcja na ESC (Anulowanie)
                if (pr.Status == PromptStatus.Cancel)
                {
                    ed.WriteMessage("\n[Kalkulator]: Przerwano działanie (ESC).");
                    return;
                }

                string input = pr.StringResult?.Trim() ?? "";

                // --- NOWOŚĆ: Wywoływanie pomocy ---
                if (input == "?")
                {
                    WypiszSciageRpn(ed);
                    continue; // Pomija obliczenia i wraca na początek pętli
                }

                // Wyjście z pętli (Zatwierdzenie)
                if (input == "=" || input == "")
                {
                    break;
                }

                try
                {
                    // Obliczamy wyrażenie w locie (wymaga 4 argumentów z Etapu 2)
                    RpnCalculator.Evaluate(input, null, null, ed);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[Błąd RPN]: {ex.Message}");
                }
            }

            // --- NOWOŚĆ: Po zatwierdzeniu Enterem (wyjściu z pętli), robimy "cichy zapis" całego stosu na dysk ---
            RpnCalculator.SaveToDWG(doc);

            // PO WYJŚCIU Z PĘTLI -> Wstrzykiwanie wyniku na ekran
            string result = RpnCalculator.GetTopAsString();
            if (!string.IsNullOrEmpty(result))
            {
                result = result.Replace(",", ".");
                ed.WriteMessage($"\n>> Wstrzyknięto wartość: {result} <<\n");
                doc.SendStringToExecute(result + "\n", true, false, false);
            }
            else
            {
                ed.WriteMessage("\n[Kalkulator]: Stos pusty, brak wartości do wstrzyknięcia.\n");
            }
        }

        // ==============================================================
        // INTERAKTYWNY KALKULATOR (TYLKO DO ODCZYTU)
        // ==============================================================
        [CommandMethod("CALC", CommandFlags.Transparent)]
        public void CommandCalc()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n\n--- INTERAKTYWNY KALKULATOR (ODCZYT) ---");
            ed.WriteMessage("\nWpisuj liczby/operatory. Wciśnij pusty [ENTER] by po prostu wyjść.");

            // --- NOWOŚĆ: Próba wczytania stosu przypisanego do tego pliku ---
            RpnCalculator.LoadFromDWG(doc);

            while (true)
            {
                if (RpnCalculator.AutoPreview)
                {
                    ed.WriteMessage("\n========================");
                    ed.WriteMessage("\n" + RpnCalculator.GetHPStackView(6));
                    ed.WriteMessage("\n========================");
                }

                PromptStringOptions pso = new PromptStringOptions("\n[RPN] Wejście (lub '=' / [ENTER] by zakończyć, '?' pomoc): ");
                pso.AllowSpaces = true;
                PromptResult pr = ed.GetString(pso);

                // Reakcja na ESC (Anulowanie)
                if (pr.Status == PromptStatus.Cancel)
                {
                    ed.WriteMessage("\n[Kalkulator]: Przerwano działanie (ESC).");
                    return;
                }

                string input = pr.StringResult?.Trim() ?? "";

                // --- NOWOŚĆ: Wywoływanie pomocy ---
                if (input == "?")
                {
                    WypiszSciageRpn(ed);
                    continue; // Pomija obliczenia i wraca na początek pętli
                }

                // Wyjście z pętli (Zatwierdzenie)
                if (input == "=" || input == "")
                {
                    break;
                }

                try
                {
                    // Obliczamy wyrażenie w locie (wymaga 4 argumentów z Etapu 2)
                    RpnCalculator.Evaluate(input, null, null, ed);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[Błąd RPN]: {ex.Message}");
                }
            }

            // --- NOWOŚĆ: Po zatwierdzeniu Enterem (wyjściu z pętli), robimy "cichy zapis" całego stosu na dysk ---
            RpnCalculator.SaveToDWG(doc);

            ed.WriteMessage($"\n--- Konic pracy. Wynik na szczycie: {RpnCalculator.GetTopAsString()} ---\n");
        }

        // ==============================================================
        // ZARZĄDZANIE PAMIĘCIĄ STOSU RPN (ETAP 1)
        // ==============================================================

        [CommandMethod("STOS", CommandFlags.Transparent)]
        public void CommandStos()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n\n--- AKTUALNY STAN STOSU RPN ---");
            ed.WriteMessage("\n" + RpnCalculator.GetStackState());
            ed.WriteMessage("-------------------------------\n");
        }

        [CommandMethod("STOS_CLEAR", CommandFlags.Transparent)]
        public void CommandStosClear()
        {
            RpnCalculator.ClearStack();
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n[Kalkulator]: Stos został całkowicie wyczyszczony.\n");
        }

        [CommandMethod("STOS_AUTO", CommandFlags.Transparent)]
        public void CommandStosAuto()
        {
            RpnCalculator.AutoPreview = !RpnCalculator.AutoPreview;
            string status = RpnCalculator.AutoPreview ? "WŁĄCZONY" : "WYŁĄCZONY";
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[Kalkulator]: Automatyczny podgląd stosu jest teraz {status}.\n");
        }

        // ==============================================================
        // EKRAN POMOCY KALKULATORA RPN (ROZBUDOWANY O FUNKCJE TEKSTOWE)
        // ==============================================================
        private void WypiszSciageRpn(Editor ed)
        {
            ed.WriteMessage("\n\n=======================================================");
            ed.WriteMessage("\n                 ŚCIĄGA KALKULATORA RPN                  ");
            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n [GEOMETRIA] DL (dystans 3D), DX, DY, DZ (odległości w osiach)");
            ed.WriteMessage("\n             -> Wstrzymują kalkulator i proszą o kliknięcie");
            ed.WriteMessage("\n [STOS]      SWAP (zamień 1 z 2), DUP (kopiuj szczyt)");
            ed.WriteMessage("\n             DROP (usuń szczyt), CLEAR (czyść stos)");
            ed.WriteMessage("\n             PICK (kopiuj n-ty element, np. 3 PICK)");
            ed.WriteMessage("\n [MATEMA]    +, -, *, /, ^, SQRT, SIN, COS, ROUND, ABS");
            ed.WriteMessage("\n [LOGIKA]    ==, !=, >, <, IFTE (Warunek Prawda Falsz IFTE)");
            ed.WriteMessage("\n [TEKSTOWE]  Szczegółowy opis operacji na ciągach znaków:");
            ed.WriteMessage("\n   CONCAT / &  : Łączy 2 teksty (np. 'A' 'B' & -> 'AB')");
            ed.WriteMessage("\n   NUM_ADD     : [Tekst] [Wartość] NUM_ADD (np. 'DN50' 20 NUM_ADD -> 'DN70')");
            ed.WriteMessage("\n   UPPER/LOWER : Zmienia wielkość liter ('abc' UPPER -> 'ABC')");
            ed.WriteMessage("\n   TRIM        : Usuwa białe znaki z początku i końca tekstu");
            ed.WriteMessage("\n   LEN         : Zwraca długość tekstu (ilość znaków)");
            ed.WriteMessage("\n   REPLACE     : [Tekst_bazowy] [Co_szukać] [Na_co_zamienić] REPLACE");
            ed.WriteMessage("\n   SUBSTR      : [Tekst_bazowy] [Indeks_startu] [Długość] SUBSTR");
            ed.WriteMessage("\n                 (Pamiętaj: indeksowanie zaczyna się od 0!)");
            ed.WriteMessage("\n   FIND        : [Tekst_bazowy] [Szukany_fragment] FIND");
            ed.WriteMessage("\n                 (Zwraca pozycję startową lub -1 gdy nie znajdzie)");
            ed.WriteMessage("\n   SPLIT       : [Tekst_bazowy] [Separator] [Indeks_wyniku] SPLIT");
            ed.WriteMessage("\n                 (np. 'A_B_C' '_' 1 SPLIT -> zwróci 'B')");
            ed.WriteMessage("\n [JEDNOSTKI] Możesz wpisywać liczby z wymiarami SI.");
            ed.WriteMessage("\n             Format: '10_m', '50_kg', '20_kPa', '15_cm2'.");
            ed.WriteMessage("\n             Kalkulator sam przelicza mm na metry, a N * m na J.");
            ed.WriteMessage("\n   CONVE  : [Wartość] [Nowa_Jednostka] CONVE (np. 10_m 'cm' CONVE)");
            ed.WriteMessage("\n   UNBASE : [Wartość] UNBASE (Konwertuje do bazowego wektora SI)");
            ed.WriteMessage("\n   UVAL   : [Wartość] UVAL (Usuwa jednostkę, zostawia samą liczbę)");
            ed.WriteMessage("\n   +UNIT  : [Liczba] [Jednostka] +UNIT (Dodaje wymiar do czystej liczby)");
            ed.WriteMessage("\n   UFACT  : Podobne do CONVE, ujednolica zapis do docelowej struktury");
            ed.WriteMessage("\n [ZMIENNE]   Wymagają znaku $ na początku (np. $MASA, $V1)!");
            ed.WriteMessage("\n             Zapis:   15_m '$DLUGOSC' STO (lub 'DLUGOSC' STO)");
            ed.WriteMessage("\n             Odczyt:  $DLUGOSC (wpisanie nazwy z dolarem)");
            ed.WriteMessage("\n             Czyszcz: VARS_CLEAR (usuwa pamięć zmiennych rysunku)");
            ed.WriteMessage("\n [STAŁE]     Poprzedzaj znakiem # : #PI, #G (grawitacja), #C, #R_GAS");
            ed.WriteMessage("\n [INFO]      Wpisz '=' lub wciśnij pusty [ENTER], by wyjść.");
            ed.WriteMessage("\n=======================================================\n");
        }

        // ==============================================================
        // BEZPOŚREDNI ODCZYT WŁAŚCIWOŚCI (Z POMINIĘCIEM AI)
        // ==============================================================
        // DODANO FLAGĘ UsePickSet - chroni zaznaczenie przed skasowaniem przez CAD po wciśnięciu Enter!
        [CommandMethod("GETPROPS", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandGetProps()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // 1. Najpierw sprawdzamy, czy użytkownik miał coś zaznaczone przed wpisaniem komendy
            PromptSelectionResult sel = ed.SelectImplied();

            // 2. Jeśli nic nie zaznaczył, CAD sam poprosi go o kliknięcie obiektów TERAZ!
            if (sel.Status != PromptStatus.OK)
            {
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\nWybierz obiekty do zbadania właściwości: ";
                sel = ed.GetSelection(pso);
            }

            // 3. Jeśli mamy zaznaczenie (przed lub po komendzie), wykonujemy narzędzie
            if (sel.Status == PromptStatus.OK && sel.Value != null)
            {
                Komendy.AktywneZaznaczenie = sel.Value.GetObjectIds();

                GetPropertiesTool tool = new GetPropertiesTool();
                string wynik = tool.Execute(doc, "{}");

                wynik = wynik.Replace("WYNIK: ", "");
                ed.WriteMessage($"\n\n--- BEZPOŚREDNI ZRZUT WŁAŚCIWOŚCI ---\n{wynik}\n-----------------------------------\n");
            }
            else
            {
                ed.WriteMessage("\n[Błąd]: Anulowano. Nie zaznaczono żadnych obiektów.");
            }
        }

        // ==============================================================
        // BEZPOŚREDNIA LISTA UNIKALNYCH BLOKÓW (Z POMINIĘCIEM AI)
        // ==============================================================
        [CommandMethod("LISTBLOCKS")]
        public void CommandListBlocks()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            ListBlocksTool tool = new ListBlocksTool();

            // ZMIANA: Zgodnie z wymogiem narzędzia, używamy scope "Database", by skanować cały plik
            string argsJson = "{\"Scope\": \"Database\"}";

            string wynik = tool.Execute(doc, argsJson);
            wynik = wynik.Replace("WYNIK: ", "");

            ed.WriteMessage($"\n\n--- UNIKALNE BLOKI W RYSUNKU ---\n{wynik}\n--------------------------------\n");
        }


        // ==============================================================
        // SZYBKIE WYKONANIE SKOPIOWANEGO TAGU (Z POMINIĘCIEM AI)
        // ==============================================================
        // Flaga UsePickSet pozwala zaznaczyć obiekty PRZED wpisaniem komendy!
        [CommandMethod("RUNTAG", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandRunTag()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // Zabezpieczenie aktywnego zaznaczenia (jeśli tag tego wymaga)
            PromptSelectionResult sel = ed.SelectImplied();
            if (sel.Status == PromptStatus.OK && sel.Value != null)
            {
                Komendy.AktywneZaznaczenie = sel.Value.GetObjectIds();
            }

            // Prosimy użytkownika o wklejenie tagu
            PromptStringOptions pso = new PromptStringOptions("\n[Agent Bielik] Wklej skopiowany tag (np. [ACTION:...]): ");
            pso.AllowSpaces = true; // Konieczne, by spacje w JSON-ie nie zadziałały jako Enter!
            PromptResult pr = ed.GetString(pso);

            if (pr.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(pr.StringResult))
            {
                string tag = pr.StringResult.Trim();

                // --- POPRAWKA: Automatyczne czyszczenie tagu ze schowka ---
                // Usuwa backslashe przed cudzysłowami (zamienia {\" na {")
                tag = tag.Replace("\\\"", "\"").Replace("\\\\", "\\");

                try
                {
                    // Przekazujemy wklejony tekst bezpośrednio do naszego głównego "rutera" narzędzi
                    string wynik = TrainingStudio.WykonywaczTagow(doc, tag);

                    // Czyścimy techniczne prefiksy dla ładniejszego wyświetlania
                    wynik = wynik.Replace("WYNIK: ", "").Replace("[INFO", "");

                    ed.WriteMessage($"\n\n--- WYNIK WYKONANIA TAGU ---\n{wynik}\n----------------------------\n");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[BŁĄD]: {ex.Message}");
                }
            }
        }
        // ==============================================================
        // BEZPOŚREDNIE OTWIERANIE PANELU CZATU
        // ==============================================================
        [CommandMethod("CZAT")]
        public void CommandCzat()
        {
            try
            {
                // Wywołujemy dokładnie tę samą funkcję, co oryginalne polecenie AGENT_UI
                new Komendy().UruchomInterfejsAgenta();
            }
            catch (System.Exception ex)
            {
                Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[Błąd ładowania okna]: {ex.Message}");
            }
        }

    }
}