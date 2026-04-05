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
        private Label lblStatus;


        // --- UI Logi Narzędzi ---
        private RichTextBox txtToolLogs;
        private Button btnCopyLogs;

        // --- Silnik V2 ---
        private LLMClient _llmClient;
        private ToolOrchestrator _orchestrator;
        private List<ChatMessage> _conversationHistory;
        private bool isDarkMode = true;
        private string _activeModel = "LM Studio / local-model"; // Domyślny model
        private AutoBenchmarkEngine _benchmarkEngine;
        private TabPage tabBenchmark;

        public static AgentControl Instance { get; private set; }
        private TabPage tabChat;

        public AgentControl()
        {
            InitializeEngineV2();
            InitializeStandardUI();
            ApplyTheme();
            Instance = this;

            // Inicjalizacja wiadomosci powitalnych
            AppendToHistory("SYSTEM", "Bielik V2 GOLD gotowy. Zasilony przez OpenAI Tool Calling Standard.\n\n" + _orchestrator.GetRegisteredToolsInfo(), isDarkMode ? Color.Orange : Color.DarkOrange);
        }

        private void InitializeEngineV2()
        {
            _orchestrator = new ToolOrchestrator();
            _orchestrator.Initialize(); // Ważne: Inicjalizacja skanowania narzędzi (w tym ExecuteMacroTool)

            // Konfigurowalny endpoint
            _llmClient = new LLMClient("http://localhost:1234/v1/chat/completions", "not-needed", _orchestrator);
            
            _llmClient.OnStatusUpdate += UpdateStatusHUD;
            _llmClient.OnToolCallLogged += AppendToolLog;
            _llmClient.OnStatsUpdate += UpdateStatsHUD;

            _benchmarkEngine = new AutoBenchmarkEngine(_llmClient);

            _conversationHistory = new List<ChatMessage>
            {
                new ChatMessage 
                { 
                    Role = "system", 
                    Content = "Jesteś asystentem BricsCAD (Bielik V2 GOLD). Działaj precyzyjnie używając narzędzi. " +
                              "NIGDY nie używaj tagów takich jak [FOR_EACH] czy [CREATE_OBJECT]. Komunikuj się WYŁĄCZNIE poprzez natywne wywołania funkcji (tool_calls). " +
                              "PAMIĘTAJ: Dla obiektów Circle używaj ZAWSZE 'Center' i 'Radius'. Parametry 'StartPoint' i 'EndPoint' są zarezerwowane WYŁĄCZNIE dla linii. " +
                              "PRZYKŁAD KONCEPCYJNY: Jeśli użytkownik prosi o '5 okręgów co 100 jednostek', użyj narzędzia 'Foreach'. W jego parametrze 'GenerateSequence' ustaw StartVector na '0,0,0', OffsetVector na '100,0,0' i Count na 5. W parametrze 'Action' przekaż JSON narzędzia 'CreateObject' z EntityType 'Circle', gdzie Center to '{item}'. " +
                              "KRYTYCZNE: ZABRONIONE JEST wypisywanie wywołań narzędzi jako tekstu w wiadomości (np. używając bloków tool_request, json lub jakichkolwiek tagów). Wywołania narzędzi MUSZĄ być wysłane w tle, wyłącznie poprzez natywny interfejs API (funkcję tool_calls)."
                }
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
            tabChat = new TabPage("💬 Czat (V2 GOLD)");

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

            Panel panStats = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(5, 2, 5, 2) };
            
            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"[Model: {_activeModel}] Gotowy."
            };

            lblStats = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "⏱ 0ms | 🧠 0 tkn | ⚡ 0 t/s"
            };

            panStats.Controls.Add(lblStats);
            panStats.Controls.Add(lblStatus);


            tabChat.Controls.Add(panStats);
            tabChat.Controls.Add(txtHistory);
            tabChat.Controls.Add(panInput);

            // ==========================================
            // ZAKŁADKA 2: LOGI NARZĘDZI (JSON)
            // ==========================================
            TabPage tabDev = new TabPage("📜 Logi Narzędzi");

            txtToolLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            btnCopyLogs = new Button
            {
                Text = "📋 Kopiuj do schowka",
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCopyLogs.Click += (s, e) => { if (!string.IsNullOrEmpty(txtToolLogs.Text)) Clipboard.SetText(txtToolLogs.Text); };

            tabDev.Controls.Add(txtToolLogs);
            tabDev.Controls.Add(btnCopyLogs);

            // ==========================================
            // ZAKŁADKA 3: BENCHMARK (OCENA LLM)
            // ==========================================
            tabBenchmark = new TabPage("📊 Benchmark");
            tabBenchmark.Controls.Add(new AutoBenchmarkControl(_benchmarkEngine));

            // ==========================================
            // ZAKŁADKA 4: TESTER (WORKBENCH V2)
            // ==========================================
            TabPage tabTester = new TabPage("🧪 Tester V2");
            tabTester.Controls.Add(new AgentTesterControl(_llmClient));

            // Dodajemy widoki
            tabControl.TabPages.Add(tabChat);
            tabControl.TabPages.Add(tabDev);
            tabControl.TabPages.Add(tabBenchmark);
            tabControl.TabPages.Add(tabTester);
            this.Controls.Add(tabControl);
        }

        public void SwitchToBenchmark()
        {
            if (tabControl != null && tabBenchmark != null)
            {
                tabControl.SelectedTab = tabBenchmark;
            }
        }

        public void SwitchToChat()
        {
            if (tabControl != null && tabChat != null)
            {
                tabControl.SelectedTab = tabChat;
            }
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
            _conversationHistory.RemoveRange(1, _conversationHistory.Count - 1);
            AgentMemoryState.Clear();
            AgentMemoryState.Variables.Clear();
            AppendToHistory("SYSTEM", "Konwersacja i pamięć zresetowane.", isDarkMode ? Color.Orange : Color.DarkOrange);
        }

        private void UpdateStatusHUD(string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateStatusHUD), status);
                return;
            }
            lblStatus.Text = $"[Model: {_activeModel}] {status}";
        }

        private void UpdateStatsHUD(long ms, int sent, int recv)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<long, int, int>(UpdateStatsHUD), ms, sent, recv);
                return;
            }
            int total = sent + recv;
            double seconds = ms / 1000.0;
            lblStats.Text = $"⏱ {seconds:F1}s | 🧠 {total} tkn | ⚡ {Math.Round((double)recv / (seconds + 0.1), 1)} t/s | [READY]";
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

        public async Task ProcessInputAsync(string userMsg)
        {
            if (string.IsNullOrEmpty(userMsg)) return;

            AppendToHistory("TY", userMsg, isDarkMode ? Color.LightSkyBlue : Color.Blue);
            btnSend.Enabled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;

            _conversationHistory.Add(new ChatMessage { Role = "user", Content = userMsg });
            UpdateStatusHUD("Oczekiwanie na analizę...");

            try
            {
                string aiResponse = await Task.Run(async () => 
                {
                    return await _llmClient.SendMessageReActAsync(_conversationHistory, doc);
                });

                AppendToHistory("BIELIK", aiResponse, isDarkMode ? Color.LightGreen : Color.DarkGreen);
                UpdateStatusHUD("Gotowy.");
            }
            catch (Exception ex)
            {
                AppendToHistory("BŁĄD", ex.Message, Color.LightCoral);
                UpdateStatusHUD("Błąd krytyczny.");
            }
            finally
            {
                btnSend.Enabled = true;
                txtInput.Focus();
            }
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            string userMsg = txtInput.Text.Trim();
            txtInput.Clear();
            await ProcessInputAsync(userMsg);
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
