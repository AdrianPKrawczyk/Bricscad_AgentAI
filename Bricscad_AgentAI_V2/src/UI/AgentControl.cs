using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Teigha.DatabaseServices;
using Application = Bricscad.ApplicationServices.Application;

namespace Bricscad_AgentAI_V2.UI
{
    public class AgentControl : UserControl
    {
        private TabControl tabControl;

        // --- UI Czat ---
        private RichTextBox txtHistory;
        private RichTextBox txtInput;
        private Button btnSend;
        private Button btnReset;
        private Label lblStats;

        // --- UI Logi Narzędzi (Nowe) ---
        private RichTextBox txtToolLogs;

        // --- Silnik V2 ---
        private LLMClient _llmClient;
        private ToolOrchestrator _orchestrator;
        private List<ChatMessage> _conversationHistory;
        private bool isDarkMode = true; // Hardcoded default dla tej iteracji. Zostanie zmostkowane do Rejestru później.

        public static AgentControl Instance { get; private set; }

        public AgentControl()
        {
            InitializeEngineV2();
            InitializeStandardUI();
            ApplyTheme();
            Instance = this;

            // Inicjalizacja wiadomosci powitalnych
            AppendToHistory("SYSTEM", "Bielik V2 gotowy. Zasilony przez OpenAI Tool Calling Standard.\n\n" + _orchestrator.GetRegisteredToolsInfo(), isDarkMode ? Color.Orange : Color.DarkOrange);
        }

