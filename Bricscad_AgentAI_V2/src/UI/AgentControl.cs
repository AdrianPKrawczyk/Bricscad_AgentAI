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
using Newtonsoft.Json.Linq;
using System.Linq;
using Application = Bricscad.ApplicationServices.Application;
using Bricscad_AgentAI_V2.UI.Forms;
using Bricscad_AgentAI_V2.UI.KnowledgeBase;

namespace Bricscad_AgentAI_V2.UI
{
    public class AgentControl : UserControl
    {
        private TabControl tabControl;
        private CheckBox chkEarlyExit;
        private DatasetStudioControl datasetStudio;
        public DatasetStudioControl DatasetStudio => datasetStudio;
        private KnowledgeBaseControl knowledgeBaseControl;

        // --- UI Czat ---
        private RichTextBox txtHistory;
        private RichTextBox txtInput;
        private Button btnSend;
        private Button btnReset;
        private Button btnAttachFile;
        private Label lblAttachedFile;
        private string _attachedFilePath;
        private Label lblStats;
        private Label lblStatus;
        private ListBox lstAutocomplete;
        private char _lastTriggerChar = '\0';


        // --- UI Logi Narzędzi ---
        private RichTextBox txtToolLogs;
        private Button btnCopyLogs;

        // --- Silnik V2 ---
        private LLMClient _llmClient;
        public ToolOrchestrator Orchestrator => _orchestrator;
        private ToolOrchestrator _orchestrator;
        private SupervisorOrchestrator _supervisor;
        private bool isDarkMode = true;
        private string _activeModel = "LM Studio / local-model"; // Domyślny model
        private AutoBenchmarkEngine _benchmarkEngine;
        private TabPage tabBenchmark;
        private TabPage tabDebug;
        private LLMStats _lastStats;

        private RichTextBox rtbEngineLogs;
        private CheckBox chkEnableTracer;
        private Button btnClearDebug;

        // --- UI Ustawienia ---
        private TabPage tabSettings;
        private TabControl tabSettingsSub;
        
        // --- UI Agenci ---
        private TabPage tabAgents;
        private TabControl tabAgentsSub;
        // Przegląd
        private TabPage tabAgentsOverview;
        private ListBox lbAgents;
        private TextBox txtAgentDescription;
        private ComboBox cbAgentPromptFile;
        private Button btnOpenAgentPromptInOverview;
        private CheckedListBox chlbAgentTools;
        private Button btnSaveAgentProfile;
        // Prompt
        private TabPage tabAgentPrompt;
        private RichTextBox txtSystemPromptEditor;
        private Button btnSaveSystemPrompt;
        private ComboBox cbPromptFile;
        // Skille
        private TabPage tabAgentSkills;
        private ListBox lbAllTools;
        private RichTextBox rtbToolSchema;

        // --- UI Diagnostyka (Logi Aplikacji) ---
        private TabPage tabDiagnosticsSub;
        private RichTextBox rtbAppLogs;
        private Button btnOpenAppLogFile;
        private Button btnClearAppLog;
        private Button btnRefreshAppLog;
        private CheckBox chkEnableAppLogging;

        public static AgentControl Instance { get; private set; }
        public string CurrentSystemPrompt { get; private set; }
        private TabPage tabChat;

        public AgentControl()
        {
            InitializeEngineV2();
            InitializeStandardUI();
            ApplyTheme();
            Instance = this;

            LLMConfigManager.OnConfigChanged += UpdateModelLabel;
            UpdateModelLabel();

            // Inicjalizacja wiadomosci powitalnych
            AppendToHistory("SYSTEM", "Bielik V2 GOLD gotowy. Zasilony przez OpenAI Tool Calling Standard.\n\n" + _orchestrator.GetRegisteredToolsInfo(), isDarkMode ? Color.Orange : Color.DarkOrange);
        }        private void InitializeEngineV2()
        {
            _orchestrator = ToolOrchestrator.Instance;
            // Inicjalizacja skanowania narzędzi odbywa się automatycznie przy pierwszym dostępie do Instance

            _llmClient = new LLMClient(_orchestrator);
            _supervisor = new SupervisorOrchestrator(_llmClient);
            
            _llmClient.OnStatusUpdate += UpdateStatusHUD;
            _llmClient.OnToolCallLogged += AppendToolLog;
            _llmClient.OnStatsUpdate += (stats) => UpdateStatsHUD(stats);

            // Subskrypcja telemetrii od odłączonych narzędzi roboczych (np. DelegateTaskTool)
            AgentTelemetry.OnStatusUpdated += UpdateStatusHUD;
            AgentTelemetry.OnToolLogged += AppendToolLog;
            AgentTelemetry.OnStatsUpdated += (stats) => UpdateStatsHUD(stats);
            AgentTelemetry.OnDatasetRecordAdded += (s, e) => {
                datasetStudio?.AddSessionRecord(e.Title, e.HistorySnapshot, e.ToolsSnapshot, e.Stats);
            };

            _benchmarkEngine = new AutoBenchmarkEngine(_llmClient);

            RebuildSystemPrompt();
        }

