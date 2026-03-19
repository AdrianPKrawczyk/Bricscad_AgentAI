using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Teigha.DatabaseServices;
using Application = Bricscad.ApplicationServices.Application;

namespace Bricscad_AgentAI
{
    // ZMIANA: Dziedziczymy z UserControl zamiast Form
    public partial class AgentControl : UserControl
    {
        public AgentControl()
        {
            InitializeComponent();
        }

        // Dodajemy logikę obsługi przycisku. 
        // Słowo kluczowe "async" pozwala na wywołanie zapytań w tle!
        private async void btnSend_Click(object sender, EventArgs e)
        {
            string userMsg = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(userMsg)) return;

            AppendToHistory("TY", userMsg);
            txtInput.Clear();
            btnSend.Enabled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;

            // --- GENIALNY TRIK CZ. 1: Łapiemy zaznaczenie ZANIM okno ukradnie fokus i wejdziemy w tło! ---
            ObjectId[] przechwyconeZaznaczenie = null;
            try
            {
                PromptSelectionResult selRes = doc.Editor.SelectImplied();
                if (selRes.Status == PromptStatus.OK)
                {
                    przechwyconeZaznaczenie = selRes.Value.GetObjectIds();
                }
            }
            catch { } // Cicha obrona przed błędami

            try
            {
                AppendToHistory("SYSTEM", "Bielik myśli...");

                // Przekazujemy nasz skarb (przechwyconeZaznaczenie) dalej do Agenta
                string aiResponse = await BricsCAD_Agent.Komendy.ZapytajAgentaAsync(userMsg, doc, przechwyconeZaznaczenie);

                AppendToHistory("BIELIK", aiResponse);
            }
            catch (Exception ex)
            {
                AppendToHistory("BŁĄD", ex.Message);
            }
            finally
            {
                btnSend.Enabled = true;
            }
        }

        // Pomocnicza metoda do formatowania tekstu w oknie historii
        public void AppendToHistory(string sender, string message)
        {
            txtHistory.AppendText($"[{sender}]: {message}{Environment.NewLine}");
            // Przewiń na sam dół
            txtHistory.SelectionStart = txtHistory.Text.Length;
            txtHistory.ScrollToCaret();
        }

        private void txtHistory_TextChanged(object sender, EventArgs e)
        {

        }
    }
}