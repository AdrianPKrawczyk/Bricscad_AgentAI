using System;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad.Windows;
using Teigha.Runtime;
using Bricscad_AgentAI_V2.UI;
using Bricscad_AgentAI_V2.Core;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using System.Linq;
using System.Collections.Generic;
using Bricscad_AgentAI_V2.Models;

[assembly: CommandClass(typeof(Bricscad_AgentAI_V2.Core.AgentStartup))]

namespace Bricscad_AgentAI_V2.Core
{
    public class AgentStartup : IExtensionApplication
    {
        private static PaletteSet _paletteSet = null;

        public static event Action<string, string> OnLispCallback;

        public void Initialize()
        {
            EnsureKnowledgeBaseFolders();
            CleanupVisionCache();
            try
            {
                BielikLogger.RegisterMainThread();
                BielikLogger.LogInfo("Inicjalizacja wtyczki AGENT BRICS-AI V2.0...");

                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                System.Windows.Forms.Application.ThreadException += Application_ThreadException;
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }
            catch (System.Exception ex)
            {
                try { BielikLogger.LogError("Błąd podczas rejestrowania obsługi wyjątków startupu", ex); } catch { }
            }
        }

        public void Terminate() 
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
                BielikLogger.LogInfo("Zamykanie wtyczki AGENT BRICS-AI V2.0.");
            }
            catch { }
        }

        private System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string dllName = new System.Reflection.AssemblyName(args.Name).Name + ".dll";
                string pluginDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string dllPath = System.IO.Path.Combine(pluginDir, dllName);

                if (System.IO.File.Exists(dllPath))
                {
                    return System.Reflection.Assembly.LoadFrom(dllPath);
                }
            }
            catch (System.Exception ex)
            {
                BielikLogger.LogError($"[AssemblyResolve] Błąd ładowania: {args.Name}", ex);
            }
            return null;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as System.Exception;
                BielikLogger.LogCritical("!!! KRYTYCZNY WYJĄTEK APLIKACJI (Unhandled AppDomain Exception) - BricsCAD może ulec awarii !!!", ex);
            }
            catch { }
        }

        private void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            try
            {
                BielikLogger.LogError("!!! WYJĄTEK WĄTKU UI (Unhandled UI Thread Exception) !!!", e.Exception);
            }
            catch { }
        }

        private void CleanupVisionCache()
        {
            try
            {
                string tempDir = System.IO.Path.GetTempPath();
                string[] files = System.IO.Directory.GetFiles(tempDir, "AgentVision_*.jpg");
                foreach (var file in files)
                {
                    try { System.IO.File.Delete(file); } catch { }
                }
            }
            catch { }
        }

        private void EnsureKnowledgeBaseFolders()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string basePath = System.IO.Path.Combine(appData, "Bricscad_AgentAI", "CustomKnowledge");
                string formulasPath = System.IO.Path.Combine(basePath, "Formulas");
                string macrosPath = System.IO.Path.Combine(basePath, "Macros");

                if (!System.IO.Directory.Exists(formulasPath))
                {
                    System.IO.Directory.CreateDirectory(formulasPath);
                }
                if (!System.IO.Directory.Exists(macrosPath))
                {
                    System.IO.Directory.CreateDirectory(macrosPath);
                }
            }
            catch (System.Exception ex)
            {
                try { BielikLogger.LogError("Błąd podczas tworzenia folderów CustomKnowledge", ex); } catch { }
            }
        }

        [CommandMethod("AI")]
        public void ShowAgentPanel()
        {
            try
            {
                if (_paletteSet == null)
                {
                    _paletteSet = new PaletteSet("AGENT BRICS-AI V2.0", new Guid("B2A1C4D3-F5E6-4879-9A8B-1C2D3E4F5A6B"));
                    _paletteSet.Add("Asystent", new AgentControl());
                    _paletteSet.Dock = DockSides.Left;
                    _paletteSet.Size = new System.Drawing.Size(400, 600);
                }
                _paletteSet.Visible = true;
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\nBłąd uruchamiania panelu V2: {ex.Message}");
            }
        }

        [CommandMethod("AI_V2", CommandFlags.Transparent)]
        public void QuickAiCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptStringOptions opts = new PromptStringOptions("\nZapytanie do Bielik V2: ");
            opts.AllowSpaces = true;
            PromptResult res = ed.GetString(opts);

            if (res.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(res.StringResult))
            {
                // Upewnij się, że panel jest widoczny
                ShowAgentPanel();

                // Jeśli AgentControl ma instancję statyczną, przekaż wiadomość
                if (AgentControl.Instance != null)
                {
                    _ = AgentControl.Instance.ProcessInputAsync(res.StringResult);
                }
                ed.WriteMessage($"\nWysłano zapytanie do Bielik V2: {res.StringResult}");
            }
        }

        [CommandMethod("AGENT_BENCHMARK_V2")]
        public void ShowBenchmarkPanel()
        {
            ShowAgentPanel();
            if (AgentControl.Instance != null)
            {
                AgentControl.Instance.SwitchToBenchmark();
            }
        }

        [CommandMethod("AGENT_TESTER_V2")]
        public void ShowTesterPanel()
        {
            ShowAgentPanel();
            // Tester jest czwartą zakładką (indeks 3)
            // Można rozbudować AgentControl o metodę SwitchToTester, ale na razie wystarczy otwarcie panelu.
        }

        // ==============================================================
        // CLI V2 - BEZPOŚREDNIE WYWOŁYWANIE NARZĘDZI
        // ==============================================================

        private void SyncSelectionWithMemory(Editor ed)
        {
            PromptSelectionResult selRes = ed.SelectImplied();
            if (selRes.Status == PromptStatus.OK && selRes.Value != null)
            {
                AgentMemoryState.Update(selRes.Value.GetObjectIds());
            }
        }

        [CommandMethod("AI_RUN", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandAiRun()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            SyncSelectionWithMemory(ed);

            PromptStringOptions opts = new PromptStringOptions("\n[Agent V2] Wklej JSON narzędzia (np. {\"toolName\": \"...\", \"arguments\": {...}}): ");
            opts.AllowSpaces = true;
            PromptResult res = ed.GetString(opts);

            if (res.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(res.StringResult))
            {
                try
                {
                    JObject json = JObject.Parse(res.StringResult);
                    string toolName = json["toolName"]?.ToString();
                    JObject args = json["arguments"] as JObject ?? new JObject();

                    if (string.IsNullOrEmpty(toolName))
                    {
                        ed.WriteMessage("\n[Błąd]: Brak pola 'toolName' w JSON.");
                        return;
                    }

                    string result = ToolOrchestrator.Instance.ExecuteTool(toolName, args, new CadExecutionContext(doc));
                    ed.WriteMessage($"\n\n--- WYNIK WYKONANIA AI_RUN ---\n{result}\n------------------------------\n");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[Błąd Parsowania]: {ex.Message}");
                }
            }
        }

        [CommandMethod("AI_TOOL", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandAiTool()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            SyncSelectionWithMemory(ed);

            // Wyświetl listę narzędzi
            ed.WriteMessage("\n\n--- DOSTĘPNE NARZĘDZIA AGENTA V2 ---");
            var tools = ToolOrchestrator.Instance.GetRegisteredTools().ToList();
            foreach (var tool in tools)
            {
                var schema = tool.GetToolSchema();
                ed.WriteMessage($"\n- {schema.Function.Name}: {schema.Function.Description}");
            }
            ed.WriteMessage("\n------------------------------------\n");

            PromptStringOptions nameOpts = new PromptStringOptions("\nPodaj nazwę narzędzia: ");
            PromptResult nameRes = ed.GetString(nameOpts);

            if (nameRes.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(nameRes.StringResult))
            {
                string toolName = nameRes.StringResult.Trim();

                PromptStringOptions argsOpts = new PromptStringOptions("\nPodaj argumenty JSON (domyślnie {}): ");
                argsOpts.AllowSpaces = true;
                PromptResult argsRes = ed.GetString(argsOpts);

                JObject args = new JObject();
                if (argsRes.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(argsRes.StringResult))
                {
                    try { args = JObject.Parse(argsRes.StringResult); }
                    catch (System.Exception) { ed.WriteMessage("\n[Błąd]: Niepoprawny JSON argumentów. Używam {}."); }
                }

                string result = ToolOrchestrator.Instance.ExecuteTool(toolName, args, new CadExecutionContext(doc));
                ed.WriteMessage($"\n\n--- WYNIK NARZĘDZIA {toolName} ---\n{result}\n----------------------------------\n");
            }
        }

        [CommandMethod("AI_PROPS", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandAiProps()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            SyncSelectionWithMemory(ed);

            if (AgentMemoryState.ActiveSelection.Length == 0)
            {
                ed.WriteMessage("\n[Błąd]: Najpierw zaznacz obiekty do sprawdzenia.");
                return;
            }

            JObject args = new JObject { ["Mode"] = "Full" };
            string result = ToolOrchestrator.Instance.ExecuteTool("GetPropertiesTool", args, new CadExecutionContext(doc));
            ed.WriteMessage($"\n\n--- AI_PROPS (FULL MODE) ---\n{result}\n----------------------------\n");
        }

        [CommandMethod("AI_DIM", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandAiDim()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            SyncSelectionWithMemory(ed);

            if (AgentMemoryState.ActiveSelection.Length == 0)
            {
                ed.WriteMessage("\n[Błąd]: Najpierw zaznacz wymiary do edycji.");
                return;
            }

            PromptStringOptions argsOpts = new PromptStringOptions("\nParametry edycji wymiaru JSON (np. {\"TextOverride\": \"200\"}): ");
            argsOpts.AllowSpaces = true;
            PromptResult argsRes = ed.GetString(argsOpts);

            JObject args = new JObject();
            if (argsRes.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(argsRes.StringResult))
            {
                try { args = JObject.Parse(argsRes.StringResult); }
                catch (System.Exception) { ed.WriteMessage("\n[Błąd]: Niepoprawny JSON. Używam {}."); }
            }

            string result = ToolOrchestrator.Instance.ExecuteTool("DimensionEditTool", args, new CadExecutionContext(doc));
        }

        [CommandMethod("AI_XDATA", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandAiXData()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            SyncSelectionWithMemory(ed);

            if (AgentMemoryState.ActiveSelection.Length == 0)
            {
                ed.WriteMessage("\n[Błąd]: Najpierw zaznacz obiekty do odczytu XData.");
                return;
            }

            PromptStringOptions opts = new PromptStringOptions("\nPodaj nazwę aplikacji (Enter dla wszystkich): ");
            opts.AllowSpaces = true;
            PromptResult res = ed.GetString(opts);

            JObject args = new JObject();
            if (res.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(res.StringResult))
            {
                args["AppName"] = res.StringResult.Trim();
            }

            // Używamy orkiestratora, aby zachować spójność z logiką V2
            string result = ToolOrchestrator.Instance.ExecuteTool("ReadXData", args, new CadExecutionContext(doc));
            ed.WriteMessage($"\n\n--- WYNIK ODCZYTU XDATA ---\n{result}\n---------------------------\n");
        }
        [CommandMethod("AI_SETXDATA", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandAiSetXData()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            SyncSelectionWithMemory(ed);

            if (AgentMemoryState.ActiveSelection.Length == 0)
            {
                ed.WriteMessage("\n[Błąd]: Najpierw zaznacz obiekty do zapisu XData.");
                return;
            }

            PromptStringOptions appOpts = new PromptStringOptions("\nPodaj nazwę aplikacji (RegApp): ");
            PromptResult appRes = ed.GetString(appOpts);
            if (appRes.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(appRes.StringResult)) return;

            PromptStringOptions dataOpts = new PromptStringOptions("\nPodaj wpisy w formacie JSON (np. [{\"Type\": \"String\", \"Value\": \"Hello\"}]): ");
            dataOpts.AllowSpaces = true;
            PromptResult dataRes = ed.GetString(dataOpts);
            if (dataRes.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(dataRes.StringResult)) return;

            try
            {
                JArray entries = JArray.Parse(dataRes.StringResult);
                JObject args = new JObject
                {
                    ["AppName"] = appRes.StringResult.Trim(),
                    ["Entries"] = entries
                };

                string result = ToolOrchestrator.Instance.ExecuteTool("WriteXData", args, new CadExecutionContext(doc));
                ed.WriteMessage($"\n\n--- WYNIK ZAPISU XDATA ---\n{result}\n---------------------------\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[Błąd]: Niepoprawny format danych JSON: {ex.Message}");
            }
        }
        [CommandMethod("AI_FINDXDATA", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void CommandAiFindXData()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            SyncSelectionWithMemory(ed);

            JObject args = new JObject();

            if (AgentMemoryState.ActiveSelection.Length > 0)
            {
                // Automatyczne skanowanie zaznaczenia (w tym bloków)
                args["Mode"] = "Selection";
            }
            else
            {
                // Brak zaznaczenia - pytamy o blok
                PromptStringOptions pso = new PromptStringOptions("\nNic nie zaznaczono. Podaj nazwę bloku do przeskanowania: ");
                PromptResult psr = ed.GetString(pso);
                if (psr.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(psr.StringResult)) return;

                args["Mode"] = "Block";
                args["BlockName"] = psr.StringResult.Trim();
            }

            string result = ToolOrchestrator.Instance.ExecuteTool("FindXData", args, new CadExecutionContext(doc));
            ed.WriteMessage($"\n\n--- WYNIK SKANOWANIA XDATA ---\n{result}\n------------------------------\n");
        }

        [CommandMethod("SKAN")]
        public void CommandSkan()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            
            // Bezpośrednie wywołanie narzędzia CaptureVisionArea
            string result = ToolOrchestrator.Instance.ExecuteTool("CaptureVisionArea", new JObject(), new CadExecutionContext(doc));
            
            if (result.StartsWith("[VISION_IMAGE_CAPTURED]|"))
            {
                string path = result.Split('|')[1];
                ed.WriteMessage($"\n[SKAN]: Zrzut zapisany pomyślnie: {path}");
            }
            else
            {
                ed.WriteMessage($"\n[SKAN]: {result}");
            }
        }

        [CommandMethod("AI_VISION")]
        public void CommandAiVision()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // 1. Wykonaj zrzut
            string toolResult = ToolOrchestrator.Instance.ExecuteTool("CaptureVisionArea", new JObject(), new CadExecutionContext(doc));
            if (!toolResult.StartsWith("[VISION_IMAGE_CAPTURED]|"))
            {
                ed.WriteMessage($"\n[AI_VISION]: Błąd przechwytywania: {toolResult}");
                return;
            }

            // 2. Przygotuj prompt dla Agenta
            PromptStringOptions pso = new PromptStringOptions("\nCo mam sprawdzić na tym zrzucie? (np. 'Odczytaj tabelkę'): ");
            pso.DefaultValue = "Przeanalizuj ten obszar i opisz co widzisz.";
            PromptResult psr = ed.GetString(pso);
            if (psr.Status != PromptStatus.OK) return;

            string userPrompt = string.IsNullOrWhiteSpace(psr.StringResult) ? pso.DefaultValue : psr.StringResult;

            // 3. Uruchom Agenta w trybie Vision (wstrzykujemy obraz do historii)
            // Uwaga: To wymaga, aby LLMClient przechwycił tag [VISION_IMAGE_CAPTURED]
            // Ponieważ wywołujemy to z CLI, musimy "udawać" pętlę ReAct lub po prostu wysłać wiadomość.
            // Najprościej: pokażemy panel Agenta i wyślemy tam zapytanie (jeśli obsługuje interfejs CLI).
            // W V2 GOLD commands_reference sugeruje, że to wywołuje Agenta.
            
            ed.WriteMessage("\n[AI_VISION]: Przesyłam obraz do analizy...");

            // W V2 wywołujemy RunVisionTask (musimy go dodać do AgentControl lub LLMClient)
            // Na razie wygramy to przez QuickAiCommand z wstrzyknięciem wyniku narzędzia
            
            if (_paletteSet == null) ShowAgentPanel();
            
            var control = AgentControl.Instance;
            if (control != null)
            {
                // Wstrzykujemy wynik narzędzia bezpośrednio do orkiestratora lub symulujemy pętlę
                // W tym przypadku najprościej będzie wywołać SendMessageReActAsync z gotową historią.
                var history = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = userPrompt }
                };
                
                // LLMClient obsłuży toolResult jeśli go "wstrzykniemy" jako asystent+tool_call+tool_result
                // Ale komenda AI_VISION ma być prostsza: Capture -> Send to LLM
                
                // Wykorzystamy istniejący ExecuteTool flow w LLMClient (przez prompt)
                control.ExternalProcessPrompt($"{userPrompt} (Wywołaj CaptureVisionArea i użyj ostatniego wyniku: {toolResult})");
            }
        }



        // ==============================================================
        // RPN ENGINE CLI (v2.20.3 - V1 Sync)
        // ==============================================================

        [CommandMethod("RPN", CommandFlags.Transparent)]
        public void CommandRpn()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            RpnCalculator.LoadStackFromDwg(doc.Database);

            ed.WriteMessage("\n--- INTERAKTYWNY KALKULATOR RPN ---");
            ed.WriteMessage("\nWpisuj operatory, '?' pomoc. Pusty [ENTER] by WSTRZYKNĄĆ wynik.");
            
            while (true)
            {
                // Odświeżanie stosu w konsoli
                ed.WriteMessage("\n========================");
                ed.WriteMessage("\n" + RpnCalculator.GetHPStackView(6));
                ed.WriteMessage("\n========================");
                
                PromptStringOptions opts = new PromptStringOptions("\n[RPN] >> ");
                opts.AllowSpaces = true;
                PromptResult res = ed.GetString(opts);

                if (res.Status != PromptStatus.OK) break;
                string input = res.StringResult.Trim();
                
                if (string.IsNullOrEmpty(input) || input == "=") break;
                if (input == "?") { WypiszSciageRpn(ed); continue; }

                try
                {
                    RpnCalculator.Evaluate(input, null, null, ed);
                    RpnCalculator.SaveStackToDwg(doc.Database);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[Błąd RPN]: {ex.Message}");
                }
            }

            // WSTRZYKIWANIE WYNIKU (Transparent mode support)
            if (RpnCalculator.GetStackState() != "Stos jest pusty.")
            {
                double cadVal = RpnCalculator.GetTopAsRawCadValue();
                string result = cadVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                
                ed.WriteMessage($"\n>> Wstrzyknięto wartość (unit-cleaned): {result} <<\n");
                doc.SendStringToExecute(result + "\n", true, false, false);
            }
        }

        [CommandMethod("CALC", CommandFlags.Transparent)]
        public void CommandCalc()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            RpnCalculator.LoadStackFromDwg(doc.Database);

            ed.WriteMessage("\n--- INTERAKTYWNY KALKULATOR (ODCZYT) ---");

            while (true)
            {
                ed.WriteMessage("\n========================");
                ed.WriteMessage("\n" + RpnCalculator.GetHPStackView(6));
                ed.WriteMessage("\n========================");
                
                PromptStringOptions opts = new PromptStringOptions("\n[CALC] >> ");
                opts.AllowSpaces = true;
                PromptResult res = ed.GetString(opts);

                if (res.Status != PromptStatus.OK) break;
                string input = res.StringResult.Trim();
                
                if (string.IsNullOrEmpty(input) || input == "=") break;
                if (input == "?") { WypiszSciageRpn(ed); continue; }

                try
                {
                    RpnCalculator.Evaluate(input, null, null, ed);
                    RpnCalculator.SaveStackToDwg(doc.Database);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[Błąd RPN]: {ex.Message}");
                }
            }
        }

        [CommandMethod("STOS", CommandFlags.Transparent)]
        public void CommandStos()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            RpnCalculator.LoadStackFromDwg(doc.Database);
            var ed = doc.Editor;
            
            ed.WriteMessage("\n--- AKTUALNY STAN STOSU RPN ---");
            ed.WriteMessage("\n" + RpnCalculator.GetStackState());
            ed.WriteMessage("-------------------------------\n");
        }

        private void WypiszSciageRpn(Editor ed)
        {
            ed.WriteMessage("\n\n=======================================================");
            ed.WriteMessage("\n                 ŚCIĄGA KALKULATORA RPN                  ");
            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n [GEOMETRIA] DL, DX, DY, DZ - Pomiary interaktywne");
            ed.WriteMessage("\n [STOS]      SWAP, DUP, DROP, CLEAR, PICK");
            ed.WriteMessage("\n [MATEMA]    +, -, *, /, ^, SQRT, SIN, COS, ROUND, ABS");
            ed.WriteMessage("\n [JEDNOSTKI] 10_m, 50_mm, cm2, kg/m3 itd. CONVE (konwersja)");
            ed.WriteMessage("\n [ZMIENNE]   10 $X STO (zapis), $X RCL (odczyt)");
            ed.WriteMessage("\n [INFO]      Pusty [ENTER] lub '=' kończy pracę.");
            ed.WriteMessage("\n             Dla RPN: wysyła wynik do linii poleceń CAD.");
            ed.WriteMessage("\n=======================================================\n");
        }

        // ==============================================================
        // LISP SELF-HEALING HOOK
        // ==============================================================
        [LispFunction("agent-callback")]
        public object LispAgentCallback(ResultBuffer args)
        {
            try
            {
                if (args == null) return false;
                
                var list = new System.Collections.Generic.List<string>();
                foreach (TypedValue tv in args)
                {
                    list.Add(tv.Value?.ToString() ?? "");
                }

                if (list.Count < 2) return false;

                string status = list[0];
                string errorMessage = list[1];

                BielikLogger.LogInfo($"[LispCallback] Odebrano status: {status}, msg: {errorMessage}");
                OnLispCallback?.Invoke(status, errorMessage);
                return true;
            }
            catch (System.Exception ex)
            {
                BielikLogger.LogError("Błąd agent-callback", ex);
                return false;
            }
        }
    }
}
