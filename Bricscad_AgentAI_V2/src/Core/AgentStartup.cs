using System;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad.Windows;
using Teigha.Runtime;
using Bricscad_AgentAI_V2.UI;
using Bricscad_AgentAI_V2.Core;

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
                    _paletteSet.Dock = DockSides.Right;
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
    }
}
