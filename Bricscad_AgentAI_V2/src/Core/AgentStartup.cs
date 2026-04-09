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

[assembly: CommandClass(typeof(Bricscad_AgentAI_V2.Core.AgentStartup))]

namespace Bricscad_AgentAI_V2.Core
{
    public class AgentStartup
    {
        private static PaletteSet _paletteSet = null;

        [CommandMethod("AGENT_V2")]
        public void ShowAgentPanel()
        {
            try
            {
                if (_paletteSet == null)
                {
                    _paletteSet = new PaletteSet("Bielik AI V2 GOLD", new Guid("B2A1C4D3-F5E6-4879-9A8B-1C2D3E4F5A6B"));
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

                    string result = ToolOrchestrator.Instance.ExecuteTool(toolName, args, doc);
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

                string result = ToolOrchestrator.Instance.ExecuteTool(toolName, args, doc);
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
            string result = ToolOrchestrator.Instance.ExecuteTool("GetPropertiesTool", args, doc);
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

            string result = ToolOrchestrator.Instance.ExecuteTool("DimensionEditTool", args, doc);
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
            string result = ToolOrchestrator.Instance.ExecuteTool("ReadXData", args, doc);
            ed.WriteMessage($"\n\n--- WYNIK ODCZYTU XDATA ---\n{result}\n---------------------------\n");
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
    }
}
