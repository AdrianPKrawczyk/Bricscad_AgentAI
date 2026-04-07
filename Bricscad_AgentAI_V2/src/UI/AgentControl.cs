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
using Newtonsoft.Json;
using Application = Bricscad.ApplicationServices.Application;

namespace Bricscad_AgentAI_V2.UI
{
    public class AgentControl : UserControl
    {
        private TabControl tabControl;
        private DataGridView dgvTools;
        private Button btnSaveConfig;
        private CheckBox chkEarlyExit;
        private DatasetStudioControl datasetStudio;

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
        private LLMStats _lastStats;

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
        }        private void InitializeEngineV2()
        {
            _orchestrator = new ToolOrchestrator();
            _orchestrator.Initialize(); // Ważne: Inicjalizacja skanowania narzędzi (w tym ExecuteMacroTool)

            // Konfigurowalny endpoint
            _llmClient = new LLMClient("http://localhost:1234/v1/chat/completions", "not-needed", _orchestrator);
            
            _llmClient.OnStatusUpdate += UpdateStatusHUD;
            _llmClient.OnToolCallLogged += AppendToolLog;
            _llmClient.OnStatsUpdate += (stats) => UpdateStatsHUD(stats);

            _benchmarkEngine = new AutoBenchmarkEngine(_llmClient);

            RebuildSystemPrompt();
        }

        private void RebuildSystemPrompt()
        {
            string systemPrompt = "Jesteś asystentem BricsCAD (Bielik V2 GOLD). Działaj precyzyjnie używając narzędzi. Komunikuj się WYŁĄCZNIE poprzez natywne wywołania funkcji (tool_calls). ZABRONIONE jest wypisywanie wywołań w zwykłym tekście.\n\n" +
                "--- 1. DELEGOWANIE OBLICZEŃ I LOGIKI (SUPERMOC RPN) ---\n" +
                "Jesteś modelem językowym, nie kalkulatorem. ZABRANIA SIĘ wykonywania obliczeń matematycznych w pamięci. Do wszystkich obliczeń wektorowych, matematycznych i tekstowych MUSISZ używać wbudowanego silnika RPN (Odwrotna Notacja Polska). Składnia: wartość zawsze zaczyna się od 'RPN: '.\n" +
                "- Matematyka (Postfix): Zamiast '2+2' piszesz 'RPN: 2 2 +'. Zamiast '(100/3)+5' piszesz 'RPN: 100 3 / 5 +'.\n" +
                "- Inteligentne Jednostki: Silnik natywnie rozumie fizykę! Zawsze podawaj wartości z jednostkami: 'WARTOŚĆ_JEDNOSTKA' (np. '100_mm', '5_m', '2_in'). Silnik sam je przeliczy do jednostek rysunku (np. 'RPN: 100_mm 20_cm +').\n" +
                "- Operacje na Stringach (CONCAT): Używaj pojedynczych cudzysłowów do tekstów. Łącz teksty operatorem CONCAT. Np. 'RPN: \\'Poziom \\' 5 2 * CONCAT' da wynik 'Poziom 10'.\n" +
                "- Logika Warunkowa (IFTE): Silnik obsługuje warunki If-Then-Else w formacie: [warunek] [prawda] [fałsz] IFTE. Np. 'RPN: {index} 2 > \\'OpcjaA\\' \\'OpcjaB\\' IFTE'.\n" +
                "- Znaki specjalne: Do łamania linii w tekstach CAD (MText/MLeader) używaj podwójnie uciecznionego znaku nowej linii: \\\\P.\n\n" +
                "--- 2. GLOBALNY SŁOWNIK WŁAŚCIWOŚCI CAD (ENTITY PROPERTIES) ---\n" +
                "Zawsze stosuj te rygorystyczne zasady formatowania, gdy wyszukujesz (SelectEntities) lub modyfikujesz (ModifyProperties) obiekty graficzne:\n" +
                "- Color (Kolor): Przyjmuje 3 formaty. 1) Zależne od struktury: 256 (ByLayer), 0 (ByBlock). 2) Standardowe kolory ACI (tylko liczby całkowite): 1=Czerwony, 2=Żółty, 3=Zielony, 4=Cyjan, 5=Niebieski, 6=Magenta, 7=Biały/Czarny, 8=Szary. 3) Paleta RGB (TrueColor): Format stringa 'R,G,B' (np. '255,128,0'). Aby znaleźć *dowolny* obiekt o zdefiniowanym własnym kolorze RGB, użyj filtru zawiera przecinek: {\"Prop\": \"Color\", \"Op\": \"contains\", \"Val\": \",\"}.\n" +
                "- LineWeight (Grubość Linii): NIE używaj standardowych ułamków! Wartości specjalne: -1 (ByLayer), -2 (ByBlock), -3 (Default). Konkretne grubości podaje się w setnych częściach milimetra jako liczby całkowite (np. wartość 25 oznacza 0.25 mm, a 50 to 0.50 mm).\n" +
                "- Transparency (Przezroczystość): Przyjmuje wartości tekstowe 'ByLayer', 'ByBlock' lub wartości numeryczne od 0 (całkowity brak przezroczystości, lita bryła) do 90 (maksymalna dopuszczalna przezroczystość).\n" +
                "- Linetype (Rodzaj Linii), Material, PlotStyleName: Zawsze wartości tekstowe, np. 'ByLayer', 'ByBlock', 'Continuous'.\n" +
                "- Percepcja Wizualna: Jeśli użytkownik prosi o obiekty, które 'wyglądają na', 'wyświetlają się' lub 'są widoczne' w danym kolorze/grubości, MUSISZ użyć wirtualnych właściwości silnika: 'VisualColor', 'VisualLinetype', 'VisualLineWeight'. Sprawdzają one, jak obiekt faktycznie renderuje się na ekranie (rozwiązując dziedziczenie z warstwy ByLayer).\n\n" +
                "--- 3. GEOMETRIA VS METADANE RYSUNKU (ZASADA KRYTYCZNA) ---\n" +
                "Musisz bezwzględnie rozróżniać Obiekty Graficzne (Geometrię leżącą fizycznie na płótnie modelu, np. Line, Circle, MText, BlockReference) od Struktury Organizacyjnej Rysunku (Metadanych zarządzających rysunkiem w tle, np. Warstwy/Layers, Style Wymiarowania, Definicje Bloków, Skale).\n" +
                "Narzędzia bazowe takie jak 'SelectEntities', 'CreateObject' i 'ModifyProperties' służą WYŁĄCZNIE do manipulacji fizyczną geometrią modelu.\n" +
                "ABSOLUTNIE ZABRONIONE JEST używanie narzędzi bazowych do tworzenia lub edycji metadanych (np. używanie CreateObject do zrobienia nowej warstwy).\n\n" +
                "--- 4. DYNAMICZNE ODKRYWANIE NARZĘDZI (DISCOVERABILITY) ---\n" +
                "Twój domyślny, początkowy arsenał (tools) zawiera tylko potężne narzędzia bazowe (Core). BricsCAD posiada jednak dziesiątki zaawansowanych, uśpionych pakietów narzędzi (np. do zarządzania strukturą warstw, edycji atrybutów, manipulacji skalami opisowymi).\n" +
                "Jeśli użytkownik prosi Cię o operację, do której NIE WIDZISZ gotowego narzędzia w swojej liście 'tools' (np. prosi o zablokowanie warstwy), ZABRONIONE JEST ZGADYWANIE jego nazwy i parametrów.\n" +
                "Zamiast tego MUSISZ w pierwszym kroku wywołać 'RequestAdditionalTools'. Jeśli wiesz jakiego narzędzia brakuje (np. pamiętasz 'ManageLayers'), użyj od razu akcji 'LoadCategory'. Jeśli nie wiesz, użyj 'ListCategories', aby pobrać katalog uśpionych narzędzi.";

            _conversationHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = systemPrompt }
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
            
