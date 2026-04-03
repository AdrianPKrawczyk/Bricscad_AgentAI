using Bricscad.ApplicationServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    // ==========================================
    // 1. STRUKTURY DANYCH Z PLIKU JSON
    // ==========================================
    public class BenchmarkConfig
    {
        public RunMetadata RunMetadata { get; set; }
        public List<BenchmarkTest> Tests { get; set; }
    }

    public class RunMetadata
    {
        public string ModelName { get; set; }
        public string RunDate { get; set; }
        public JObject LMStudioConfig { get; set; }
        public double GlobalScore { get; set; }
        public Dictionary<string, double> CategoriesScores { get; set; } = new Dictionary<string, double>();
    }

    public class BenchmarkTest
    {
        public int Id { get; set; }
        public string Category { get; set; }
        public int Difficulty { get; set; }
        public string TestName { get; set; }
        public string Description { get; set; }
        public string UserPrompt { get; set; }
        public List<string> SimulatedCADResponses { get; set; } = new List<string>();
        public List<ValidationRule> ValidationRules { get; set; } = new List<ValidationRule>();

        // Zmienne wynikowe (wypełniane podczas testu)
        [JsonIgnore] public bool Passed { get; set; }
        public string GeneratedTag { get; set; }
        [JsonIgnore] public List<string> FailedRulesErrors { get; set; } = new List<string>();
    }

    public class ValidationRule
    {
        public string RuleType { get; set; }
        public string Value { get; set; }
        public Dictionary<string, string> MockData { get; set; }
        public string ExpectedOutput { get; set; }
        public string ErrorMessage { get; set; }
    }

    // ==========================================
    // 2. GŁÓWNY SILNIK BENCHMARKU
    // ==========================================
    public class AutoBenchmarkEngine
    {
        public async Task UruchomBenchmarkAsync(string sciezkaDoJson)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\n[AutoBenchmark]: Rozpoczynam wieloetapowy test inteligencji...");

            string jsonContent = File.ReadAllText(sciezkaDoJson);
            BenchmarkConfig config = JsonConvert.DeserializeObject<BenchmarkConfig>(jsonContent);

            int passedCount = 0;

            foreach (var test in config.Tests)
            {
                // --- 1. RESET PRZED KAŻDYM TESTEM ---
                Komendy.historiaRozmowy.Clear();
                string calyLogOdpowiedziAI = "";

                // --- 2. PIERWSZE PYTANIE (USER PROMPT) ---
                string aiResponse = await SymulujPytanieDoModelu(test.UserPrompt);
                calyLogOdpowiedziAI = aiResponse;

                // --- 3. SYMULACJA KOLEJNYCH ETAPÓW (CAD RESPONSES) ---
                // Jeśli w JSON są odpowiedzi typu "WYNIK: ...", wysyłamy je po kolei
                if (test.SimulatedCADResponses != null && test.SimulatedCADResponses.Count > 0)
                {
                    foreach (var cadMsg in test.SimulatedCADResponses)
                    {
                        doc.Editor.WriteMessage($"\n   [Symulacja CAD]: {cadMsg}");

                        // Wysyłamy odpowiedź z CAD jako kolejną wiadomość od użytkownika
                        string kolejnaReakcjaAI = await SymulujPytanieDoModelu(cadMsg);

                        // Łączymy wszystkie odpowiedzi modelu, aby sędzia mógł sprawdzić całą sekwencję tagów
                        calyLogOdpowiedziAI += " | " + kolejnaReakcjaAI;
                    }
                }

                // --- 4. WALIDACJA CAŁEGO ŁAŃCUCHA ---
                test.GeneratedTag = calyLogOdpowiedziAI;
                test.Passed = WalidujOdpowiedz(test, calyLogOdpowiedziAI);

                if (test.Passed) passedCount++;
                else doc.Editor.WriteMessage($"\n[AutoBenchmark] OBLANO Test {test.Id}: {test.TestName}");
            }

            // --- 5. RAPORTOWANIE ---
            config.RunMetadata.GlobalScore = Math.Round(((double)passedCount / config.Tests.Count) * 100, 2);
            string raportPath = sciezkaDoJson.Replace(".json", $"_RAPORT_{DateTime.Now:yyyyMMdd_HHmm}.json");
            File.WriteAllText(raportPath, JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented));

            doc.Editor.WriteMessage($"\n[AutoBenchmark]: Zakończono! Skuteczność: {config.RunMetadata.GlobalScore}%");
        }

        // ==========================================
        // 3. LOGIKA EWALUATORA (AUTO-SĘDZIA)
        // ==========================================
        private bool WalidujOdpowiedz(BenchmarkTest test, string aiResponse)
        {
            bool isPassed = true;

            foreach (var rule in test.ValidationRules)
            {
                bool rulePassed = true;

                switch (rule.RuleType)
                {
                    case "MustContain":
                        if (!aiResponse.Contains(rule.Value)) rulePassed = false;
                        break;

                    case "MustNotContain":
                        if (aiResponse.Contains(rule.Value)) rulePassed = false;
                        break;

                    case "RegexMatch":
                        if (!Regex.IsMatch(aiResponse, rule.Value)) rulePassed = false;
                        break;

                    case "MaxLength":
                        if (int.TryParse(rule.Value, out int maxLen) && aiResponse.Length > maxLen) rulePassed = false;
                        break;

                    case "SequenceMatch":
                        var tagi = rule.Value.Split(',');
                        int currentIndex = -1;
                        foreach (var tag in tagi)
                        {
                            int foundIndex = aiResponse.IndexOf(tag.Trim(), currentIndex + 1);
                            if (foundIndex == -1 || foundIndex <= currentIndex)
                            {
                                rulePassed = false;
                                break;
                            }
                            currentIndex = foundIndex;
                        }
                        break;

                    case "ValidJsonInsideTag":
                        var match = Regex.Match(aiResponse, @"\[ACTION:[^\{]*(\{.*\})\]", RegexOptions.Singleline);
                        if (match.Success)
                        {
                            try
                            {
                                JObject.Parse(match.Groups[1].Value);
                            }
                            catch
                            {
                                rulePassed = false;
                            }
                        }
                        else rulePassed = false;
                        break;

                    case "EvaluateRPN":
                        // Wyciągnięcie formuły z odpowiedzi (szuka tekstu po 'RPN:' aż do zamykającego cudzysłowu)
                        var rpnMatch = Regex.Match(aiResponse, @"RPN:\s*(.*?)(?=\"")");
                        if (rpnMatch.Success)
                        {
                            string wyluskaneRPN = rpnMatch.Groups[1].Value;

                            // 1. Podmiana MockData (symulacja danych z CAD)
                            if (rule.MockData != null)
                            {
                                foreach (var kvp in rule.MockData)
                                {
                                    wyluskaneRPN = wyluskaneRPN.Replace(kvp.Key, kvp.Value);
                                }
                            }

                            try
                            {
                                // 2. Czyścimy stos, żeby testy nie zakłócały się nawzajem
                                RpnCalculator.ClearStack();

                                // 3. Przepuszczamy wygenerowany wzór przez Twój silnik RPN!
                                string calculated = RpnCalculator.Evaluate(wyluskaneRPN);

                                // 4. Sprawdzamy, czy wynik matematyczny pokrywa się z oczekiwaniami
                                if (calculated != rule.ExpectedOutput)
                                {
                                    rulePassed = false;
                                    rule.ErrorMessage = $"Błąd obliczeń. Oczekiwano '{rule.ExpectedOutput}', a silnik AI/RPN wygenerował '{calculated}'. Wzór po podstawieniu: {wyluskaneRPN}";
                                }
                            }
                            catch (Exception ex)
                            {
                                rulePassed = false;
                                rule.ErrorMessage = $"Silnik RPN odrzucił składnię AI. Wyrzucono błąd: {ex.Message}. Wzór po podstawieniu: {wyluskaneRPN}";
                            }
                        }
                        else
                        {
                            rulePassed = false;
                            rule.ErrorMessage = "Nie znaleziono wzoru zaczynającego się od 'RPN:' wewnątrz tagu.";
                        }
                        break;
                }

                if (!rulePassed)
                {
                    isPassed = false;
                    test.FailedRulesErrors.Add(rule.ErrorMessage);
                }
            }

            return isPassed;
        }

        private async Task<string> SymulujPytanieDoModelu(string prompt)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage($"\n   [Wysyłam zapytanie do AI]: {prompt}");

            // Tutaj wywołujemy Twoją główną funkcję z AgentCommand
            string aiResponse = await Komendy.ZapytajAgentaAsync(prompt, doc);

            // Zwracamy czysty tekst odpowiedzi modelu
            return aiResponse;
        }
    }
}