        private void UpdateModelLabel()
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(UpdateModelLabel));
                return;
            }
            var config = LLMConfigManager.GetActiveProvider();
            _activeModel = config != null ? $"{config.Name} / {config.ModelName}" : "Brak dostawcy";
            if (lblStatus != null)
            {
                UpdateStatusHUD("Gotowy.");
            }
        }

        private void RebuildSystemPrompt()
        {
            string dllDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = System.IO.Path.Combine(dllDir, "system_prompt.txt");

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    CurrentSystemPrompt = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd odczytu system_prompt.txt: {ex.Message}");
                    LoadEmbeddedSystemPrompt();
                }
            }
            else
            {
                LoadEmbeddedSystemPrompt();
                try
                {
                    System.IO.File.WriteAllText(filePath, CurrentSystemPrompt, System.Text.Encoding.UTF8);
                }
                catch { }
            }

            _supervisor?.ClearHistory();

            if (txtSystemPromptEditor != null)
            {
                if (txtSystemPromptEditor.InvokeRequired)
                {
                    txtSystemPromptEditor.Invoke(new Action(() => txtSystemPromptEditor.Text = CurrentSystemPrompt));
                }
                else
                {
                    txtSystemPromptEditor.Text = CurrentSystemPrompt;
                }
            }
        }

        private void LoadEmbeddedSystemPrompt()
        {
            CurrentSystemPrompt = "Jesteś asystentem BricsCAD (Bielik V2 GOLD). Działaj precyzyjnie używając narzędzi. Komunikuj się WYŁĄCZNIE poprzez natywne wywołania funkcji (tool_calls). ZABRONIONE jest wypisywanie wywołań w zwykłym tekście.\n\n" +
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
                "If użytkownik prosi Cię o operację, do której NIE WIDZISZ gotowego narzędzia w swojej liście 'tools' (np. prosi o zablokowanie warstwy), ZABRONIONE JEST ZGADYWANIE jego nazwy i parametrów.\n" +
                "Zamiast tego MUSISZ w pierwszym kroku wywołać 'RequestAdditionalTools'. Jeśli wiesz jakiego narzędzia brakuje (np. pamiętasz 'ManageLayers'), użyj od razu akcji 'LoadCategory'. Jeśli nie wiesz, użyj 'ListCategories', aby pobrać katalog uśpionych narzędzi.";
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
            lstAutocomplete.Items.Clear();
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

            Button btnSettings = new Button
            {
                Text = "⚙️",
                Dock = DockStyle.Right,
                Width = 40,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += (s, e) => {
                var dialog = new Forms.LLMConfigDialog();
                dialog.ShowDialog();
            };

            btnAttachFile = new Button
            {
                Text = "📎",
                Dock = DockStyle.Left,
                Width = 40,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnAttachFile.FlatAppearance.BorderSize = 0;
            btnAttachFile.Click += BtnAttachFile_Click;

            lblAttachedFile = new Label
            {
                Text = "",
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = Color.Orange,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 5, 0, 0),
                Visible = false
            };

            panInput.Controls.Add(lblAttachedFile);
            panInput.Controls.Add(btnAttachFile);
            panInput.Controls.Add(chkEarlyExit);
            panInput.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panInput.Controls.Add(btnSettings);
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
            // ZAKŁADKA 5: AGENCI (Przegląd, Prompt, Skille)
            // ==========================================
            tabAgents = new TabPage("🤖 Agenci");
            tabAgentsSub = new TabControl { Dock = DockStyle.Fill };
            
            // PODZAKŁADKA 1: Przegląd
            tabAgentsOverview = new TabPage("👨‍💻 Przegląd");
            lbAgents = new ListBox
            {
                Dock = DockStyle.Left,
                Width = 200,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font(this.Font.FontFamily, 11f, FontStyle.Regular),
                IntegralHeight = false
            };
            
            Panel panAgentOverviewRight = new Panel { Dock = DockStyle.Fill };
            
            // Górny panel opisu i wyboru promptu
            Panel panAgentOverviewTop = new Panel { Dock = DockStyle.Top, Height = 130, Padding = new Padding(10) };
            txtAgentDescription = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 80,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical
            };
            
            Panel panAgentPromptSelection = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 10, 0, 0) };
            Label lblAgentPrompt = new Label { Text = "Plik promptu:", Dock = DockStyle.Left, ForeColor = Color.White, Width = 90, TextAlign = ContentAlignment.MiddleLeft };
            cbAgentPromptFile = new ComboBox { Dock = DockStyle.Left, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbAgentPromptFile.Items.AddRange(new object[] { "system_prompt.txt", "system_prompt_supervisor.txt", "system_prompt_math.txt" });
            
            btnOpenAgentPromptInOverview = new Button { Text = "📝 Otwórz w Notatniku", Dock = DockStyle.Left, Width = 150, Margin = new Padding(10, 0, 0, 0), BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOpenAgentPromptInOverview.Click += BtnOpenAgentPromptInOverview_Click;

            panAgentPromptSelection.Controls.Add(btnOpenAgentPromptInOverview);
            panAgentPromptSelection.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panAgentPromptSelection.Controls.Add(cbAgentPromptFile);
            panAgentPromptSelection.Controls.Add(lblAgentPrompt);
            
            panAgentOverviewTop.Controls.Add(panAgentPromptSelection);
            panAgentOverviewTop.Controls.Add(txtAgentDescription);
            
            // Dolny panel skilli
            Panel panAgentOverviewBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Label lblAgentSkills = new Label { Text = "Przypisane Skille (Dozwolone Narzędzia):", Dock = DockStyle.Top, ForeColor = Color.White, Height = 25 };
            chlbAgentTools = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                CheckOnClick = true
            };
            // Wypełniamy listę wszystkich skilli raz
            foreach (var key in ToolConfigManager.GetAllSettings().Keys) chlbAgentTools.Items.Add(key);

            btnSaveAgentProfile = new Button
            {
                Text = "💾 Zapisz Profil Agenta",
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveAgentProfile.Click += BtnSaveAgentProfile_Click;

            panAgentOverviewBottom.Controls.Add(chlbAgentTools);
            panAgentOverviewBottom.Controls.Add(lblAgentSkills);
            panAgentOverviewBottom.Controls.Add(btnSaveAgentProfile);

            panAgentOverviewRight.Controls.Add(panAgentOverviewBottom);
            panAgentOverviewRight.Controls.Add(panAgentOverviewTop);

            tabAgentsOverview.Controls.Add(panAgentOverviewRight);
            tabAgentsOverview.Controls.Add(new Splitter() { Dock = DockStyle.Left, Width = 5, BackColor = Color.FromArgb(45, 45, 45) });
            tabAgentsOverview.Controls.Add(lbAgents);

            // Inicjalizacja profili po zbudowaniu prawej strony
            lbAgents.SelectedIndexChanged += LbAgents_SelectedIndexChanged;
            
            // Zasilenie profili
            var profiles = ToolConfigManager.GetProfiles();
            foreach (var key in profiles.Keys)
            {
                lbAgents.Items.Add(key);
            }
            if (lbAgents.Items.Count > 0) lbAgents.SelectedIndex = 0;

            // PODZAKŁADKA 2: Prompt
            tabAgentPrompt = new TabPage("📝 Prompt");
            Panel panAgentsPromptTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 45) };
            Label lblPromptTitle = new Label 
            { 
                Text = "Wybierz profil:", 
                Dock = DockStyle.Left, 
                ForeColor = Color.White, 
                Font = new Font(this.Font, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 100
            };

            cbPromptFile = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var key in profiles.Keys) cbPromptFile.Items.Add(key);
            cbPromptFile.SelectedIndexChanged += CbPromptFile_SelectedIndexChanged;

            btnSaveSystemPrompt = new Button 
            { 
                Text = "💾 Zapisz Prompt", 
                Width = 120, 
                Dock = DockStyle.Right,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(this.Font, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSaveSystemPrompt.Click += BtnSaveSystemPrompt_Click;

            panAgentsPromptTop.Controls.Add(cbPromptFile);
            panAgentsPromptTop.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panAgentsPromptTop.Controls.Add(lblPromptTitle);
            panAgentsPromptTop.Controls.Add(btnSaveSystemPrompt);

            txtSystemPromptEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            tabAgentPrompt.Controls.Add(txtSystemPromptEditor);
            tabAgentPrompt.Controls.Add(panAgentsPromptTop);
            
            if (cbPromptFile.Items.Count > 0) cbPromptFile.SelectedIndex = 0;

            // PODZAKŁADKA 3: Skille / Narzędzia (Leksykon)
            tabAgentSkills = new TabPage("🛠️ Leksykon Skilli");
            lbAllTools = new ListBox
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font(this.Font.FontFamily, 10f, FontStyle.Regular),
                IntegralHeight = false
            };
            rtbToolSchema = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Both
            };
            
            // Wypełnienie listy narzędzi
            var allToolsSettings = ToolConfigManager.GetAllSettings();
            foreach (var key in allToolsSettings.Keys)
            {
                lbAllTools.Items.Add(key);
            }
            lbAllTools.SelectedIndexChanged += LbAllTools_SelectedIndexChanged;
            if (lbAllTools.Items.Count > 0) lbAllTools.SelectedIndex = 0;

            tabAgentSkills.Controls.Add(rtbToolSchema);
            tabAgentSkills.Controls.Add(new Splitter() { Dock = DockStyle.Left, Width = 5, BackColor = Color.FromArgb(45, 45, 45) });
            tabAgentSkills.Controls.Add(lbAllTools);

            // Dodajemy podzakładki do Agenci
            tabAgentsSub.TabPages.Add(tabAgentsOverview);
            tabAgentsSub.TabPages.Add(tabAgentPrompt);
            tabAgentsSub.TabPages.Add(tabAgentSkills);
            tabAgents.Controls.Add(tabAgentsSub);

            // Dodajemy widoki
            tabControl.TabPages.Add(tabChat);
            tabControl.TabPages.Add(tabDev);
            tabControl.TabPages.Add(tabBenchmark);
            tabControl.TabPages.Add(tabTester);
            tabControl.TabPages.Add(tabAgents);
            
            var tabDataset = new TabPage("💾 Dataset Studio");
            tabDataset.Controls.Add(datasetStudio);
            tabControl.TabPages.Add(tabDataset);

            var tabKnowledgeBase = new TabPage("📚 Baza Wiedzy AI");
            knowledgeBaseControl = new KnowledgeBaseControl();
            tabKnowledgeBase.Controls.Add(knowledgeBaseControl);
            tabControl.TabPages.Add(tabKnowledgeBase);

            // ==========================================
            // ZAKŁADKA 7: DEBUG (ENGINE TRACER)
            // ==========================================
            tabDebug = new TabPage("🐛 Debug (Engine)");
            Panel panDebugTop = new Panel { Dock = DockStyle.Top, Height = 35, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 45) };
            
            chkEnableTracer = new CheckBox 
            { 
                Text = "Śledź zdarzenia bazy Teigha", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Dock = DockStyle.Left 
            };
            chkEnableTracer.CheckedChanged += (s, e) => EngineTracer.Enable(chkEnableTracer.Checked);

            btnClearDebug = new Button 
            { 
                Text = "Wyczyść logi", 
                Width = 100, 
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClearDebug.Click += (s, e) => rtbEngineLogs.Clear();

            panDebugTop.Controls.Add(chkEnableTracer);
            panDebugTop.Controls.Add(btnClearDebug);

            rtbEngineLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None
            };

            tabDebug.Controls.Add(rtbEngineLogs);
            tabDebug.Controls.Add(panDebugTop);
            tabControl.TabPages.Add(tabDebug);

            // ==========================================
            // ZAKŁADKA 8: USTAWIENIA (PANEL BAZOWY)
            // ==========================================
            tabSettings = new TabPage("⚙️ Ustawienia");
            tabSettingsSub = new TabControl { Dock = DockStyle.Fill };

            // Podzakładka Prompt została przeniesiona do tabAgents

            // ==========================================
            // PODZAKŁADKA: Diagnostyka (BielikLogger log)
            // ==========================================
            tabDiagnosticsSub = new TabPage("Diagnostyka");
            Panel panDiagnosticsTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 45) };

            chkEnableAppLogging = new CheckBox
            {
                Text = "Włącz logowanie debugowania",
                AutoSize = true,
                ForeColor = Color.White,
                Dock = DockStyle.Left,
                Checked = BielikLogger.IsEnabled
            };
            chkEnableAppLogging.CheckedChanged += (s, e) => BielikLogger.IsEnabled = chkEnableAppLogging.Checked;

            btnRefreshAppLog = new Button
            {
                Text = "🔄 Odśwież log",
                Width = 100,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRefreshAppLog.Click += (s, e) => RefreshAppLogView();

            btnClearAppLog = new Button
            {
                Text = "🗑️ Wyczyść",
                Width = 90,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearAppLog.Click += (s, e) => {
                if (MessageBox.Show("Czy na pewno chcesz wyczyścić plik logu debugowania?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    BielikLogger.ClearLog();
                    RefreshAppLogView();
                }
            };

            btnOpenAppLogFile = new Button
            {
                Text = "📂 Otwórz plik logu",
                Width = 130,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOpenAppLogFile.Click += (s, e) => {
                try
                {
                    string path = BielikLogger.GetLogPath();
                    if (System.IO.File.Exists(path))
                    {
                        System.Diagnostics.Process.Start("notepad.exe", path);
                    }
                    else
                    {
                        MessageBox.Show("Plik logu jeszcze nie istnieje. Zostanie utworzony po zapisaniu pierwszych logów.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd otwierania logu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            panDiagnosticsTop.Controls.Add(chkEnableAppLogging);
            panDiagnosticsTop.Controls.Add(btnOpenAppLogFile);
            panDiagnosticsTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panDiagnosticsTop.Controls.Add(btnClearAppLog);
            panDiagnosticsTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panDiagnosticsTop.Controls.Add(btnRefreshAppLog);

            rtbAppLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            tabDiagnosticsSub.Controls.Add(rtbAppLogs);
            tabDiagnosticsSub.Controls.Add(panDiagnosticsTop);
            tabSettingsSub.TabPages.Add(tabDiagnosticsSub);

            tabSettingsSub.SelectedIndexChanged += (s, e) => {
                if (tabSettingsSub.SelectedTab == tabDiagnosticsSub)
                {
                    RefreshAppLogView();
                }
            };

            // ==========================================
            // PODZAKŁADKA: Ścieżki i Dane
            // ==========================================
            TabPage tabPathsSub = new TabPage("Ścieżki i Dane");
            tabPathsSub.BackColor = Color.FromArgb(45, 45, 45);
            tabPathsSub.ForeColor = Color.White;
            
            Label lblPathsTitle = new Label { Text = "Konfiguracja Bazy Wiedzy (CustomKnowledge)", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Padding = new Padding(10,10,0,0) };
            
            Panel panPathSetup = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            Label lblPathCurrent = new Label { Text = "Aktualny folder Bazy Wiedzy:", Left = 10, Top = 10, Width = 180 };
            TextBox txtCurrentPath = new TextBox { Left = 200, Top = 8, Width = 400, ReadOnly = true, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGray };
            txtCurrentPath.Text = AppPaths.GetCustomKnowledgePath();
            
            Button btnChangePath = new Button { Text = "📂 Wybierz inny folder...", Left = 610, Top = 7, Width = 150, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            
            panPathSetup.Controls.Add(lblPathCurrent);
            panPathSetup.Controls.Add(txtCurrentPath);
            panPathSetup.Controls.Add(btnChangePath);

            Label lblPathInfo = new Label { Text = "Domyślnie agent zapisuje wyuczone formuły i makra w folderze systemowym AppData. Możesz zmienić ten folder na np. swój dysk w chmurze (OneDrive/Dropbox), aby synchronizować bazę wiedzy między komputerami.", Dock = DockStyle.Top, Height = 60, Padding = new Padding(10), ForeColor = Color.DarkGray };

            tabPathsSub.Controls.Add(panPathSetup);
            tabPathsSub.Controls.Add(lblPathInfo);
            tabPathsSub.Controls.Add(lblPathsTitle);
            
            btnChangePath.Click += (s, e) => {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Wybierz folder docelowy dla Bazy Wiedzy (CustomKnowledge):";
                    if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        string oldPath = AppPaths.GetCustomKnowledgePath();
                        string newPath = fbd.SelectedPath;
                        
                        if (oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase)) return;

                        bool shouldCopy = false;
                        if (MessageBox.Show("Zmieniono folder Bazy Wiedzy.\n\nCzy chcesz przenieść (skopiować) istniejące formuły i makra ze starego folderu do nowego?", "Kopiowanie danych", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            shouldCopy = true;
                        }

                        UISettingsManager.Settings.CustomKnowledgePath = newPath;
                        UISettingsManager.Save();
                        txtCurrentPath.Text = newPath;

                        if (shouldCopy)
                        {
                            try
                            {
                                if (System.IO.Directory.Exists(oldPath))
                                {
                                    // Kopiowanie podkatalogów (Formulas, Macros)
                                    foreach (string dirPath in System.IO.Directory.GetDirectories(oldPath, "*", System.IO.SearchOption.AllDirectories))
                                    {
                                        System.IO.Directory.CreateDirectory(dirPath.Replace(oldPath, newPath));
                                    }
                                    foreach (string newFilePath in System.IO.Directory.GetFiles(oldPath, "*.*", System.IO.SearchOption.AllDirectories))
                                    {
                                        System.IO.File.Copy(newFilePath, newFilePath.Replace(oldPath, newPath), true);
                                    }
                                    MessageBox.Show("Dane zostały poprawnie skopiowane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                            catch(Exception ex)
                            {
                                MessageBox.Show($"Wystąpił błąd podczas kopiowania plików: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        
                        // Odświeżenie systemu
                        Bricscad_AgentAI_V2.Core.DynamicSystems.DynamicFormulaManager.LoadAndCompileAll();
                        Bricscad_AgentAI_V2.Core.DynamicSystems.MacroManager.LoadAllMacros();
                        if (tabKnowledgeBase != null && tabControl.TabPages.Contains(tabKnowledgeBase))
                        {
                            knowledgeBaseControl.LoadData();
                        }
                        MessageBox.Show("Ścieżka do bazy wiedzy została zaktualizowana.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            tabSettingsSub.TabPages.Add(tabPathsSub);

            tabSettings.Controls.Add(tabSettingsSub);
            tabControl.TabPages.Add(tabSettings);


            // Rejestracja callbacku
            EngineTracer.SetLogCallback(AppendEngineLog);

            this.Controls.Add(tabControl);
            
            // Załadowanie początkowych danych do bazy wiedzy
            knowledgeBaseControl.LoadData();
        }


        private void RefreshAppLogView()
        {
            try
            {
                if (rtbAppLogs != null)
                {
                    rtbAppLogs.Text = BielikLogger.ReadLastLines(150);
                    rtbAppLogs.SelectionStart = rtbAppLogs.Text.Length;
                    rtbAppLogs.ScrollToCaret();
                }
            }
            catch { }
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

            if (txtSystemPromptEditor != null)
            {
                txtSystemPromptEditor.BackColor = bgMain;
                txtSystemPromptEditor.ForeColor = fgText;
            }

            if (rtbAppLogs != null)
            {
                rtbAppLogs.BackColor = isDarkMode ? Color.Black : Color.FromArgb(245, 245, 245);
                rtbAppLogs.ForeColor = isDarkMode ? Color.LightGray : Color.Black;
            }

            if (tabSettingsSub != null)
            {
                foreach (TabPage page in tabSettingsSub.TabPages) page.BackColor = bgMain;
                foreach (TabPage page in tabAgentsSub.TabPages) page.BackColor = bgMain;
            }
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

            string textSoFar = txtInput.Text.Substring(0, index);
            int lastHashIndex = textSoFar.LastIndexOf('#');
            int lastDollarIndex = textSoFar.LastIndexOf('$');

            int activeIndex = Math.Max(lastHashIndex, lastDollarIndex);
            if (activeIndex == -1)
            {
                lstAutocomplete.Visible = false;
                return;
            }

            _lastTriggerChar = textSoFar[activeIndex];
            string searchString = textSoFar.Substring(activeIndex).ToLower();

            if (searchString.Contains(" "))
            {
                lstAutocomplete.Visible = false;
                return;
            }

            List<string> options = new List<string>();
            if (_lastTriggerChar == '#')
            {
                options.Add("#core");
                options.Add("#all");
                options.AddRange(ToolConfigManager.GetAvailableCategories().Select(c => c.StartsWith("#") ? c : "#" + c));
            }
            else if (_lastTriggerChar == '$')
            {
                options.AddRange(RecipeManager.GetAll().Select(r => "$" + r.Trigger));
            }

            var matches = options.Where(o => o.StartsWith(searchString, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count > 0)
            {
                lstAutocomplete.BeginUpdate();
                lstAutocomplete.Items.Clear();
                foreach (var m in matches) lstAutocomplete.Items.Add(m);
                lstAutocomplete.EndUpdate();
                lstAutocomplete.SelectedIndex = 0;

                Point pos = txtInput.GetPositionFromCharIndex(activeIndex);
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

        private void InsertSelectedTag()
        {
            if (lstAutocomplete.SelectedItem == null) return;

            string tag = lstAutocomplete.SelectedItem.ToString();
            int caretIndex = txtInput.SelectionStart;

            string textSoFar = txtInput.Text.Substring(0, caretIndex);
            int triggerIndex = textSoFar.LastIndexOf(_lastTriggerChar);

            if (triggerIndex != -1)
            {
                txtInput.Select(triggerIndex, caretIndex - triggerIndex);
                txtInput.SelectedText = tag + " ";
            }

            lstAutocomplete.Visible = false;
            txtInput.Focus();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            AgentMemoryState.Clear();
            AgentMemoryState.Variables.Clear();
            ToolConfigManager.SessionDynamicTags.Clear();
            _supervisor?.ClearHistory();
            RebuildSystemPrompt();
            AppendToHistory("SYSTEM", "Konwersacja i pamięć zresetowane.", isDarkMode ? Color.Orange : Color.DarkOrange);
        }

        public void UpdateStatusHUD(string status)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(UpdateStatusHUD), status);
                return;
            }
            lblStatus.Text = $"[Model: {_activeModel}] {status}";
        }

        public void UpdateStatsHUD(LLMStats stats)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<LLMStats>(UpdateStatsHUD), stats);
                return;
            }
            _lastStats = stats;
            lblStats.Text = $"Czas: {stats.TotalTimeMs}ms | In: {stats.PromptTokens} | Out: {stats.CompletionTokens} | T/s: {stats.TokensPerSecond:F1}";
        }

        public void AppendToolLog(string rawJsonCall)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(AppendToolLog), rawJsonCall);
                return;
            }
            txtToolLogs.AppendText($"\n--- WYWOŁANIE [{DateTime.Now:HH:mm:ss}] ---\n");
            txtToolLogs.AppendText(rawJsonCall + "\n");
            txtToolLogs.SelectionStart = txtToolLogs.Text.Length;
            txtToolLogs.ScrollToCaret();
        }

        public async Task ProcessInputAsync(string rawInput)
        {
            if (string.IsNullOrEmpty(rawInput) && string.IsNullOrEmpty(_attachedFilePath)) return;

            string attachedFilePath = _attachedFilePath;
            _attachedFilePath = null;
            if (lblAttachedFile != null)
            {
                lblAttachedFile.Visible = false;
                lblAttachedFile.Text = "";
            }

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

            object payload = cleanMsg;

            if (!string.IsNullOrEmpty(attachedFilePath))
            {
                try
                {
                    string ext = System.IO.Path.GetExtension(attachedFilePath).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                    {
                        string base64 = FileExtractor.GetImageBase64(attachedFilePath);
                        var visionContent = new List<VisionContentPart>
                        {
                            new VisionContentPart { Type = "text", Text = cleanMsg },
                            new VisionContentPart
                            {
                                Type = "image_url",
                                ImageUrl = new VisionImageUrl { Url = base64 }
                            }
                        };
                        payload = visionContent;
                    }
                    else
                    {
                        string text = FileExtractor.ExtractText(attachedFilePath);
                        cleanMsg += $"\n\n[ZAŁĄCZNIK: {System.IO.Path.GetFileName(attachedFilePath)}]\n{text}";
                        payload = cleanMsg;
                    }
                }
                catch (Exception ex)
                {
                    AppendToHistory("BŁĄD ZAŁĄCZNIKA", ex.Message, Color.LightCoral);
                    btnSend.Enabled = true;
                    return; // Przerywamy przetwarzanie w przypadku błędu parsowania
                }
            }

            // 2. Instant Recipe Execution ($trigger$)
            var recipeMatch = System.Text.RegularExpressions.Regex.Match(rawInput, @"^\$(\w+)\$$");
            if (recipeMatch.Success)
            {
                string trigger = recipeMatch.Groups[1].Value;
                var recipe = RecipeManager.GetByTrigger(trigger);
                if (recipe != null)
                {
                    AppendToHistory("TY", rawInput, isDarkMode ? Color.LightSkyBlue : Color.Blue);
                    AppendToHistory("SYSTEM", $"Wywoływanie recepty: {trigger}...", Color.Orange);
                    
                    await Task.Run(() => ExecuteRecipeDirectly(recipe));
                    return;
                }
            }

            AppendToHistory("TY", rawInput, isDarkMode ? Color.LightSkyBlue : Color.Blue);
            if (!string.IsNullOrEmpty(attachedFilePath))
            {
                AppendToHistory("SYSTEM", $"Dołączono plik: {System.IO.Path.GetFileName(attachedFilePath)}", Color.Orange);
            }

            btnSend.Enabled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;

            // _conversationHistory zostaje usunięte - wszystko leci przez Supervisora
            UpdateStatusHUD("Oczekiwanie na analizę przez Supervisora...");

            try
            {
                string aiResponse = await Task.Run(async () => 
                {
                    var result = await _supervisor.ProcessInputAsync(payload, new CadExecutionContext(doc));
                    return result.DisplayMessage;
                });

                AppendToHistory("BIELIK (Supervisor)", aiResponse, isDarkMode ? Color.LightGreen : Color.DarkGreen);

                // --- DATASET STUDIO INTEGRATION ---
                try
                {
                    // KRYTYCZNE: Izolacja snapshotu przez głęboką kopię listy
                    var historySnapshot = new List<ChatMessage>(_supervisor.GetHistory());
                    var toolsSnapshot = _orchestrator.GetToolsPayloadForProfile("SupervisorProfile");
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

        private void BtnAttachFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Wszystkie obsługiwane|*.py;*.csv;*.txt;*.xlsx;*.xls;*.pdf;*.png;*.jpg;*.jpeg|Wszystkie pliki|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _attachedFilePath = ofd.FileName;
                    lblAttachedFile.Text = $"Załącznik: {System.IO.Path.GetFileName(_attachedFilePath)}";
                    lblAttachedFile.Visible = true;
                }
            }
        }

        public void AppendToHistory(string sender, string message, Color color)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, string, Color>(AppendToHistory), sender, message, color);
                return;
            }

            string formattedMessage = LatexToUnicodeConverter.Convert(message);

            txtHistory.SelectionStart = txtHistory.TextLength;
            txtHistory.SelectionLength = 0;
            txtHistory.SelectionColor = color;
            txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Bold);
            txtHistory.AppendText($"[{sender}]: ");

            txtHistory.SelectionColor = isDarkMode ? Color.White : Color.Black;
            txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Regular);
            txtHistory.AppendText($"{formattedMessage}\n\n");

            txtHistory.SelectionStart = txtHistory.Text.Length;
            txtHistory.ScrollToCaret();
        }

        public void AppendEngineLog(string message)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(AppendEngineLog), message);
                return;
            }
            if (rtbEngineLogs != null)
            {
                rtbEngineLogs.AppendText(message + Environment.NewLine);
                rtbEngineLogs.SelectionStart = rtbEngineLogs.Text.Length;
                rtbEngineLogs.ScrollToCaret();
            }
        }

        private void ExecuteRecipeDirectly(AgentRecipe recipe)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            var logs = new System.Text.StringBuilder();
            int successCount = 0;
            int total = recipe.ToolExample.Count;

            using (var loc = doc.LockDocument())
            {
                foreach (var call in recipe.ToolExample)
                {
                    string name = call["function"]?["name"]?.ToString();
                    var args = call["function"]?["arguments"] as JObject;

                    if (string.IsNullOrEmpty(name) || args == null) continue;

                    string result = _orchestrator.ExecuteTool(name, args, new CadExecutionContext(doc));
                    if (result.Contains("BŁĄD"))
                    {
                        AppendToHistory("BŁĄD RECEPTY", $"Krok {successCount + 1} ({name}): {result}", Color.LightCoral);
                        return;
                    }
                    successCount++;
                }
            }

            AppendToHistory("BIELIK", successCount == total 
                ? $"✅ Recepta `${recipe.Trigger}$` wykonana pomyślnie ({successCount} kroków)." 
                : $"⚠️ Recepta przerwana. Wykonano {successCount}/{total} kroków.", 
                isDarkMode ? Color.LightGreen : Color.DarkGreen);
        }
        public async void ExternalProcessPrompt(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return;
            await ProcessInputAsync(prompt);
        }

        private void LbAgents_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbAgents.SelectedItem == null) return;
            string profileName = lbAgents.SelectedItem.ToString();
            
            var profiles = ToolConfigManager.GetProfiles();
            if (profiles.TryGetValue(profileName, out var profile))
            {
                // Ustaw plik promptu w cb
                int promptIndex = cbAgentPromptFile.FindStringExact(profile.SystemPromptFile);
                if (promptIndex >= 0) cbAgentPromptFile.SelectedIndex = promptIndex;
                else if (cbAgentPromptFile.Items.Count > 0) cbAgentPromptFile.SelectedIndex = 0;

                // Odśwież zaznaczenia skilli
                for (int i = 0; i < chlbAgentTools.Items.Count; i++)
                {
                    string toolName = chlbAgentTools.Items[i].ToString();
                    bool isActive = profile.AllowedTools != null && profile.AllowedTools.Contains(toolName, StringComparer.OrdinalIgnoreCase);
                    chlbAgentTools.SetItemChecked(i, isActive);
                }
            }
        }

        private void BtnOpenAgentPromptInOverview_Click(object sender, EventArgs e)
        {
            if (cbAgentPromptFile.SelectedItem == null) return;
            string filename = cbAgentPromptFile.SelectedItem.ToString();
            string dllDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = System.IO.Path.Combine(dllDir, filename);

            if (!System.IO.File.Exists(filePath))
            {
                System.IO.File.WriteAllText(filePath, "Podstawowy prompt...", System.Text.Encoding.UTF8);
            }

            try
            {
                System.Diagnostics.Process.Start("notepad.exe", filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania pliku w Notatniku: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSaveAgentProfile_Click(object sender, EventArgs e)
        {
            if (lbAgents.SelectedItem == null || cbAgentPromptFile.SelectedItem == null) return;
            string profileName = lbAgents.SelectedItem.ToString();
            string promptFile = cbAgentPromptFile.SelectedItem.ToString();
            
            List<string> selectedTools = new List<string>();
            foreach (var item in chlbAgentTools.CheckedItems)
            {
                selectedTools.Add(item.ToString());
            }

            ToolConfigManager.UpdateAgentProfile(profileName, promptFile, selectedTools);
            MessageBox.Show($"Zaktualizowano profil: {profileName}", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LbAllTools_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbAllTools.SelectedItem == null) return;
            string toolName = lbAllTools.SelectedItem.ToString();
            
            var tools = ToolOrchestrator.Instance.GetRegisteredTools();
            var tool = tools.FirstOrDefault(t => 
                t.GetType().Name.Equals(toolName, StringComparison.OrdinalIgnoreCase) ||
                t.GetType().Name.Equals(toolName + "Tool", StringComparison.OrdinalIgnoreCase) ||
                (t.GetToolSchema()?.Function?.Name != null && t.GetToolSchema().Function.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase))
            );
            if (tool != null)
            {
                rtbToolSchema.Text = Newtonsoft.Json.JsonConvert.SerializeObject(tool.GetToolSchema(), Newtonsoft.Json.Formatting.Indented);
            }
            else
            {
                rtbToolSchema.Text = "Nie można załadować schematu dla tego narzędzia.";
            }
        }

        private void CbPromptFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbPromptFile == null || txtSystemPromptEditor == null || cbPromptFile.SelectedItem == null) return;
            
            string profileName = cbPromptFile.SelectedItem.ToString();
            var profiles = ToolConfigManager.GetProfiles();
            if (!profiles.TryGetValue(profileName, out var profile)) return;
            
            string filename = string.IsNullOrEmpty(profile.SystemPromptFile) ? "system_prompt.txt" : profile.SystemPromptFile;
            string dllDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = System.IO.Path.Combine(dllDir, filename);

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    txtSystemPromptEditor.Text = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd odczytu pliku promptu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                txtSystemPromptEditor.Text = "";
            }
        }

        private void BtnSaveSystemPrompt_Click(object sender, EventArgs e)
        {
            if (txtSystemPromptEditor == null || cbPromptFile.SelectedItem == null) return;

            string profileName = cbPromptFile.SelectedItem.ToString();
            var profiles = ToolConfigManager.GetProfiles();
            if (!profiles.TryGetValue(profileName, out var profile)) return;
            
            string filename = string.IsNullOrEmpty(profile.SystemPromptFile) ? "system_prompt.txt" : profile.SystemPromptFile;
            string newPrompt = txtSystemPromptEditor.Text;
            string dllDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = System.IO.Path.Combine(dllDir, filename);

            try
            {
                System.IO.File.WriteAllText(filePath, newPrompt, System.Text.Encoding.UTF8);
                _supervisor?.ClearHistory();
                MessageBox.Show($"Prompt ({filename}) został pomyślnie zapisany dla profilu {profileName}!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu promptu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
