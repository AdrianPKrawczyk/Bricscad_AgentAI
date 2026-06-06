using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.Core
{
    public class ToolSettings
    {
        public bool IsCore { get; set; }
        public string Tags { get; set; } // Rozdzielane przecinkami, np. "#bloki, #architektura"
        public bool SupportsEarlyExit { get; set; }
    }

    public class AgentProfileConfig
    {
        public string SystemPromptFile { get; set; }
        public List<string> AllowedTools { get; set; } = new List<string>();
        public List<string> AllowedTags { get; set; } = new List<string>();
    }

    public class ToolConfigRoot
    {
        public Dictionary<string, ToolSettings> Tools { get; set; } = new Dictionary<string, ToolSettings>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, AgentProfileConfig> Profiles { get; set; } = new Dictionary<string, AgentProfileConfig>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Zarządza dynamiczną konfiguracją narzędzi (IsCore, Tagi) zapisaną w JSON.
    /// Zapobiega twardemu kodowaniu tagów wewnątrz klas IToolV2.
    /// </summary>
    public static class ToolConfigManager
    {
        private static ToolConfigRoot _config = new ToolConfigRoot();
        private static string _configPath;

        public static HashSet<string> SessionDynamicTags { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string ConfigPath
        {
            get
            {
                if (_configPath == null)
                {
                    _configPath = Path.Combine(
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                        "tools_config.json"
                    );
                }
                return _configPath;
            }
        }

        /// <summary>
        /// Inicjalizuje konfigurację. Jeśli plik nie istnieje, generuje domyślny 
        /// na podstawie zarejestrowanych narzędzi.
        /// </summary>
        public static void Initialize(IEnumerable<IToolV2> registeredTools)
        {
            EnsureSupervisorPromptFile();
            EnsureMathPromptFile();
            EnsureNotesPromptFile();

            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    // Próba deserializacji do nowego formatu
                    var root = JsonConvert.DeserializeObject<ToolConfigRoot>(json);
                    if (root != null && root.Tools != null)
                    {
                        _config = root;
                    }
                    else
                    {
                        // Fallback dla starego formatu
                        var oldSettings = JsonConvert.DeserializeObject<Dictionary<string, ToolSettings>>(json);
                        if (oldSettings != null)
                        {
                            _config.Tools = oldSettings;
                        }
                    }
                    
                    // Uzupełnij o ewentualne nowe narzędzia, których nie ma w JSON
                    SyncWithTools(registeredTools);
                }
                catch
                {
                    GenerateDefaultConfig(registeredTools);
                }
            }
            else
            {
                GenerateDefaultConfig(registeredTools);
            }
        }

        private static void EnsureSupervisorPromptFile()
        {
            try
            {
                string supervisorPromptPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "system_prompt_supervisor.txt"
                );
                bool needsWrite = !File.Exists(supervisorPromptPath);
                if (!needsWrite)
                {
                    try
                    {
                        string currentText = File.ReadAllText(supervisorPromptPath);
                        if (!currentText.Contains("CadGeometryProfile") || 
                            !currentText.Contains("LUŹNA ROZMOWA") || 
                            !currentText.Contains("CadMathProfile") ||
                            !currentText.Contains("Profile NIE są narzędziami") ||
                            !currentText.Contains("OBLICZENIA MATEMATYCZNE, FIZYCZNE") ||
                            !currentText.Contains("NotesProfile"))
                        {
                            needsWrite = true; // Auto-upgrade starych wersji promptu
                        }
                    }
                    catch { }
                }

                if (needsWrite)
                {
                    string defaultSupervisorPrompt = 
                        "Jesteś uniwersalnym Asystentem i Głównym Menedżerem (Supervisorem) systemu Bielik V2 w BricsCAD.\n" +
                        "Twoim zadaniem jest pomoc użytkownikowi – zarówno w zadaniach CAD, jak i w ogólnych pytaniach i obliczeniach.\n\n" +
                        "ZASADY OBSŁUGI ZAPYTAŃ:\n" +
                        "1. LUŹNA ROZMOWA I WIEDZA OGÓLNA (np. \"kto był pierwszym królem Polski?\", \"jak się czujesz?\", pytania o ogólną historię, geografię, literaturę):\n" +
                        "   - Odpowiedz na nie bezpośrednio, zwięźle i przyjaźnie w zwykłym tekście. Nie używaj żadnych narzędzi ani DelegateTask.\n" +
                        "2. OBLICZENIA MATEMATYCZNE, FIZYCZNE I PRZELICZANIE JEDNOSTEK (np. \"policz energię kinetyczną kuli...\", \"jaka jest objętość rury...\", \"przelicz 10 cali na mm\"):\n" +
                        "   - Nie wykonuj ich samodzielnie w swoim oknie kontekstowym, aby zapobiec czeskim błędom i niedokładnościom.\n" +
                        "   - MUSISZ natychmiast wydelegować to zadanie do profilu `CadMathProfile` przy użyciu narzędzia DelegateTask.\n" +
                        "   - Przed wywołaniem DelegateTask nie pisz żadnego tekstu objaśniającego ani zapowiadającego.\n" +
                        "3. ZADANIA CAD / OPERACJE NA RYSUNKU (np. rysowanie, zaznaczanie, zmiana kolorów, warstw, odczyt atrybutów lub XData):\n" +
                        "   - Nie wykonuj ich samodzielnie. MUSISZ natychmiast wydelegować zadanie do odpowiedniego eksperta za pomocą narzędzia DelegateTask.\n" +
                        "   - Przed wywołaniem DelegateTask nie pisz żadnego tekstu objaśniającego ani zapowiadającego.\n" +
                        "   - Po zakończeniu pracy przez eksperta przedstaw krótko i rzeczowo wynik użytkownikowi.\n\n" +
                        "UWAGA KRYTYCZNA: Profile NIE są narzędziami! Nigdy nie wywołuj nazwy profilu (np. CadMathProfile, CadGeometryProfile) jako nazwy funkcji w tool_calls. Jedynym narzędziem do delegowania jest DelegateTask, w którym podajesz TargetProfile jako parametr. Wywołanie profilu bezpośrednio jako funkcji spowoduje błąd krytyczny i nie zostanie wykonane!\n\n" +
                        "Dostępne profile ekspertów do zadań (wybierz najbardziej optymalny):\n" +
                        "- CadGeometryProfile: ekspert od tworzenia i modyfikacji geometrii (linie, polilinie, kreskowania, warstwy, wymiary, teksty, właściwości obiektów, np. kolory, grubość linii).\n" +
                        "- CadBlocksProfile: ekspert od bloków i atrybutów (tworzenie bloków, wstawianie, listowanie, edycja atrybutów bloku).\n" +
                        "- CadMetadataProfile: ekspert od analityki rysunku, pomiarów, XData (czytanie właściwości, metadane XData, wyszukiwanie w rysunku, inspekcja obiektów, zrzuty ekranu CAD).\n" +
                        "- CadMathProfile: ekspert od obliczeń matematycznych, fizycznych, konwersji jednostek i analizy wymiarowej rysunku.\n" +
                        "- NotesProfile: system przechowuje notatki inżynierskie dla każdego rysunku w plikach [Nazwa].ai_note.md. Masz do dyspozycji wyspecjalizowanego sub-agenta 'NotesProfile'. Jeśli użytkownik wyraźnie prosi Cię o zapisanie czegoś w notatce, ZAWSZE używaj narzędzia DelegateTask przekazując mu to zadanie, lub poinformuj użytkownika o możliwości użycia komendy /notatka.\n" +
                        "- CadProfile: uniwersalny profil awaryjny (używaj tylko jeśli zadanie łączy wiele z powyższych dziedzin w jeden ciąg).";
                    File.WriteAllText(supervisorPromptPath, defaultSupervisorPrompt, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas generowania system_prompt_supervisor.txt: {ex.Message}");
            }
        }

        private static void EnsureMathPromptFile()
        {
            try
            {
                string mathPromptPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "system_prompt_math.txt"
                );
                bool needsWrite = !File.Exists(mathPromptPath);
                if (!needsWrite)
                {
                    try
                    {
                        string currentText = File.ReadAllText(mathPromptPath);
                        if (!currentText.Contains("WZORY I PRZYKŁADY RPN") || 
                            !currentText.Contains("Częsty błąd przy ułamkach") || 
                            !currentText.Contains("UNIKAJ DANGLED STACK") ||
                            !currentText.Contains("Wyrażenie RPN z konwersją do cm3") ||
                            !currentText.Contains("ExecuteFormula"))
                        {
                            needsWrite = true; // Auto-upgrade starych wersji promptu
                        }
                    }
                    catch { }
                }

                if (needsWrite)
                {
                    string defaultMathPrompt =
                        "Jesteś ekspert-analitykiem i kalkulatorem systemu Bielik V2 (CadMathProfile).\n" +
                        "Twoim zadaniem jest wykonywanie precyzyjnych obliczeń inżynieryjnych, fizycznych i geometrycznych.\n\n" +
                        "Masz do dyspozycji DWA GŁÓWNE PODEJŚCIA do obliczeń:\n" +
                        "PODEJŚCIE A: Baza Wiedzy (ExecuteFormula)\n" +
                        "- Jeśli użytkownik prosi o użycie lub uruchomienie konkretnej 'formuły' (np. 'Cisnienie_Hydrostatyczne'), ZAWSZE używaj narzędzia `ExecuteFormula`.\n" +
                        "- Nie przeliczaj tego ręcznie ani nie używaj narzędzia RPN w tym przypadku.\n\n" +
                        "PODEJŚCIE B: Kalkulator RPN (CalculateRpn / CalculateMath)\n" +
                        "- Używaj, gdy nie ma gotowej formuły.\n" +
                        "ZASADY NOTACJI ALGEBRAICZNEJ Z JEDNOSTKAMI DLA RPN:\n" +
                        "1. Zapisuj wyrażenia w sposób naturalny, zawsze oddzielając operatory spacjami, np. `( 10_cm / 2 ) ^ 2`.\n" +
                        "2. Format zapisu wartości: `wartość_jednostka` (np. `100_mm`, `10_m`, `5_cm`, `11.34_g/cm3`, `5.94_kg`). Znak '_' łączy wartość z jednostką.\n" +
                        "   - KRYTYCZNE: ZAWSZE dodawaj jednostkę nawet do podstawowych danych wejściowych! Jeśli napiszesz `100` zamiast `100_mm`, system błędnie założy, że to metry!\n" +
                        "3. Podstawowe operatory: `+`, `-`, `*`, `/`, `^`.\n" +
                        "   - Zawsze pamiętaj o kolejności działań i nawiasach.\n" +
                        "   - Aby podnieść do kwadratu, użyj np. `50_mm ^ 2`.\n" +
                        "4. Stałe:\n" +
                        "   - `#PI` (pi wynosi ok. 3.141592)\n" +
                        "   - `#G` (przyspieszenie ziemskie wynosi ok. 9.81_m/s2)\n" +
                        "5. Konwersja jednostek:\n" +
                        "   - Używaj parametru `TargetUnit` w narzędziu `CalculateMath`, np. `TargetUnit='cm3'`, zamiast wpisywać konwersje ręcznie.\n\n" +
                        "STRATEGIA ROZWIĄZYWANIA ZADAŃ:\n" +
                        "- ZABRANIA SIĘ wykonywania złożonych obliczeń we własnej pamięci LLM, aby zapobiec czeskim błędom. Zamiast tego ZAWSZE używaj narzędzia CalculateMath.\n" +
                        "- Dziel duże zadania na pojedyncze, logiczne kroki (osobne wywołania narzędzia CalculateMath) zamiast tworzyć jedno ogromne, skomplikowane wyrażenie.\n" +
                        "- Zapisuj cząstkowe wyniki przy użyciu parametru `SaveAs` (np. `SaveAs='Promien'`, `SaveAs='Masa'`), a potem odwołuj się do nich w kolejnych krokach za pomocą `@zmienna` (np. `@Promien ^ 2 * #PI`).\n\n" +
                        "WZORY I PRZYKŁADY:\n\n" +
                        "1. Pole koła (P = pi * r^2 dla d = 100 mm):\n" +
                        "   - Krok 1 (promień): `100_mm / 2` (zapisz jako `Promien` -> SaveAs='Promien')\n" +
                        "   - Krok 2 (pole w mm2): `@Promien ^ 2 * #PI` (TargetUnit='mm2' -> SaveAs='Pole')\n\n" +
                        "2. Objętość kuli (V = 4/3 * pi * r^3 dla średnicy 10 cm => r = 5 cm):\n" +
                        "   - `( 4 / 3 ) * #PI * ( 5_cm ^ 3 )` (TargetUnit='cm3')\n\n" +
                        "3. Masa kuli z ołowiu (gęstość = 11.34 g/cm3, V = 523.6 cm3):\n" +
                        "   - `523.598776_cm3 * 11.34_g/cm3` (TargetUnit='kg' -> SaveAs='MasaKg')\n\n" +
                        "4. Energia kinetyczna/potencjalna (Ek = Ep = mgh dla m = 5.94 kg i h = 10 m):\n" +
                        "   - `5.937609_kg * #G * 10_m` (jednostka J zostanie przypisana automatycznie!)\n\n" +
                        "5. Objętość rury/walca (V = pi * r^2 * h dla wewn. 10 cm i dł. 11 m):\n" +
                        "   - Krok 1 (promień): `10_cm / 2` (zapisz jako `Promien` -> SaveAs='Promien')\n" +
                        "   - Krok 2 (objętość w litrach): `@Promien ^ 2 * #PI * 11_m` (TargetUnit='L' -> SaveAs='ObjetoscLitry')";
                    File.WriteAllText(mathPromptPath, defaultMathPrompt, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas generowania system_prompt_math.txt: {ex.Message}");
            }
        }

        private static void EnsureNotesPromptFile()
        {
            try
            {
                string notesPromptPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "system_prompt_notes.txt"
                );
                if (!File.Exists(notesPromptPath))
                {
                    string defaultNotesPrompt = 
                        "Jesteś Sub-Agentem ds. Notatek Projektowych (NotesProfile).\n" +
                        "Twoim jedynym zadaniem jest generowanie i zwracanie czystej treści notatki w formacie Markdown na podstawie poleceń użytkownika.\n" +
                        "Nie używaj żadnych narzędzi poza wygenerowaniem tekstu i przekazaniem go jako końcowy wynik. Twoja odpowiedź nadpisze plik [Nazwa].ai_note.md w głównym systemie.";
                    File.WriteAllText(notesPromptPath, defaultNotesPrompt, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas generowania system_prompt_notes.txt: {ex.Message}");
            }
        }

        private static void SyncWithTools(IEnumerable<IToolV2> registeredTools)
        {
            bool changed = false;
            foreach (var tool in registeredTools)
            {
                string name = tool.GetToolSchema()?.Function?.Name ?? tool.GetType().Name;
                if (!_config.Tools.ContainsKey(name))
                {
                    _config.Tools[name] = new ToolSettings { IsCore = false, Tags = "", SupportsEarlyExit = false };
                    changed = true;
                }
            }

            if (_config.Profiles == null)
            {
                _config.Profiles = new Dictionary<string, AgentProfileConfig>(StringComparer.OrdinalIgnoreCase);
                changed = true;
            }

            // 1. Zabezpieczenie/Synchronizacja SupervisorProfile
            if (!_config.Profiles.TryGetValue("SupervisorProfile", out var supervisorProf))
            {
                supervisorProf = new AgentProfileConfig { SystemPromptFile = "system_prompt_supervisor.txt" };
                _config.Profiles["SupervisorProfile"] = supervisorProf;
                changed = true;
            }
            var supervisorDefaults = new List<string> { "UserInput", "UserChoice", "ReadFromBlackboard", "WriteToBlackboard", "DelegateTask", "SearchKnowledgeBase", "SavePermanentFormula", "SaveMacro", "ExecuteFormula", "ExecuteMacro", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset", "ImportCsvDataset", "ManageDataset", "ReadProjectFile", "WriteProjectFile" };
            if (supervisorProf.AllowedTools == null)
            {
                supervisorProf.AllowedTools = new List<string>();
                changed = true;
            }
            foreach (var tool in supervisorDefaults)
            {
                if (!supervisorProf.AllowedTools.Contains(tool))
                {
                    supervisorProf.AllowedTools.Add(tool);
                    changed = true;
                }
            }

            // 2. Zabezpieczenie/Synchronizacja CadProfile
            if (!_config.Profiles.TryGetValue("CadProfile", out var cadProf))
            {
                cadProf = new AgentProfileConfig { SystemPromptFile = "system_prompt.txt", AllowedTags = new List<string> { "#cad", "#wymiary", "#xdata" } };
                _config.Profiles["CadProfile"] = cadProf;
                changed = true;
            }
            if (cadProf.SystemPromptFile != "system_prompt.txt")
            {
                cadProf.SystemPromptFile = "system_prompt.txt";
                changed = true;
            }
            var cadDefaults = new List<string> 
            { 
                "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", 
                "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice",
                "DimensionEditTool", "ExecuteMacro", "ReadPropertyTool", "InspectEntity", "GetPropertiesTool",
                "AnalyzeSelectionTool", "ReadTextSampleTool", "TextEditTool", "ManageAnnoScales", "EditBlock",
                "EditAttributes", "ListBlocks", "InsertBlock", "CreateBlock", "ReadXData", "WriteXData",
                "FindXData", "CaptureVisionArea", "SearchKnowledgeBase", "SaveMacro", "ExecuteFormula", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset"
            };
            if (cadProf.AllowedTools == null)
            {
                cadProf.AllowedTools = new List<string>();
                changed = true;
            }
            foreach (var tool in cadDefaults)
            {
                if (!cadProf.AllowedTools.Contains(tool))
                {
                    cadProf.AllowedTools.Add(tool);
                    changed = true;
                }
            }

            // 3. Zabezpieczenie/Synchronizacja CadGeometryProfile
            if (!_config.Profiles.TryGetValue("CadGeometryProfile", out var geomProf))
            {
                geomProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = "system_prompt.txt", 
                    AllowedTools = new List<string> { "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "DimensionEditTool", "TextEditTool" },
                    AllowedTags = new List<string> { "#cad" }
                };
                _config.Profiles["CadGeometryProfile"] = geomProf;
                changed = true;
            }

            // 4. Zabezpieczenie/Synchronizacja CadBlocksProfile
            if (!_config.Profiles.TryGetValue("CadBlocksProfile", out var blocksProf))
            {
                blocksProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = "system_prompt.txt", 
                    AllowedTools = new List<string> { "ListBlocks", "InsertBlock", "CreateBlock", "EditBlock", "EditAttributes", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice" },
                    AllowedTags = new List<string> { "#bloki" }
                };
                _config.Profiles["CadBlocksProfile"] = blocksProf;
                changed = true;
            }

            // 5. Zabezpieczenie/Synchronizacja CadMetadataProfile
            if (!_config.Profiles.TryGetValue("CadMetadataProfile", out var metadataProf))
            {
                metadataProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = "system_prompt.txt", 
                    AllowedTools = new List<string> { "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool", "ReadTextSampleTool", "ReadXData", "WriteXData", "FindXData", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "CaptureVisionArea" },
                    AllowedTags = new List<string> { "#xdata" }
                };
                _config.Profiles["CadMetadataProfile"] = metadataProf;
                changed = true;
            }

            // 6. Zabezpieczenie/Synchronizacja CadMathProfile
            if (!_config.Profiles.TryGetValue("CadMathProfile", out var mathProf))
            {
                mathProf = new AgentProfileConfig { SystemPromptFile = "system_prompt_math.txt", AllowedTags = new List<string> { "#math", "#obliczenia" } };
                _config.Profiles["CadMathProfile"] = mathProf;
                changed = true;
            }
            if (mathProf.SystemPromptFile != "system_prompt_math.txt")
            {
                mathProf.SystemPromptFile = "system_prompt_math.txt";
                changed = true;
            }
            var mathDefaults = new List<string> { "CalculateMath", "CalculateRpn", "ReadFromBlackboard", "WriteToBlackboard", "UserInput", "UserChoice", "SearchKnowledgeBase", "ExecuteFormula", "SavePermanentFormula", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset" };
            if (mathProf.AllowedTools == null)
            {
                mathProf.AllowedTools = new List<string>();
                changed = true;
            }
            foreach (var tool in mathDefaults)
            {
                if (!mathProf.AllowedTools.Contains(tool))
                {
                    mathProf.AllowedTools.Add(tool);
                    changed = true;
                }
            }

            // 7. Zabezpieczenie/Synchronizacja NotesProfile
            if (!_config.Profiles.TryGetValue("NotesProfile", out var notesProf))
            {
                notesProf = new AgentProfileConfig { SystemPromptFile = "system_prompt_notes.txt" };
                _config.Profiles["NotesProfile"] = notesProf;
                changed = true;
            }

            if (changed) SaveConfig();
        }

        private static void GenerateDefaultConfig(IEnumerable<IToolV2> registeredTools)
        {
            _config = new ToolConfigRoot();
            var coreTools = new[] { "CreateObject", "SelectEntities", "ModifyProperties", "Foreach", "RequestAdditionalTools", "UserInput", "UserChoice", "WriteToBlackboard", "ReadFromBlackboard" };
            var earlyExitTools = new[] { "CreateObject", "ModifyProperties", "ManageLayers", "InsertBlock", "CreateBlock", "ExecuteMacro" };

            foreach (var tool in registeredTools)
            {
                var schema = tool.GetToolSchema();
                if (schema == null || schema.Function == null) continue;
                string apiName = schema.Function.Name;

                _config.Tools[apiName] = new ToolSettings
                {
                    IsCore = coreTools.Contains(apiName, StringComparer.OrdinalIgnoreCase),
                    SupportsEarlyExit = earlyExitTools.Contains(apiName, StringComparer.OrdinalIgnoreCase),
                    Tags = ""
                };
            }
            
            _config.Profiles["SupervisorProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt_supervisor.txt",
                AllowedTools = new List<string> { "UserInput", "UserChoice", "ReadFromBlackboard", "WriteToBlackboard", "DelegateTask", "SearchKnowledgeBase", "SavePermanentFormula", "SaveMacro", "ExecuteFormula", "ExecuteMacro", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset", "ImportCsvDataset", "ManageDataset", "ReadProjectFile", "WriteProjectFile" },
                AllowedTags = new List<string>()
            };
            
            _config.Profiles["CadProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt.txt",
                AllowedTools = new List<string> 
                { 
                    "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", 
                    "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice",
                    "DimensionEditTool", "ExecuteMacro", "ReadPropertyTool", "InspectEntity", "GetPropertiesTool",
                    "AnalyzeSelectionTool", "ReadTextSampleTool", "TextEditTool", "ManageAnnoScales", "EditBlock",
                    "EditAttributes", "ListBlocks", "InsertBlock", "CreateBlock", "ReadXData", "WriteXData",
                    "FindXData", "CaptureVisionArea", "SearchKnowledgeBase", "SaveMacro", "ExecuteFormula", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset"
                },
                AllowedTags = new List<string> { "#cad", "#wymiary", "#xdata" }
            };

            _config.Profiles["CadGeometryProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt.txt",
                AllowedTools = new List<string> { "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "DimensionEditTool", "TextEditTool" },
                AllowedTags = new List<string> { "#cad" }
            };

            _config.Profiles["CadBlocksProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt.txt",
                AllowedTools = new List<string> { "ListBlocks", "InsertBlock", "CreateBlock", "EditBlock", "EditAttributes", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice" },
                AllowedTags = new List<string> { "#bloki" }
            };

            _config.Profiles["CadMetadataProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt.txt",
                AllowedTools = new List<string> { "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool", "ReadTextSampleTool", "ReadXData", "WriteXData", "FindXData", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "CaptureVisionArea" },
                AllowedTags = new List<string> { "#xdata" }
            };

            _config.Profiles["CadMathProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt_math.txt",
                AllowedTools = new List<string> { "CalculateMath", "ReadFromBlackboard", "WriteToBlackboard", "UserInput", "UserChoice", "SearchKnowledgeBase", "ExecuteFormula", "SavePermanentFormula", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset" },
                AllowedTags = new List<string> { "#math", "#obliczenia" }
            };

            _config.Profiles["NotesProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt_notes.txt",
                AllowedTools = new List<string>(),
                AllowedTags = new List<string>()
            };

            // BEZWZGLĘDNY ZAPIS PO WYGENEROWANIU
            SaveConfig();
        }

        public static void SaveConfig()
        {
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }

        public static Dictionary<string, ToolSettings> GetAllSettings() => _config.Tools;

        public static Dictionary<string, AgentProfileConfig> GetProfiles() => _config.Profiles;

        public static void UpdateSettings(Dictionary<string, ToolSettings> newSettings)
        {
            _config.Tools = newSettings;
            SaveConfig();
        }

        public static void UpdateAgentProfile(string profileName, string promptFile, List<string> allowedTools)
        {
            if (_config.Profiles.TryGetValue(profileName, out var profile))
            {
                profile.SystemPromptFile = promptFile;
                profile.AllowedTools = allowedTools;
                SaveConfig();
            }
        }

        /// <summary>
        /// Sprawdza, czy narzędzie o podanej nazwie klasy powinno być aktywne 
        /// dla zestawu żądanych tagów.
        /// </summary>
        public static bool IsToolActive(string apiName, IEnumerable<string> requestedTags)
        {
            if (SessionDynamicTags.Contains(apiName)) return true;
            if (requestedTags != null && requestedTags.Any(rt => SessionDynamicTags.Contains(rt))) return true;

            if (!_config.Tools.TryGetValue(apiName, out var s)) return false;

            // Narzędzia Core są ZAWSZE aktywne
            if (s.IsCore) return true;

            // Jeśli to nie Core, a użytkownik poprosił o #all, ładuj wszystko
            if (requestedTags != null && requestedTags.Any(rt => rt.Equals("#all", StringComparison.OrdinalIgnoreCase))) return true;

            // Logika filtrowania tagów dla Tool Pools
            if (requestedTags == null || !requestedTags.Any()) return false;

            // ZMIANA: Aktywacja bezpośrednio po nazwie narzędzia (fallback dla braku tagów)
            if (requestedTags.Any(rt => rt.Equals(apiName, StringComparison.OrdinalIgnoreCase))) return true;
            var toolTags = s.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToLower());
            return requestedTags.Any(rt => toolTags.Contains(rt.ToLower()));
        }

        /// <summary>
        /// Zwraca listę unikalnych tagów (spoza core) dostępnych w systemie.
        /// </summary>
        public static IEnumerable<string> GetAvailableCategories()
        {
            return _config.Tools.Values
                .SelectMany(s => s.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Zwraca listę narzędzi przypisanych do danej kategorii.
        /// </summary>
        public static IEnumerable<string> GetToolsInCategory(string category)
        {
            return _config.Tools
                .Where(kv => kv.Value.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Any(t => t.Trim().Equals(category, StringComparison.OrdinalIgnoreCase)))
                .Select(kv => kv.Key);
        }

        public static ToolSettings GetSettings(string toolClassName)
        {
            if (_config.Tools.TryGetValue(toolClassName, out var s)) return s;
            return null;
        }
    }
}
