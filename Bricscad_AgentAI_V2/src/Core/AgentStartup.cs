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
            ed.WriteMessage($"\n\n--- WYNIK EDYCJI WYMIARÓW ---\n{result}\n-----------------------------\n");
        }



        // ==============================================================
        // RPN ENGINE CLI (FINAL v2.20.2)
        // ==============================================================

        [CommandMethod("RPN", CommandFlags.Transparent)]
        public void CommandRpn()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            RpnCalculator.LoadStackFromDwg(doc.Database);

            ed.WriteMessage("\n[Agent RPN] Tryb interaktywny. Wpisz '?' dla pomocy, 'EXIT' lub puste by wyjść.");
            
            while (true)
            {
                // Podgląd górnej części stosu
                string view = RpnCalculator.GetHPStackView(3).Replace("\n", " | ");
                ed.WriteMessage($"\nSTACK: {view}");
                
                PromptStringOptions opts = new PromptStringOptions("\nRPN >> ");
                opts.AllowSpaces = true;
                PromptResult res = ed.GetString(opts);

                if (res.Status != PromptStatus.OK) break;
                string input = res.StringResult.Trim();
                if (string.IsNullOrEmpty(input) || input.ToUpperInvariant() == "EXIT" || input.ToUpperInvariant() == "QUIT") break;

                if (input == "?") { WypiszSciageRpn(ed); continue; }

                try
                {
                    string result = RpnCalculator.Evaluate(input, null, null, ed);
                    ed.WriteMessage($"\nWynik: {result}");
                    RpnCalculator.SaveStackToDwg(doc.Database);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n{ex.Message}");
                }
            }
        }

        [CommandMethod("CALC", CommandFlags.Transparent)]
        public void CommandCalc()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            RpnCalculator.LoadStackFromDwg(doc.Database);

            // BricsCAD w trybie .NET przy ed.GetString() z CommandFlags.Transparent 
            // potrafi przechwycić tekst wpisany po nazwie komendy z bufora.
            PromptStringOptions opts = new PromptStringOptions("\n[CALC] Wyrażenie: ");
            opts.AllowSpaces = true;
            PromptResult res = ed.GetString(opts);

            if (res.Status == PromptStatus.OK)
            {
                string input = res.StringResult.Trim();
                if (string.IsNullOrEmpty(input)) return;
                if (input == "?") { WypiszSciageRpn(ed); return; }

                try
                {
                    string result = RpnCalculator.Evaluate(input, null, null, ed);
                    ed.WriteMessage($"\n[CALC] Wynik: {result}");
                    RpnCalculator.SaveStackToDwg(doc.Database);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[CALC Błąd]: {ex.Message}");
                }
            }
        }

        [CommandMethod("STOS", CommandFlags.Transparent)]
        public void CommandStos()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            RpnCalculator.LoadStackFromDwg(doc.Database);
            var stack = RpnCalculator.GetStack();
            
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n--- STOS AGENTA (DWG PERSISTENT) ---");
            if (stack.Count == 0)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n(Stos jest pusty)");
            }
            else
            {
                for (int i = 0; i < stack.Count; i++)
                {
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n  {stack.Count - i}: {stack[i]}");
                }
            }
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n-------------------------------------");
        }

        private void WypiszSciageRpn(Editor ed)
        {
            ed.WriteMessage("\n--- ŚCIĄGAWKA RPN AGENTA ---");
            ed.WriteMessage("\nPodstawowe: +, -, *, /, ^ (potęga), SQRT (pierwiastek)");
            ed.WriteMessage("\nStos: DROP (usuń), SWAP (zamień), DUP (duplikuj), CLEAR (czyść)");
            ed.WriteMessage("\nStałe: #PI, #G (9.81), #C (światło), #UNITL (jedn. rysunku)");
            ed.WriteMessage("\nJednostki: 10_m, 50_mm, 2_degC, 10_m2, 5_kg/m3");
            ed.WriteMessage("\nKonwersja: 'mm' CONVE (na mm), 'm' +UNIT (nadaj m)");
            ed.WriteMessage("\n\nPOMIARY CAD (Wymagają kliknięcia punktów):");
            ed.WriteMessage("\n  DL  - Odległość między punktami.");
            ed.WriteMessage("\n  DX  - Różnica współrzędnych X.");
            ed.WriteMessage("\n  DY  - Różnica współrzędnych Y.");
            ed.WriteMessage("\n  DZ  - Różnica współrzędnych Z.");
            ed.WriteMessage("\n\nZmienne: 10 $X STO (zapisz do $X), $X RCL (czytaj $X)");
            ed.WriteMessage("\n---------------------------");
        }
    }
}