        private void InitializeEngineV2()
        {
            _orchestrator = new ToolOrchestrator();

            // Zmień endpoint URL i token na dopasowane do środowiska (np. LM Studio: http://localhost:1234/v1/chat/completions)
            _llmClient = new LLMClient("http://localhost:1234/v1/chat/completions", "not-needed", _orchestrator);
            
            // Subskrybowanie zdarzeń z LLMClient do aktualizacji interfejsu 
            _llmClient.OnStatusUpdate += UpdateStatusHUD;
            _llmClient.OnToolCallLogged += AppendToolLog;

            _conversationHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "Jesteś asystentem BricsCAD (Bielik V2). Zawsze używaj dostępnych narzędzi do odczytywania struktury rysunku oraz tworzenia obiektów. Jeśli chcesz dokonać zmian na rysunku, upewnij się wpierw, że właściwe elementy są w ActiveSelection (SelectEntitiesTool)." }
            };
        }

        private void InitializeStandardUI()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9.5f);

            tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(120, 25) };

            // ==========================================
            // ZAKŁADKA 1: CZAT Z AI 
            // ==========================================
            TabPage tabChat = new TabPage("💬 Czat (V2)");

            txtHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None
            };

            Panel panInput = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(5) };

            txtInput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            txtInput.KeyDown += TxtInput_KeyDown;

            btnSend = new Button
            {
                Text = "Wyślij\n(Ctrl+Enter)",
                Dock = DockStyle.Right,
                Width = 100,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += btnSend_Click;

            btnReset = new Button
            {
                Text = "Reset\nPamięci",
                Dock = DockStyle.Right,
                Width = 80,
                BackColor = Color.Crimson,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += BtnReset_Click;

            Panel inputBorder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Color.Gray };
            inputBorder.Controls.Add(txtInput);

            panInput.Controls.Add(inputBorder);
            panInput.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panInput.Controls.Add(btnReset);
            panInput.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panInput.Controls.Add(btnSend);

            Panel panStats = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(5, 0, 0, 0) };
            lblStats = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Bielik V2 gotowy do pracy z narzędziami."
            };
            panStats.Controls.Add(lblStats);

            tabChat.Controls.Add(panStats);
            tabChat.Controls.Add(txtHistory);
            tabChat.Controls.Add(panInput);

            // ==========================================
            // ZAKŁADKA 2: LOGI NARZĘDZI (Zastępuje "Logi tagów")
            // ==========================================
            TabPage tabDev = new TabPage("📜 Logi Narzędzi (JSON)");

            txtToolLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            tabDev.Controls.Add(txtToolLogs);

            // Dodajemy widoki
            tabControl.TabPages.Add(tabChat);
            tabControl.TabPages.Add(tabDev);
            this.Controls.Add(tabControl);
        }

        private void ApplyTheme()
        {
            Color bgMain = isDarkMode ? Color.FromArgb(30, 30, 30) : Color.White;
            Color bgControl = isDarkMode ? Color.FromArgb(45, 45, 48) : Color.WhiteSmoke;
            Color fgText = isDarkMode ? Color.White : Color.Black;

            this.BackColor = bgMain;
            foreach (TabPage page in tabControl.TabPages) page.BackColor = bgMain;

            txtHistory.BackColor = bgMain;
            txtToolLogs.BackColor = bgMain;
            txtInput.BackColor = bgControl;

            txtHistory.ForeColor = fgText;
            txtToolLogs.ForeColor = Color.LightSkyBlue;
            txtInput.ForeColor = fgText;
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                btnSend_Click(btnSend, EventArgs.Empty);
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            _conversationHistory.RemoveRange(1, _conversationHistory.Count - 1); // Zachowaj tylko prompt Systemowy
            AgentMemoryState.Clear();
            AgentMemoryState.Variables.Clear();
            AppendToHistory("SYSTEM", "Konwersacja, Cache (zmienne) oraz ActiveSelection całkowicie zresetowane.", isDarkMode ? Color.Orange : Color.DarkOrange);
        }

        // --- EVENTY KIEROWANE Z LLMClient ---
        private void UpdateStatusHUD(string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateStatusHUD), status);
                return;
            }
            lblStats.Text = status;
        }

        private void AppendToolLog(string rawJsonCall)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendToolLog), rawJsonCall);
                return;
            }
            txtToolLogs.AppendText($"\n--- WYWOŁANIE [{DateTime.Now:HH:mm:ss}] ---\n");
            txtToolLogs.AppendText(rawJsonCall + "\n");
            txtToolLogs.SelectionStart = txtToolLogs.Text.Length;
            txtToolLogs.ScrollToCaret();
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            string userMsg = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(userMsg)) return;

            AppendToHistory("TY", userMsg, isDarkMode ? Color.LightSkyBlue : Color.Blue);
            txtInput.Clear();
            btnSend.Enabled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;

            // Przechwycenie manualnego zaznaczenia do pamięci izolowanej
            try
            {
                PromptSelectionResult selRes = doc.Editor.SelectImplied();
                if (selRes.Status == PromptStatus.OK)
                {
                    AgentMemoryState.Update(selRes.Value.GetObjectIds());
                    AppendToHistory("SYSTEM", $"Manualnie przechwycono {AgentMemoryState.ActiveSelection.Length} obiektów do pamięci.", Color.Gray);
                }
            }
            catch { }

            _conversationHistory.Add(new ChatMessage { Role = "user", Content = userMsg });
            UpdateStatusHUD("Oczekiwanie na analizę przez Bielik V2...");

            try
            {
                // Asynchroniczne odpalenie ReAct by nie zamrozić BricsCADa! (KLAZULA: Użycie Task.Run do oddzielenia weawer'a UI od synchronicznych pętli LLM)
                string aiResponse = await Task.Run(async () => 
                {
                    return await _llmClient.SendMessageReActAsync(_conversationHistory, doc);
                });

                AppendToHistory("BIELIK", aiResponse, isDarkMode ? Color.LightGreen : Color.DarkGreen);
                UpdateStatusHUD("Operacja zakończona sukcesem.");
            }
            catch (Exception ex)
            {
                AppendToHistory("BŁĄD", ex.Message, Color.LightCoral);
                UpdateStatusHUD("Krytyczny błąd wykonania.");
            }
            finally
            {
                btnSend.Enabled = true;
                txtInput.Focus();
            }
        }

        public void AppendToHistory(string sender, string message, Color color)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string, string, Color>(AppendToHistory), sender, message, color);
                return;
            }

            txtHistory.SelectionStart = txtHistory.TextLength;
            txtHistory.SelectionLength = 0;
            txtHistory.SelectionColor = color;
            txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Bold);
            txtHistory.AppendText($"[{sender}]: ");

            txtHistory.SelectionColor = isDarkMode ? Color.White : Color.Black;
            txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Regular);
            txtHistory.AppendText($"{message}\n\n");

            txtHistory.SelectionStart = txtHistory.Text.Length;
            txtHistory.ScrollToCaret();
        }
    }
}
