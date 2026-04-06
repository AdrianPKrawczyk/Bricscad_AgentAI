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
        private AgentTesterControl testerControl;
        private DataGridView dgvTools;
        private Button btnSaveConfig;
        private CheckBox chkEarlyExit;

        // --- UI Czat ---
        private RichTextBox txtHistory;
        private RichTextBox txtInput;
        private Button btnSend;
        private Button btnReset;
        private Label lblStats;
        private Label lblStatus;
        private ListBox lstAutocomplete;
        private string[] availableTags = new[] { "#core", "#bloki", "#warstwy", "#tekst", "#makro", "#all" };


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
                              "PRZYKŁAD KONCEPCYJNY: Jeśli użytkownik prosi o 'N okręgów co wektor X,Y,Z', użyj narzędzia 'Foreach'. W jego parametrze 'GenerateSequence' ustaw Count na zadaną liczbę N, OffsetVector na podany wektor. W parametrze 'Action' przekaż JSON docelowego narzędzia (np. 'CreateObject'), gdzie pole odpowiadające za pozycję ma wartość '{item}'. " +
                              "DELEGOWANIE OBLICZEŃ (RPN) [SUPERMOCE]: KRYTYCZNE ZAGROŻENIE BŁĘDEM: Jesteś modelem językowym, a nie kalkulatorem. Twoje obliczenia w pamięci ZAWSZE są błędne. Jeśli w zadaniu musisz dodać, odjąć lub pomnożyć JAKĄKOLWIEK wartość (współrzędną lub promień), MASZ CAŁKOWITY ZAKAZ podawania gotowego wyniku liczbowego. ZAMIAST TEGO MUSISZ użyć notacji RPN. Jeśli obliczysz wektor samodzielnie, zniszczysz projekt konstrukcyjny. " +
                              "Składnia RPN: Użyj przedrostka 'RPN: ' wewnątrz wartości parametru (np. '2 2 +'). Przykłady: " +
                              "1. Promień (100/3): 'Radius': 'RPN: 100 3 /'. " +
                              "2. Wektor pionowy (start Y=10, długość 100): 'EndPoint': '50, RPN: 10 100 +, 0'. " +
                              "3. Dynamiczna pętla: 'Center': '{item}, RPN: {item} 2 *, 0'. " +
                              "- Jednostki fizyczne: Zawsze możesz podać wartość wraz z jednostką używając formatu 'WARTOŚĆ_JEDNOSTKA' (np. '100_mm', '5_m', '2.5_kg', '10_MPa'). " +
                              "- Inteligentna konwersja wymiarów: System sam przelicza jednostki! Możesz zlecić 'RPN: 100_mm 20_cm +' a system poprawnie to doda. " +
                              "- Jeśli użytkownik prosi o geometrię w innych jednostkach niż domyślne dla dokumentu (np. chce okrąg o promieniu 2 cale), użyj notacji wymiarowej: 'Radius': 'RPN: 2_in'. " +
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

            lstAutocomplete = new ListBox
            {
                Visible = false,
                Width = 150,
                Height = 120,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                Cursor = Cursors.Hand
            };
            lstAutocomplete.Items.AddRange(availableTags);
            lstAutocomplete.DoubleClick += (s, e) => InsertSelectedTag();
            this.Controls.Add(lstAutocomplete);
            lstAutocomplete.BringToFront();

            Panel inputBorder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Color.Gray };
            inputBorder.Controls.Add(txtInput);
            txtInput.TextChanged += TxtInput_TextChanged;

            panInput.Controls.Add(inputBorder);
            
            chkEarlyExit = new CheckBox
            {
                Text = "⚡ Tryb Szybki (Early Exit)",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.LightGray,
                Dock = DockStyle.Left,
                Padding = new Padding(10, 0, 0, 0)
            };

            panInput.Controls.Add(chkEarlyExit);
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
            
            // ==========================================
            // ZAKŁADKA 5: KONFIGURACJA TAGÓW
            // ==========================================
            TabPage tabTags = new TabPage("🏷 Tagi / Core");
            dgvTools = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Black, // Tekst w komórkach (WinForms DataGrid ma czasem problemy z ciemnym motywem bez pełnego owner-draw)
                AllowUserToAddRows = false,
                RowHeadersVisible = false
            };
            
            dgvTools.Columns.Add(new DataGridViewTextBoxColumn { Name = "ToolName", HeaderText = "Narzędzie", ReadOnly = true });
            dgvTools.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsCore", HeaderText = "Core (#core)" });
            dgvTools.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tags", HeaderText = "Tagi (rozdzielane przecinkiem)" });
            dgvTools.Columns.Add(new DataGridViewCheckBoxColumn { Name = "EarlyExit", HeaderText = "⚡ Early Exit" });

            btnSaveConfig = new Button
            {
                Text = "💾 Zapisz konfigurację narzędzi",
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveConfig.Click += BtnSaveConfig_Click;

            tabTags.Controls.Add(dgvTools);
            tabTags.Controls.Add(btnSaveConfig);
            tabControl.TabPages.Add(tabTags);

            LoadToolConfigToGrid();

            this.Controls.Add(tabControl);
        }

        private void LoadToolConfigToGrid()
        {
            dgvTools.Rows.Clear();
            var settings = ToolConfigManager.GetAllSettings();
            foreach (var kvp in settings)
            {
                dgvTools.Rows.Add(kvp.Key, kvp.Value.IsCore, kvp.Value.Tags, kvp.Value.SupportsEarlyExit);
            }
        }

        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            var newSettings = new Dictionary<string, ToolSettings>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in dgvTools.Rows)
            {
                if (row.Cells["ToolName"].Value == null) continue;
                string name = row.Cells["ToolName"].Value.ToString();
                bool isCore = (bool)(row.Cells["IsCore"].Value ?? false);
                string tags = row.Cells["Tags"].Value?.ToString() ?? "";
                bool earlyExit = (bool)(row.Cells["EarlyExit"].Value ?? false);
                
                newSettings[name] = new ToolSettings { IsCore = isCore, Tags = tags, SupportsEarlyExit = earlyExit };
            }
            ToolConfigManager.UpdateSettings(newSettings);
            MessageBox.Show("Konfiguracja narzędzi została zapisana!", "Agent AI V2", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // 1. Obsługa nawigacji Autocomplete (tylko gdy lista jest widoczna i pole tekstowe aktywne)
            if (lstAutocomplete != null && lstAutocomplete.Visible && txtInput.Focused)
            {
                if (keyData == Keys.Down)
                {
                    if (lstAutocomplete.SelectedIndex < lstAutocomplete.Items.Count - 1)
                        lstAutocomplete.SelectedIndex++;
                    return true; // Blokujemy dalsze przetwarzanie klawisza
                }
                else if (keyData == Keys.Up)
                {
                    if (lstAutocomplete.SelectedIndex > 0)
                        lstAutocomplete.SelectedIndex--;
                    return true;
                }
                else if (keyData == Keys.Enter || keyData == Keys.Tab)
                {
                    InsertSelectedTag();
                    return true;
                }
                else if (keyData == Keys.Escape)
                {
                    lstAutocomplete.Visible = false;
                    return true;
                }
            }

            // 2. Obsługa Ctrl+Enter dla wysyłania wiadomości
            if (keyData == (Keys.Control | Keys.Enter))
            {
                btnSend_Click(btnSend, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void TxtInput_TextChanged(object sender, EventArgs e)
        {
            int index = txtInput.SelectionStart;
            if (index <= 0)
            {
                lstAutocomplete.Visible = false;
                return;
            }

            // Znajdź początek aktualnie wpisywanego tagu (ostatnie # przed kursorem)
            string textSoFar = txtInput.Text.Substring(0, index);
            int lastHashIndex = textSoFar.LastIndexOf('#');

            if (lastHashIndex != -1)
            {
                string searchString = textSoFar.Substring(lastHashIndex).ToLower();
                
                // Jeśli po # jest spacja, to już nie filtrujemy tagu
                if (searchString.Contains(" "))
                {
                    lstAutocomplete.Visible = false;
                    return;
                }

                // Filtrujemy dostępne tagi
                var matches = new List<string>();
                foreach (var tag in availableTags)
                {
                    if (tag.StartsWith(searchString, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(tag);
                    }
                }

                if (matches.Count > 0)
                {
                    lstAutocomplete.BeginUpdate();
                    lstAutocomplete.Items.Clear();
                    foreach (var m in matches) lstAutocomplete.Items.Add(m);
                    lstAutocomplete.EndUpdate();
                    lstAutocomplete.SelectedIndex = 0;

                    // Pozycjonowanie listy
                    Point pos = txtInput.GetPositionFromCharIndex(lastHashIndex);
                    Point screenPos = txtInput.PointToScreen(pos);
                    Point clientPos = this.PointToClient(screenPos);
                    lstAutocomplete.Location = new Point(clientPos.X, clientPos.Y - lstAutocomplete.Height - 5);
                    
                    lstAutocomplete.Visible = true;
                    lstAutocomplete.BringToFront();
                }
                else
                {
                    lstAutocomplete.Visible = false;
                }
            }
            else
            {
                lstAutocomplete.Visible = false;
            }
        }

        private void InsertSelectedTag()
        {
            if (lstAutocomplete.SelectedItem == null) return;

            string tag = lstAutocomplete.SelectedItem.ToString();
            int caretIndex = txtInput.SelectionStart;

            // Znajdujemy indeks ostatniego # przed kursorem
            string textSoFar = txtInput.Text.Substring(0, caretIndex);
            int hashIndex = textSoFar.LastIndexOf('#');

            if (hashIndex != -1)
            {
                // Podmieniamy tekst od # do kursora na tag + spacja
                txtInput.Select(hashIndex, caretIndex - hashIndex);
                txtInput.SelectedText = tag + " ";
            }

            lstAutocomplete.Visible = false;
            txtInput.Focus();
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

        public async Task ProcessInputAsync(string rawInput)
        {
            if (string.IsNullOrEmpty(rawInput)) return;

            // 1. Semantic Tag Pre-processing (Regex)
            // Wyłuskujemy wszystkie tagi zaczynające się od #
            var tagMatches = System.Text.RegularExpressions.Regex.Matches(rawInput, @"#\w+");
            List<string> extractedTags = new List<string>();
            string cleanMsg = rawInput;

            foreach (System.Text.RegularExpressions.Match match in tagMatches)
            {
                extractedTags.Add(match.Value.ToLower());
                // Usuwamy tag z czystej wiadomości dla LLM
                cleanMsg = cleanMsg.Replace(match.Value, "").Trim();
            }

            AppendToHistory("TY", rawInput, isDarkMode ? Color.LightSkyBlue : Color.Blue);
            btnSend.Enabled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;

            _conversationHistory.Add(new ChatMessage { Role = "user", Content = cleanMsg });
            UpdateStatusHUD("Oczekiwanie na analizę...");

            try
            {
                string aiResponse = await Task.Run(async () => 
                {
                    // Przekazujemy wyłuskane tagi do klienta LLM
                    bool earlyExit = chkEarlyExit.Checked;
                    return await _llmClient.SendMessageReActAsync(_conversationHistory, doc, extractedTags, earlyExit);
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
