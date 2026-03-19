using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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

            // 1. Wypisujemy na ekran naszą wiadomość i czyścimy pole
            AppendToHistory("TY", userMsg);
            txtInput.Clear();
            btnSend.Enabled = false; // Blokujemy przycisk na czas myślenia AI

            // 2. Pobieramy aktualny dokument CAD
            Document doc = Application.DocumentManager.MdiActiveDocument;

            try
            {
                AppendToHistory("SYSTEM", "Bielik myśli...");

                // 3. Wywołujemy asynchroniczną metodę z naszej głównej klasy Komendy
                // "await" sprawia, że interfejs BricsCADa NIE ZAMARZA!
                string aiResponse = await BricsCAD_Agent.Komendy.ZapytajAgentaAsync(userMsg, doc);

                // 4. Wypisujemy odpowiedź Agenta
                AppendToHistory("BIELIK", aiResponse);
            }
            catch (Exception ex)
            {
                AppendToHistory("BŁĄD", ex.Message);
            }
            finally
            {
                btnSend.Enabled = true; // Odblokowujemy przycisk
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
    }
}