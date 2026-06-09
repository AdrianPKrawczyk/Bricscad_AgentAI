using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
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
        private System.Drawing.Image _attachedClipboardImage = null;
        private Label lblStats;
        private Label lblStatus;
        private Label lblSessionInfo;
        private ListBox lstAutocomplete;
        private char _lastTriggerChar = '\0';

        // --- UI Kontekst i Sesje ---
        private TabPage tabSessions;
        private DataGridView gridSessions;
        private Button btnLoadSession;
        private Button btnDeleteSession;
        private Button btnNewSession;

        private Panel panContextBar;
        private ProgressBar pbContext;
        private Label lblContextTokens;
        private Button btnCompressContext;


        // --- UI Logi NarzÄ‚â€žĂ˘â€žËdzi ---
        private RichTextBox txtToolLogs;
        private Button btnCopyLogs;

        // --- Silnik V2 ---
        private LLMClient _llmClient;
        public ToolOrchestrator Orchestrator => _orchestrator;
        private ToolOrchestrator _orchestrator;
        private SupervisorOrchestrator _supervisor;
        private bool isDarkMode = true;
        private string _activeModel = "LM Studio / local-model"; // DomyÄÄ…Ă˘â‚¬Ĺźlny model
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
        private TabPage tabHelp;
        // PrzeglÄ‚â€žĂ˘â‚¬Â¦d
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
        private RichTextBox txtUserPromptEditor;
        private Button btnSaveSystemPrompt;
        private Button btnClearUserPrompt;
        private ComboBox cbPromptFile;
        private Label lblPromptFileInfo;
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

            LispManager.OnLispExecutionRequested += (lispId, code) =>
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => ExecuteLispFromExternal(lispId, code)));
                }
                else
                {
                    ExecuteLispFromExternal(lispId, code);
                }
            };

            // Inicjalizacja wiadomosci powitalnych
            AppendToHistory("SYSTEM", "Bielik V2 GOLD gotowy. Zasilony przez OpenAI Tool Calling Standard.\n\n" + _orchestrator.GetRegisteredToolsInfo(), isDarkMode ? Color.Orange : Color.DarkOrange);
            
            // Zachowanie przy starcie
            if (UISettingsManager.Settings.AIStartupBehavior == 2)
            {
                tabControl.SelectedTab = tabSessions;
            }
        }        private void InitializeEngineV2()
        {
            _orchestrator = ToolOrchestrator.Instance;
            // Inicjalizacja skanowania narzÄ‚â€žĂ˘â€žËdzi odbywa siÄ‚â€žĂ˘â€žË automatycznie przy pierwszym dostÄ‚â€žĂ˘â€žËpie do Instance

            _llmClient = new LLMClient(_orchestrator);
            _supervisor = new SupervisorOrchestrator(_llmClient);
            
            _llmClient.OnStatusUpdate += UpdateStatusHUD;
            _llmClient.OnToolCallLogged += AppendToolLog;
            _llmClient.OnStatsUpdate += (stats) => UpdateStatsHUD(stats);
            _llmClient.OnStatsUpdate += (stats) => UpdateTokenBar(stats.TotalTokens);

            // Subskrypcja telemetrii od odÄÄ…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â‚¬Â¦czonych narzÄ‚â€žĂ˘â€žËdzi roboczych (np. DelegateTaskTool)
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
            try
            {
                CurrentSystemPrompt = ToolConfigManager.LoadEffectivePromptForProfile("CadProfile");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad odczytu promptu CadProfile: {ex.Message}");
                LoadEmbeddedSystemPrompt();
            }

            _supervisor?.ClearHistory();
        }

        private void LoadEmbeddedSystemPrompt()
        {
            CurrentSystemPrompt = ToolConfigManager.GetDefaultCadPromptText();
        }

        private void InitializeStandardUI()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9.5f);

            tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(120, 25) };

            // ==========================================
            // ZAKÄÄ… ADKA 1: CZAT Z AI 
            // ==========================================
            tabChat = new TabPage("CZAT");

            // --- Pasek Kontekstu (Context Bar) ---
            panContextBar = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(5) };
            lblContextTokens = new Label { Dock = DockStyle.Left, AutoSize = true, Padding = new Padding(0, 0, 10, 0), Text = "Kontekst: 0/8192 (0%)", TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.LightGray };
            pbContext = new ProgressBar { Dock = DockStyle.Fill };
            btnCompressContext = new Button { Dock = DockStyle.Right, Width = 40, Text = "đź—śď¸Ź", FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnCompressContext.Click += BtnCompressContext_Click;
            panContextBar.Controls.Add(pbContext);
            panContextBar.Controls.Add(lblContextTokens);
            panContextBar.Controls.Add(btnCompressContext);


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
                Text = "WyĹ›lij\n(Ctrl+Enter)",
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
                Text = "Reset\nPamiÄ™ci",
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
                AutoSize = true, Padding = new Padding(0, 0, 10, 0),
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
            txtInput.KeyDown += TxtInput_KeyDown;

            panInput.Controls.Add(inputBorder);
            
            chkEarlyExit = new CheckBox
            {
                Text = "Tryb Szybki (Early Exit)",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.LightGray,
                Dock = DockStyle.Left,
                Padding = new Padding(10, 0, 0, 0)
            };

            Button btnSettings = new Button
            {
                Text = "đź§ ",
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
                Text = "âž•",
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

            Panel panStats = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(5, 2, 5, 2) };
            
            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"[Model: {_activeModel}] Gotowy."
            };

            lblSessionInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.Yellow,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Sesja: Brak"
            };

            lblStats = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "0ms | 0 tkn | 0 t/s"
            };

            panStats.Controls.Add(lblStats);
            panStats.Controls.Add(lblSessionInfo);
            panStats.Controls.Add(lblStatus);


            tabChat.Controls.Add(panStats);
            tabChat.Controls.Add(panContextBar);
            tabChat.Controls.Add(txtHistory);
            tabChat.Controls.Add(panInput);
            txtHistory.BringToFront();

            // ==========================================
            // ZAKÄÄ… ADKA 2: LOGI NARZÄ‚â€žĂ‹Ĺ›DZI (JSON)
            // ==========================================
            TabPage tabDev = new TabPage("Logi NarzÄ™dzi");

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
                Text = "Kopiuj do schowka",
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
            // ZAKÄÄ… ADKA 3: BENCHMARK (OCENA LLM)
            // ==========================================
            tabBenchmark = new TabPage("Benchmark");
            tabBenchmark.Controls.Add(new AutoBenchmarkControl(_benchmarkEngine));

            // ==========================================
            // ZAKÄÄ… ADKA 4: TESTER (WORKBENCH V2)
            // ==========================================
            TabPage tabTester = new TabPage("Tester");
            tabTester.Controls.Add(new AgentTesterControl(_llmClient));

            TabPage tabAutotest = new TabPage("Autotest");
            tabAutotest.Controls.Add(new AgentAutotestControl(_llmClient));

            TabPage tabAutotestChat = new TabPage("Autotest-Czat");
            tabAutotestChat.Controls.Add(new AgentQAChatControl(_llmClient));

            TabPage tabAgentChat = new TabPage("Agent-Czat");
            tabAgentChat.Controls.Add(new SubAgentChatControl());

            // ==========================================
            // ZAKÄÄ… ADKA 5: AGENCI (PrzeglÄ‚â€žĂ˘â‚¬Â¦d, Prompt, Skille)
            // ==========================================
            tabAgents = new TabPage("Agenci");
            tabAgentsSub = new TabControl { Dock = DockStyle.Fill };
            
            // PODZAKÄÄ… ADKA 1: PrzeglÄ‚â€žĂ˘â‚¬Â¦d
            tabAgentsOverview = new TabPage("PrzeglÄ…d");
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
            
            // GÄâ€šĹâ€šrny panel opisu i wyboru promptu
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
                BorderStyle = BorderStyle.None
            };
            Panel panAgentPromptSelection = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 10, 0, 0) };
            Label lblAgentPrompt = new Label { Text = "Plik promptu:", Dock = DockStyle.Left, ForeColor = Color.White, Width = 90, TextAlign = ContentAlignment.MiddleLeft };
            cbAgentPromptFile = new ComboBox { Dock = DockStyle.Left, Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
            cbAgentPromptFile.Items.AddRange(new object[] { ToolConfigManager.GetDefaultSystemPromptFile("SupervisorProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadGeometryProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadBlocksProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadMetadataProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadMathProfile"), ToolConfigManager.GetDefaultSystemPromptFile("NotesProfile"), ToolConfigManager.GetDefaultSystemPromptFile("AuditorProfile") });
            
            btnOpenAgentPromptInOverview = new Button { Text = "Prompt systemowy z repo", Dock = DockStyle.Left, AutoSize = true, Padding = new Padding(0, 0, 10, 0), Margin = new Padding(10, 0, 0, 0), BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };

            panAgentPromptSelection.Controls.Add(btnOpenAgentPromptInOverview);
            panAgentPromptSelection.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panAgentPromptSelection.Controls.Add(cbAgentPromptFile);
            panAgentPromptSelection.Controls.Add(lblAgentPrompt);
            
            panAgentOverviewTop.Controls.Add(panAgentPromptSelection);
            panAgentOverviewTop.Controls.Add(txtAgentDescription);
            
            // Dolny panel skilli
            Panel panAgentOverviewBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Label lblAgentSkills = new Label { Text = "Przypisane Skille (Dozwolone NarzÄ‚â€žĂ˘â€žËdzia):", Dock = DockStyle.Top, ForeColor = Color.White, Height = 25 };
            chlbAgentTools = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                CheckOnClick = true
            };
            // WypeÄÄ…Ă˘â‚¬Ĺˇniamy listÄ‚â€žĂ˘â€žË wszystkich skilli raz
            foreach (var key in ToolConfigManager.GetAllSettings().Keys) chlbAgentTools.Items.Add(key);

            btnSaveAgentProfile = new Button
            {
                Text = "Zapisz Profil Agenta",
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
            // PODZAKLADKA 2: Prompt
            tabAgentPrompt = new TabPage("Prompt");
            Panel panAgentsPromptTop = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 45) };
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

            btnClearUserPrompt = new Button
            {
                Text = "Wyczysc User Prompt",
                Width = 150,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(90, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearUserPrompt.Click += BtnClearUserPrompt_Click;

            btnSaveSystemPrompt = new Button
            {
                Text = "Zapisz User Prompt",
                Width = 150,
                Dock = DockStyle.Right,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(this.Font, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSaveSystemPrompt.Click += BtnSaveSystemPrompt_Click;

            lblPromptFileInfo = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                ForeColor = Color.Gainsboro,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Prompt systemowy: - | User Prompt: AppData"
            };

            panAgentsPromptTop.Controls.Add(btnClearUserPrompt);
            panAgentsPromptTop.Controls.Add(btnSaveSystemPrompt);
            panAgentsPromptTop.Controls.Add(cbPromptFile);
            panAgentsPromptTop.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panAgentsPromptTop.Controls.Add(lblPromptTitle);
            panAgentsPromptTop.Controls.Add(lblPromptFileInfo);

            SplitContainer splitPromptEditors = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260,
                BackColor = Color.FromArgb(45, 45, 45)
            };

            Panel panSystemPrompt = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            Label lblSystemPrompt = new Label { Text = "Prompt systemowy (repo, tylko odczyt)", Dock = DockStyle.Top, Height = 24, ForeColor = Color.White };
            txtSystemPromptEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                ReadOnly = true
            };
            panSystemPrompt.Controls.Add(txtSystemPromptEditor);
            panSystemPrompt.Controls.Add(lblSystemPrompt);

            Panel panUserPrompt = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            Label lblUserPrompt = new Label { Text = "Prompt uzytkownika (doprecyzowanie profilu)", Dock = DockStyle.Top, Height = 24, ForeColor = Color.White };
            txtUserPromptEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Both
            };
            panUserPrompt.Controls.Add(txtUserPromptEditor);
            panUserPrompt.Controls.Add(lblUserPrompt);

            splitPromptEditors.Panel1.Controls.Add(panSystemPrompt);
            splitPromptEditors.Panel2.Controls.Add(panUserPrompt);

            tabAgentPrompt.Controls.Add(splitPromptEditors);
            tabAgentPrompt.Controls.Add(panAgentsPromptTop);

            if (cbPromptFile.Items.Count > 0) cbPromptFile.SelectedIndex = 0;

            // PODZAKÄÄ… ADKA 3: Skille / NarzÄ‚â€žĂ˘â€žËdzia (Leksykon)
            tabAgentSkills = new TabPage("Leksykon Skilli");
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
            
            // WypeÄÄ…Ă˘â‚¬Ĺˇnienie listy narzÄ‚â€žĂ˘â€žËdzi
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

            // Dodajemy podzakÄÄ…Ă˘â‚¬Ĺˇadki do Agenci
            tabAgentsSub.TabPages.Add(tabAgentsOverview);
            tabAgentsSub.TabPages.Add(tabAgentPrompt);
            tabAgentsSub.TabPages.Add(tabAgentSkills);
            tabAgents.Controls.Add(tabAgentsSub);

            // Dodajemy widoki
            InitializeSessionTab();
            tabControl.TabPages.Add(tabSessions);
            tabControl.TabPages.Add(tabChat);
            // Przeniesiono tabDev i tabDebug do tabSettingsSub
            
            var tabTests = new TabPage("Testy");
            var tabTestsSub = new TabControl { Dock = DockStyle.Fill };
            tabTests.Controls.Add(tabTestsSub);
            tabTestsSub.TabPages.Add(tabBenchmark);
            tabTestsSub.TabPages.Add(tabTester);
            tabTestsSub.TabPages.Add(tabAutotest);
            tabTestsSub.TabPages.Add(tabAutotestChat);
            tabTestsSub.TabPages.Add(tabAgentChat);
            tabControl.TabPages.Add(tabTests);
            tabControl.TabPages.Add(tabAgents);
            
            var tabDataset = new TabPage("Dataset Studio");
            tabDataset.Controls.Add(datasetStudio);
            tabControl.TabPages.Add(tabDataset);

            var tabKnowledgeBase = new TabPage("Baza Wiedzy AI");
            knowledgeBaseControl = new KnowledgeBaseControl();
            tabKnowledgeBase.Controls.Add(knowledgeBaseControl);
            tabControl.TabPages.Add(tabKnowledgeBase);

            tabHelp = new TabPage("âť“ Pomoc");
            tabHelp.BackColor = Color.FromArgb(30, 30, 30);
            var helpCtrl = new HelpCenterControl();
            tabHelp.Controls.Add(helpCtrl);
            tabControl.TabPages.Add(tabHelp);

            // ==========================================
            // ZAKÄÄ… ADKA 7: DEBUG (ENGINE TRACER)
            // ==========================================
            tabDebug = new TabPage("Debug (Engine)");
            Panel panDebugTop = new Panel { Dock = DockStyle.Top, Height = 35, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 45) };
            
            chkEnableTracer = new CheckBox 
            { 
                Text = "ĹšledĹş zdarzenia bazy Teigha", 
                AutoSize = true, 
                ForeColor = Color.White, 
                Dock = DockStyle.Left 
            };
            chkEnableTracer.CheckedChanged += (s, e) => EngineTracer.Enable(chkEnableTracer.Checked);

            btnClearDebug = new Button 
            { 
                Text = "WyczyĹ›Ä‡ logi", 
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
            // tabControl.TabPages.Add(tabDebug); // przeniesiono do UstawieĹ„

            // ==========================================
            // ZAKÄÄ… ADKA 8: USTAWIENIA (PANEL BAZOWY)
            // ==========================================
            tabSettings = new TabPage("Ustawienia");
            tabSettingsSub = new TabControl { Dock = DockStyle.Fill };
            
            tabSettingsSub.TabPages.Add(tabDev);
            tabSettingsSub.TabPages.Add(tabDebug);

            // PodzakÄÄ…Ă˘â‚¬Ĺˇadka Prompt zostaÄÄ…Ă˘â‚¬Ĺˇa przeniesiona do tabAgents

            // ==========================================
            // PODZAKÄÄ…Ă‚ÂADKA: Diagnostyka (BielikLogger log)
            // ==========================================
            tabDiagnosticsSub = new TabPage("Diagnostyka");
            Panel panDiagnosticsTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 45) };

            chkEnableAppLogging = new CheckBox
            {
                Text = "WĹ‚Ä…cz logowanie debugowania",
                AutoSize = true,
                ForeColor = Color.White,
                Dock = DockStyle.Left,
                Checked = BielikLogger.IsEnabled
            };
            chkEnableAppLogging.CheckedChanged += (s, e) => BielikLogger.IsEnabled = chkEnableAppLogging.Checked;

            btnRefreshAppLog = new Button
            {
                Text = "OdĹ›wieĹĽ log",
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
                Text = "WyczyĹ›Ä‡",
                Width = 90,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearAppLog.Click += (s, e) => {
                if (MessageBox.Show("Czy na pewno chcesz wyczyĹ›ciÄ‡ plik logu debugowania?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    BielikLogger.ClearLog();
                    RefreshAppLogView();
                }
            };

            btnOpenAppLogFile = new Button
            {
                Text = "OtwĂłrz plik logu",
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
                        MessageBox.Show("Plik logu jeszcze nie istnieje. Zostanie utworzony po zapisaniu pierwszych logÄâ€šĹâ€šw.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"BĹ‚Ä…d otwierania logu: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            // PODZAKĹADKA: ĹšcieĹĽki i Dane
            // ==========================================
            TabPage tabPathsSub = new TabPage("ĹšcieĹĽki i Dane");
            tabPathsSub.BackColor = Color.FromArgb(45, 45, 45);
            tabPathsSub.ForeColor = Color.White;
            
            Label lblPathsTitle = new Label { Text = "Konfiguracja Bazy Wiedzy (CustomKnowledge)", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Padding = new Padding(10,10,0,0) };
            
            Panel panPathSetup = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            Label lblPathCurrent = new Label { Text = "Aktualny folder Bazy Wiedzy:", Left = 10, Top = 10, Width = 180 };
            TextBox txtCurrentPath = new TextBox { Left = 200, Top = 8, Width = 400, ReadOnly = true, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGray };
            txtCurrentPath.Text = AppPaths.GetCustomKnowledgePath();
            
            Button btnChangePath = new Button { Text = "Wybierz inny folder...", Left = 610, Top = 7, AutoSize = true, Padding = new Padding(0, 0, 10, 0), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            
            panPathSetup.Controls.Add(lblPathCurrent);
            panPathSetup.Controls.Add(txtCurrentPath);
            panPathSetup.Controls.Add(btnChangePath);

            Label lblPathInfo = new Label { Text = "DomyĹ›lnie agent zapisuje wyuczone formuĹ‚y i makra w folderze systemowym AppData. MoĹĽesz zmieniÄ‡ ten folder na np. swĂłj dysk w chmurze (OneDrive/Dropbox), aby synchronizowaÄ‡ bazÄ™ wiedzy miÄ™dzy komputerami.", Dock = DockStyle.Top, Height = 80, Padding = new Padding(10), ForeColor = Color.DarkGray };

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
                        if (MessageBox.Show("Zmieniono folder Bazy Wiedzy.\n\nCzy chcesz przenieÄÄ…Ă˘â‚¬ĹźÄ‚â€žĂ˘â‚¬Ë‡ (skopiowaÄ‚â€žĂ˘â‚¬Ë‡) istniejÄ‚â€žĂ˘â‚¬Â¦ce formuÄÄ…Ă˘â‚¬Ĺˇy i makra ze starego folderu do nowego?", "Kopiowanie danych", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
                                    // Kopiowanie podkatalogÄâ€šĹâ€šw (Formulas, Macros)
                                    foreach (string dirPath in System.IO.Directory.GetDirectories(oldPath, "*", System.IO.SearchOption.AllDirectories))
                                    {
                                        System.IO.Directory.CreateDirectory(dirPath.Replace(oldPath, newPath));
                                    }
                                    foreach (string newFilePath in System.IO.Directory.GetFiles(oldPath, "*.*", System.IO.SearchOption.AllDirectories))
                                    {
                                        System.IO.File.Copy(newFilePath, newFilePath.Replace(oldPath, newPath), true);
                                    }
                                    MessageBox.Show("Dane zostaĹ‚y poprawnie skopiowane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                            catch(Exception ex)
                            {
                                MessageBox.Show($"WystÄ…piĹ‚ bĹ‚Ä…d podczas kopiowania plikĂłw: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        
                        // OdÄÄ…Ă˘â‚¬ĹźwieÄÄ…ÄËťenie systemu
                        Bricscad_AgentAI_V2.Core.DynamicSystems.DynamicFormulaManager.LoadAndCompileAll();
                        Bricscad_AgentAI_V2.Core.DynamicSystems.MacroManager.LoadAllMacros();
                        if (tabKnowledgeBase != null && tabControl.TabPages.Contains(tabKnowledgeBase))
                        {
                            knowledgeBaseControl.LoadData();
                        }
                        MessageBox.Show("ĹšcieĹĽka do bazy wiedzy zostaĹ‚a zaktualizowana.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            tabSettingsSub.TabPages.Add(tabPathsSub);

            TabPage tabWorkflowSub = new TabPage("Workflow");
            tabWorkflowSub.Padding = new Padding(20);

            Label lblAIStartup = new Label { Text = "Zachowanie przy starcie systemu AI:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            ComboBox cmbAIStartup = new ComboBox { Location = new Point(20, 45), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAIStartup.Items.AddRange(new string[] { "Ĺaduj poprzedniÄ… sesjÄ™", "TwĂłrz nowÄ… sesjÄ™", "WybĂłr manualny" });
            cmbAIStartup.SelectedIndex = UISettingsManager.Settings.AIStartupBehavior;
            cmbAIStartup.SelectedIndexChanged += (s, e) =>
            {
                UISettingsManager.Settings.AIStartupBehavior = cmbAIStartup.SelectedIndex;
                UISettingsManager.Save();
            };

            Label lblBricsCADStartup = new Label { Text = "Zachowanie przy starcie BricsCAD:", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            ComboBox cmbBricsCADStartup = new ComboBox { Location = new Point(20, 110), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBricsCADStartup.Items.AddRange(new string[] { "Automatycznie uruchom agenta AI", "Uruchomienie manualne (komenda \"AI\")" });
            cmbBricsCADStartup.SelectedIndex = UISettingsManager.Settings.BricsCADStartupBehavior;
            cmbBricsCADStartup.SelectedIndexChanged += (s, e) =>
            {
                UISettingsManager.Settings.BricsCADStartupBehavior = cmbBricsCADStartup.SelectedIndex;
                UISettingsManager.Save();
            };

            tabWorkflowSub.Controls.Add(lblAIStartup);
            tabWorkflowSub.Controls.Add(cmbAIStartup);
            tabWorkflowSub.Controls.Add(lblBricsCADStartup);
            tabWorkflowSub.Controls.Add(cmbBricsCADStartup);

            tabSettingsSub.TabPages.Add(tabWorkflowSub);

            tabSettings.Controls.Add(tabSettingsSub);
            tabControl.TabPages.Add(tabSettings);


            // Rejestracja callbacku
            EngineTracer.SetLogCallback(AppendEngineLog);

            this.Controls.Add(tabControl);
            
            // ZaÄÄ…Ă˘â‚¬Ĺˇadowanie poczÄ‚â€žĂ˘â‚¬Â¦tkowych danych do bazy wiedzy
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
            // 1. ObsÄÄ…Ă˘â‚¬Ĺˇuga nawigacji Autocomplete (tylko gdy lista jest widoczna i pole tekstowe aktywne)
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

            // 2. ObsÄÄ…Ă˘â‚¬Ĺˇuga Ctrl+Enter dla wysyÄÄ…Ă˘â‚¬Ĺˇania wiadomoÄÄ…Ă˘â‚¬Ĺźci
            if (keyData == (Keys.Control | Keys.Enter))
            {
                btnSend_Click(btnSend, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                if (Clipboard.ContainsImage())
                {
                    // Pobranie obrazu ze schowka
                    _attachedClipboardImage = Clipboard.GetImage();
                    _attachedFilePath = null; // Czyszczenie Ĺâ€şcieĹÄ˝ki pliku, bo priorytet ma schowek
                    
                    // Aktualizacja UI
                    lblAttachedFile.Text = "Ä‘Ĺşâ€śĹ˝ [Obraz ze schowka]";
                    lblAttachedFile.Visible = true;
                    
                    // Zablokowanie domyĹâ€şlnego wklejenia (aby nie dodawaĹâ€šo siÄâ„˘ do tekstu jeĹâ€şli to RichTextBox)
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
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
            int lastSlashIndex = textSoFar.LastIndexOf('/');
            int lastPercentIndex = textSoFar.LastIndexOf('%');
            
            // Reagujemy na ukoĹ›nik tylko na poczÄ…tku linii lub po spacji/nowej linii
            if (lastSlashIndex > 0 && textSoFar[lastSlashIndex - 1] != ' ' && textSoFar[lastSlashIndex - 1] != '\n')
            {
                lastSlashIndex = -1;
            }

            int activeIndex = Math.Max(lastPercentIndex, Math.Max(lastHashIndex, Math.Max(lastDollarIndex, lastSlashIndex)));
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
                options.AddRange(SkillManager.GetAvailableSkills()
                    .Select(s => "#" + s.Id + (string.IsNullOrWhiteSpace(s.Description) ? "" : " - " + s.Description)));
            }
            else if (_lastTriggerChar == '$')
            {
                options.AddRange(RecipeManager.GetAll().Select(r => "$" + r.Trigger + (string.IsNullOrWhiteSpace(r.Description) ? "" : " - " + r.Description)));
            }
            else if (_lastTriggerChar == '/')
            {
                options.Add("/notatka - Zleca utworzenie lub aktualizacjÄ™ notatki inĹĽynierskiej");
                options.Add("/czytaj_notatke - WyĹ›wietla aktualnÄ… notatkÄ™ rysunkowÄ…");
                options.Add("/new_session - Rozpoczyna nowÄ… sesjÄ™ i czyĹ›ci pamiÄ™Ä‡");
                options.Add("/compress - Kompresuje historiÄ™ kontekstu (oszczÄ™dza tokeny)");
            }
            else if (_lastTriggerChar == '%')
            {
                options.AddRange(Core.DynamicSystems.LispManager.LoadAllLisps()
                    .Select(l => "%" + l.LispId + (string.IsNullOrWhiteSpace(l.Description) ? "" : " - " + l.Description)));
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
            int dashIndex = tag.IndexOf(" - ");
            if (dashIndex != -1)
            {
                tag = tag.Substring(0, dashIndex);
            }

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
            AppendToHistory("SYSTEM", "Konwersacja i pamiÄ‚â€žĂ˘â€žËÄ‚â€žĂ˘â‚¬Ë‡ zresetowane.", isDarkMode ? Color.Orange : Color.DarkOrange);
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
            txtToolLogs.AppendText($"\n--- WYWOÄÄ… ANIE [{DateTime.Now:HH:mm:ss}] ---\n");
            txtToolLogs.AppendText(rawJsonCall + "\n");
            txtToolLogs.SelectionStart = txtToolLogs.Text.Length;
            txtToolLogs.ScrollToCaret();
        }

        private void ExecuteLispFromExternal(string lispId, string code)
        {
            try
            {
                SaveTempLisp(code);
                AppendToHistory("SYSTEM", $"Przygotowano do wykonania skrypt z Bazy Wiedzy: '{lispId}'", isDarkMode ? Color.Orange : Color.DarkOrange);
                ExecuteLispFromTemp(lispId);
            }
            catch (Exception ex) { BielikLogger.LogError("BĹ‚Ä…d Ĺ‚adowania zewnÄ™trznego skryptu LISP", ex); }
        }

        private void SaveTempLisp(string code)
        {
            string tempDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bricscad_AgentAI", "Temp");
            System.IO.Directory.CreateDirectory(tempDir);
            string tempLispFile = System.IO.Path.Combine(tempDir, "temp_agent.lsp");
            System.IO.File.WriteAllText(tempLispFile, code);
        }

        private void ExecuteLispFromTemp(string lispId)
        {
            string tempDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bricscad_AgentAI", "Temp");
            string tempLispFile = System.IO.Path.Combine(tempDir, "temp_agent.lsp");
            if (System.IO.File.Exists(tempLispFile))
            {
                string safePath = tempLispFile.Replace("\\", "/");
                var doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    // Ĺaduje LISP do pamiÄ™ci
                    doc.SendStringToExecute($"(load \"{safePath}\") ", true, false, false);
                    // Odpala zdefiniowanÄ… komendÄ™
                    doc.SendStringToExecute($"{lispId} ", true, false, false);
                }
            }
        }

        public async Task ProcessInputAsync(string rawInput, string activeDwgPath = "")
        {
            if (string.IsNullOrEmpty(rawInput) && string.IsNullOrEmpty(_attachedFilePath) && _attachedClipboardImage == null) return;

            string attachedFilePath = _attachedFilePath;
            _attachedFilePath = null;
            
            System.Drawing.Image attachedClipboardImage = _attachedClipboardImage;
            _attachedClipboardImage = null;

            if (lblAttachedFile != null)
            {
                lblAttachedFile.Visible = false;
                lblAttachedFile.Text = "";
            }

            // 1. Semantic Tag Pre-processing (Regex)
            // WyÄÄ…Ă˘â‚¬Ĺˇuskujemy wszystkie tagi zaczynajÄ‚â€žĂ˘â‚¬Â¦ce siÄ‚â€žĂ˘â€žË od #
            var tagMatches = System.Text.RegularExpressions.Regex.Matches(rawInput, @"#\w+");
            List<string> extractedTags = new List<string>();
            string cleanMsg = rawInput;

            foreach (System.Text.RegularExpressions.Match match in tagMatches)
            {
                string tag = match.Value.ToLower();
                extractedTags.Add(tag);
                // Usuwamy tag z czystej wiadomoÄÄ…Ă˘â‚¬Ĺźci dla LLM
                cleanMsg = cleanMsg.Replace(match.Value, "").Trim();

                string skillId = tag.Substring(1);
                try
                {
                    var skill = SkillManager.GetSkill(skillId);
                    SessionManager.CurrentSession.Messages.Add(new ChatMessage { Role = "system", Content = $"[PamiÄ™Ä‡ - Wczytano Skill: {skill.Id}]\n{skill.Content}" });
                }
                catch { /* Ignoruj jeĹ›li nie znaleziono skilla */ }
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
                        cleanMsg += $"\n\n[ZAĹÂÄ„CZNIK: {System.IO.Path.GetFileName(attachedFilePath)}]\n{text}";
                        payload = cleanMsg;
                    }
                }
                catch (Exception ex)
                {
                    AppendToHistory("BÄÄ…Ă‚ÂÄ‚â€žĂ˘â‚¬ĹľD ZAÄÄ…Ă‚ÂÄ‚â€žĂ˘â‚¬ĹľCZNIKA", ex.Message, Color.LightCoral);
                    btnSend.Enabled = true;
                    return; // Przerywamy przetwarzanie w przypadku bÄÄ…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â€žËdu parsowania
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
                    AppendToHistory("SYSTEM", $"WywoÄÄ…Ă˘â‚¬Ĺˇywanie recepty: {trigger}...", Color.Orange);
                    
                    await Task.Run(() => ExecuteRecipeDirectly(recipe));
                    return;
                }
            }
            else if (attachedClipboardImage != null)
            {
                try
                {
                    string base64 = FileExtractor.GetImageBase64(attachedClipboardImage);
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
                    
                    AppendToHistory("SYSTEM", "DoĹâ€šÄâ€¦czono obraz ze schowka", Color.Orange);
                }
                catch (Exception ex)
                {
                    BielikLogger.LogError("BĹâ€šÄâ€¦d przetwarzania obrazu ze schowka", ex);
                    AppendToHistory("BĹÂÄ„D", $"Nie udaĹâ€šo siÄâ„˘ przetworzyÄâ€ˇ obrazu ze schowka: {ex.Message}", Color.Red);
                }
                finally
                {
                    attachedClipboardImage.Dispose();
                }
            }

            AppendToHistory("TY", rawInput, isDarkMode ? Color.LightSkyBlue : Color.Blue);
            if (!string.IsNullOrEmpty(attachedFilePath))
            {
                AppendToHistory("SYSTEM", $"DoĹâ€šÄâ€¦czono plik: {System.IO.Path.GetFileName(attachedFilePath)}", Color.Orange);
            }

            btnSend.Enabled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;
            SyncImpliedSelectionToAgentMemory(doc);

            // _conversationHistory zostaje usuniÄ‚â€žĂ˘â€žËte - wszystko leci przez Supervisora
            UpdateStatusHUD("Oczekiwanie na analizÄ‚â€žĂ˘â€žË przez Supervisora...");

            try
            {
                string aiResponse = await Task.Run(async () => 
                {
                    var result = await _supervisor.ProcessInputAsync(payload, new CadExecutionContext(doc), activeDwgPath);
                    return result.DisplayMessage;
                });

                AppendToHistory("(Supervisor)", aiResponse, isDarkMode ? Color.LightGreen : Color.DarkGreen);

                // --- DATASET STUDIO INTEGRATION ---
                try
                {
                    // KRYTYCZNE: Izolacja snapshotu przez gÄÄ…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â€žËbokÄ‚â€žĂ˘â‚¬Â¦ kopiÄ‚â€žĂ˘â€žË listy
                    var historySnapshot = new List<ChatMessage>(_supervisor.GetHistory());
                    var toolsSnapshot = _orchestrator.GetToolsPayloadForProfile("SupervisorProfile");
                    datasetStudio.AddSessionRecord($"[{DateTime.Now:HH:mm:ss}] {rawInput}", historySnapshot, toolsSnapshot, _lastStats);
                }
                catch { /* Silent fail for dataset studio integration */ }

                UpdateStatusHUD("Gotowy.");
            }
            catch (Exception ex)
            {
                AppendToHistory("BÄÄ…Ă‚ÂÄ‚â€žĂ˘â‚¬ĹľD", ex.Message, Color.LightCoral);
                UpdateStatusHUD("BĹ‚Ä…d krytyczny.");
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
            
            // Pobieranie aktywnego dokumentu bezpiecznie na gĹ‚Ăłwnym wÄ…tku UI
            string activeDwgPath = "";
            try {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null) activeDwgPath = doc.Name;
            } catch { }

            // Przechwytywacz Komend (Command Interceptor)
            if (userMsg.StartsWith("/"))
            {
                string[] parts = userMsg.Split(new[] { ' ' }, 2);
                string cmd = parts[0].ToLowerInvariant();
                string args = parts.Length > 1 ? parts[1] : "";

                if (cmd == "/new_session")
                {
                    BtnNewSession_Click(this, EventArgs.Empty);
                    return;
                }
                else if (cmd == "/compress")
                {
                    BtnCompressContext_Click(this, EventArgs.Empty);
                    return;
                }
                else if (cmd == "/czytaj_notatke")
                {
                    string note = DrawingNoteManager.ReadNote(activeDwgPath);
                    if (string.IsNullOrWhiteSpace(note)) note = "Brak notatki dla tego pliku.";
                    AppendToHistory("SYSTEM", $"Notatka dla {activeDwgPath}:\n{note}", isDarkMode ? Color.Orange : Color.DarkOrange);
                    return;
                }
                else if (cmd == "/notatka")
                {
                    await GenerateNoteSubAgentAsync(activeDwgPath, args);
                    return;
                }
            }

            await ProcessInputAsync(userMsg, activeDwgPath);
        }

        private async Task GenerateNoteSubAgentAsync(string activeDwgPath, string instructions)
        {
            AppendToHistory("SYSTEM", $"Uruchamiam Sub-Agenta Notatek dla: {activeDwgPath}...", Color.Orange);
            
            try
            {
                string currentNote = DrawingNoteManager.ReadNote(activeDwgPath);
                
                var recentMsgs = SessionManager.CurrentSession.Messages
                    .Where(m => m.Role != "system" && m.Content != null)
                    .Select(m => $"{m.Role}: {m.Content}")
                    .Reverse().Take(10).Reverse().ToList();
                
                string conversationContext = string.Join("\n", recentMsgs);
                
                string prompt = $@"JesteĹ› inĹĽynierem dokumentacji. PoniĹĽej znajduje siÄ™ wycinek ostatniej rozmowy uĹĽytkownika oraz obecna notatka dla rysunku {activeDwgPath}. UĹĽytkownik prosi o: {instructions}. Wygeneruj nowÄ…, kompletnÄ… zawartoĹ›Ä‡ pliku Markdown aktualizujÄ…cÄ… tÄ™ notatkÄ™. ZwrĂłÄ‡ TYLKO czysty kod Markdown.

Obecna notatka:
{currentNote}

Ostatnia rozmowa:
{conversationContext}";

                var msgs = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = prompt }
                };

                // Zablokowanie narzÄ™dzi przez puste ID profilu (albo brak wywoĹ‚ania tool)
                // UĹĽyjemy domyĹ›lnego configu klienta LLM bez profilu i przekaĹĽemy puste narzÄ™dzia
                var response = await _llmClient.SendMessageReActAsync(msgs, null, new string[0], true, 1, "EMPTY_TOOLS_PROFILE");
                
                if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.DisplayMessage))
                {
                    string markdown = response.DisplayMessage.Trim('`', '\n', '\r');
                    if (markdown.StartsWith("markdown\n")) markdown = markdown.Substring(9);
                    
                    DrawingNoteManager.SaveNote(activeDwgPath, markdown);
                    AppendToHistory("SYSTEM", $"âś… Notatka dla pliku {activeDwgPath} zostaĹ‚a zaktualizowana i zapisana na dysku.", isDarkMode ? Color.LightGreen : Color.DarkGreen);
                }
                else
                {
                    AppendToHistory("SYSTEM", "Nie udaĹ‚o siÄ™ wygenerowaÄ‡ notatki.", Color.LightCoral);
                }
            }
            catch (Exception ex)
            {
                AppendToHistory("SYSTEM", $"BĹ‚Ä…d sub-agenta notatek: {ex.Message}", Color.LightCoral);
            }
        }

        private void BtnAttachFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Wszystkie obsĹâ€šugiwane|*.py;*.csv;*.txt;*.xlsx;*.xls;*.pdf;*.png;*.jpg;*.jpeg;*.json;*.xml|Wszystkie pliki|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _attachedFilePath = ofd.FileName;
                    lblAttachedFile.Text = $"ZaĹâ€šÄâ€¦cznik: {System.IO.Path.GetFileName(_attachedFilePath)}";
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

            Color msgColor = isDarkMode ? Color.White : Color.Black;
            AppendMarkdownText(txtHistory, formattedMessage, msgColor);
            txtHistory.AppendText("\n\n");

            txtHistory.SelectionStart = txtHistory.Text.Length;
            txtHistory.ScrollToCaret();
        }

        private void AppendMarkdownText(RichTextBox rtb, string text, Color defaultColor)
        {
            // Konwersja nagĹ‚ĂłwkĂłw (#, ##, ###) na pogrubienie z zachowaniem znakĂłw #
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^#+\s+.*", "**$&**", System.Text.RegularExpressions.RegexOptions.Multiline);

            // PodziaĹ‚ tekstu na bloki. UĹĽywamy (?s:...) dla blokĂłw kodu wielolinijkowych (```), 
            // a dla reszty (.) nie Ĺ‚apie nowych linii, by uniknÄ…Ä‡ rozjeĹĽdĹĽania siÄ™ formatowania.
            var segments = System.Text.RegularExpressions.Regex.Split(text, @"((?s:```.*?```)|\*\*.*?\*\*|\*.*?\*|`.*?`)");
            
            Font regularFont = new Font(rtb.Font, FontStyle.Regular);
            Font boldFont = new Font(rtb.Font, FontStyle.Bold);
            Font italicFont = new Font(rtb.Font, FontStyle.Italic);
            Font codeFont = new Font("Consolas", rtb.Font.Size, FontStyle.Regular);

            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment)) continue;

                rtb.SelectionStart = rtb.TextLength;
                rtb.SelectionLength = 0;

                if (segment.StartsWith("```") && segment.EndsWith("```") && segment.Length >= 6)
                {
                    rtb.SelectionFont = codeFont;
                    rtb.SelectionColor = isDarkMode ? Color.LightGray : Color.DarkSlateGray;
                    rtb.SelectionBackColor = isDarkMode ? Color.FromArgb(40, 40, 40) : Color.LightGray;
                    rtb.AppendText("\n" + segment.Substring(3, segment.Length - 6).Trim('\r', '\n') + "\n");
                    rtb.SelectionBackColor = rtb.BackColor;
                }
                else if (segment.StartsWith("**") && segment.EndsWith("**") && segment.Length >= 4)
                {
                    rtb.SelectionFont = boldFont;
                    rtb.SelectionColor = defaultColor;
                    rtb.AppendText(segment.Substring(2, segment.Length - 4));
                }
                else if (segment.StartsWith("*") && segment.EndsWith("*") && segment.Length >= 2)
                {
                    rtb.SelectionFont = italicFont;
                    rtb.SelectionColor = defaultColor;
                    rtb.AppendText(segment.Substring(1, segment.Length - 2));
                }
                else if (segment.StartsWith("`") && segment.EndsWith("`") && segment.Length >= 2)
                {
                    rtb.SelectionFont = codeFont;
                    rtb.SelectionColor = isDarkMode ? Color.LightGray : Color.DarkSlateGray;
                    rtb.SelectionBackColor = isDarkMode ? Color.FromArgb(40, 40, 40) : Color.LightGray;
                    rtb.AppendText(segment.Substring(1, segment.Length - 2));
                    rtb.SelectionBackColor = rtb.BackColor;
                }
                else
                {
                    rtb.SelectionFont = regularFont;
                    rtb.SelectionColor = defaultColor;
                    rtb.AppendText(segment);
                }
            }
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
                    if (result.Contains("BÄÄ…Ă‚ÂÄ‚â€žĂ˘â‚¬ĹľD"))
                    {
                        AppendToHistory("BÄÄ…Ă‚ÂÄ‚â€žĂ˘â‚¬ĹľD RECEPTY", $"Krok {successCount + 1} ({name}): {result}", Color.LightCoral);
                        return;
                    }
                    successCount++;
                }
            }

            AppendToHistory("BIELIK", successCount == total 
                ? $"Ä‚ËĹâ€şĂ˘â‚¬Â¦ Recepta `${recipe.Trigger}$` wykonana pomyÄÄ…Ă˘â‚¬Ĺźlnie ({successCount} krokÄâ€šĹâ€šw)." 
                : $"Ä‚ËĹË‡Ă‚Â ÄĹąĂ‚Â¸ĹÄ… Recepta przerwana. Wykonano {successCount}/{total} krokÄâ€šĹâ€šw.", 
                isDarkMode ? Color.LightGreen : Color.DarkGreen);
        }
        private void SyncImpliedSelectionToAgentMemory(Document doc)
        {
            if (doc == null) return;

            try
            {
                PromptSelectionResult selRes = doc.Editor.SelectImplied();
                if (selRes.Status == PromptStatus.OK && selRes.Value != null)
                {
                    AgentMemoryState.Update(selRes.Value.GetObjectIds());
                }
            }
            catch (Exception ex)
            {
                BielikLogger.LogError("BĹ‚Ä…d synchronizacji zaznaczenia BricsCAD z pamiÄ™ciÄ… Agenta", ex);
            }
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

                // OdÄÄ…Ă˘â‚¬ĹźwieÄÄ…ÄËť zaznaczenia skilli
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
            string filePath = ToolConfigManager.GetRuntimePromptPath(filename);

            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    MessageBox.Show($"Nie znaleziono pliku promptu: {filePath}", "Brak pliku", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                System.Diagnostics.Process.Start("notepad.exe", filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹ‚Ä…d otwierania pliku w Notatniku: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                rtbToolSchema.Text = "Nie moÄÄ…ÄËťna zaÄÄ…Ă˘â‚¬ĹˇadowaÄ‚â€žĂ˘â‚¬Ë‡ schematu dla tego narzÄ‚â€žĂ˘â€žËdzia.";
            }
        }

        private void CbPromptFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbPromptFile == null || txtSystemPromptEditor == null || txtUserPromptEditor == null || cbPromptFile.SelectedItem == null) return;

            string profileName = cbPromptFile.SelectedItem.ToString();
            var profiles = ToolConfigManager.GetProfiles();
            if (!profiles.TryGetValue(profileName, out var profile)) return;

            try
            {
                txtSystemPromptEditor.Text = ToolConfigManager.LoadSystemPromptText(profile.SystemPromptFile);
                txtUserPromptEditor.Text = ToolConfigManager.GetUserPromptOverride(profileName);
                lblPromptFileInfo.Text = $"Prompt systemowy: {profile.SystemPromptFile} | User Prompt: {AppPaths.GetPromptOverrideFilePath(profileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad odczytu promptu: {ex.Message}", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSaveSystemPrompt_Click(object sender, EventArgs e)
        {
            if (txtUserPromptEditor == null || cbPromptFile.SelectedItem == null) return;

            string profileName = cbPromptFile.SelectedItem.ToString();

            try
            {
                ToolConfigManager.SaveUserPromptOverride(profileName, txtUserPromptEditor.Text);
                if (profileName.Equals("CadProfile", StringComparison.OrdinalIgnoreCase))
                {
                    RebuildSystemPrompt();
                }
                _supervisor?.ClearHistory();
                MessageBox.Show($"Zapisano prompt uzytkownika dla profilu {profileName}.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu promptu uzytkownika: {ex.Message}", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnClearUserPrompt_Click(object sender, EventArgs e)
        {
            if (cbPromptFile?.SelectedItem == null || txtUserPromptEditor == null) return;

            string profileName = cbPromptFile.SelectedItem.ToString();
            txtUserPromptEditor.Clear();
            ToolConfigManager.SaveUserPromptOverride(profileName, string.Empty);
            if (profileName.Equals("CadProfile", StringComparison.OrdinalIgnoreCase))
            {
                RebuildSystemPrompt();
            }
            _supervisor?.ClearHistory();
            MessageBox.Show($"Wyczyszczono prompt uzytkownika dla profilu {profileName}.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void InitializeSessionTab()
        {
            tabSessions = new TabPage("Sesje");
            
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
            
            btnNewSession = new Button { Text = "Nowa Sesja", Dock = DockStyle.Left, Width = 100, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnNewSession.Click += BtnNewSession_Click;
            
            btnLoadSession = new Button { Text = "Wczytaj", Dock = DockStyle.Left, Width = 80, Margin = new Padding(5, 0, 0, 0), BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnLoadSession.Click += BtnLoadSession_Click;

            btnDeleteSession = new Button { Text = "UsuĹ„", Dock = DockStyle.Right, Width = 80, BackColor = Color.Crimson, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDeleteSession.Click += BtnDeleteSession_Click;

            panTop.Controls.Add(btnLoadSession);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panTop.Controls.Add(btnNewSession);
            panTop.Controls.Add(btnDeleteSession);

            gridSessions = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Black,
                MultiSelect = false
            };

            tabSessions.Controls.Add(gridSessions);
            tabSessions.Controls.Add(panTop);

            RefreshSessionsGrid();
        }

        private void RefreshSessionsGrid()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(RefreshSessionsGrid));
                return;
            }

            var sessions = SessionManager.GetAllSessions();
            gridSessions.DataSource = sessions.Select(s => new { Id = s.Id, Tytul = s.Description, Data = s.UpdatedAt }).ToList();
        }

        private void BtnNewSession_Click(object sender, EventArgs e)
        {
            string desc = Microsoft.VisualBasic.Interaction.InputBox("Podaj opis nowej sesji:", "Nowa Sesja", "Sesja " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            if (!string.IsNullOrWhiteSpace(desc))
            {
                SessionManager.CreateNewSession(); SessionManager.CurrentSession.Description = desc; SessionManager.SaveSession();
                _supervisor?.ClearHistory();
                RefreshSessionsGrid();
                ReloadChatHistoryFromSession();
                tabControl.SelectedTab = tabChat;
            }
        }

        private void BtnLoadSession_Click(object sender, EventArgs e)
        {
            if (gridSessions.SelectedRows.Count > 0)
            {
                string id = gridSessions.SelectedRows[0].Cells["Id"].Value.ToString();
                SessionManager.LoadSession(id);
                RefreshSessionsGrid();
                ReloadChatHistoryFromSession();
                tabControl.SelectedTab = tabChat;
            }
        }

        private void BtnDeleteSession_Click(object sender, EventArgs e)
        {
            if (gridSessions.SelectedRows.Count > 0)
            {
                string id = gridSessions.SelectedRows[0].Cells["Id"].Value.ToString();
                if (MessageBox.Show("Czy na pewno usunÄ…Ä‡ tÄ™ sesjÄ™?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    SessionManager.DeleteSession(id);
                    RefreshSessionsGrid();
                    ReloadChatHistoryFromSession();
                }
            }
        }

        private void ReloadChatHistoryFromSession()
        {
            txtHistory.Clear();
            var session = SessionManager.CurrentSession;
            if (lblSessionInfo != null) lblSessionInfo.Text = $"Sesja: {session?.Description ?? session?.Id ?? "Brak"}";
            foreach (var msg in session.Messages)
            {
                if (msg.Role == "user") AppendToHistory("TY", msg.Content.ToString(), Color.LightSkyBlue);
                else AppendToHistory("BIELIK", msg.Content.ToString(), Color.LightGreen);
            }
        }

        private void BtnCompressContext_Click(object sender, EventArgs e)
        {
            // Na razie czyszczenie starej historii - potem dodamy podsumowanie LLM
            if (_supervisor != null)
            {
                var history = _supervisor.GetHistory();
                if (history.Count > 2)
                {
                    history.RemoveRange(0, history.Count - 2); // Zostaw tylko 2 ostatnie
                    SessionManager.SaveSession();
                    ReloadChatHistoryFromSession();
                    MessageBox.Show("Skompresowano kontekst (pozostawiono 2 ostatnie wiadomoĹ›ci).", "Kompresja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void UpdateTokenBar(int totalTokens)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<int>(UpdateTokenBar), totalTokens);
                return;
            }

            var provider = LLMConfigManager.GetActiveProvider();
            int maxTokens = provider?.LoadContextLength > 0 ? provider.LoadContextLength : (LLMConfigManager.Current?.MaxContextTokens > 0 ? LLMConfigManager.Current.MaxContextTokens : 8192);
            int percent = (int)((double)totalTokens / maxTokens * 100);
            if (percent > 100) percent = 100;

            pbContext.Value = percent;
            lblContextTokens.Text = $"Kontekst: {totalTokens}/{maxTokens} ({percent}%)";

            if (percent >= 90) pbContext.ForeColor = Color.Red;
            else if (percent >= 70) pbContext.ForeColor = Color.Orange;
            else pbContext.ForeColor = Color.Green;
        }

    }
}
