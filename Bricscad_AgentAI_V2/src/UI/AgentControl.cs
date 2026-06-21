using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
using Bricscad_AgentAI_V2.Models.Session;

namespace Bricscad_AgentAI_V2.UI
{
    public class AgentControl : UserControl
    {
        private TabControl tabControl;
        private CheckBox chkEarlyExit;
        // Auditor (Agent Rewident) - Filar 3 + 4
        private CheckBox chkAuditorEnabled;
        private CheckBox chkAuditorPrewarm;
        private CheckBox chkAuditorEvidence;
        private CheckBox chkCircuitBreaker;
        private NumericUpDown numCircuitBreakerThreshold;
        private Label lblCircuitBreakerState;
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
        private RichTextBox txtLoopLogs;
        private Button btnCopyLoopLogs;

        // --- Silnik V2 ---
        public LLMClient LlmClient => _llmClient;
        private LLMClient _llmClient;
        public ToolOrchestrator Orchestrator => _orchestrator;
        private ToolOrchestrator _orchestrator;
        public SupervisorOrchestrator Supervisor => _supervisor;
        private SupervisorOrchestrator _supervisor;
        private bool isDarkMode = true;
        private string _activeModel = "LM Studio / local-model"; // DomyÄÄ…Ă˘â‚¬Ĺźlny model
        private AutoBenchmarkEngine _benchmarkEngine;
        private TabPage tabBenchmark;
        private TabPage tabBenchmarkAnalytics;
        private bool _benchmarkAnalyticsInitialized;
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
        private ComboBox cbAgentLlmProvider;
        private ComboBox cbAgentLlmModel;
        private ComboBox cbAgentReasoning;
        private ComboBox cbAgentContextPolicy;
        private CheckBox chkAgentUseProviderPayload;
        private CheckBox chkAgentAutoLoad;
        private NumericUpDown numAgentTemp;
        private NumericUpDown numAgentContext;
        private NumericUpDown numAgentMaxTokens;
        private NumericUpDown numAgentTopP;
        private NumericUpDown numAgentTopK;
        private NumericUpDown numAgentMinP;
        private NumericUpDown numAgentRepPenalty;
        private Label lblAgentLlmPreview;
        private Button btnRefreshAgentModels;
        private CheckBox chkAgentUseDefaultProvider;
        private bool _suppressAgentLlmUiEvents;
        private CheckBox chkVisionOcrEnabled;
        private ComboBox cbVisionOcrProvider;
        private ComboBox cbVisionOcrModel;
        private CheckBox chkVisionOcrUseProviderPayload;
        private NumericUpDown numVisionOcrTemp;
        private NumericUpDown numVisionOcrContext;
        private NumericUpDown numVisionOcrMaxTokens;
        private NumericUpDown numVisionOcrTopP;
        private NumericUpDown numVisionOcrTopK;
        private NumericUpDown numVisionOcrMinP;
        private NumericUpDown numVisionOcrRepPenalty;
        private NumericUpDown numVisionOcrClipboardPixels;
        private NumericUpDown numVisionOcrAttachmentPixels;
        private ComboBox cbVisionOcrTilingMode;
        private NumericUpDown numVisionOcrTileMaxDim;
        private NumericUpDown numVisionOcrTileOverlap;
        private NumericUpDown numVisionOcrTileMaxCount;
        private NumericUpDown numVisionOcrTileMaxAspect;
        private NumericUpDown numVisionOcrPdfDpi;
        private NumericUpDown numVisionOcrPdfMaxPages;
        private TextBox txtVisionOcrPdfRendererPath;
        private ComboBox cbVisionOcrQualityPreset;
        private TextBox txtVisionOcrPresetName;
        private TextBox txtVisionOcrTestFile;
        private TextBox txtVisionOcrTestPrompt;
        private TextBox txtVisionOcrSystemPrompt;
        private TextBox txtVisionOcrTestResult;
        private Button btnVisionOcrTestRun;
        private ListBox lstVisionOcrTestRuns;
        private ComboBox cbVisionOcrReasoning;
        private CheckBox chkVisionOcrAutoLoad;
        private ComboBox cbVisionOcrContextPolicy;
        private Label lblVisionOcrPreview;
        private Button btnRefreshVisionOcrModels;
        private bool _suppressVisionOcrUiEvents;
        private bool _visionOcrProviderChangedByUser;
        private CancellationTokenSource _promptWarmupCts;
        private System.Windows.Forms.Timer _promptWarmupTypingTimer;
        private string _lastPromptWarmupKey;
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

            SchedulePromptWarmup("ai-open", 250);
        }        private void InitializeEngineV2()
        {
            _orchestrator = ToolOrchestrator.Instance;
            // Inicjalizacja skanowania narzÄ‚â€žĂ˘â€žËdzi odbywa siÄ‚â€žĂ˘â€žË automatycznie przy pierwszym dostÄ‚â€žĂ˘â€žËpie do Instance

            _llmClient = new LLMClient(_orchestrator);
            _supervisor = new SupervisorOrchestrator(_llmClient);
            
            _llmClient.OnStatusUpdate += UpdateStatusHUD;
            _llmClient.OnToolCallLogged += AppendToolLog;
            _llmClient.OnLoopLogged += AppendLoopLog;
            _llmClient.OnStatsUpdate += (stats) => UpdateStatsHUD(stats);
            _llmClient.OnStatsUpdate += (stats) => UpdateTokenBar(stats.TotalTokens);

            // Subskrypcja telemetrii od odÄÄ…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â‚¬Â¦czonych narzÄ‚â€žĂ˘â€žËdzi roboczych (np. DelegateTaskTool)
            AgentTelemetry.OnStatusUpdated += UpdateStatusHUD;
            AgentTelemetry.OnToolLogged += AppendToolLog;
            AgentTelemetry.OnLoopLogged += AppendLoopLog;
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
            RefreshAgentProviderDropdown();
            RefreshVisionOcrProviderDropdown();
            CancelPromptWarmup();
            SchedulePromptWarmup("config-changed", 500);
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

        private void SchedulePromptWarmup(string reason, int delayMs = 0)
        {
            var settings = UISettingsManager.Settings;
            if (!settings.EnablePromptWarmup) return;
            if (reason == "ai-open" && !settings.PromptWarmupOnAiOpen) return;
            if (reason == "session-load" && !settings.PromptWarmupOnSessionLoad) return;

            if (reason == "typing")
            {
                if (settings.PromptWarmupAfterTypingIdleMs <= 0) return;
                if (_promptWarmupTypingTimer == null)
                {
                    _promptWarmupTypingTimer = new System.Windows.Forms.Timer();
                    _promptWarmupTypingTimer.Tick += (s, e) =>
                    {
                        _promptWarmupTypingTimer.Stop();
                        _ = RunPromptWarmupAsync("typing", 0);
                    };
                }

                _promptWarmupTypingTimer.Stop();
                _promptWarmupTypingTimer.Interval = Math.Max(500, settings.PromptWarmupAfterTypingIdleMs);
                _promptWarmupTypingTimer.Start();
                return;
            }

            _ = RunPromptWarmupAsync(reason, delayMs);
        }

        private async Task RunPromptWarmupAsync(string reason, int delayMs)
        {
            CancelPromptWarmup();
            var cts = new CancellationTokenSource();
            _promptWarmupCts = cts;

            try
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, cts.Token);
                }

                var messages = BuildPromptWarmupMessages();
                if (messages.Count == 0) return;

                string key = BuildPromptWarmupKey(messages);
                if (reason != "typing" && string.Equals(_lastPromptWarmupKey, key, StringComparison.Ordinal))
                {
                    return;
                }

                UpdateStatusHUD("[Warmup] Rozgrzewam prompt Supervisora...");
                bool ok = await _llmClient.WarmupPromptAsync(messages, "SupervisorProfile", cts.Token);
                if (cts.IsCancellationRequested) return;