            datasetStudio = new DatasetStudioControl();

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

            // Dodajemy widoki
            tabControl.TabPages.Add(tabChat);
            tabControl.TabPages.Add(tabDev);
            tabControl.TabPages.Add(tabBenchmark);
            tabControl.TabPages.Add(tabTester);
            tabControl.TabPages.Add(tabTags);
            
            var tabDataset = new TabPage("💾 Dataset Studio");
            tabDataset.Controls.Add(datasetStudio);
            tabControl.TabPages.Add(tabDataset);

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
            
            // WYMUSZENIE ODŚWIEŻENIA W LOCIE
            _orchestrator.Initialize();
            RebuildSystemPrompt();
            
            MessageBox.Show("Konfiguracja narzędzi została zapisana i zaaplikowana w locie!", "Agent AI V2", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            AgentMemoryState.Clear();
            AgentMemoryState.Variables.Clear();
            RebuildSystemPrompt();
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

        private void UpdateStatsHUD(LLMStats stats)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<LLMStats>(UpdateStatsHUD), stats);
                return;
            }
            _lastStats = stats;
            lblStats.Text = $"Czas: {stats.TotalTimeMs}ms | In: {stats.PromptTokens} | Out: {stats.CompletionTokens} | T/s: {stats.TokensPerSecond:F1}";
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

                // --- DATASET STUDIO INTEGRATION ---
                try
                {
                    // KRYTYCZNE: Izolacja snapshotu przez głęboką kopię listy
                    var historySnapshot = new List<ChatMessage>(_conversationHistory);
                    var toolsSnapshot = _orchestrator.GetToolsPayload(extractedTags);
                    datasetStudio.AddSessionRecord($"[{DateTime.Now:HH:mm:ss}] {rawInput}", historySnapshot, toolsSnapshot, _lastStats);
                }
                catch { /* Silent fail for dataset studio integration */ }

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