                if (ok)
                {
                    _lastPromptWarmupKey = key;
                    UpdateStatusHUD("[Warmup] Prompt Supervisora rozgrzany.");
                }
                else
                {
                    UpdateStatusHUD("Gotowy.");
                }
            }
            catch (OperationCanceledException)
            {
                // Realne zapytanie uzytkownika ma pierwszenstwo przed warmupem.
            }
            catch (Exception ex)
            {
                BielikLogger.LogWarn($"[Warmup] Blad koordynatora: {ex.Message}");
                UpdateStatusHUD("Gotowy.");
            }
            finally
            {
                if (_promptWarmupCts == cts)
                {
                    _promptWarmupCts = null;
                }
                cts.Dispose();
            }
        }

        private List<ChatMessage> BuildPromptWarmupMessages()
        {
            string activeDwgPath = GetActiveDwgPath();
            var session = SessionManager.CurrentSession;
            var sourceMessages = session?.Messages;
            var result = new List<ChatMessage>();

            if (sourceMessages != null && sourceMessages.Count > 0)
            {
                foreach (var message in sourceMessages)
                {
                    result.Add(CloneChatMessage(message));
                }

                if (!result.Any(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Insert(0, new ChatMessage { Role = "system", Content = _supervisor.BuildSupervisorSystemPrompt(activeDwgPath) });
                }
            }
            else
            {
                result.Add(new ChatMessage { Role = "system", Content = _supervisor.BuildSupervisorSystemPrompt(activeDwgPath) });
            }

            return result;
        }

        private ChatMessage CloneChatMessage(ChatMessage message)
        {
            if (message == null) return new ChatMessage();
            string json = JsonConvert.SerializeObject(message);
            return JsonConvert.DeserializeObject<ChatMessage>(json) ?? new ChatMessage();
        }

        private string BuildPromptWarmupKey(List<ChatMessage> messages)
        {
            var config = LLMConfigManager.ResolveProviderForProfile("SupervisorProfile");
            var session = SessionManager.CurrentSession;
            string endpoint = config?.EndpointUrl ?? string.Empty;
            string model = config?.ModelName ?? string.Empty;
            string sessionId = session?.Id ?? string.Empty;
            string updated = session?.UpdatedAt.ToString("O") ?? string.Empty;
            return $"{endpoint}|{model}|{sessionId}|{updated}|{messages.Count}|{GetActiveDwgPath()}";
        }

        private string GetActiveDwgPath()
        {
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                return doc?.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void CancelPromptWarmup()
        {
            try
            {
                _promptWarmupTypingTimer?.Stop();
                _promptWarmupCts?.Cancel();
            }
            catch { }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            CancelPromptWarmup();
            _promptWarmupTypingTimer?.Dispose();
            _promptWarmupCts?.Dispose();
            base.OnHandleDestroyed(e);
        }

        private void TabBenchmarkAnalytics_Enter(object sender, EventArgs e)
        {
            if (_benchmarkAnalyticsInitialized || tabBenchmarkAnalytics == null)
            {
                return;
            }

            _benchmarkAnalyticsInitialized = true;
            try
            {
                tabBenchmarkAnalytics.Controls.Clear();
                tabBenchmarkAnalytics.Controls.Add(new BenchmarkAnalyticsControl());
            }
            catch (Exception ex)
            {
                BielikLogger.LogError("Błąd inicjalizacji zakładki Analiza benchmarków", ex);

                var errorLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    ForeColor = Color.OrangeRed,
                    BackColor = Color.FromArgb(30, 30, 30),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "Nie udało się załadować zakładki Analiza.\nSprawdź bielik_debug.log."
                };

                tabBenchmarkAnalytics.Controls.Add(errorLabel);
            }
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

            // ============== AUDITOR PANEL (Agent Rewident - Filar 3 + 4) ==============
            Panel panAuditor = new Panel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding(10, 0, 0, 0)
            };

            chkAuditorEnabled = new CheckBox
            {
                Text = "Audytor (Auditor)",
                AutoSize = true,
                ForeColor = Color.LightGreen,
                Dock = DockStyle.Top
            };
            chkAuditorEnabled.Checked = UISettingsManager.Settings.AuditorEnabled;
            chkAuditorEnabled.CheckedChanged += (s, e) =>
            {
                AgentMemoryState.AuditorEnabled = chkAuditorEnabled.Checked;
                UISettingsManager.Settings.AuditorEnabled = chkAuditorEnabled.Checked;
                UISettingsManager.Save();
                chkAuditorPrewarm.Enabled = chkAuditorEnabled.Checked;
                chkAuditorEvidence.Enabled = chkAuditorEnabled.Checked;
                UpdateCircuitBreakerLabel();
            };
            panAuditor.Controls.Add(chkAuditorEnabled);

            chkAuditorPrewarm = new CheckBox
            {
                Text = "Pre-warm KV cache (Multi-GPU)",
                AutoSize = true,
                ForeColor = Color.LightGray,
                Dock = DockStyle.Top,
                Enabled = UISettingsManager.Settings.AuditorEnabled
            };
            chkAuditorPrewarm.Checked = UISettingsManager.Settings.AuditorPrewarmEnabled;
            chkAuditorPrewarm.CheckedChanged += (s, e) =>
            {
                AgentMemoryState.AuditorPrewarmEnabled = chkAuditorPrewarm.Checked;
                UISettingsManager.Settings.AuditorPrewarmEnabled = chkAuditorPrewarm.Checked;
                UISettingsManager.Save();
            };
            panAuditor.Controls.Add(chkAuditorPrewarm);

            chkAuditorEvidence = new CheckBox
            {
                Text = "Chain of Evidence (snapshot before/after)",
                AutoSize = true,
                ForeColor = Color.LightGray,
                Dock = DockStyle.Top,
                Enabled = UISettingsManager.Settings.AuditorEnabled
            };
            chkAuditorEvidence.Checked = UISettingsManager.Settings.AuditorEvidenceEnabled;
            chkAuditorEvidence.CheckedChanged += (s, e) =>
            {
                AgentMemoryState.EvidenceEnabled = chkAuditorEvidence.Checked;
                UISettingsManager.Settings.AuditorEvidenceEnabled = chkAuditorEvidence.Checked;
                UISettingsManager.Save();
            };
            panAuditor.Controls.Add(chkAuditorEvidence);

            chkCircuitBreaker = new CheckBox
            {
                Text = "Circuit Breaker (blokuj po N awariach)",
                AutoSize = true,
                ForeColor = Color.LightSalmon,
                Dock = DockStyle.Top
            };
            chkCircuitBreaker.Checked = UISettingsManager.Settings.CircuitBreakerEnabled;
            chkCircuitBreaker.CheckedChanged += (s, e) =>
            {
                AgentMemoryState.CircuitBreakerEnabled = chkCircuitBreaker.Checked;
                UISettingsManager.Settings.CircuitBreakerEnabled = chkCircuitBreaker.Checked;
                UISettingsManager.Save();
                numCircuitBreakerThreshold.Enabled = chkCircuitBreaker.Checked;
                UpdateCircuitBreakerLabel();
            };
            panAuditor.Controls.Add(chkCircuitBreaker);

            Panel panCbRow = new Panel { Dock = DockStyle.Top, AutoSize = true };
            Label lblCbThreshold = new Label
            {
                Text = "Prog:",
                AutoSize = true,
                ForeColor = Color.LightGray,
                Location = new System.Drawing.Point(0, 4)
            };
            numCircuitBreakerThreshold = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 20,
                Value = UISettingsManager.Settings.CircuitBreakerThreshold,
                Width = 50,
                Left = 40,
                Top = 0,
                Enabled = UISettingsManager.Settings.CircuitBreakerEnabled
            };
            numCircuitBreakerThreshold.ValueChanged += (s, e) =>
            {
                int v = (int)numCircuitBreakerThreshold.Value;
                AgentMemoryState.CircuitBreakerThreshold = v;
                UISettingsManager.Settings.CircuitBreakerThreshold = v;
                UISettingsManager.Save();
                UpdateCircuitBreakerLabel();
            };
            lblCircuitBreakerState = new Label
            {
                Text = "",
                AutoSize = true,
                ForeColor = Color.Gold,
                Location = new System.Drawing.Point(100, 4)
            };
            panCbRow.Controls.Add(lblCbThreshold);
            panCbRow.Controls.Add(numCircuitBreakerThreshold);
            panCbRow.Controls.Add(lblCircuitBreakerState);
            panAuditor.Controls.Add(panCbRow);
            // =========================================================================

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
            panInput.Controls.Add(panAuditor);
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

            TabPage tabLoop = new TabPage("Log pętli");

            txtLoopLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            btnCopyLoopLogs = new Button
            {
                Text = "Kopiuj do schowka",
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCopyLoopLogs.Click += (s, e) => { if (!string.IsNullOrEmpty(txtLoopLogs.Text)) Clipboard.SetText(txtLoopLogs.Text); };

            tabLoop.Controls.Add(txtLoopLogs);
            tabLoop.Controls.Add(btnCopyLoopLogs);

            // ==========================================
            // ZAKÄÄ… ADKA 3: BENCHMARK (OCENA LLM)
            // ==========================================
            tabBenchmark = new TabPage("Benchmark");
            tabBenchmark.Controls.Add(new AutoBenchmarkControl(_benchmarkEngine));

            tabBenchmarkAnalytics = new TabPage("Analiza");
            tabBenchmarkAnalytics.Enter += TabBenchmarkAnalytics_Enter;

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
            Panel panAgentOverviewTop = new Panel { Dock = DockStyle.Top, Height = 260, Padding = new Padding(10) };
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
            cbAgentPromptFile.Items.AddRange(new object[] { ToolConfigManager.GetDefaultSystemPromptFile("SupervisorProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadGeometryProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadBlocksProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadMetadataProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadMathProfile"), ToolConfigManager.GetDefaultSystemPromptFile("CadLayoutProfile"), ToolConfigManager.GetDefaultSystemPromptFile("NotesProfile"), ToolConfigManager.GetDefaultSystemPromptFile("AuditorProfile"), ToolConfigManager.GetDefaultSystemPromptFile("Modeler3DProfile") });
            
            btnOpenAgentPromptInOverview = new Button { Text = "Prompt systemowy z repo", Dock = DockStyle.Left, AutoSize = true, Padding = new Padding(0, 0, 10, 0), Margin = new Padding(10, 0, 0, 0), BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };

            panAgentPromptSelection.Controls.Add(btnOpenAgentPromptInOverview);
            panAgentPromptSelection.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panAgentPromptSelection.Controls.Add(cbAgentPromptFile);
            panAgentPromptSelection.Controls.Add(lblAgentPrompt);

            Panel panAgentLlm = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(0, 8, 0, 0) };
            FlowLayoutPanel panAgentLlmRow1 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight, WrapContents = false };
            FlowLayoutPanel panAgentLlmRow2 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight, WrapContents = false };
            FlowLayoutPanel panAgentLlmRow3 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight, WrapContents = false };

            panAgentLlmRow1.Controls.Add(new Label { Text = "LLM:", Width = 38, Height = 24, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            chkAgentUseDefaultProvider = new CheckBox { Text = "Domyślny provider", Checked = true, AutoSize = true, Height = 24, ForeColor = Color.LightGray };
            chkAgentUseDefaultProvider.CheckedChanged += AgentLlmControlChanged;
            panAgentLlmRow1.Controls.Add(chkAgentUseDefaultProvider);
            cbAgentLlmProvider = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbAgentLlmProvider.SelectedIndexChanged += CbAgentLlmProvider_SelectedIndexChanged;
            panAgentLlmRow1.Controls.Add(cbAgentLlmProvider);
            panAgentLlmRow1.Controls.Add(new Label { Text = "Model:", Width = 48, Height = 24, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            cbAgentLlmModel = new ComboBox { Width = 265, DropDownStyle = ComboBoxStyle.DropDown, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbAgentLlmModel.TextChanged += AgentLlmControlChanged;
            panAgentLlmRow1.Controls.Add(cbAgentLlmModel);
            btnRefreshAgentModels = new Button { Text = "Modele", Width = 70, Height = 24, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnRefreshAgentModels.Click += async (s, e) => await RefreshAgentModelsAsync();
            panAgentLlmRow1.Controls.Add(btnRefreshAgentModels);
            chkAgentUseProviderPayload = new CheckBox { Text = "Payload providera", Checked = true, AutoSize = true, Height = 24, ForeColor = Color.LightGray };
            chkAgentUseProviderPayload.CheckedChanged += AgentLlmControlChanged;
            panAgentLlmRow1.Controls.Add(chkAgentUseProviderPayload);

            panAgentLlmRow2.Controls.Add(CreateAgentNumericField("Temp", out numAgentTemp, 0.0m, 2.0m, 2, 0.1m, 58));
            panAgentLlmRow2.Controls.Add(CreateAgentNumericField("Ctx", out numAgentContext, 0m, 128000m, 0, 1000m, 78));
            panAgentLlmRow2.Controls.Add(CreateAgentNumericField("Max", out numAgentMaxTokens, 1m, 128000m, 0, 1000m, 78));
            panAgentLlmRow2.Controls.Add(CreateAgentNumericField("TopP", out numAgentTopP, 0.0m, 1.0m, 2, 0.05m, 58));
            panAgentLlmRow2.Controls.Add(CreateAgentNumericField("TopK", out numAgentTopK, 0m, 200m, 0, 1m, 58));
            panAgentLlmRow2.Controls.Add(CreateAgentNumericField("MinP", out numAgentMinP, 0.0m, 1.0m, 2, 0.05m, 58));
            panAgentLlmRow2.Controls.Add(CreateAgentNumericField("Rep", out numAgentRepPenalty, 1.0m, 2.0m, 2, 0.05m, 58));
            cbAgentReasoning = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbAgentReasoning.Items.AddRange(new object[] { "none", "low", "medium", "high" });
            cbAgentReasoning.SelectedIndexChanged += AgentLlmControlChanged;
            panAgentLlmRow2.Controls.Add(new Label { Text = "Reason", Width = 52, Height = 24, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            panAgentLlmRow2.Controls.Add(cbAgentReasoning);

            chkAgentAutoLoad = new CheckBox { Text = "AutoLoad", AutoSize = true, Height = 24, ForeColor = Color.LightGray };
            chkAgentAutoLoad.CheckedChanged += AgentLlmControlChanged;
            panAgentLlmRow3.Controls.Add(chkAgentAutoLoad);
            cbAgentContextPolicy = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbAgentContextPolicy.Items.AddRange(new object[] { "UseLoadedIfAtLeastRequested", "NeverReloadAutomatically", "ReloadOnlyIfTooSmall" });
            cbAgentContextPolicy.SelectedIndexChanged += AgentLlmControlChanged;
            panAgentLlmRow3.Controls.Add(new Label { Text = "Kontekst:", Width = 64, Height = 24, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
            panAgentLlmRow3.Controls.Add(cbAgentContextPolicy);
            lblAgentLlmPreview = new Label { Width = 520, Height = 24, ForeColor = Color.LightGreen, TextAlign = ContentAlignment.MiddleLeft };
            panAgentLlmRow3.Controls.Add(lblAgentLlmPreview);

            panAgentLlm.Controls.Add(panAgentLlmRow3);
            panAgentLlm.Controls.Add(panAgentLlmRow2);
            panAgentLlm.Controls.Add(panAgentLlmRow1);
            
            panAgentOverviewTop.Controls.Add(panAgentLlm);
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
            RefreshAgentProviderDropdown();
            
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
            tabTestsSub.TabPages.Add(tabBenchmarkAnalytics);
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
            tabSettingsSub.TabPages.Add(tabLoop);
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
            // PODZAKLADKA: Sciezki i Dane
            // ==========================================
            TabPage tabPathsSub = new TabPage("Sciezki i Dane");
            tabPathsSub.Padding = new Padding(12);

            var pathsMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            pathsMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pathsMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pathsMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            pathsMain.Controls.Add(BuildPathsLlmConfigGroup(), 0, 0);
            pathsMain.Controls.Add(BuildPathsKnowledgeGroup(), 0, 1);

            tabPathsSub.Controls.Add(pathsMain);
            tabSettingsSub.TabPages.Add(tabPathsSub);

            TabPage tabWorkflowSub = new TabPage("Workflow");
            tabWorkflowSub.Padding = new Padding(12);

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            main.Controls.Add(BuildWorkflowStartupGroup(), 0, 0);
            main.Controls.Add(BuildWorkflowWarmupGroup(), 0, 1);

            tabWorkflowSub.Controls.Add(main);
            tabSettingsSub.TabPages.Add(tabWorkflowSub);
            tabSettingsSub.TabPages.Add(CreateVisionOcrSettingsTab());

            tabSettings.Controls.Add(tabSettingsSub);
            tabControl.TabPages.Add(tabSettings);


            // Rejestracja callbacku
            EngineTracer.SetLogCallback(AppendEngineLog);

            this.Controls.Add(tabControl);

            // Załadowanie początkowych danych do bazy wiedzy
            knowledgeBaseControl.LoadData();

            // Synchronizacja flagi Auditora (Filar 3+4) z ustawieniami UI
            SyncAuditorSettingsToAgentMemoryState();
            UpdateCircuitBreakerLabel();
        }

        /// <summary>
        /// Synchronizuje flagi Agenta Rewidenta z UISettings do statycznego AgentMemoryState.
        /// Wymuszone po inicjalizacji UI, bo ustawienie CheckBox.Checked nie odpala
        /// eventu CheckedChanged jesli wartosc nie zmienila sie od defaultu.
        /// </summary>
        private void SyncAuditorSettingsToAgentMemoryState()
        {
            var s = UISettingsManager.Settings;
            AgentMemoryState.AuditorEnabled = s.AuditorEnabled;
            AgentMemoryState.AuditorPrewarmEnabled = s.AuditorPrewarmEnabled;
            AgentMemoryState.EvidenceEnabled = s.AuditorEvidenceEnabled;
            AgentMemoryState.CircuitBreakerEnabled = s.CircuitBreakerEnabled;
            AgentMemoryState.CircuitBreakerThreshold = s.CircuitBreakerThreshold;
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
            txtLoopLogs.BackColor = bgMain;
            txtInput.BackColor = bgControl;

            txtHistory.ForeColor = fgText;
            txtToolLogs.ForeColor = Color.LightSkyBlue;
            txtLoopLogs.ForeColor = Color.LightGreen;
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
                    _attachedFilePath = null; // Czyszczenie ścieżki pliku, bo priorytet ma schowek

                    // Aktualizacja UI
                    lblAttachedFile.Text = "📋 [Obraz ze schowka]";
                    lblAttachedFile.Visible = true;

                    // Zablokowanie domyślnego wklejenia (aby nie dodawało się do tekstu jeśli to RichTextBox)
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
        }

        /// <summary>
        /// Aktualizuje etykiete stanu Circuit Breaker (ktore profile sa zablokowane).
        /// </summary>
        private void UpdateCircuitBreakerLabel()
        {
            if (lblCircuitBreakerState == null) return;
            var snapshots = CircuitBreakerState.GetAllSnapshots();
            if (snapshots.Count == 0)
            {
                lblCircuitBreakerState.Text = "";
                lblCircuitBreakerState.ForeColor = Color.LightGray;
            }
            else
            {
                var tripped = snapshots.FindAll(s => s.IsTripped);
                if (tripped.Count == 0)
                {
                    lblCircuitBreakerState.Text = $"{snapshots.Count} profil(e) sledzone";
                    lblCircuitBreakerState.ForeColor = Color.LightGreen;
                }
                else
                {
                    lblCircuitBreakerState.Text = "BLOKADA: " + string.Join(", ", tripped.ConvertAll(s => s.ProfileName));
                    lblCircuitBreakerState.ForeColor = Color.Red;
                }
            }
        }

        private void TxtInput_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtInput.Text))
            {
                SchedulePromptWarmup("typing");
            }

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
            SchedulePromptWarmup("session-load", 250);
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

        private async Task<string> BuildVisionOcrTextPayloadAsync(string imageDataUrl, string userPrompt, string sourceLabel, VisionImageContext imageContext = null, bool isRequery = false)
        {
            string prompt = string.IsNullOrWhiteSpace(userPrompt)
                ? "Przeanalizuj dolaczony obraz."
                : userPrompt;

            string technicalContext = "Zrodlo obrazu: " + sourceLabel;
            if (imageContext != null)
            {
                technicalContext += "\nImageId: " + imageContext.ImageId;
                technicalContext += "\nPlik cache: " + imageContext.CachedPath;
                technicalContext += "\nRozmiar oryginalny: " + imageContext.OriginalWidth + "x" + imageContext.OriginalHeight;
                if (imageContext.TileCount > 0 && imageContext.Tiles != null && imageContext.Tiles.Count > 0)
                {
                    technicalContext += "\n\n[TILING OCR]\n";
                    technicalContext += BuildTileSpatialPrompt(imageContext);
                }
                var previous = imageContext.OcrHistory?
                    .Where(o => o.Success && !string.IsNullOrWhiteSpace(o.Result))
                    .Reverse()
                    .Take(3)
                    .Reverse()
                    .Select(o => "- " + o.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") + ": " + TruncateForPrompt(o.Result, 2500))
                    .ToList();
                if (previous != null && previous.Count > 0)
                {
                    technicalContext += "\n\nPoprzednie wyniki OCR dla tego samego obrazu:\n" + string.Join("\n", previous);
                }
            }

            if (isRequery)
            {
                prompt = "Ponownie przeanalizuj ten sam obraz, odpowiadajac na nowe pytanie uzytkownika. " +
                    "Nie opieraj sie wylacznie na poprzednim opisie; sprawdz obraz jeszcze raz. " +
                    "Jesli pytanie dotyczy tabliczki, legendy, rząpi, wymiarow lub konkretnego napisu, skup sie na tym obszarze.\n\n" +
                    "Pytanie uzytkownika:\n" + prompt;
            }

            UpdateStatusHUD("Vision/OCR analizuje obraz...");
            var imageDataUrls = BuildVisionOcrImageDataUrls(imageDataUrl, imageContext);
            var ocr = await _llmClient.AnalyzeImagesWithVisionOcrAsync(
                imageDataUrls,
                prompt,
                technicalContext);

            RecordVisionOcrObservation(imageContext, prompt, ocr.ok, ocr.ok ? ocr.text : null, ocr.ok ? null : ocr.text);

            if (ocr.ok)
            {
                UpdateStatusHUD("Vision/OCR zakonczyl analize obrazu.");
                string header = isRequery ? "VISION/OCR REQUERY" : "VISION/OCR";
                string imageInfo = imageContext != null ? $" image_id={imageContext.ImageId}" : "";
                return userPrompt + $"\n\n[{header} - {sourceLabel}{imageInfo}]\n" + ocr.text;
            }

            UpdateStatusHUD("Blad Vision/OCR.");
            return userPrompt + $"\n\n[VISION/OCR ERROR - {sourceLabel}]\n" + ocr.text;
        }

        private async Task<string> BuildPdfVisionOcrPayloadAsync(string pdfPath, string userPrompt, string pdfText)
        {
            string prompt = string.IsNullOrWhiteSpace(userPrompt)
                ? "Przeanalizuj dolaczony PDF z rzutami."
                : userPrompt;

            string pdfName = Path.GetFileName(pdfPath);
            var settings = UISettingsManager.Settings;
            var imageDataUrls = new List<string>();
            var pageContexts = new List<VisionImageContext>();
            var technical = new StringBuilder();
            technical.AppendLine("Zrodlo: PDF");
            technical.AppendLine("Plik: " + pdfName);
            technical.AppendLine("Renderer: " + (string.IsNullOrWhiteSpace(settings.VisionOcrPdfRendererPath) ? "pdftoppm.exe" : settings.VisionOcrPdfRendererPath));
            technical.AppendLine("DPI: " + settings.VisionOcrPdfDpi);
            technical.AppendLine("Maks. stron OCR: " + settings.VisionOcrPdfMaxPages);

            if (!string.IsNullOrWhiteSpace(pdfText))
            {
                technical.AppendLine();
                technical.AppendLine("[PDF_TEXT - tekst wektorowy / osadzony]");
                technical.AppendLine(TruncateForPrompt(pdfText, 12000));
            }

            try
            {
                string renderFolder = Path.Combine(
                    AppPaths.GetSessionImagesPath(),
                    SessionManager.CurrentSession.Id,
                    "pdf_render_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));

                var renderedPages = FileExtractor.RenderPdfPagesToPng(
                    pdfPath,
                    renderFolder,
                    settings.VisionOcrPdfDpi,
                    settings.VisionOcrPdfMaxPages,
                    settings.VisionOcrPdfRendererPath);

                int pageIndex = 1;
                foreach (string pagePath in renderedPages)
                {
                    var context = RegisterVisionImageFromFile(pagePath, $"PDF {pdfName}, strona {pageIndex}", settings.VisionOcrAttachmentMaxPixels);
                    if (context != null)
                    {
                        pageContexts.Add(context);
                        string base64 = FileExtractor.GetImageBase64(context.CachedPath, settings.VisionOcrAttachmentMaxPixels);
                        imageDataUrls.AddRange(BuildVisionOcrImageDataUrls(base64, context));
                        technical.AppendLine();
                        technical.AppendLine($"[PDF_PAGE_RENDER {pageIndex}]");
                        technical.AppendLine("ImageId: " + context.ImageId);
                        technical.AppendLine("Rozmiar renderu: " + context.OriginalWidth + "x" + context.OriginalHeight);
                        if (context.TileCount > 0)
                        {
                            technical.AppendLine(BuildTileSpatialPrompt(context));
                        }
                    }
                    pageIndex++;
                }
            }
            catch (Exception ex)
            {
                BielikLogger.LogError("[PDF OCR] Blad renderowania PDF do PNG", ex);
                return prompt + $"\n\n[PDF_TEXT - {pdfName}]\n" + TruncateForPrompt(pdfText, 20000) +
                    "\n\n[PDF_VISION/OCR ERROR]\nNie udalo sie wyrenderowac PDF do PNG przez pdftoppm: " + ex.Message;
            }

            if (imageDataUrls.Count == 0)
            {
                return prompt + $"\n\n[PDF_TEXT - {pdfName}]\n" + TruncateForPrompt(pdfText, 20000) +
                    "\n\n[PDF_VISION/OCR ERROR]\nNie wygenerowano obrazow stron PDF.";
            }

            UpdateStatusHUD("Vision/OCR analizuje strony PDF...");
            string ocrPrompt = "Przeanalizuj renderowane strony PDF z rysunkiem technicznym. " +
                "Polacz informacje z obrazu stron z tekstem PDF przekazanym w kontekscie. " +
                "Skup sie na rzutach, tabliczkach, legendach, opisach, wymiarach, symbolach i ukladzie instalacji.\n\n" +
                "Polecenie uzytkownika:\n" + prompt;

            var ocr = await _llmClient.AnalyzeImagesWithVisionOcrAsync(imageDataUrls, ocrPrompt, technical.ToString());

            foreach (var context in pageContexts)
            {
                RecordVisionOcrObservation(context, ocrPrompt, ocr.ok, ocr.ok ? ocr.text : null, ocr.ok ? null : ocr.text);
            }

            UpdateStatusHUD(ocr.ok ? "Vision/OCR zakonczyl analize PDF." : "Blad Vision/OCR dla PDF.");

            return prompt +
                $"\n\n[PDF_TEXT - {pdfName}]\n" + TruncateForPrompt(pdfText, 20000) +
                $"\n\n[PDF_VISION/OCR - {pdfName}, strony 1-{Math.Min(settings.VisionOcrPdfMaxPages, pageContexts.Count)}]\n" +
                (ocr.ok ? ocr.text : "BLAD OCR PDF: " + ocr.text);
        }

        private List<string> BuildVisionOcrImageDataUrls(string imageDataUrl, VisionImageContext imageContext)
        {
            if (imageContext?.Tiles != null && imageContext.Tiles.Count > 0)
            {
                var urls = new List<string>();
                foreach (var tile in imageContext.Tiles.OrderBy(t => t.Row).ThenBy(t => t.Column))
                {
                    if (!string.IsNullOrWhiteSpace(tile.CachedPath) && File.Exists(tile.CachedPath))
                    {
                        int maxPixels = Math.Max(tile.SourceWidth, tile.SourceHeight);
                        urls.Add(FileExtractor.GetImageBase64(tile.CachedPath, maxPixels));
                    }
                }

                if (urls.Count > 0) return urls;
            }

            return new List<string> { imageDataUrl };
        }

        private string BuildTileSpatialPrompt(VisionImageContext imageContext)
        {
            var tileSet = new VisionTileSet
            {
                IsTiled = imageContext.TileCount > 0,
                Mode = imageContext.OcrTilingMode,
                OriginalWidth = imageContext.OriginalWidth,
                OriginalHeight = imageContext.OriginalHeight,
                Rows = imageContext.TileRows,
                Columns = imageContext.TileColumns,
                EffectiveTileMaxDim = imageContext.TileMaxDim,
                Overlap = imageContext.TileOverlap,
                MaxTileAspectRatio = UISettingsManager.Settings.VisionOcrTileMaxAspectRatio,
                Tiles = imageContext.Tiles ?? new List<VisionImageTileContext>()
            };
            return tileSet.BuildSpatialPrompt();
        }

        private VisionImageContext RegisterVisionImageFromFile(string path, string sourceLabel, int maxPixels)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            EnsureSessionVisionImages();

            string imageId = "img_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string sessionFolder = Path.Combine(AppPaths.GetSessionImagesPath(), SessionManager.CurrentSession.Id);
            Directory.CreateDirectory(sessionFolder);

            string ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
            string cachedPath = Path.Combine(sessionFolder, imageId + ext.ToLowerInvariant());
            File.Copy(path, cachedPath, true);

            int width = 0;
            int height = 0;
            using (var image = System.Drawing.Image.FromFile(cachedPath))
            {
                width = image.Width;
                height = image.Height;
            }

            var config = LLMConfigManager.ResolveVisionOcrProvider();
            var context = new VisionImageContext
            {
                ImageId = imageId,
                SourceLabel = sourceLabel,
                OriginalPath = path,
                CachedPath = cachedPath,
                MimeType = GetMimeTypeFromExtension(ext),
                Sha256 = ComputeSha256(cachedPath),
                FileSizeBytes = new FileInfo(cachedPath).Length,
                OriginalWidth = width,
                OriginalHeight = height,
                MaxPixelsUsed = maxPixels,
                ProviderName = config?.Name,
                ModelName = config?.ModelName
            };

            ApplyVisionOcrTilingIfNeeded(context);
            SessionManager.CurrentSession.VisionImages.Add(context);
            SessionManager.SaveSession();
            return context;
        }

        private VisionImageContext RegisterVisionImageFromClipboardImage(System.Drawing.Image image, string sourceLabel, int maxPixels)
        {
            if (image == null) return null;
            EnsureSessionVisionImages();

            string imageId = "img_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string sessionFolder = Path.Combine(AppPaths.GetSessionImagesPath(), SessionManager.CurrentSession.Id);
            Directory.CreateDirectory(sessionFolder);
            string cachedPath = Path.Combine(sessionFolder, imageId + ".png");
            image.Save(cachedPath, System.Drawing.Imaging.ImageFormat.Png);

            var config = LLMConfigManager.ResolveVisionOcrProvider();
            var context = new VisionImageContext
            {
                ImageId = imageId,
                SourceLabel = sourceLabel,
                OriginalPath = null,
                CachedPath = cachedPath,
                MimeType = "image/png",
                Sha256 = ComputeSha256(cachedPath),
                FileSizeBytes = new FileInfo(cachedPath).Length,
                OriginalWidth = image.Width,
                OriginalHeight = image.Height,
                MaxPixelsUsed = maxPixels,
                ProviderName = config?.Name,
                ModelName = config?.ModelName
            };

            ApplyVisionOcrTilingIfNeeded(context);
            SessionManager.CurrentSession.VisionImages.Add(context);
            SessionManager.SaveSession();
            return context;
        }

        private void ApplyVisionOcrTilingIfNeeded(VisionImageContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.CachedPath) || !File.Exists(context.CachedPath)) return;

            var settings = UISettingsManager.Settings;
            string mode = string.IsNullOrWhiteSpace(settings.VisionOcrTilingMode) ? "Auto" : settings.VisionOcrTilingMode;
            string tileFolder = Path.Combine(
                Path.GetDirectoryName(context.CachedPath) ?? AppPaths.GetSessionImagesPath(),
                context.ImageId + "_tiles");

            var tileSet = VisionOcrTiler.CreateTiles(
                context.CachedPath,
                tileFolder,
                mode,
                settings.VisionOcrTileMaxDim,
                settings.VisionOcrTileOverlap,
                settings.VisionOcrTileMaxCount,
                settings.VisionOcrTileMaxAspectRatio);

            context.OcrTilingMode = tileSet.Mode;
            context.TileMaxDim = tileSet.EffectiveTileMaxDim;
            context.TileOverlap = tileSet.Overlap;
            context.TileRows = tileSet.Rows;
            context.TileColumns = tileSet.Columns;
            context.TileCount = tileSet.TileCount;
            context.Tiles = tileSet.Tiles ?? new List<VisionImageTileContext>();

            if (tileSet.IsTiled && tileSet.TileCount > 0)
            {
                BielikLogger.LogInfo($"[VISION OCR TILING] {context.ImageId}: {context.OriginalWidth}x{context.OriginalHeight} -> {tileSet.Rows}x{tileSet.Columns}, tiles={tileSet.TileCount}, effectiveMax={tileSet.EffectiveTileMaxDim}, overlap={tileSet.Overlap}");
            }
        }

        private void RecordVisionOcrObservation(VisionImageContext imageContext, string prompt, bool success, string result, string error)
        {
            if (imageContext == null) return;
            if (imageContext.OcrHistory == null) imageContext.OcrHistory = new List<VisionOcrObservation>();
            var config = LLMConfigManager.ResolveVisionOcrProvider();
            imageContext.OcrHistory.Add(new VisionOcrObservation
            {
                Prompt = prompt,
                Success = success,
                Result = result,
                Error = error,
                ProviderName = config?.Name,
                ModelName = config?.ModelName
            });
            imageContext.ProviderName = config?.Name ?? imageContext.ProviderName;
            imageContext.ModelName = config?.ModelName ?? imageContext.ModelName;
            imageContext.UpdatedAt = DateTime.Now;
            SessionManager.SaveSession();
        }

        private async Task<string> TryBuildPreviousImageRequeryPayloadAsync(string userPrompt)
        {
            if (ToolConfigManager.GetVisionOcrBinding()?.Enabled != true) return null;
            if (!ShouldRequeryPreviousImage(userPrompt)) return null;
            var imageContext = GetLastVisionImageContext();
            if (imageContext == null || string.IsNullOrWhiteSpace(imageContext.CachedPath) || !File.Exists(imageContext.CachedPath)) return null;

            int maxPixels = imageContext.MaxPixelsUsed > 0 ? imageContext.MaxPixelsUsed : UISettingsManager.Settings.VisionOcrAttachmentMaxPixels;
            string base64 = FileExtractor.GetImageBase64(imageContext.CachedPath, maxPixels);
            return await BuildVisionOcrTextPayloadAsync(
                base64,
                userPrompt,
                "poprzedni obraz: " + (imageContext.SourceLabel ?? imageContext.ImageId),
                imageContext,
                true);
        }

        private string TryBuildPreviousImageCachedPayload(string userPrompt)
        {
            if (!ShouldUsePreviousImageCachedContext(userPrompt)) return null;
            var imageContext = GetLastVisionImageContext();
            if (imageContext == null) return null;

            string cachedContext = BuildCachedVisionOcrContext(imageContext);
            if (string.IsNullOrWhiteSpace(cachedContext)) return null;

            return userPrompt +
                "\n\n[VISION/OCR CACHE - poprzedni obraz, bez ponownego skanowania]\n" +
                cachedContext +
                "\n\nJesli odpowiedzi nie ma w powyzszym zapisie OCR, napisz uzytkownikowi, ze moze poprosic o ponowny OCR ostatniego obrazu.";
        }

        private VisionImageContext GetLastVisionImageContext()
        {
            EnsureSessionVisionImages();
            return SessionManager.CurrentSession.VisionImages
                .Where(i => !string.IsNullOrWhiteSpace(i.CachedPath))
                .OrderByDescending(i => i.UpdatedAt)
                .FirstOrDefault();
        }

        private bool ShouldRequeryPreviousImage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.ToLowerInvariant();
            string[] explicitRequery =
            {
                "ponownie przeanaliz", "przeanalizuj ponownie", "przeanalizuj jeszcze raz",
                "zeskanuj ponownie", "skanuj ponownie", "ponowny ocr", "zrob ocr",
                "zrob ponowny ocr", "odczytaj ponownie", "sprawdz dokladniej",
                "dokladniej obraz", "powieksz i odczytaj", "requery", "rescan"
            };
            string[] imageRefs =
            {
                "obraz", "obrazek", "rysunek", "załącz", "zalacz", "wcześniej", "wczesniej",
                "poprzedni", "ten plik", "tym pliku", "jeszcze raz", "ponownie"
            };
            string[] focusedTerms =
            {
                "tablicz", "inwestor", "projekt", "adres", "legenda", "rząp", "rzap",
                "wymiar", "napis", "tekst", "odczyt", "znajd", "sprawd"
            };

            return explicitRequery.Any(t.Contains) && imageRefs.Any(t.Contains);
        }

        private bool ShouldUsePreviousImageCachedContext(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.ToLowerInvariant();
            if (ShouldRequeryPreviousImage(text)) return false;
            if (LooksLikeCadCommand(t) && !MentionsPreviousImage(t)) return false;

            string[] cachedContextTerms =
            {
                "tablicz", "inwestor", "projekt", "adres", "legenda", "wodomierz",
                "wlaz", "właz", "skala", "data", "autor", "rysunku", "rysunek",
                "obraz", "obrazek", "pdf", "plik", "zalacz", "załącz",
                "poprzedni", "wczesniej", "wcześniej", "co widzisz", "co jest na",
                "odczytaj", "dane z", "dane projektu"
            };

            return cachedContextTerms.Any(t.Contains);
        }

        private bool MentionsPreviousImage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.ToLowerInvariant();
            string[] imageRefs =
            {
                "obraz", "obrazek", "rysunek", "zalacz", "załącz", "wczesniej", "wcześniej",
                "poprzedni", "ten plik", "tym pliku", "pdf", "plik", "ostatni"
            };

            return imageRefs.Any(t.Contains);
        }

        private bool LooksLikeCadCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] cadVerbs =
            {
                "narysuj", "wstaw", "utworz", "zaznacz", "usun", "zmien",
                "przesun", "skopiuj", "obroc", "polilin", "linia", "linie",
                "kostk", "blok", "wymiarow", "wymiarowa", "warstw", "kolor",
                "tekst o", "wysokosc"
            };

            return cadVerbs.Any(text.Contains);
        }

        private string BuildCachedVisionOcrContext(VisionImageContext imageContext)
        {
            if (imageContext == null || imageContext.OcrHistory == null) return null;
            var observations = imageContext.OcrHistory
                .Where(o => o != null && o.Success && !string.IsNullOrWhiteSpace(o.Result))
                .OrderByDescending(o => o.CreatedAt)
                .Take(2)
                .ToList();

            if (observations.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("ImageId: " + imageContext.ImageId);
            sb.AppendLine("Zrodlo: " + (imageContext.SourceLabel ?? imageContext.OriginalPath ?? imageContext.CachedPath));
            if (!string.IsNullOrWhiteSpace(imageContext.ProviderName) || !string.IsNullOrWhiteSpace(imageContext.ModelName))
            {
                sb.AppendLine("OCR model: " + imageContext.ProviderName + " / " + imageContext.ModelName);
            }

            if (imageContext.TileCount > 0)
            {
                sb.AppendLine($"Tiling: {imageContext.TileRows}x{imageContext.TileColumns}, kafelkow={imageContext.TileCount}, overlap={imageContext.TileOverlap}px");
            }

            foreach (var observation in observations)
            {
                sb.AppendLine();
                sb.AppendLine("[ZAPISANY WYNIK OCR " + observation.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") + "]");
                sb.AppendLine(TruncateForPrompt(observation.Result, 8000));
            }

            return sb.ToString();
        }

        private void EnsureSessionVisionImages()
        {
            if (SessionManager.CurrentSession == null) return;
            if (SessionManager.CurrentSession.VisionImages == null)
            {
                SessionManager.CurrentSession.VisionImages = new List<VisionImageContext>();
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string GetMimeTypeFromExtension(string ext)
        {
            string e = (ext ?? "").ToLowerInvariant();
            return e == ".jpg" || e == ".jpeg" ? "image/jpeg" : "image/png";
        }

        private static string TruncateForPrompt(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "\n...[przycieto]";
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

        public void AppendLoopLog(string message)
        {
            AppendLoopLogWithColor(message, Color.LightGreen);
        }

        public void AppendLoopLogWithColor(string message, Color color)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, Color>(AppendLoopLogWithColor), message, color);
                return;
            }
            txtLoopLogs.SelectionStart = txtLoopLogs.TextLength;
            txtLoopLogs.SelectionLength = 0;
            txtLoopLogs.SelectionColor = color;
            txtLoopLogs.AppendText($"\n--- PETLA [{DateTime.Now:HH:mm:ss}] ---\n");
            txtLoopLogs.AppendText(message + "\n");
            txtLoopLogs.SelectionStart = txtLoopLogs.Text.Length;
            txtLoopLogs.ScrollToCaret();
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
            CancelPromptWarmup();

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
                        bool visionOcrEnabled = ToolConfigManager.GetVisionOcrBinding()?.Enabled == true;
                        int maxPixels = visionOcrEnabled ? UISettingsManager.Settings.VisionOcrAttachmentMaxPixels : 1024;
                        VisionImageContext imageContext = visionOcrEnabled
                            ? RegisterVisionImageFromFile(attachedFilePath, $"zalacznik: {System.IO.Path.GetFileName(attachedFilePath)}", maxPixels)
                            : null;
                        string imagePathForPayload = imageContext?.CachedPath ?? attachedFilePath;
                        string base64 = FileExtractor.GetImageBase64(imagePathForPayload, maxPixels);
                        if (visionOcrEnabled)
                        {
                            payload = await BuildVisionOcrTextPayloadAsync(base64, cleanMsg, $"zalacznik: {System.IO.Path.GetFileName(attachedFilePath)}", imageContext);
                        }
                        else
                        {
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
                    }
                    else if (ext == ".pdf")
                    {
                        string text = FileExtractor.ExtractText(attachedFilePath);
                        bool visionOcrEnabled = ToolConfigManager.GetVisionOcrBinding()?.Enabled == true;
                        if (visionOcrEnabled)
                        {
                            payload = await BuildPdfVisionOcrPayloadAsync(attachedFilePath, cleanMsg, text);
                        }
                        else
                        {
                            cleanMsg += $"\n\n[PDF_TEXT: {System.IO.Path.GetFileName(attachedFilePath)}]\n{text}";
                            payload = cleanMsg;
                        }
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
                    bool visionOcrEnabled = ToolConfigManager.GetVisionOcrBinding()?.Enabled == true;
                    int maxPixels = visionOcrEnabled ? UISettingsManager.Settings.VisionOcrClipboardMaxPixels : 1024;
                    VisionImageContext imageContext = visionOcrEnabled
                        ? RegisterVisionImageFromClipboardImage(attachedClipboardImage, "obraz ze schowka", maxPixels)
                        : null;
                    string base64 = imageContext != null
                        ? FileExtractor.GetImageBase64(imageContext.CachedPath, maxPixels)
                        : FileExtractor.GetImageBase64(attachedClipboardImage, ".png", maxPixels);
                    if (visionOcrEnabled)
                    {
                        payload = await BuildVisionOcrTextPayloadAsync(base64, cleanMsg, "obraz ze schowka", imageContext);
                    }
                    else
                    {
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
            else
            {
                string requeryPayload = await TryBuildPreviousImageRequeryPayloadAsync(cleanMsg);
                if (!string.IsNullOrWhiteSpace(requeryPayload))
                {
                    payload = requeryPayload;
                    AppendToHistory("SYSTEM", "Ponownie przeanalizowano ostatni obraz przez Vision/OCR.", Color.Orange);
                }
                else
                {
                    string cachedVisionPayload = TryBuildPreviousImageCachedPayload(cleanMsg);
                    if (!string.IsNullOrWhiteSpace(cachedVisionPayload))
                    {
                        payload = cachedVisionPayload;
                    }
                }
            }

            AppendToHistory("TY", rawInput, isDarkMode ? Color.LightSkyBlue : Color.Blue);
            AppendLoopLogWithColor($"[USER PROMPT]\n{rawInput}", isDarkMode ? Color.LightSkyBlue : Color.Blue);
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
                    bool earlyExitEnabled = chkEarlyExit?.Checked ?? true;
                    var result = await _supervisor.ProcessInputAsync(payload, new CadExecutionContext(doc), activeDwgPath, earlyExitEnabled);
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
            if (string.IsNullOrWhiteSpace(userMsg)) return;
            txtInput.Clear();
            await HandleUserInputAsync(userMsg);
        }

        public async Task HandleUserInputAsync(string userMsg)
        {
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

        public event Action<string, string, Color> OnHistoryAppended;

        public void AppendToHistory(string sender, string message, Color color)
        {
            OnHistoryAppended?.Invoke(sender, message, color);
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

                LoadAgentLlmBindingToUi(profileName, profile);
            }
        }

        private GroupBox BuildPathsLlmConfigGroup()
        {
            var group = new GroupBox
            {
                Text = "Folder plikow konfiguracyjnych LLM (llm_providers.json, ui_settings.json)",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("Folder ustawien AI/OCR:"), 0, 0);

            var pathBox = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 3, 0, 3)
            };
            pathBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pathBox.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathBox.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            TextBox txtCurrentLLMConfigPath = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 6, 0)
            };
            txtCurrentLLMConfigPath.Text = AppPaths.GetAppDataRoot();
            pathBox.Controls.Add(txtCurrentLLMConfigPath, 0, 0);

            var buttonsBox = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = Padding.Empty
            };

            Button btnChangeLLMConfigPath = new Button
            {
                Text = "Wybierz inny folder...",
                AutoSize = true,
                Height = 26,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 6, 0)
            };
            Button btnResetLLMConfigPath = new Button
            {
                Text = "Domyslny (AppData)",
                AutoSize = true,
                Height = 26,
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = Padding.Empty
            };
            buttonsBox.Controls.Add(btnChangeLLMConfigPath);
            buttonsBox.Controls.Add(btnResetLLMConfigPath);
            pathBox.Controls.Add(buttonsBox, 1, 0);
            grid.Controls.Add(pathBox, 1, 0);

            var llmInfoLabel = MakeInfoLabel(
                "Domyslnie pliki konfiguracyjne AI/OCR (Providerzy, UI, ustawienia agentow, Vision/OCR i tools_config.json) zapisywane sa w %APPDATA%\\Bricscad_AgentAI - dzieki temu przetrwaja kompilacje projektu. Mozesz wskazac inny folder (np. OneDrive), aby synchronizowac ustawienia miedzy komputerami. Zmiany wchodza w zycie po restarcie BricsCAD.",
                48);
            grid.Controls.Add(llmInfoLabel, 0, 1);
            grid.SetColumnSpan(llmInfoLabel, 2);

            btnChangeLLMConfigPath.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Wybierz folder docelowy dla plikow konfiguracyjnych AI/OCR:";
                    if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        UISettingsManager.UpdateCustomLLMConfigPath(fbd.SelectedPath);
                        txtCurrentLLMConfigPath.Text = AppPaths.GetAppDataRoot();
                        MessageBox.Show("Sciezka ustawien AI/OCR zostala zaktualizowana. Uruchom ponownie BricsCAD, aby zmiany weszly w zycie.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            btnResetLLMConfigPath.Click += (s, e) =>
            {
                UISettingsManager.UpdateCustomLLMConfigPath(string.Empty);
                txtCurrentLLMConfigPath.Text = AppPaths.GetAppDataRoot();
                MessageBox.Show("Przywrocono domyslna lokalizacje (AppData). Uruchom ponownie BricsCAD.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildPathsKnowledgeGroup()
        {
            var group = new GroupBox
            {
                Text = "Konfiguracja Bazy Wiedzy (CustomKnowledge)",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("Aktualny folder Baz:"), 0, 0);

            var pathBox = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 3, 0, 3)
            };
            pathBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pathBox.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathBox.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            TextBox txtCurrentPath = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 6, 0)
            };
            txtCurrentPath.Text = AppPaths.GetCustomKnowledgePath();
            pathBox.Controls.Add(txtCurrentPath, 0, 0);

            Button btnChangePath = new Button
            {
                Text = "Wybierz inny folder...",
                AutoSize = true,
                Height = 26,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = Padding.Empty
            };
            pathBox.Controls.Add(btnChangePath, 1, 0);
            grid.Controls.Add(pathBox, 1, 0);

            var knowledgeInfoLabel = MakeInfoLabel(
                "Domyslnie agent zapisuje wyuczone formuly i makra w folderze systemowym AppData. Mozesz zmienic ten folder na np. swoj dysk w chmurze (OneDrive/Dropbox), aby synchronizowac baze wiedzy miedzy komputerami.",
                48);
            grid.Controls.Add(knowledgeInfoLabel, 0, 1);
            grid.SetColumnSpan(knowledgeInfoLabel, 2);

            btnChangePath.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Wybierz folder docelowy dla Bazy Wiedzy (CustomKnowledge):";
                    if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        string oldPath = AppPaths.GetCustomKnowledgePath();
                        string newPath = fbd.SelectedPath;

                        if (oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase)) return;

                        bool shouldCopy = false;
                        if (MessageBox.Show("Zmieniono folder Bazy Wiedzy.\n\nCzy chcesz przeniesc (skopiowac) istniejace formuly i makra ze starego folderu do nowego?",
                            "Kopiowanie danych", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
                                    foreach (string dirPath in System.IO.Directory.GetDirectories(oldPath, "*", System.IO.SearchOption.AllDirectories))
                                    {
                                        System.IO.Directory.CreateDirectory(dirPath.Replace(oldPath, newPath));
                                    }
                                    foreach (string newFilePath in System.IO.Directory.GetFiles(oldPath, "*.*", System.IO.SearchOption.AllDirectories))
                                    {
                                        System.IO.File.Copy(newFilePath, newFilePath.Replace(oldPath, newPath), true);
                                    }
                                    MessageBox.Show("Dane zostaly poprawnie skopiowane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Wystapil blad podczas kopiowania plikow: {ex.Message}", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        Bricscad_AgentAI_V2.Core.DynamicSystems.DynamicFormulaManager.LoadAndCompileAll();
                        Bricscad_AgentAI_V2.Core.DynamicSystems.MacroManager.LoadAllMacros();
                        if (knowledgeBaseControl != null)
                        {
                            knowledgeBaseControl.LoadData();
                        }
                        MessageBox.Show("Sciezka do bazy wiedzy zostala zaktualizowana.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            group.Controls.Add(grid);
            return group;
        }

        private static Label MakeInfoLabel(string text, int height)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = height,
                Padding = new Padding(0, 6, 0, 0),
                ForeColor = Color.DarkGray
            };
        }

        private GroupBox BuildWorkflowStartupGroup()
        {
            var group = new GroupBox
            {
                Text = "Zachowanie przy starcie",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("System AI:"), 0, 0);
            ComboBox cmbAIStartup = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 3, 0, 3)
            };
            cmbAIStartup.Items.AddRange(new string[] { "Laduj poprzednia sesje", "Tworz nowa sesje", "Wybor manualny" });
            cmbAIStartup.SelectedIndex = ClampSelectedIndex(cmbAIStartup, UISettingsManager.Settings.AIStartupBehavior);
            cmbAIStartup.SelectedIndexChanged += (s, e) =>
            {
                UISettingsManager.Settings.AIStartupBehavior = cmbAIStartup.SelectedIndex;
                UISettingsManager.Save();
            };
            grid.Controls.Add(cmbAIStartup, 1, 0);

            grid.Controls.Add(MakeFieldLabel("BricsCAD:"), 0, 1);
            ComboBox cmbBricsCADStartup = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 3, 0, 3)
            };
            cmbBricsCADStartup.Items.AddRange(new string[] { "Automatycznie uruchom agenta AI", "Uruchomienie manualne (komenda \"AI\")" });
            cmbBricsCADStartup.SelectedIndex = ClampSelectedIndex(cmbBricsCADStartup, UISettingsManager.Settings.BricsCADStartupBehavior);
            cmbBricsCADStartup.SelectedIndexChanged += (s, e) =>
            {
                UISettingsManager.Settings.BricsCADStartupBehavior = cmbBricsCADStartup.SelectedIndex;
                UISettingsManager.Save();
            };
            grid.Controls.Add(cmbBricsCADStartup, 1, 1);

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildWorkflowWarmupGroup()
        {
            var group = new GroupBox
            {
                Text = "Ciche rozgrzewanie promptu Supervisora (lokalne modele LM Studio / llama.cpp)",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            CheckBox chkEnablePromptWarmup = new CheckBox
            {
                Text = "Wlacz ciche rozgrzewanie promptu Supervisora",
                AutoSize = true,
                ForeColor = Color.White,
                Margin = new Padding(0, 2, 0, 4)
            };
            chkEnablePromptWarmup.Checked = UISettingsManager.Settings.EnablePromptWarmup;
            chkEnablePromptWarmup.CheckedChanged += (s, e) =>
            {
                UISettingsManager.Settings.EnablePromptWarmup = chkEnablePromptWarmup.Checked;
                UISettingsManager.Save();
            };
            outer.Controls.Add(chkEnablePromptWarmup, 0, 0);

            CheckBox chkPromptWarmupOnAiOpen = new CheckBox
            {
                Text = "Rozgrzewaj prompt po otwarciu AI",
                AutoSize = true,
                ForeColor = Color.LightGray,
                Margin = new Padding(20, 0, 0, 4)
            };
            chkPromptWarmupOnAiOpen.Checked = UISettingsManager.Settings.PromptWarmupOnAiOpen;
            chkPromptWarmupOnAiOpen.CheckedChanged += (s, e) =>
            {
                UISettingsManager.Settings.PromptWarmupOnAiOpen = chkPromptWarmupOnAiOpen.Checked;
                UISettingsManager.Save();
            };
            outer.Controls.Add(chkPromptWarmupOnAiOpen, 0, 1);

            var idleRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 0)
            };
            idleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            idleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            idleRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            idleRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            CheckBox chkPromptWarmupAfterTyping = new CheckBox
            {
                Text = "Odswiez rozgrzanie po pauzie w pisaniu",
                AutoSize = true,
                ForeColor = Color.LightGray,
                Margin = new Padding(20, 2, 0, 0)
            };
            chkPromptWarmupAfterTyping.Checked = UISettingsManager.Settings.PromptWarmupAfterTypingIdleMs > 0;
            chkPromptWarmupAfterTyping.CheckedChanged += (s, e) =>
            {
                UISettingsManager.Settings.PromptWarmupAfterTypingIdleMs = chkPromptWarmupAfterTyping.Checked ? Math.Max(500, UISettingsManager.Settings.PromptWarmupAfterTypingIdleMs) : 0;
                UISettingsManager.Save();
            };
            idleRow.Controls.Add(chkPromptWarmupAfterTyping, 0, 0);
            idleRow.SetColumnSpan(chkPromptWarmupAfterTyping, 2);

            idleRow.Controls.Add(MakeFieldLabel("Pauza pisania (ms):"), 0, 1);
            idleRow.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 200f);
            NumericUpDown numWarmupIdle = new NumericUpDown
            {
                Dock = DockStyle.Left,
                Minimum = 500,
                Maximum = 30000,
                Increment = 500,
                Width = 110,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(20, 0, 0, 0)
            };
            numWarmupIdle.Value = Math.Max(500, UISettingsManager.Settings.PromptWarmupAfterTypingIdleMs > 0 ? UISettingsManager.Settings.PromptWarmupAfterTypingIdleMs : 2500);
            numWarmupIdle.ValueChanged += (s, e) =>
            {
                if (chkPromptWarmupAfterTyping.Checked)
                {
                    UISettingsManager.Settings.PromptWarmupAfterTypingIdleMs = (int)numWarmupIdle.Value;
                    UISettingsManager.Save();
                }
            };
            idleRow.Controls.Add(numWarmupIdle, 1, 1);

            outer.Controls.Add(idleRow, 0, 2);

            group.Controls.Add(outer);
            return group;
        }

        private TabPage CreateVisionOcrSettingsTab()
        {
            var tab = new TabPage("Vision/OCR") { Padding = new Padding(12) };

            var info = new Label
            {
                Text = "Globalny model Vision/OCR analizuje obrazy przed przekazaniem ich do glownego agenta. Glowny agent dostaje tekstowy wynik OCR/opisu i nadal uzywa swojego modelu oraz narzedzi.",
                Dock = DockStyle.Top,
                Height = 48,
                ForeColor = Color.DarkGray,
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblVisionOcrPreview = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.LightGreen,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 4, 4, 4)
            };

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 8,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            main.Controls.Add(BuildVisionOcrConnectionGroup(), 0, 0);
            main.Controls.Add(BuildVisionOcrPayloadGroup(), 0, 1);
            main.Controls.Add(BuildVisionOcrLoadingGroup(), 0, 2);
            main.Controls.Add(BuildVisionOcrImageQualityGroup(), 0, 3);
            main.Controls.Add(BuildVisionOcrTilingGroup(), 0, 4);
            main.Controls.Add(BuildVisionOcrPdfGroup(), 0, 5);
            main.Controls.Add(BuildVisionOcrQualityPresetGroup(), 0, 6);
            main.Controls.Add(BuildVisionOcrTestGroup(), 0, 7);

            scroll.Controls.Add(main);
            tab.Controls.Add(scroll);
            tab.Controls.Add(lblVisionOcrPreview);
            tab.Controls.Add(info);

            LoadVisionOcrBindingToUi();
            ScheduleVisionOcrModelRefreshIfNeeded();
            return tab;
        }

        private GroupBox BuildVisionOcrConnectionGroup()
        {
            var group = new GroupBox
            {
                Text = "Polaczenie",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            chkVisionOcrEnabled = new CheckBox
            {
                Text = "Wlacz nadrzedny model Vision/OCR",
                AutoSize = true,
                ForeColor = Color.White,
                Margin = new Padding(0, 2, 0, 6)
            };
            chkVisionOcrEnabled.CheckedChanged += VisionOcrControlChanged;
            grid.SetColumnSpan(chkVisionOcrEnabled, 3);
            grid.Controls.Add(chkVisionOcrEnabled, 0, 0);

            grid.Controls.Add(MakeFieldLabel("Provider:"), 0, 1);
            cbVisionOcrProvider = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 3, 6, 3)
            };
            cbVisionOcrProvider.SelectedIndexChanged += CbVisionOcrProvider_SelectedIndexChanged;
            grid.Controls.Add(cbVisionOcrProvider, 1, 1);

            btnRefreshVisionOcrModels = new Button
            {
                Text = "Modele",
                Width = 90,
                Height = 26,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 0, 2)
            };
            btnRefreshVisionOcrModels.Click += async (s, e) => await RefreshVisionOcrModelsAsync();
            grid.Controls.Add(btnRefreshVisionOcrModels, 2, 1);

            grid.Controls.Add(MakeFieldLabel("Model:"), 0, 2);
            cbVisionOcrModel = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 3, 0, 3)
            };
            cbVisionOcrModel.TextChanged += VisionOcrControlChanged;
            grid.Controls.Add(cbVisionOcrModel, 1, 2);
            grid.SetColumnSpan(cbVisionOcrModel, 2);

            chkVisionOcrUseProviderPayload = new CheckBox
            {
                Text = "Uzyj payloadu providera (odznacz, aby nadpisac parametry OCR)",
                AutoSize = true,
                ForeColor = Color.White,
                Margin = new Padding(0, 6, 0, 0)
            };
            chkVisionOcrUseProviderPayload.CheckedChanged += VisionOcrControlChanged;
            grid.SetColumnSpan(chkVisionOcrUseProviderPayload, 3);
            grid.Controls.Add(chkVisionOcrUseProviderPayload, 0, 3);

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildVisionOcrPayloadGroup()
        {
            var group = new GroupBox
            {
                Text = "Parametry zapytania OCR (aktywne, gdy payload providera jest wylaczony)",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            for (int i = 0; i < 8; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("Temperature"), 0, 0); grid.Controls.Add(MakeVisionNumeric(out numVisionOcrTemp, 0.0m, 2.0m, 2, 0.1m), 0, 1);
            grid.Controls.Add(MakeFieldLabel("Context (Ctx)"), 1, 0); grid.Controls.Add(MakeVisionNumeric(out numVisionOcrContext, 0m, 128000m, 0, 1000m), 1, 1);
            grid.Controls.Add(MakeFieldLabel("Max tokens"), 2, 0); grid.Controls.Add(MakeVisionNumeric(out numVisionOcrMaxTokens, 1m, 128000m, 0, 1000m), 2, 1);
            grid.Controls.Add(MakeFieldLabel("Top-P"), 3, 0); grid.Controls.Add(MakeVisionNumeric(out numVisionOcrTopP, 0.0m, 1.0m, 2, 0.05m), 3, 1);
            grid.Controls.Add(MakeFieldLabel("Top-K"), 4, 0); grid.Controls.Add(MakeVisionNumeric(out numVisionOcrTopK, 0m, 200m, 0, 1m), 4, 1);
            grid.Controls.Add(MakeFieldLabel("Min-P"), 5, 0); grid.Controls.Add(MakeVisionNumeric(out numVisionOcrMinP, 0.0m, 1.0m, 2, 0.05m), 5, 1);
            grid.Controls.Add(MakeFieldLabel("Repeat penalty"), 6, 0); grid.Controls.Add(MakeVisionNumeric(out numVisionOcrRepPenalty, 1.0m, 2.0m, 2, 0.05m), 6, 1);

            grid.Controls.Add(MakeFieldLabel("Reasoning effort"), 7, 0);
            cbVisionOcrReasoning = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(2, 2, 2, 2)
            };
            cbVisionOcrReasoning.Items.AddRange(new object[] { "none", "low", "medium", "high" });
            cbVisionOcrReasoning.SelectedIndexChanged += VisionOcrControlChanged;
            grid.Controls.Add(cbVisionOcrReasoning, 7, 1);

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildVisionOcrLoadingGroup()
        {
            var group = new GroupBox
            {
                Text = "Ladowanie modelu i polityka kontekstu",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            chkVisionOcrAutoLoad = new CheckBox
            {
                Text = "AutoLoad (zaladuj model do VRAM przed pierwszym OCR)",
                AutoSize = true,
                ForeColor = Color.White,
                Margin = new Padding(0, 4, 12, 4)
            };
            chkVisionOcrAutoLoad.CheckedChanged += VisionOcrControlChanged;
            grid.Controls.Add(chkVisionOcrAutoLoad, 0, 0);

            var policyRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = Padding.Empty
            };
            policyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            policyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            policyRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            policyRow.Controls.Add(MakeFieldLabel("Kontekst:"), 0, 0);
            cbVisionOcrContextPolicy = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 0, 2)
            };
            cbVisionOcrContextPolicy.Items.AddRange(new object[] { "UseLoadedIfAtLeastRequested", "NeverReloadAutomatically", "ReloadOnlyIfTooSmall" });
            cbVisionOcrContextPolicy.SelectedIndexChanged += VisionOcrControlChanged;
            policyRow.Controls.Add(cbVisionOcrContextPolicy, 1, 0);

            grid.Controls.Add(policyRow, 1, 0);

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildVisionOcrImageQualityGroup()
        {
            var group = new GroupBox
            {
                Text = "Jakosc obrazu wysylanego do modelu Vision/OCR",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("Schowek (px):"), 0, 0);
            numVisionOcrClipboardPixels = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 512,
                Maximum = 4096,
                DecimalPlaces = 0,
                Increment = 256,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 12, 2)
            };
            numVisionOcrClipboardPixels.ValueChanged += VisionOcrImageQualityChanged;
            grid.Controls.Add(numVisionOcrClipboardPixels, 1, 0);

            grid.Controls.Add(MakeFieldLabel("Zalaczniki (px):"), 2, 0);
            numVisionOcrAttachmentPixels = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 512,
                Maximum = 4096,
                DecimalPlaces = 0,
                Increment = 256,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 0, 2)
            };
            numVisionOcrAttachmentPixels.ValueChanged += VisionOcrImageQualityChanged;
            grid.Controls.Add(numVisionOcrAttachmentPixels, 3, 0);

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildVisionOcrTilingGroup()
        {
            var group = new GroupBox
            {
                Text = "Adaptacyjny tiling duzych arkuszy",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            for (int i = 0; i < 5; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("Tiling"), 0, 0);
            cbVisionOcrTilingMode = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(2, 2, 2, 2)
            };
            cbVisionOcrTilingMode.Items.AddRange(new object[] { "Auto", "Wylaczony", "Zawsze" });
            cbVisionOcrTilingMode.SelectedIndexChanged += VisionOcrTilingChanged;
            grid.Controls.Add(cbVisionOcrTilingMode, 0, 1);

            grid.Controls.Add(MakeFieldLabel("Kafelek px"), 1, 0);
            grid.Controls.Add(MakeVisionNumeric(out numVisionOcrTileMaxDim, 512m, 8192m, 0, 128m), 1, 1);
            numVisionOcrTileMaxDim.ValueChanged += VisionOcrTilingChanged;

            grid.Controls.Add(MakeFieldLabel("Overlap px"), 2, 0);
            grid.Controls.Add(MakeVisionNumeric(out numVisionOcrTileOverlap, 0m, 2048m, 0, 50m), 2, 1);
            numVisionOcrTileOverlap.ValueChanged += VisionOcrTilingChanged;

            grid.Controls.Add(MakeFieldLabel("Maks. kafelkow"), 3, 0);
            grid.Controls.Add(MakeVisionNumeric(out numVisionOcrTileMaxCount, 1m, 64m, 0, 1m), 3, 1);
            numVisionOcrTileMaxCount.ValueChanged += VisionOcrTilingChanged;

            grid.Controls.Add(MakeFieldLabel("Maks. proporcja"), 4, 0);
            grid.Controls.Add(MakeVisionNumeric(out numVisionOcrTileMaxAspect, 1.0m, 6.0m, 1, 0.1m), 4, 1);
            numVisionOcrTileMaxAspect.ValueChanged += VisionOcrTilingChanged;

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildVisionOcrPdfGroup()
        {
            var group = new GroupBox
            {
                Text = "PDF -> tekst + PNG dla Vision/OCR",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("DPI:"), 0, 0);
            grid.Controls.Add(MakeVisionNumeric(out numVisionOcrPdfDpi, 72m, 600m, 0, 25m), 1, 0);
            numVisionOcrPdfDpi.ValueChanged += VisionOcrPdfChanged;

            grid.Controls.Add(MakeFieldLabel("Maks. stron:"), 2, 0);
            grid.Controls.Add(MakeVisionNumeric(out numVisionOcrPdfMaxPages, 1m, 50m, 0, 1m), 3, 0);
            numVisionOcrPdfMaxPages.ValueChanged += VisionOcrPdfChanged;

            grid.Controls.Add(MakeFieldLabel("Renderer:"), 4, 0);
            txtVisionOcrPdfRendererPath = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 0, 2)
            };
            txtVisionOcrPdfRendererPath.TextChanged += VisionOcrPdfChanged;
            grid.Controls.Add(txtVisionOcrPdfRendererPath, 5, 0);

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildVisionOcrQualityPresetGroup()
        {
            var group = new GroupBox
            {
                Text = "Presety jakosci obrazu i tilingu",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("Preset:"), 0, 0);
            cbVisionOcrQualityPreset = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 8, 2)
            };
            cbVisionOcrQualityPreset.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressVisionOcrUiEvents) return;
                ApplySelectedVisionOcrQualityPreset();
            };
            grid.Controls.Add(cbVisionOcrQualityPreset, 1, 0);

            grid.Controls.Add(MakeFieldLabel("Nazwa:"), 2, 0);
            txtVisionOcrPresetName = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 8, 2)
            };
            grid.Controls.Add(txtVisionOcrPresetName, 3, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = Padding.Empty
            };
            buttons.Controls.Add(MakeSmallVisionButton("Zapisz", (s, e) => SaveVisionOcrQualityPreset(false)));
            buttons.Controls.Add(MakeSmallVisionButton("Nadpisz", (s, e) => SaveVisionOcrQualityPreset(true)));
            buttons.Controls.Add(MakeSmallVisionButton("Usun", (s, e) => DeleteSelectedVisionOcrQualityPreset()));
            grid.Controls.Add(buttons, 4, 0);
            grid.SetColumnSpan(buttons, 2);

            group.Controls.Add(grid);
            return group;
        }

        private GroupBox BuildVisionOcrTestGroup()
        {
            var group = new GroupBox
            {
                Text = "Szybki test Vision/OCR",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 10),
                ForeColor = Color.LightGray
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 8,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < 8; i++) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grid.Controls.Add(MakeFieldLabel("Plik PNG/JPG/PDF:"), 0, 0);
            txtVisionOcrTestFile = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 8, 2)
            };
            grid.Controls.Add(txtVisionOcrTestFile, 1, 0);
            grid.Controls.Add(MakeSmallVisionButton("Wybierz", (s, e) => BrowseVisionOcrTestFile()), 2, 0);

            grid.Controls.Add(MakeFieldLabel("Prompt testowy:"), 0, 1);
            txtVisionOcrTestPrompt = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = 54,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Odczytaj tabliczke rysunkowa i podaj dane inwestora, projektu, adresu, numer rysunku, date i skale.",
                Margin = new Padding(0, 2, 8, 2)
            };
            txtVisionOcrTestPrompt.TextChanged += (s, e) =>
            {
                if (txtVisionOcrSystemPrompt != null)
                {
                    txtVisionOcrSystemPrompt.Text = BuildVisionOcrPromptPreview(txtVisionOcrTestPrompt.Text);
                }
            };
            grid.Controls.Add(txtVisionOcrTestPrompt, 1, 1);
            grid.SetColumnSpan(txtVisionOcrTestPrompt, 2);

            grid.Controls.Add(MakeFieldLabel("Prompt OCR:"), 0, 2);
            txtVisionOcrSystemPrompt = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = 92,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 8, 2)
            };
            grid.Controls.Add(txtVisionOcrSystemPrompt, 1, 2);
            grid.SetColumnSpan(txtVisionOcrSystemPrompt, 2);
            txtVisionOcrSystemPrompt.Text = BuildVisionOcrPromptPreview(txtVisionOcrTestPrompt.Text);

            btnVisionOcrTestRun = MakeSmallVisionButton("Uruchom OCR", async (s, e) => await RunVisionOcrTestAsync());
            btnVisionOcrTestRun.Width = 120;
            grid.Controls.Add(btnVisionOcrTestRun, 2, 3);

            grid.Controls.Add(MakeFieldLabel("Odpowiedz:"), 0, 4);
            txtVisionOcrTestResult = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = 220,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                WordWrap = false,
                Margin = new Padding(0, 2, 0, 2)
            };
            grid.Controls.Add(txtVisionOcrTestResult, 1, 4);
            grid.SetColumnSpan(txtVisionOcrTestResult, 2);

            grid.Controls.Add(MakeFieldLabel("Zapisane testy:"), 0, 5);
            lstVisionOcrTestRuns = new ListBox
            {
                Dock = DockStyle.Fill,
                Height = 96,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                Margin = new Padding(0, 2, 8, 2)
            };
            lstVisionOcrTestRuns.SelectedIndexChanged += (s, e) => PreviewSelectedVisionOcrTestRun();
            lstVisionOcrTestRuns.DoubleClick += (s, e) => OpenSelectedVisionOcrTestReport();
            grid.Controls.Add(lstVisionOcrTestRuns, 1, 5);
            grid.SetColumnSpan(lstVisionOcrTestRuns, 2);

            var reportButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 2)
            };
            reportButtons.Controls.Add(MakeSmallVisionButton("Otworz raport", (s, e) => OpenSelectedVisionOcrTestReport()));
            reportButtons.Controls.Add(MakeSmallVisionButton("Kopiuj raport", (s, e) => CopySelectedVisionOcrTestReport()));
            reportButtons.Controls.Add(MakeSmallVisionButton("Otworz folder", (s, e) => OpenVisionOcrTestReportsFolder()));
            reportButtons.Controls.Add(MakeSmallVisionButton("Odswiez", (s, e) => RefreshVisionOcrTestRunList()));
            grid.Controls.Add(reportButtons, 1, 6);
            grid.SetColumnSpan(reportButtons, 2);

            var info = MakeInfoLabel("Test uzywa aktualnie zapisanych ustawien Vision/OCR, jakosci obrazu i tilingu. Nie dopisuje wyniku do sesji czatu.", 34);
            grid.Controls.Add(info, 0, 7);
            grid.SetColumnSpan(info, 3);

            group.Controls.Add(grid);
            RefreshVisionOcrTestRunList();
            return group;
        }

        private class VisionOcrTestRunListItem
        {
            public string DisplayName { get; set; }
            public string MarkdownPath { get; set; }
            public string JsonPath { get; set; }

            public override string ToString()
            {
                return DisplayName ?? Path.GetFileName(MarkdownPath);
            }
        }

        private void RefreshVisionOcrTestRunList()
        {
            if (lstVisionOcrTestRuns == null) return;

            lstVisionOcrTestRuns.Items.Clear();
            string folder = AppPaths.GetVisionOcrTestRunsPath();
            Directory.CreateDirectory(folder);

            foreach (string mdPath in Directory.GetFiles(folder, "*.md")
                .OrderByDescending(File.GetLastWriteTime)
                .Take(100))
            {
                string jsonPath = Path.ChangeExtension(mdPath, ".json");
                lstVisionOcrTestRuns.Items.Add(new VisionOcrTestRunListItem
                {
                    DisplayName = BuildVisionOcrTestRunDisplayName(mdPath, jsonPath),
                    MarkdownPath = mdPath,
                    JsonPath = jsonPath
                });
            }
        }

        private static string BuildVisionOcrTestRunDisplayName(string markdownPath, string jsonPath)
        {
            try
            {
                if (!File.Exists(jsonPath))
                {
                    return Path.GetFileName(markdownPath);
                }

                var json = JObject.Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
                string created = ParseVisionOcrReportDate(json["created_at"]?.ToString());
                string status = json["success"]?.Value<bool>() == true ? "OK" : "ERR";
                string source = json["source_file"]?["name"]?.ToString() ?? Path.GetFileName(markdownPath);
                string preset = json["preset"]?["SelectedName"]?.ToString();
                string provider = json["provider"]?["Name"]?.ToString();
                string model = json["model_payload"]?["UiModel"]?.ToString();
                if (string.IsNullOrWhiteSpace(model))
                {
                    model = json["provider"]?["ModelName"]?.ToString();
                }

                if (string.IsNullOrWhiteSpace(preset)) preset = "bez presetu";
                if (string.IsNullOrWhiteSpace(provider)) provider = "brak providera";
                if (string.IsNullOrWhiteSpace(model)) model = "brak modelu";

                return $"{created} [{status}] {source} | preset: {preset} | {provider} | {model}";
            }
            catch
            {
                return Path.GetFileName(markdownPath);
            }
        }

        private static string ParseVisionOcrReportDate(string value)
        {
            if (DateTime.TryParse(value, out var date))
            {
                return date.ToString("yyyy-MM-dd HH:mm:ss");
            }

            return File.Exists(value) ? File.GetLastWriteTime(value).ToString("yyyy-MM-dd HH:mm:ss") : "";
        }

        private VisionOcrTestRunListItem GetSelectedVisionOcrTestRun()
        {
            return lstVisionOcrTestRuns?.SelectedItem as VisionOcrTestRunListItem;
        }

        private void PreviewSelectedVisionOcrTestRun()
        {
            var item = GetSelectedVisionOcrTestRun();
            if (item == null || txtVisionOcrTestResult == null) return;
            PreviewVisionOcrTestRun(item);
        }

        private void PreviewVisionOcrTestRun(VisionOcrTestRunListItem item)
        {
            if (item == null || txtVisionOcrTestResult == null) return;

            try
            {
                if (File.Exists(item.MarkdownPath))
                {
                    txtVisionOcrTestResult.Text = File.ReadAllText(item.MarkdownPath, Encoding.UTF8);
                    return;
                }

                if (File.Exists(item.JsonPath))
                {
                    txtVisionOcrTestResult.Text = File.ReadAllText(item.JsonPath, Encoding.UTF8);
                    return;
                }

                txtVisionOcrTestResult.Text = "Raport Vision/OCR nie istnieje juz na dysku: " + item.MarkdownPath;
            }
            catch (Exception ex)
            {
                txtVisionOcrTestResult.Text = "Nie udalo sie wczytac raportu Vision/OCR: " + ex.Message;
            }
        }

        private void OpenSelectedVisionOcrTestReport()
        {
            var item = GetSelectedVisionOcrTestRun();
            if (item == null || !File.Exists(item.MarkdownPath))
            {
                MessageBox.Show("Wybierz zapisany raport Vision/OCR.", "Vision/OCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = item.MarkdownPath, UseShellExecute = true });
        }

        private void CopySelectedVisionOcrTestReport()
        {
            var item = GetSelectedVisionOcrTestRun();
            if (item == null || !File.Exists(item.MarkdownPath))
            {
                MessageBox.Show("Wybierz zapisany raport Vision/OCR.", "Vision/OCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Clipboard.SetText(File.ReadAllText(item.MarkdownPath, Encoding.UTF8));
            UpdateStatusHUD("Skopiowano raport Vision/OCR do schowka.");
        }

        private void OpenVisionOcrTestReportsFolder()
        {
            string folder = AppPaths.GetVisionOcrTestRunsPath();
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }

        private void SaveVisionOcrTestReport(string sourcePath, string userPrompt, string systemPrompt, string resultText, string diagnostics, bool success, string error)
        {
            string folder = AppPaths.GetVisionOcrTestRunsPath();
            Directory.CreateDirectory(folder);

            string testId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string sourceName = Path.GetFileName(sourcePath);
            string presetName = cbVisionOcrQualityPreset?.Text ?? UISettingsManager.Settings.LastVisionOcrQualityPresetName ?? string.Empty;
            string status = success ? "OK" : "ERROR";
            string baseName = $"{testId}_{status}_{MakeSafeReportFileName(Path.GetFileNameWithoutExtension(sourceName))}";
            string jsonPath = Path.Combine(folder, baseName + ".json");
            string markdownPath = Path.Combine(folder, baseName + ".md");

            var effectiveProvider = LLMConfigManager.ResolveVisionOcrProvider();
            var uiProvider = GetSelectedVisionOcrProvider();
            var provider = effectiveProvider ?? uiProvider ?? LLMConfigManager.GetActiveProvider();
            var binding = ToolConfigManager.GetVisionOcrBinding();
            var settings = UISettingsManager.Settings;
            var sourceInfo = new FileInfo(sourcePath);

            var report = new
            {
                report_version = "1.0",
                test_id = testId,
                created_at = DateTime.Now,
                success,
                error,
                source_file = new
                {
                    path = sourcePath,
                    name = sourceName,
                    extension = Path.GetExtension(sourcePath),
                    size_bytes = sourceInfo.Exists ? sourceInfo.Length : 0
                },
                provider = provider == null ? null : new
                {
                    provider.Id,
                    provider.Name,
                    provider.EndpointUrl,
                    provider.ModelName,
                    ApiKeyConfigured = !string.IsNullOrWhiteSpace(provider.ApiKey) && provider.ApiKey != "not-needed"
                },
                ui_provider = uiProvider == null ? null : new
                {
                    uiProvider.Id,
                    uiProvider.Name,
                    uiProvider.EndpointUrl,
                    uiProvider.ModelName
                },
                model_payload = new
                {
                    UiModel = cbVisionOcrModel?.Text,
                    EffectiveModel = provider?.ModelName,
                    UseProviderPayload = chkVisionOcrUseProviderPayload?.Checked == true,
                    Temperature = numVisionOcrTemp?.Value,
                    Context = numVisionOcrContext?.Value,
                    MaxTokens = numVisionOcrMaxTokens?.Value,
                    TopP = numVisionOcrTopP?.Value,
                    TopK = numVisionOcrTopK?.Value,
                    MinP = numVisionOcrMinP?.Value,
                    RepetitionPenalty = numVisionOcrRepPenalty?.Value,
                    ReasoningEffort = cbVisionOcrReasoning?.Text,
                    AutoLoad = chkVisionOcrAutoLoad?.Checked == true,
                    ContextPolicy = cbVisionOcrContextPolicy?.Text,
                    Binding = binding
                },
                image_quality = new
                {
                    ClipboardMaxPixels = settings.VisionOcrClipboardMaxPixels,
                    AttachmentMaxPixels = settings.VisionOcrAttachmentMaxPixels
                },
                tiling = new
                {
                    Mode = settings.VisionOcrTilingMode,
                    TileMaxDim = settings.VisionOcrTileMaxDim,
                    Overlap = settings.VisionOcrTileOverlap,
                    MaxTileCount = settings.VisionOcrTileMaxCount,
                    MaxAspectRatio = settings.VisionOcrTileMaxAspectRatio
                },
                pdf = new
                {
                    Dpi = settings.VisionOcrPdfDpi,
                    MaxPages = settings.VisionOcrPdfMaxPages,
                    RendererPath = settings.VisionOcrPdfRendererPath
                },
                preset = new
                {
                    SelectedName = presetName,
                    LastSavedName = settings.LastVisionOcrQualityPresetName
                },
                prompts = new
                {
                    UserPrompt = userPrompt,
                    SystemPromptPreview = systemPrompt
                },
                diagnostics,
                response = resultText
            };

            string json = JsonConvert.SerializeObject(report, Formatting.Indented);
            File.WriteAllText(jsonPath, json, Encoding.UTF8);
            File.WriteAllText(markdownPath, BuildVisionOcrTestMarkdown(report, json, jsonPath), Encoding.UTF8);

            RefreshVisionOcrTestRunList();
            SelectVisionOcrReportInList(markdownPath);
            UpdateStatusHUD("Zapisano raport Vision/OCR: " + Path.GetFileName(markdownPath));
        }

        private static string BuildVisionOcrTestMarkdown(object report, string reportJson, string jsonPath)
        {
            var token = JObject.FromObject(report);
            var sb = new StringBuilder();
            sb.AppendLine("# Vision/OCR Test Run");
            sb.AppendLine();
            sb.AppendLine("- Test ID: `" + token["test_id"] + "`");
            sb.AppendLine("- Data: `" + token["created_at"] + "`");
            sb.AppendLine("- Status: `" + ((bool)token["success"] ? "OK" : "ERROR") + "`");
            sb.AppendLine("- Plik: `" + token["source_file"]?["path"] + "`");
            sb.AppendLine("- Provider: `" + token["provider"]?["Name"] + "`");
            sb.AppendLine("- Model: `" + (token["model_payload"]?["EffectiveModel"] ?? token["model_payload"]?["UiModel"] ?? token["provider"]?["ModelName"]) + "`");
            sb.AppendLine("- JSON: `" + jsonPath + "`");
            sb.AppendLine();
            sb.AppendLine("## Prompt Uzytkownika");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine((string)token["prompts"]?["UserPrompt"] ?? string.Empty);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## Prompt Systemowy / Payload Tekstowy");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine((string)token["prompts"]?["SystemPromptPreview"] ?? string.Empty);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## Diagnostyka");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine((string)token["diagnostics"] ?? string.Empty);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## Odpowiedz");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine((string)token["response"] ?? string.Empty);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## Pelny Snapshot JSON");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(reportJson);
            sb.AppendLine("```");
            return sb.ToString();
        }

        private void SelectVisionOcrReportInList(string markdownPath)
        {
            if (lstVisionOcrTestRuns == null) return;
            for (int i = 0; i < lstVisionOcrTestRuns.Items.Count; i++)
            {
                var item = lstVisionOcrTestRuns.Items[i] as VisionOcrTestRunListItem;
                if (item != null && string.Equals(item.MarkdownPath, markdownPath, StringComparison.OrdinalIgnoreCase))
                {
                    lstVisionOcrTestRuns.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string MakeSafeReportFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "vision_ocr_test";
            string safe = value;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }

            safe = safe.Trim();
            if (safe.Length > 80) safe = safe.Substring(0, 80);
            return string.IsNullOrWhiteSpace(safe) ? "vision_ocr_test" : safe;
        }

        private Button MakeSmallVisionButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 26,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 6, 2)
            };
            button.Click += onClick;
            return button;
        }

        private void RefreshVisionOcrQualityPresetDropdown()
        {
            if (cbVisionOcrQualityPreset == null) return;
            var presets = UISettingsManager.Settings.VisionOcrQualityPresets ?? new List<VisionOcrQualityPreset>();

            try
            {
                _suppressVisionOcrUiEvents = true;
                string current = UISettingsManager.Settings.LastVisionOcrQualityPresetName;
                cbVisionOcrQualityPreset.Items.Clear();
                foreach (var preset in presets.OrderBy(p => p.Name))
                {
                    if (!string.IsNullOrWhiteSpace(preset.Name))
                    {
                        cbVisionOcrQualityPreset.Items.Add(preset.Name);
                    }
                }

                if (!string.IsNullOrWhiteSpace(current) && cbVisionOcrQualityPreset.Items.Contains(current))
                {
                    cbVisionOcrQualityPreset.SelectedItem = current;
                    if (txtVisionOcrPresetName != null) txtVisionOcrPresetName.Text = current;
                }
                else if (cbVisionOcrQualityPreset.Items.Count > 0)
                {
                    cbVisionOcrQualityPreset.SelectedIndex = 0;
                    if (txtVisionOcrPresetName != null) txtVisionOcrPresetName.Text = cbVisionOcrQualityPreset.Text;
                }
            }
            finally
            {
                _suppressVisionOcrUiEvents = false;
            }
        }

        private void ApplySelectedVisionOcrQualityPreset()
        {
            string name = cbVisionOcrQualityPreset?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return;

            var preset = (UISettingsManager.Settings.VisionOcrQualityPresets ?? new List<VisionOcrQualityPreset>())
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (preset == null) return;

            try
            {
                _suppressVisionOcrUiEvents = true;
                ApplyVisionOcrQualityPresetToControls(preset);
                if (txtVisionOcrPresetName != null) txtVisionOcrPresetName.Text = preset.Name;
                UISettingsManager.Settings.LastVisionOcrQualityPresetName = preset.Name;
                SaveVisionOcrQualityAndTilingSettings();
            }
            finally
            {
                _suppressVisionOcrUiEvents = false;
            }

            UISettingsManager.Save();
            UpdateVisionOcrPreview();
        }

        private void ApplyLastVisionOcrQualityPresetToControls()
        {
            string name = UISettingsManager.Settings.LastVisionOcrQualityPresetName;
            if (string.IsNullOrWhiteSpace(name)) return;

            var preset = (UISettingsManager.Settings.VisionOcrQualityPresets ?? new List<VisionOcrQualityPreset>())
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (preset == null) return;

            ApplyVisionOcrQualityPresetToControls(preset);
            if (cbVisionOcrQualityPreset != null && cbVisionOcrQualityPreset.Items.Contains(preset.Name))
            {
                cbVisionOcrQualityPreset.SelectedItem = preset.Name;
            }

            if (txtVisionOcrPresetName != null) txtVisionOcrPresetName.Text = preset.Name;
            SaveVisionOcrQualityAndTilingSettings();
        }

        private void SaveVisionOcrQualityPreset(bool overwrite)
        {
            string name = (overwrite ? cbVisionOcrQualityPreset?.SelectedItem?.ToString() : txtVisionOcrPresetName?.Text)?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Podaj nazwe presetu.", "Vision/OCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (UISettingsManager.Settings.VisionOcrQualityPresets == null)
            {
                UISettingsManager.Settings.VisionOcrQualityPresets = new List<VisionOcrQualityPreset>();
            }

            var presets = UISettingsManager.Settings.VisionOcrQualityPresets;
            var existing = presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null && !overwrite)
            {
                MessageBox.Show("Preset o tej nazwie juz istnieje. Uzyj Nadpisz albo wybierz inna nazwe.", "Vision/OCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var preset = BuildVisionOcrQualityPresetFromControls(name);
            if (existing == null)
            {
                presets.Add(preset);
            }
            else
            {
                existing.ClipboardMaxPixels = preset.ClipboardMaxPixels;
                existing.AttachmentMaxPixels = preset.AttachmentMaxPixels;
                existing.TilingMode = preset.TilingMode;
                existing.TileMaxDim = preset.TileMaxDim;
                existing.TileOverlap = preset.TileOverlap;
                existing.TileMaxCount = preset.TileMaxCount;
                existing.TileMaxAspectRatio = preset.TileMaxAspectRatio;
            }

            UISettingsManager.Settings.LastVisionOcrQualityPresetName = name;
            UISettingsManager.Save();
            RefreshVisionOcrQualityPresetDropdown();
            if (cbVisionOcrQualityPreset != null) cbVisionOcrQualityPreset.SelectedItem = name;
        }

        private void DeleteSelectedVisionOcrQualityPreset()
        {
            string name = cbVisionOcrQualityPreset?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (MessageBox.Show($"Usunac preset '{name}'?", "Vision/OCR", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var presets = UISettingsManager.Settings.VisionOcrQualityPresets;
            if (presets != null)
            {
                presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(UISettingsManager.Settings.LastVisionOcrQualityPresetName, name, StringComparison.OrdinalIgnoreCase))
            {
                UISettingsManager.Settings.LastVisionOcrQualityPresetName = string.Empty;
            }

            UISettingsManager.Save();
            RefreshVisionOcrQualityPresetDropdown();
        }

        private VisionOcrQualityPreset BuildVisionOcrQualityPresetFromControls(string name)
        {
            return new VisionOcrQualityPreset
            {
                Name = name,
                ClipboardMaxPixels = numVisionOcrClipboardPixels != null ? (int)numVisionOcrClipboardPixels.Value : UISettingsManager.Settings.VisionOcrClipboardMaxPixels,
                AttachmentMaxPixels = numVisionOcrAttachmentPixels != null ? (int)numVisionOcrAttachmentPixels.Value : UISettingsManager.Settings.VisionOcrAttachmentMaxPixels,
                TilingMode = cbVisionOcrTilingMode?.SelectedItem?.ToString() ?? UISettingsManager.Settings.VisionOcrTilingMode ?? "Auto",
                TileMaxDim = numVisionOcrTileMaxDim != null ? (int)numVisionOcrTileMaxDim.Value : UISettingsManager.Settings.VisionOcrTileMaxDim,
                TileOverlap = numVisionOcrTileOverlap != null ? (int)numVisionOcrTileOverlap.Value : UISettingsManager.Settings.VisionOcrTileOverlap,
                TileMaxCount = numVisionOcrTileMaxCount != null ? (int)numVisionOcrTileMaxCount.Value : UISettingsManager.Settings.VisionOcrTileMaxCount,
                TileMaxAspectRatio = numVisionOcrTileMaxAspect != null ? (double)numVisionOcrTileMaxAspect.Value : UISettingsManager.Settings.VisionOcrTileMaxAspectRatio
            };
        }

        private void ApplyVisionOcrQualityPresetToControls(VisionOcrQualityPreset preset)
        {
            if (preset == null) return;
            if (numVisionOcrClipboardPixels != null) numVisionOcrClipboardPixels.Value = ClampAgentDecimal(preset.ClipboardMaxPixels, numVisionOcrClipboardPixels.Minimum, numVisionOcrClipboardPixels.Maximum);
            if (numVisionOcrAttachmentPixels != null) numVisionOcrAttachmentPixels.Value = ClampAgentDecimal(preset.AttachmentMaxPixels, numVisionOcrAttachmentPixels.Minimum, numVisionOcrAttachmentPixels.Maximum);
            if (cbVisionOcrTilingMode != null) cbVisionOcrTilingMode.SelectedItem = cbVisionOcrTilingMode.Items.Contains(preset.TilingMode) ? preset.TilingMode : "Auto";
            if (numVisionOcrTileMaxDim != null) numVisionOcrTileMaxDim.Value = ClampAgentDecimal(preset.TileMaxDim, numVisionOcrTileMaxDim.Minimum, numVisionOcrTileMaxDim.Maximum);
            if (numVisionOcrTileOverlap != null) numVisionOcrTileOverlap.Value = ClampAgentDecimal(preset.TileOverlap, numVisionOcrTileOverlap.Minimum, numVisionOcrTileOverlap.Maximum);
            if (numVisionOcrTileMaxCount != null) numVisionOcrTileMaxCount.Value = ClampAgentDecimal(preset.TileMaxCount, numVisionOcrTileMaxCount.Minimum, numVisionOcrTileMaxCount.Maximum);
            if (numVisionOcrTileMaxAspect != null) numVisionOcrTileMaxAspect.Value = ClampAgentDecimal((decimal)preset.TileMaxAspectRatio, numVisionOcrTileMaxAspect.Minimum, numVisionOcrTileMaxAspect.Maximum);
        }

        private void SaveVisionOcrQualityAndTilingSettings()
        {
            if (numVisionOcrClipboardPixels != null) UISettingsManager.Settings.VisionOcrClipboardMaxPixels = (int)numVisionOcrClipboardPixels.Value;
            if (numVisionOcrAttachmentPixels != null) UISettingsManager.Settings.VisionOcrAttachmentMaxPixels = (int)numVisionOcrAttachmentPixels.Value;
            if (cbVisionOcrTilingMode != null) UISettingsManager.Settings.VisionOcrTilingMode = cbVisionOcrTilingMode.SelectedItem?.ToString() ?? "Auto";
            if (numVisionOcrTileMaxDim != null) UISettingsManager.Settings.VisionOcrTileMaxDim = (int)numVisionOcrTileMaxDim.Value;
            if (numVisionOcrTileOverlap != null) UISettingsManager.Settings.VisionOcrTileOverlap = (int)numVisionOcrTileOverlap.Value;
            if (numVisionOcrTileMaxCount != null) UISettingsManager.Settings.VisionOcrTileMaxCount = (int)numVisionOcrTileMaxCount.Value;
            if (numVisionOcrTileMaxAspect != null) UISettingsManager.Settings.VisionOcrTileMaxAspectRatio = (double)numVisionOcrTileMaxAspect.Value;
        }

        private string BuildVisionOcrPromptPreview(string userPrompt)
        {
            return "[SYSTEM]\r\n" + LLMClient.BuildVisionOcrSystemPrompt() +
                "\r\n\r\n[USER TEXT]\r\n" + LLMClient.BuildVisionOcrUserPrompt(userPrompt, "Tu zostanie dopisany prompt przestrzenny tilingu, jesli obraz zostanie pociety na kafelki.");
        }

        private void BrowseVisionOcrTestFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Obrazy i PDF|*.png;*.jpg;*.jpeg;*.pdf|Wszystkie pliki|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtVisionOcrTestFile.Text = ofd.FileName;
                }
            }
        }

        private async Task RunVisionOcrTestAsync()
        {
            string path = txtVisionOcrTestFile?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show("Wybierz istniejacy plik PNG/JPG/PDF.", "Vision/OCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveVisionOcrBindingFromUi();
            SaveVisionOcrQualityAndTilingSettings();
            UISettingsManager.Save();

            btnVisionOcrTestRun.Enabled = false;
            txtVisionOcrTestResult.Text = "Przetwarzanie OCR...";
            string prompt = string.IsNullOrWhiteSpace(txtVisionOcrTestPrompt?.Text)
                ? "Przeanalizuj obraz."
                : txtVisionOcrTestPrompt.Text.Trim();
            string systemPrompt = BuildVisionOcrPromptPreview(prompt);
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".pdf")
                {
                    string pdfText = FileExtractor.ExtractText(path);
                    string pdfPayload = await BuildPdfVisionOcrPayloadAsync(path, prompt, pdfText);
                    string pdfDiagnostics = string.IsNullOrWhiteSpace(_llmClient.LastVisionOcrDiagnostics)
                        ? "Brak diagnostyki odpowiedzi OCR."
                        : _llmClient.LastVisionOcrDiagnostics;
                    string output = "[DIAGNOSTYKA OCR]\r\n" + pdfDiagnostics + "\r\n\r\n[PDF HYBRYDOWY]\r\n" + pdfPayload;
                    txtVisionOcrTestResult.Text = output;
                    bool pdfOcrOk = !pdfPayload.Contains("[PDF_VISION/OCR ERROR]") && !pdfPayload.Contains("BLAD OCR PDF:");
                    if (!pdfOcrOk)
                    {
                        UpdateStatusHUD("Blad Vision/OCR PDF - render PDF mogl sie udac, ale model OCR zwrocil blad.");
                    }
                    SaveVisionOcrTestReport(path, prompt, systemPrompt, output, pdfDiagnostics, pdfOcrOk, pdfOcrOk ? null : "Blad modelu Vision/OCR podczas analizy PDF.");
                    return;
                }

                string base64 = FileExtractor.GetImageBase64(path, UISettingsManager.Settings.VisionOcrAttachmentMaxPixels);
                var imageContext = new VisionImageContext
                {
                    ImageId = "ocr_test_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"),
                    SourceLabel = "test Vision/OCR",
                    CachedPath = path,
                    OriginalPath = path,
                    MaxPixelsUsed = UISettingsManager.Settings.VisionOcrAttachmentMaxPixels,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                using (var img = System.Drawing.Image.FromFile(path))
                {
                    imageContext.OriginalWidth = img.Width;
                    imageContext.OriginalHeight = img.Height;
                }
                ApplyVisionOcrTilingIfNeeded(imageContext);

                var imageUrls = BuildVisionOcrImageDataUrls(base64, imageContext);
                string technicalContext = BuildTileSpatialPrompt(imageContext);
                var result = await _llmClient.AnalyzeImagesWithVisionOcrAsync(imageUrls, prompt, technicalContext);
                string diagnostics = string.IsNullOrWhiteSpace(_llmClient.LastVisionOcrDiagnostics)
                    ? "Brak diagnostyki odpowiedzi OCR."
                    : _llmClient.LastVisionOcrDiagnostics;
                string normalOutput = "[DIAGNOSTYKA OCR]\r\n" + diagnostics + "\r\n\r\n[ODPOWIEDZ OCR]\r\n" + (result.ok ? result.text : "BLAD OCR: " + result.text);
                txtVisionOcrTestResult.Text = normalOutput;
                SaveVisionOcrTestReport(path, prompt, systemPrompt, normalOutput, diagnostics, result.ok, result.ok ? null : result.text);
            }
            catch (Exception ex)
            {
                string errorOutput = "BLAD OCR: " + ex.Message;
                txtVisionOcrTestResult.Text = errorOutput;
                SaveVisionOcrTestReport(path, prompt, systemPrompt, errorOutput, _llmClient.LastVisionOcrDiagnostics, false, ex.Message);
            }
            finally
            {
                btnVisionOcrTestRun.Enabled = chkVisionOcrEnabled?.Checked == true;
            }
        }

        private static Label MakeFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Margin = new Padding(0, 4, 4, 0)
            };
        }

        private static NumericUpDown MakeVisionNumeric(out NumericUpDown numeric, decimal min, decimal max, int decimalPlaces, decimal increment)
        {
            numeric = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimalPlaces,
                Increment = increment,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(2, 2, 2, 2)
            };
            return numeric;
        }

        private int ClampSelectedIndex(ComboBox combo, int requestedIndex, int fallbackIndex = 0)
        {
            if (combo == null || combo.Items.Count == 0) return -1;
            int safeFallback = Math.Max(0, Math.Min(fallbackIndex, combo.Items.Count - 1));
            if (requestedIndex < 0 || requestedIndex >= combo.Items.Count) return safeFallback;
            return requestedIndex;
        }

        private void RefreshVisionOcrProviderDropdown(Guid? preferredProviderId = null)
        {
            if (cbVisionOcrProvider == null) return;
            try
            {
                _suppressVisionOcrUiEvents = true;
                var binding = ToolConfigManager.GetVisionOcrBinding();
                var bindingProvider = ResolveVisionOcrProviderForUi(binding);
                var currentId = preferredProviderId
                    ?? bindingProvider?.Id
                    ?? GetSelectedVisionOcrProvider()?.Id
                    ?? LLMConfigManager.GetActiveProvider()?.Id;
                var providers = LLMConfigManager.Current?.Providers ?? new List<LLMProviderConfig>();
                cbVisionOcrProvider.DataSource = null;
                cbVisionOcrProvider.Items.Clear();
                cbVisionOcrProvider.DisplayMember = "Name";
                cbVisionOcrProvider.ValueMember = "Id";
                foreach (var provider in providers)
                {
                    cbVisionOcrProvider.Items.Add(provider);
                }
                SelectVisionOcrProvider(currentId, binding?.ProviderNameFallback);
            }
            finally
            {
                _suppressVisionOcrUiEvents = false;
            }
        }

        private LLMProviderConfig GetSelectedVisionOcrProvider()
        {
            if (cbVisionOcrProvider == null) return null;
            if (cbVisionOcrProvider.SelectedItem is LLMProviderConfig selectedProvider) return selectedProvider;

            var providers = LLMConfigManager.Current?.Providers ?? new List<LLMProviderConfig>();

            if (cbVisionOcrProvider.SelectedValue is Guid selectedId)
            {
                var byValue = providers.FirstOrDefault(p => p.Id == selectedId);
                if (byValue != null) return byValue;
            }

            string text = cbVisionOcrProvider.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var byText = providers.FirstOrDefault(p => string.Equals(p.Name, text, StringComparison.OrdinalIgnoreCase));
                if (byText != null) return byText;
            }

            var bindingProvider = ResolveVisionOcrProviderForUi(ToolConfigManager.GetVisionOcrBinding());
            if (bindingProvider != null) return bindingProvider;

            return LLMConfigManager.GetActiveProvider();
        }

        private LLMProviderConfig GetVisionOcrProviderById(Guid? providerId)
        {
            if (!providerId.HasValue) return null;
            return LLMConfigManager.Current?.Providers?.FirstOrDefault(p => p.Id == providerId.Value);
        }

        private void SelectVisionOcrProvider(Guid? providerId, string providerNameFallback = null)
        {
            var providers = LLMConfigManager.Current?.Providers;
            if (providers == null || cbVisionOcrProvider == null) return;
            if (providers.Count == 0)
            {
                cbVisionOcrProvider.SelectedIndex = -1;
                return;
            }

            Guid targetId = providerId ?? LLMConfigManager.GetActiveProvider()?.Id ?? Guid.Empty;
            if (cbVisionOcrProvider.Items.Count == 0) return;

            if (targetId != Guid.Empty)
            {
                try
                {
                    cbVisionOcrProvider.SelectedValue = targetId;
                    if (cbVisionOcrProvider.SelectedValue is Guid selectedValue && selectedValue == targetId)
                    {
                        return;
                    }
                }
                catch
                {
                    // Some WinForms combo states reject SelectedValue during rebinding; fall back to item scan.
                }

                for (int i = 0; i < cbVisionOcrProvider.Items.Count; i++)
                {
                    if (cbVisionOcrProvider.Items[i] is LLMProviderConfig item && item.Id == targetId)
                    {
                        cbVisionOcrProvider.SelectedIndex = i;
                        return;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(providerNameFallback))
            {
                for (int i = 0; i < cbVisionOcrProvider.Items.Count; i++)
                {
                    if (cbVisionOcrProvider.Items[i] is LLMProviderConfig item &&
                        string.Equals(item.Name, providerNameFallback, StringComparison.OrdinalIgnoreCase))
                    {
                        cbVisionOcrProvider.SelectedIndex = i;
                        return;
                    }
                }
            }

            cbVisionOcrProvider.SelectedIndex = ClampSelectedIndex(cbVisionOcrProvider, 0);
        }

        private void LoadVisionOcrBindingToUi()
        {
            if (cbVisionOcrProvider == null) return;
            try
            {
                _suppressVisionOcrUiEvents = true;

                var binding = ToolConfigManager.GetVisionOcrBinding();
                chkVisionOcrEnabled.Checked = binding?.Enabled == true;

                var provider = ResolveVisionOcrProviderForUi(binding) ?? LLMConfigManager.GetActiveProvider();
                RefreshVisionOcrProviderDropdown(provider?.Id);
                SelectVisionOcrProvider(provider?.Id, binding?.ProviderNameFallback);
                provider = GetVisionOcrProviderById(provider?.Id) ?? GetSelectedVisionOcrProvider() ?? provider;

                cbVisionOcrModel.Items.Clear();
                if (!string.IsNullOrWhiteSpace(provider?.ModelName))
                {
                    cbVisionOcrModel.Items.Add(provider.ModelName);
                }
                cbVisionOcrModel.Text = !string.IsNullOrWhiteSpace(binding?.ModelName) ? binding.ModelName : provider?.ModelName ?? string.Empty;

                bool overridePayload = binding?.OverridePayload == true;
                chkVisionOcrUseProviderPayload.Checked = !overridePayload;
                ApplyVisionOcrPayloadToControls(provider, binding);
                numVisionOcrClipboardPixels.Value = ClampAgentDecimal(UISettingsManager.Settings.VisionOcrClipboardMaxPixels, numVisionOcrClipboardPixels.Minimum, numVisionOcrClipboardPixels.Maximum);
                numVisionOcrAttachmentPixels.Value = ClampAgentDecimal(UISettingsManager.Settings.VisionOcrAttachmentMaxPixels, numVisionOcrAttachmentPixels.Minimum, numVisionOcrAttachmentPixels.Maximum);
                string tilingMode = string.IsNullOrWhiteSpace(UISettingsManager.Settings.VisionOcrTilingMode) ? "Auto" : UISettingsManager.Settings.VisionOcrTilingMode;
                cbVisionOcrTilingMode.SelectedItem = cbVisionOcrTilingMode.Items.Contains(tilingMode) ? tilingMode : "Auto";
                numVisionOcrTileMaxDim.Value = ClampAgentDecimal(UISettingsManager.Settings.VisionOcrTileMaxDim, numVisionOcrTileMaxDim.Minimum, numVisionOcrTileMaxDim.Maximum);
                numVisionOcrTileOverlap.Value = ClampAgentDecimal(UISettingsManager.Settings.VisionOcrTileOverlap, numVisionOcrTileOverlap.Minimum, numVisionOcrTileOverlap.Maximum);
                numVisionOcrTileMaxCount.Value = ClampAgentDecimal(UISettingsManager.Settings.VisionOcrTileMaxCount, numVisionOcrTileMaxCount.Minimum, numVisionOcrTileMaxCount.Maximum);
                numVisionOcrTileMaxAspect.Value = ClampAgentDecimal((decimal)UISettingsManager.Settings.VisionOcrTileMaxAspectRatio, numVisionOcrTileMaxAspect.Minimum, numVisionOcrTileMaxAspect.Maximum);
                numVisionOcrPdfDpi.Value = ClampAgentDecimal(UISettingsManager.Settings.VisionOcrPdfDpi, numVisionOcrPdfDpi.Minimum, numVisionOcrPdfDpi.Maximum);
                numVisionOcrPdfMaxPages.Value = ClampAgentDecimal(UISettingsManager.Settings.VisionOcrPdfMaxPages, numVisionOcrPdfMaxPages.Minimum, numVisionOcrPdfMaxPages.Maximum);
                txtVisionOcrPdfRendererPath.Text = string.IsNullOrWhiteSpace(UISettingsManager.Settings.VisionOcrPdfRendererPath) ? "pdftoppm.exe" : UISettingsManager.Settings.VisionOcrPdfRendererPath;
                RefreshVisionOcrQualityPresetDropdown();
                ApplyLastVisionOcrQualityPresetToControls();
                if (txtVisionOcrSystemPrompt != null)
                {
                    txtVisionOcrSystemPrompt.Text = BuildVisionOcrPromptPreview(txtVisionOcrTestPrompt?.Text);
                }

                string policy = string.IsNullOrWhiteSpace(binding?.ContextPolicy) ? "UseLoadedIfAtLeastRequested" : binding.ContextPolicy;
                cbVisionOcrContextPolicy.SelectedItem = cbVisionOcrContextPolicy.Items.Contains(policy) ? policy : "UseLoadedIfAtLeastRequested";
                _visionOcrProviderChangedByUser = false;
            }
            finally
            {
                _suppressVisionOcrUiEvents = false;
            }

            UpdateVisionOcrPayloadControlsEnabled();
            UpdateVisionOcrPreview();
        }

        private LLMProviderConfig ResolveVisionOcrProviderForUi(VisionOcrBinding binding)
        {
            if (binding == null) return null;
            if (binding.ProviderId.HasValue)
            {
                var byId = LLMConfigManager.GetProviderById(binding.ProviderId.Value);
                if (byId != null) return byId;
            }

            if (!string.IsNullOrWhiteSpace(binding.ProviderNameFallback))
            {
                return LLMConfigManager.Current?.Providers?.FirstOrDefault(p =>
                    string.Equals(p.Name, binding.ProviderNameFallback, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private void ApplyVisionOcrPayloadToControls(LLMProviderConfig provider, VisionOcrBinding binding = null)
        {
            if (provider == null || numVisionOcrTemp == null) return;

            numVisionOcrTemp.Value = ClampAgentDecimal((decimal)(binding?.Temperature ?? provider.Temperature), numVisionOcrTemp.Minimum, numVisionOcrTemp.Maximum);
            numVisionOcrContext.Value = ClampAgentDecimal(binding?.LoadContextLength ?? provider.LoadContextLength, numVisionOcrContext.Minimum, numVisionOcrContext.Maximum);
            numVisionOcrMaxTokens.Value = ClampAgentDecimal(binding?.MaxTokens ?? provider.MaxTokens, numVisionOcrMaxTokens.Minimum, numVisionOcrMaxTokens.Maximum);
            numVisionOcrTopP.Value = ClampAgentDecimal((decimal)(binding?.TopP ?? provider.TopP), numVisionOcrTopP.Minimum, numVisionOcrTopP.Maximum);
            numVisionOcrTopK.Value = ClampAgentDecimal(binding?.TopK ?? provider.TopK, numVisionOcrTopK.Minimum, numVisionOcrTopK.Maximum);
            numVisionOcrMinP.Value = ClampAgentDecimal((decimal)(binding?.MinP ?? provider.MinP), numVisionOcrMinP.Minimum, numVisionOcrMinP.Maximum);
            numVisionOcrRepPenalty.Value = ClampAgentDecimal((decimal)(binding?.RepetitionPenalty ?? provider.RepetitionPenalty), numVisionOcrRepPenalty.Minimum, numVisionOcrRepPenalty.Maximum);
            bool defaultAutoLoad = provider != null && LLMClient.SupportsLocalModelManagement(provider)
                ? true
                : provider?.AutoLoadModel == true;
            chkVisionOcrAutoLoad.Checked = binding?.AutoLoadModel ?? defaultAutoLoad;

            string reasoning = binding?.ReasoningEffort ?? provider.ReasoningEffort;
            if (string.IsNullOrWhiteSpace(reasoning)) reasoning = "none";
            cbVisionOcrReasoning.SelectedItem = cbVisionOcrReasoning.Items.Contains(reasoning) ? reasoning : "none";
        }

        private void VisionOcrControlChanged(object sender, EventArgs e)
        {
            if (_suppressVisionOcrUiEvents) return;

            if (sender == chkVisionOcrUseProviderPayload && chkVisionOcrUseProviderPayload.Checked)
            {
                var provider = GetSelectedVisionOcrProvider();
                if (provider != null)
                {
                    try
                    {
                        _suppressVisionOcrUiEvents = true;
                        ApplyVisionOcrPayloadToControls(provider);
                    }
                    finally
                    {
                        _suppressVisionOcrUiEvents = false;
                    }
                }
            }

            UpdateVisionOcrPayloadControlsEnabled();
            UpdateVisionOcrPreview();
            SaveVisionOcrBindingFromUi();
        }

        private void VisionOcrImageQualityChanged(object sender, EventArgs e)
        {
            if (_suppressVisionOcrUiEvents) return;
            if (numVisionOcrClipboardPixels == null || numVisionOcrAttachmentPixels == null) return;

            UISettingsManager.Settings.VisionOcrClipboardMaxPixels = (int)numVisionOcrClipboardPixels.Value;
            UISettingsManager.Settings.VisionOcrAttachmentMaxPixels = (int)numVisionOcrAttachmentPixels.Value;
            UISettingsManager.Save();
            UpdateVisionOcrPreview();
        }

        private void VisionOcrTilingChanged(object sender, EventArgs e)
        {
            if (_suppressVisionOcrUiEvents) return;
            if (cbVisionOcrTilingMode == null || numVisionOcrTileMaxDim == null) return;

            UISettingsManager.Settings.VisionOcrTilingMode = cbVisionOcrTilingMode.SelectedItem?.ToString() ?? "Auto";
            UISettingsManager.Settings.VisionOcrTileMaxDim = (int)numVisionOcrTileMaxDim.Value;
            UISettingsManager.Settings.VisionOcrTileOverlap = (int)numVisionOcrTileOverlap.Value;
            UISettingsManager.Settings.VisionOcrTileMaxCount = (int)numVisionOcrTileMaxCount.Value;
            UISettingsManager.Settings.VisionOcrTileMaxAspectRatio = (double)numVisionOcrTileMaxAspect.Value;
            UISettingsManager.Save();
            UpdateVisionOcrPreview();
        }

        private void VisionOcrPdfChanged(object sender, EventArgs e)
        {
            if (_suppressVisionOcrUiEvents) return;
            if (numVisionOcrPdfDpi == null || numVisionOcrPdfMaxPages == null || txtVisionOcrPdfRendererPath == null) return;

            UISettingsManager.Settings.VisionOcrPdfDpi = (int)numVisionOcrPdfDpi.Value;
            UISettingsManager.Settings.VisionOcrPdfMaxPages = (int)numVisionOcrPdfMaxPages.Value;
            UISettingsManager.Settings.VisionOcrPdfRendererPath = string.IsNullOrWhiteSpace(txtVisionOcrPdfRendererPath.Text) ? "pdftoppm.exe" : txtVisionOcrPdfRendererPath.Text.Trim();
            UISettingsManager.Save();
            UpdateVisionOcrPreview();
        }

        private void CbVisionOcrProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressVisionOcrUiEvents) return;
            _visionOcrProviderChangedByUser = true;

            var provider = GetSelectedVisionOcrProvider();
            if (provider == null)
            {
                UpdateVisionOcrPreview();
                return;
            }

            cbVisionOcrModel.Items.Clear();
            if (!string.IsNullOrWhiteSpace(provider.ModelName))
            {
                cbVisionOcrModel.Items.Add(provider.ModelName);
                cbVisionOcrModel.Text = provider.ModelName;
            }

            if (chkVisionOcrUseProviderPayload.Checked)
            {
                ApplyVisionOcrPayloadToControls(provider);
            }

            UpdateVisionOcrPreview();
            SaveVisionOcrBindingFromUi();
        }

        private void UpdateVisionOcrPayloadControlsEnabled()
        {
            if (numVisionOcrTemp == null) return;
            bool enabled = chkVisionOcrEnabled?.Checked == true;
            bool overridePayload = enabled && chkVisionOcrUseProviderPayload != null && !chkVisionOcrUseProviderPayload.Checked;

            cbVisionOcrProvider.Enabled = enabled;
            cbVisionOcrModel.Enabled = enabled;
            btnRefreshVisionOcrModels.Enabled = enabled;
            chkVisionOcrUseProviderPayload.Enabled = enabled;
            numVisionOcrTemp.Enabled = overridePayload;
            numVisionOcrContext.Enabled = overridePayload;
            numVisionOcrMaxTokens.Enabled = overridePayload;
            numVisionOcrTopP.Enabled = overridePayload;
            numVisionOcrTopK.Enabled = overridePayload;
            numVisionOcrMinP.Enabled = overridePayload;
            numVisionOcrRepPenalty.Enabled = overridePayload;
            cbVisionOcrReasoning.Enabled = overridePayload;
            chkVisionOcrAutoLoad.Enabled = enabled;
            cbVisionOcrContextPolicy.Enabled = enabled;
            if (numVisionOcrClipboardPixels != null) numVisionOcrClipboardPixels.Enabled = enabled;
            if (numVisionOcrAttachmentPixels != null) numVisionOcrAttachmentPixels.Enabled = enabled;
            if (cbVisionOcrTilingMode != null) cbVisionOcrTilingMode.Enabled = enabled;
            if (numVisionOcrTileMaxDim != null) numVisionOcrTileMaxDim.Enabled = enabled;
            if (numVisionOcrTileOverlap != null) numVisionOcrTileOverlap.Enabled = enabled;
            if (numVisionOcrTileMaxCount != null) numVisionOcrTileMaxCount.Enabled = enabled;
            if (numVisionOcrTileMaxAspect != null) numVisionOcrTileMaxAspect.Enabled = enabled;
            if (numVisionOcrPdfDpi != null) numVisionOcrPdfDpi.Enabled = enabled;
            if (numVisionOcrPdfMaxPages != null) numVisionOcrPdfMaxPages.Enabled = enabled;
            if (txtVisionOcrPdfRendererPath != null) txtVisionOcrPdfRendererPath.Enabled = enabled;
            if (cbVisionOcrQualityPreset != null) cbVisionOcrQualityPreset.Enabled = enabled;
            if (txtVisionOcrPresetName != null) txtVisionOcrPresetName.Enabled = enabled;
            if (txtVisionOcrTestFile != null) txtVisionOcrTestFile.Enabled = enabled;
            if (txtVisionOcrTestPrompt != null) txtVisionOcrTestPrompt.Enabled = enabled;
            if (btnVisionOcrTestRun != null) btnVisionOcrTestRun.Enabled = enabled;
        }

        private void UpdateVisionOcrPreview()
        {
            if (lblVisionOcrPreview == null) return;
            var provider = GetSelectedVisionOcrProvider();
            string model = cbVisionOcrModel?.Text;
            string mode = chkVisionOcrEnabled?.Checked == true ? "aktywny" : "wylaczony";
            string payloadMode = chkVisionOcrUseProviderPayload?.Checked == true ? "payload providera" : "payload OCR";
            lblVisionOcrPreview.Text = provider == null
                ? $"Vision/OCR: {mode}, brak providera"
                : $"Vision/OCR: {mode}, {provider.Name} -> {model} ({payloadMode}), schowek {UISettingsManager.Settings.VisionOcrClipboardMaxPixels}px, zalaczniki {UISettingsManager.Settings.VisionOcrAttachmentMaxPixels}px, tiling {UISettingsManager.Settings.VisionOcrTilingMode} {UISettingsManager.Settings.VisionOcrTileMaxDim}px/{UISettingsManager.Settings.VisionOcrTileOverlap}px max {UISettingsManager.Settings.VisionOcrTileMaxCount}, PDF {UISettingsManager.Settings.VisionOcrPdfDpi}dpi/{UISettingsManager.Settings.VisionOcrPdfMaxPages}str.";
        }

        private VisionOcrBinding BuildVisionOcrBindingFromUi()
        {
            var existingBinding = ToolConfigManager.GetVisionOcrBinding();
            var bindingProvider = ResolveVisionOcrProviderForUi(existingBinding);
            var selectedProvider = GetSelectedVisionOcrProvider();
            var provider = _visionOcrProviderChangedByUser || bindingProvider == null
                ? selectedProvider ?? bindingProvider ?? LLMConfigManager.ResolveVisionOcrProvider() ?? LLMConfigManager.GetActiveProvider()
                : bindingProvider;
            bool overridePayload = chkVisionOcrUseProviderPayload == null || !chkVisionOcrUseProviderPayload.Checked;
            var binding = new VisionOcrBinding
            {
                Enabled = chkVisionOcrEnabled?.Checked == true,
                UseDefaultProvider = false,
                ProviderId = provider?.Id,
                ProviderNameFallback = provider?.Name,
                ModelName = string.IsNullOrWhiteSpace(cbVisionOcrModel?.Text) ? provider?.ModelName : cbVisionOcrModel.Text.Trim(),
                OverridePayload = overridePayload,
                AutoLoadModel = chkVisionOcrAutoLoad?.Checked == true,
                ContextPolicy = cbVisionOcrContextPolicy?.SelectedItem?.ToString() ?? "UseLoadedIfAtLeastRequested"
            };

            if (overridePayload)
            {
                binding.Temperature = (double)numVisionOcrTemp.Value;
                binding.LoadContextLength = (int)numVisionOcrContext.Value;
                binding.MaxTokens = (int)numVisionOcrMaxTokens.Value;
                binding.TopP = (double)numVisionOcrTopP.Value;
                binding.TopK = (int)numVisionOcrTopK.Value;
                binding.MinP = (double)numVisionOcrMinP.Value;
                binding.RepetitionPenalty = (double)numVisionOcrRepPenalty.Value;
                binding.ReasoningEffort = cbVisionOcrReasoning.Text;
            }

            return binding;
        }

        private void SaveVisionOcrBindingFromUi()
        {
            if (_suppressVisionOcrUiEvents || chkVisionOcrEnabled == null) return;
            ToolConfigManager.UpdateVisionOcrBinding(BuildVisionOcrBindingFromUi());
        }

        private async Task RefreshVisionOcrModelsAsync()
        {
            var existingBinding = ToolConfigManager.GetVisionOcrBinding();
            var provider = _visionOcrProviderChangedByUser
                ? GetSelectedVisionOcrProvider()
                : ResolveVisionOcrProviderForUi(existingBinding) ?? GetSelectedVisionOcrProvider();
            if (provider == null || cbVisionOcrModel == null) return;

            btnRefreshVisionOcrModels.Enabled = false;
            try
            {
                var models = await _llmClient.GetAvailableModelsAsync(provider);
                string current = cbVisionOcrModel.Text;
                cbVisionOcrModel.Items.Clear();
                var modelList = models.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var model in modelList)
                {
                    cbVisionOcrModel.Items.Add(model);
                }

                if (!string.IsNullOrWhiteSpace(current) &&
                    !IsDefaultModelPlaceholderForUi(current) &&
                    modelList.Any(m => string.Equals(m, current, StringComparison.OrdinalIgnoreCase)))
                {
                    cbVisionOcrModel.Text = modelList.First(m => string.Equals(m, current, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    cbVisionOcrModel.Text = SelectPreferredVisionOcrModelForUi(modelList) ?? provider.ModelName ?? current;
                }

                SaveVisionOcrBindingFromUi();
            }
            finally
            {
                btnRefreshVisionOcrModels.Enabled = chkVisionOcrEnabled?.Checked == true;
            }
        }

        private async Task RefreshVisionOcrModelsIfNeededAsync()
        {
            if (chkVisionOcrEnabled?.Checked != true || cbVisionOcrModel == null) return;
            if (!string.IsNullOrWhiteSpace(cbVisionOcrModel.Text) && !IsDefaultModelPlaceholderForUi(cbVisionOcrModel.Text)) return;
            await RefreshVisionOcrModelsAsync();
        }

        private void ScheduleVisionOcrModelRefreshIfNeeded()
        {
            Action refresh = async () => await RefreshVisionOcrModelsIfNeededAsync();
            if (IsHandleCreated)
            {
                BeginInvoke(refresh);
                return;
            }

            EventHandler handler = null;
            handler = (s, e) =>
            {
                HandleCreated -= handler;
                BeginInvoke(refresh);
            };
            HandleCreated += handler;
        }

        private static string SelectPreferredVisionOcrModelForUi(IEnumerable<string> models)
        {
            var list = models?
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (list.Count == 0) return null;
            if (list.Count == 1) return list[0];

            string[] preferredMarkers = { "vision", "vl", "vlm", "mm", "ocr", "gemma", "qwen2-vl", "qwen2.5-vl", "llava", "pixtral", "moondream", "minicpm" };
            foreach (string marker in preferredMarkers)
            {
                var match = list.FirstOrDefault(m => m.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrWhiteSpace(match)) return match;
            }

            return list[0];
        }

        private static bool IsDefaultModelPlaceholderForUi(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return true;
            string m = modelName.Trim();
            return string.Equals(m, "local-model", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(m, "model", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(m, "llama3", StringComparison.OrdinalIgnoreCase);
        }

        private Control CreateAgentNumericField(string label, out NumericUpDown numeric, decimal min, decimal max, int decimalPlaces, decimal increment, int width)
        {
            var panel = new Panel { Width = width + 42, Height = 26, Margin = new Padding(0, 0, 4, 0) };
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Left, Width = 38, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });

            numeric = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimalPlaces,
                Increment = increment,
                Width = width,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            numeric.ValueChanged += AgentLlmControlChanged;
            panel.Controls.Add(numeric);
            return panel;
        }

        private void RefreshAgentProviderDropdown()
        {
            if (cbAgentLlmProvider == null) return;
            try
            {
                _suppressAgentLlmUiEvents = true;
                var providers = LLMConfigManager.Current?.Providers ?? new List<LLMProviderConfig>();
                cbAgentLlmProvider.DataSource = null;
                cbAgentLlmProvider.DisplayMember = "Name";
                cbAgentLlmProvider.DataSource = providers.ToList();
            }
            finally
            {
                _suppressAgentLlmUiEvents = false;
            }
        }

        private LLMProviderConfig GetSelectedAgentProvider()
        {
            return cbAgentLlmProvider?.SelectedItem as LLMProviderConfig;
        }

        private void SelectAgentProvider(Guid? providerId)
        {
            var providers = LLMConfigManager.Current?.Providers;
            if (providers == null || cbAgentLlmProvider == null) return;
            if (providers.Count == 0)
            {
                cbAgentLlmProvider.SelectedIndex = -1;
                return;
            }

            Guid targetId = providerId ?? LLMConfigManager.GetActiveProvider()?.Id ?? Guid.Empty;
            int idx = providers.FindIndex(p => p.Id == targetId);
            if (idx < 0) idx = 0;
            if (cbAgentLlmProvider.Items.Count == 0) return;
            if (idx >= cbAgentLlmProvider.Items.Count) idx = 0;
            cbAgentLlmProvider.SelectedIndex = idx;
        }

        private void LoadAgentLlmBindingToUi(string profileName, AgentProfileConfig profile)
        {
            if (cbAgentLlmProvider == null || profile == null) return;

            try
            {
                _suppressAgentLlmUiEvents = true;
                RefreshAgentProviderDropdown();

                var binding = profile.LlmBinding;
                bool useDefaultProvider = binding == null || binding.UseDefaultProvider;
                chkAgentUseDefaultProvider.Checked = useDefaultProvider;
                SelectAgentProvider(useDefaultProvider ? LLMConfigManager.GetActiveProvider()?.Id : binding?.ProviderId);
                var provider = useDefaultProvider
                    ? LLMConfigManager.GetActiveProvider()
                    : (GetSelectedAgentProvider() ?? LLMConfigManager.GetActiveProvider());
                var effective = LLMConfigManager.ResolveProviderForProfile(profileName) ?? provider;

                cbAgentLlmModel.Items.Clear();
                if (!string.IsNullOrWhiteSpace(effective?.ModelName))
                {
                    cbAgentLlmModel.Items.Add(effective.ModelName);
                }
                cbAgentLlmModel.Text = !useDefaultProvider && !string.IsNullOrWhiteSpace(binding?.ModelName)
                    ? binding.ModelName
                    : effective?.ModelName ?? string.Empty;

                bool overridePayload = binding?.OverridePayload == true;
                chkAgentUseProviderPayload.Checked = !overridePayload;

                ApplyAgentPayloadToControls(effective);

                string policy = string.IsNullOrWhiteSpace(binding?.ContextPolicy) ? "UseLoadedIfAtLeastRequested" : binding.ContextPolicy;
                cbAgentContextPolicy.SelectedItem = cbAgentContextPolicy.Items.Contains(policy) ? policy : "UseLoadedIfAtLeastRequested";
            }
            finally
            {
                _suppressAgentLlmUiEvents = false;
            }

            UpdateAgentPayloadControlsEnabled();
            UpdateAgentLlmPreview();
        }

        private void ApplyAgentPayloadToControls(LLMProviderConfig config)
        {
            if (config == null || numAgentTemp == null) return;

            numAgentTemp.Value = ClampAgentDecimal((decimal)config.Temperature, numAgentTemp.Minimum, numAgentTemp.Maximum);
            numAgentContext.Value = ClampAgentDecimal(config.LoadContextLength, numAgentContext.Minimum, numAgentContext.Maximum);
            numAgentMaxTokens.Value = ClampAgentDecimal(config.MaxTokens, numAgentMaxTokens.Minimum, numAgentMaxTokens.Maximum);
            numAgentTopP.Value = ClampAgentDecimal((decimal)config.TopP, numAgentTopP.Minimum, numAgentTopP.Maximum);
            numAgentTopK.Value = ClampAgentDecimal(config.TopK, numAgentTopK.Minimum, numAgentTopK.Maximum);
            numAgentMinP.Value = ClampAgentDecimal((decimal)config.MinP, numAgentMinP.Minimum, numAgentMinP.Maximum);
            numAgentRepPenalty.Value = ClampAgentDecimal((decimal)config.RepetitionPenalty, numAgentRepPenalty.Minimum, numAgentRepPenalty.Maximum);
            chkAgentAutoLoad.Checked = config.AutoLoadModel;

            string reasoning = string.IsNullOrWhiteSpace(config.ReasoningEffort) ? "none" : config.ReasoningEffort;
            cbAgentReasoning.SelectedItem = cbAgentReasoning.Items.Contains(reasoning) ? reasoning : "none";
        }

        private decimal ClampAgentDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void AgentLlmControlChanged(object sender, EventArgs e)
        {
            if (_suppressAgentLlmUiEvents) return;

            if (sender == chkAgentUseDefaultProvider)
            {
                var provider = chkAgentUseDefaultProvider.Checked
                    ? LLMConfigManager.GetActiveProvider()
                    : GetSelectedAgentProvider();
                if (provider != null)
                {
                    try
                    {
                        _suppressAgentLlmUiEvents = true;
                        cbAgentLlmModel.Items.Clear();
                        if (!string.IsNullOrWhiteSpace(provider.ModelName))
                        {
                            cbAgentLlmModel.Items.Add(provider.ModelName);
                            cbAgentLlmModel.Text = provider.ModelName;
                        }
                        if (chkAgentUseProviderPayload.Checked)
                        {
                            ApplyAgentPayloadToControls(provider);
                        }
                    }
                    finally
                    {
                        _suppressAgentLlmUiEvents = false;
                    }
                }
            }

            if (sender == chkAgentUseProviderPayload && chkAgentUseProviderPayload.Checked)
            {
                var provider = chkAgentUseDefaultProvider?.Checked == true
                    ? LLMConfigManager.GetActiveProvider()
                    : GetSelectedAgentProvider();
                if (provider != null)
                {
                    try
                    {
                        _suppressAgentLlmUiEvents = true;
                        ApplyAgentPayloadToControls(provider);
                    }
                    finally
                    {
                        _suppressAgentLlmUiEvents = false;
                    }
                }
            }

            UpdateAgentPayloadControlsEnabled();
            UpdateAgentLlmPreview();
        }

        private void CbAgentLlmProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressAgentLlmUiEvents) return;

            var provider = GetSelectedAgentProvider();
            if (provider != null)
            {
                cbAgentLlmModel.Items.Clear();
                if (!string.IsNullOrWhiteSpace(provider.ModelName))
                {
                    cbAgentLlmModel.Items.Add(provider.ModelName);
                    cbAgentLlmModel.Text = provider.ModelName;
                }

                if (chkAgentUseProviderPayload.Checked)
                {
                    ApplyAgentPayloadToControls(provider);
                }
            }

            UpdateAgentLlmPreview();
        }

        private void UpdateAgentPayloadControlsEnabled()
        {
            bool enabled = chkAgentUseProviderPayload != null && !chkAgentUseProviderPayload.Checked;
            bool useDefaultProvider = chkAgentUseDefaultProvider?.Checked == true;
            if (numAgentTemp == null) return;

            cbAgentLlmProvider.Enabled = !useDefaultProvider;
            cbAgentLlmModel.Enabled = !useDefaultProvider;
            btnRefreshAgentModels.Enabled = !useDefaultProvider;
            numAgentTemp.Enabled = enabled;
            numAgentContext.Enabled = enabled;
            numAgentMaxTokens.Enabled = enabled;
            numAgentTopP.Enabled = enabled;
            numAgentTopK.Enabled = enabled;
            numAgentMinP.Enabled = enabled;
            numAgentRepPenalty.Enabled = enabled;
            cbAgentReasoning.Enabled = enabled;
            chkAgentAutoLoad.Enabled = enabled;
            cbAgentContextPolicy.Enabled = enabled;
        }

        private void UpdateAgentLlmPreview()
        {
            if (lblAgentLlmPreview == null) return;

            bool useDefaultProvider = chkAgentUseDefaultProvider?.Checked == true;
            var provider = useDefaultProvider ? LLMConfigManager.GetActiveProvider() : GetSelectedAgentProvider();
            string model = cbAgentLlmModel?.Text;
            string payloadMode = chkAgentUseProviderPayload?.Checked == true ? "payload providera" : "payload profilu";
            string providerMode = useDefaultProvider ? "domyślny provider" : "provider profilu";
            lblAgentLlmPreview.Text = provider == null
                ? "LLM: aktywny provider globalny"
                : $"{provider.Name} -> {model} ({providerMode}, {payloadMode})";
        }

        private AgentLlmBinding BuildAgentLlmBindingFromUi()
        {
            bool useDefaultProvider = chkAgentUseDefaultProvider?.Checked == true;
            var provider = useDefaultProvider ? null : GetSelectedAgentProvider();
            if (!useDefaultProvider && provider == null) return null;

            bool overridePayload = chkAgentUseProviderPayload == null || !chkAgentUseProviderPayload.Checked;
            var binding = new AgentLlmBinding
            {
                UseDefaultProvider = useDefaultProvider,
                ProviderId = useDefaultProvider ? (Guid?)null : provider.Id,
                ProviderNameFallback = useDefaultProvider ? null : provider.Name,
                ModelName = useDefaultProvider || string.IsNullOrWhiteSpace(cbAgentLlmModel?.Text) ? null : cbAgentLlmModel.Text.Trim(),
                OverridePayload = overridePayload,
                ContextPolicy = cbAgentContextPolicy?.SelectedItem?.ToString() ?? "UseLoadedIfAtLeastRequested"
            };

            if (overridePayload)
            {
                binding.Temperature = (double)numAgentTemp.Value;
                binding.LoadContextLength = (int)numAgentContext.Value;
                binding.MaxTokens = (int)numAgentMaxTokens.Value;
                binding.TopP = (double)numAgentTopP.Value;
                binding.TopK = (int)numAgentTopK.Value;
                binding.MinP = (double)numAgentMinP.Value;
                binding.RepetitionPenalty = (double)numAgentRepPenalty.Value;
                binding.ReasoningEffort = cbAgentReasoning.Text;
                binding.AutoLoadModel = chkAgentAutoLoad.Checked;
            }

            return binding;
        }

        private async Task RefreshAgentModelsAsync()
        {
            var provider = GetSelectedAgentProvider();
            if (provider == null || cbAgentLlmModel == null) return;

            btnRefreshAgentModels.Enabled = false;
            try
            {
                var models = await _llmClient.GetAvailableModelsAsync(provider);
                string current = cbAgentLlmModel.Text;
                cbAgentLlmModel.Items.Clear();
                foreach (var model in models.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    cbAgentLlmModel.Items.Add(model);
                }
                cbAgentLlmModel.Text = !string.IsNullOrWhiteSpace(current) ? current : provider.ModelName;
            }
            finally
            {
                btnRefreshAgentModels.Enabled = true;
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

            ToolConfigManager.UpdateAgentProfile(profileName, promptFile, selectedTools, BuildAgentLlmBindingFromUi());
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
                SchedulePromptWarmup("session-load", 250);
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
                SchedulePromptWarmup("session-load", 250);
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
                    SchedulePromptWarmup("session-load", 250);
                }
            }
        }

        public event Action OnSessionReloaded;

        private void ReloadChatHistoryFromSession()
        {
            txtHistory.Clear();
            var session = SessionManager.CurrentSession;
            if (lblSessionInfo != null) lblSessionInfo.Text = $"Sesja: {session?.Description ?? session?.Id ?? "Brak"}";
            OnSessionReloaded?.Invoke();
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
