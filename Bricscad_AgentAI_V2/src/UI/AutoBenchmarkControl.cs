using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Microsoft.Win32;

namespace Bricscad_AgentAI_V2.UI
{
    public class AutoBenchmarkControl : UserControl
    {
        private readonly AutoBenchmarkEngine _engine;
        private CancellationTokenSource _cts;
        private BenchmarkConfig _currentConfig;
        private readonly List<BenchmarkQueueItem> _benchmarkQueue = new List<BenchmarkQueueItem>();
        private BenchmarkQueueItem _activeQueueItem;
        private bool _isBatchRunning;
        private BenchmarkBatchSummary _lastBatchSummary;

        // Kontrolki UI
        private Button btnLoadJson, btnStart, btnStop, btnSendToChat, btnResetTest, btnColumns, btnClearQueue;
        private ComboBox cbProfiles;
        private DataGridView dgvTests, dgvQueue;
        private RichTextBox txtLogs, txtDetails, txtTaskDesc, txtErrorLog;
        private ProgressBar progressBar, progressBarBatch;
        private Label lblGlobalStatus, lblBatchStatus;
        private TabControl tabLogs;
        private ContextMenuStrip columnVisibilityMenu;

        // Provider/Model picker
        private ComboBox cbProviders, cbModels;
        private Button btnRefreshModels, btnUnloadModel;
        private CheckBox chkLiveStatus;
        private Label lblModelStatus;
        private System.Windows.Forms.Timer liveStatusTimer;
        private bool _suppressModelChangedEvent;
        private const string REG_PATH = @"Software\BricscadAgentAI";
        private const string REG_KEY = "LastBenchmarkPath";

        public AutoBenchmarkControl(AutoBenchmarkEngine engine)
        {
            _engine = engine;
            _engine.OnLogMessage += Engine_OnLogMessage;
            _engine.OnTestFinished += Engine_OnTestFinished;
            _engine.OnBenchmarkCompleted += Engine_OnBenchmarkCompleted;

            InitializeUI();
            LoadLastPath();

            // Subskrybuj zmiany konfiguracji z innych części UI (np. okno Ustawień)
            LLMConfigManager.OnConfigChanged += OnExternalConfigChanged;
            // Początkowe wypełnienie dropdownów
            this.HandleCreated += (s, e) => BeginInvoke(new Action(RefreshProviderDropdown));
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f);

            // Pasek górny (Przyciski)
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(5) };

            Label lblProfile = new Label { Text = "Profil:", ForeColor = Color.White, Dock = DockStyle.Left, Width = 50, TextAlign = ContentAlignment.MiddleLeft };
            cbProfiles = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cbProfiles.Items.Add("(Brak profilu - wszystkie narzędzia)");
            try
            {
                foreach (var p in ToolConfigManager.GetProfiles().Keys)
                {
                    cbProfiles.Items.Add(p);
                }
            }
            catch { }
            RestoreLastBenchmarkProfileSelection();
            cbProfiles.SelectedIndexChanged += CbProfiles_SelectedIndexChanged;

            panTop.Controls.Add(cbProfiles);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panTop.Controls.Add(lblProfile);

            btnLoadJson = CreateStyledButton("📂 Wczytaj JSON", Color.FromArgb(60, 60, 60));
            btnLoadJson.Click += BtnLoadJson_Click;
            btnLoadJson.Text = "Wczytaj JSON-y";

            btnResetTest = CreateStyledButton("Reset testu", Color.FromArgb(90, 90, 90));
            btnResetTest.Enabled = false;
            btnResetTest.Click += BtnResetTest_Click;

            btnClearQueue = CreateStyledButton("Wyczysc kolejke", Color.FromArgb(80, 80, 80));
            btnClearQueue.Enabled = false;
            btnClearQueue.Click += BtnClearQueue_Click;

            btnStart = CreateStyledButton("▶ Start", Color.FromArgb(0, 122, 204));
            btnStart.Enabled = false;
            btnStart.Click += BtnStart_Click;

            btnStop = CreateStyledButton("⏹ Stop", Color.Crimson);
            btnStop.Enabled = false;
            btnStop.Click += BtnStop_Click;

            btnColumns = CreateStyledButton("Kolumny", Color.FromArgb(70, 70, 70));
            btnColumns.Click += BtnColumns_Click;

            panTop.Controls.Add(btnStop);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panTop.Controls.Add(btnStart);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panTop.Controls.Add(btnClearQueue);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panTop.Controls.Add(btnResetTest);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panTop.Controls.Add(btnColumns);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panTop.Controls.Add(btnLoadJson);

            // === Drugi pasek: Provider / Model / Live status ===
            Panel panModelPicker = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(5, 2, 5, 2) };

            Label lblProvider = new Label { Text = "Provider:", ForeColor = Color.White, Dock = DockStyle.Left, Width = 60, TextAlign = ContentAlignment.MiddleLeft };
            cbProviders = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cbProviders.SelectedIndexChanged += CbProviders_SelectedIndexChanged;

            Label lblModel = new Label { Text = "Model:", ForeColor = Color.White, Dock = DockStyle.Left, Width = 50, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
            cbModels = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 250,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cbModels.SelectedIndexChanged += CbModels_SelectedIndexChanged;

            btnRefreshModels = new Button
            {
                Text = "🔄",
                Dock = DockStyle.Left,
                Width = 32,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Margin = new Padding(2, 0, 2, 0)
            };
            btnRefreshModels.Click += BtnRefreshModels_Click;

            btnUnloadModel = new Button
            {
                Text = "⏏ Rozładuj",
                Dock = DockStyle.Left,
                Width = 80,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(150, 60, 60),
                ForeColor = Color.White,
                Margin = new Padding(2, 0, 2, 0)
            };
            btnUnloadModel.Click += BtnUnloadModel_Click;

            chkLiveStatus = new CheckBox
            {
                Text = "🔴 Live",
                Dock = DockStyle.Left,
                Width = 80,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(6, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            chkLiveStatus.CheckedChanged += ChkLiveStatus_CheckedChanged;

            lblModelStatus = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGreen,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                Padding = new Padding(0, 0, 5, 0)
            };

            panModelPicker.Controls.Add(lblModelStatus);
            panModelPicker.Controls.Add(chkLiveStatus);
            panModelPicker.Controls.Add(btnRefreshModels);
            panModelPicker.Controls.Add(btnUnloadModel);
            panModelPicker.Controls.Add(cbModels);
            panModelPicker.Controls.Add(lblModel);
            panModelPicker.Controls.Add(cbProviders);
            panModelPicker.Controls.Add(lblProvider);

            liveStatusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            liveStatusTimer.Tick += LiveStatusTimer_Tick;

            // Stopka (Progres)
            Panel panFooter = new Panel { Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(5) };
            lblBatchStatus = new Label { Text = "Kolejka benchmarkow: 0 plikow.", Dock = DockStyle.Top, Height = 18, ForeColor = Color.LightGray };
            progressBarBatch = new ProgressBar { Dock = DockStyle.Top, Height = 10 };
            lblGlobalStatus = new Label { Text = "Oczekiwanie na pliki JSON...", Dock = DockStyle.Top, Height = 20, Font = new Font(this.Font, FontStyle.Bold) };
            progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 10 };
            panFooter.Controls.Add(progressBar);
            panFooter.Controls.Add(lblGlobalStatus);
            panFooter.Controls.Add(progressBarBatch);
            panFooter.Controls.Add(lblBatchStatus);

            // Tabela testów
            dgvQueue = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.Black,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(60, 60, 60)
            };
            DgvQueueColumnsInit();
            dgvQueue.SelectionChanged += DgvQueue_SelectionChanged;

            dgvTests = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.Black,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(60, 60, 60)
            };
            dgvTestsColumnsInit();
            InitializeColumnVisibilityMenu();
            dgvTests.SelectionChanged += DgvTests_SelectionChanged;
            
            // Przyciski akcji dodatkowych
            Panel panTestActions = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5) };
            btnSendToChat = CreateStyledButton("💬 Wyślij do czatu", Color.FromArgb(0, 150, 136));
            btnSendToChat.Enabled = false;
            btnSendToChat.Width = 140;
            btnSendToChat.Click += BtnSendToChat_Click;
            panTestActions.Controls.Add(btnSendToChat);

            // Zakładki dla Logów i Szczegółów
            tabLogs = new TabControl { Dock = DockStyle.Fill };
            TabPage pageResults = new TabPage("📊 Wyniki");
            TabPage pageTasks = new TabPage("📝 Opis zadań");
            TabPage pageErrors = new TabPage("❌ Log błędów");
            TabPage pageFullLog = new TabPage("📄 Pełny log (Engine)");
            TabPage pageDetails = new TabPage("🔍 Detale testu");
            
            foreach(TabPage p in new[] { pageResults, pageTasks, pageErrors, pageFullLog, pageDetails })
                p.BackColor = Color.FromArgb(30, 30, 30);

            // Inicjalizacja pól tekstowych
            txtLogs = CreateLogBox();
            txtDetails = CreateLogBox(Color.LightSkyBlue);
            txtTaskDesc = CreateLogBox(Color.LightGray);
            txtErrorLog = CreateLogBox(Color.LightCoral);

            SplitContainer splitResults = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 150,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            splitResults.SizeChanged += (s, e) =>
            {
                int targetHeight = Math.Max(120, (int)(splitResults.Height * 0.25));
                if (targetHeight < splitResults.Height - 80)
                {
                    splitResults.SplitterDistance = targetHeight;
                }
            };
            splitResults.Panel1.Controls.Add(dgvQueue);
            splitResults.Panel2.Controls.Add(dgvTests);

            pageResults.Controls.Add(splitResults);
            pageTasks.Controls.Add(txtTaskDesc);
            pageErrors.Controls.Add(txtErrorLog);
            pageFullLog.Controls.Add(txtLogs);
            
            // Detale testu potrzebują przycisku akcji
            pageDetails.Controls.Add(txtDetails);
            pageDetails.Controls.Add(panTestActions);

            tabLogs.TabPages.Add(pageResults);
            tabLogs.TabPages.Add(pageTasks);
            tabLogs.TabPages.Add(pageErrors);
            tabLogs.TabPages.Add(pageFullLog);
            tabLogs.TabPages.Add(pageDetails);

            this.Controls.Add(tabLogs);
            this.Controls.Add(panFooter);
            this.Controls.Add(panModelPicker);
            this.Controls.Add(panTop);
        }

        private void dgvTestsColumnsInit()
        {
            dgvTests.Columns.Clear();
            dgvTests.Columns.Add("Id", "ID");
            dgvTests.Columns.Add("Category", "Kategoria");
            dgvTests.Columns.Add("Name", "Nazwa Testu");
            dgvTests.Columns.Add("Prompt", "Prompt");
            dgvTests.Columns.Add("Status", "Status");
            dgvTests.Columns.Add("Time", "Czas (s)");
            dgvTests.Columns["Id"].FillWeight = 6;
            dgvTests.Columns["Category"].FillWeight = 16;
            dgvTests.Columns["Name"].FillWeight = 20;
            dgvTests.Columns["Prompt"].FillWeight = 42;
            dgvTests.Columns["Status"].FillWeight = 8;
            dgvTests.Columns["Time"].FillWeight = 8;
            dgvTests.Columns["Id"].MinimumWidth = 45;
            dgvTests.Columns["Status"].MinimumWidth = 70;
            dgvTests.Columns["Time"].MinimumWidth = 70;
            dgvTests.Columns["Prompt"].DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvTests.RowTemplate.Height = 28;
            dgvTests.ColumnHeadersHeight = 45;
            dgvTests.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        private void DgvQueueColumnsInit()
        {
            dgvQueue.Columns.Clear();
            dgvQueue.Columns.Add("QueueIndex", "#");
            dgvQueue.Columns.Add("FileName", "Plik");
            dgvQueue.Columns.Add("BenchmarkName", "Benchmark");
            dgvQueue.Columns.Add("TestCount", "Testy");
            dgvQueue.Columns.Add("QueueStatus", "Status");
            dgvQueue.Columns.Add("QueueScore", "Wynik %");
            dgvQueue.Columns.Add("QueuePassed", "Pass");
            dgvQueue.Columns.Add("QueueTime", "Czas");
            dgvQueue.Columns.Add("QueueErrors", "Errory");

            dgvQueue.Columns["QueueIndex"].FillWeight = 5;
            dgvQueue.Columns["FileName"].FillWeight = 22;
            dgvQueue.Columns["BenchmarkName"].FillWeight = 22;
            dgvQueue.Columns["TestCount"].FillWeight = 8;
            dgvQueue.Columns["QueueStatus"].FillWeight = 14;
            dgvQueue.Columns["QueueScore"].FillWeight = 8;
            dgvQueue.Columns["QueuePassed"].FillWeight = 8;
            dgvQueue.Columns["QueueTime"].FillWeight = 7;
            dgvQueue.Columns["QueueErrors"].FillWeight = 6;
            dgvQueue.Columns["QueueIndex"].MinimumWidth = 35;
            dgvQueue.Columns["TestCount"].MinimumWidth = 55;
            dgvQueue.Columns["QueueScore"].MinimumWidth = 65;
            dgvQueue.Columns["QueuePassed"].MinimumWidth = 70;
            dgvQueue.Columns["QueueTime"].MinimumWidth = 65;
            dgvQueue.Columns["QueueErrors"].MinimumWidth = 55;
            dgvQueue.ColumnHeadersHeight = 36;
            dgvQueue.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        private void InitializeColumnVisibilityMenu()
        {
            columnVisibilityMenu = new ContextMenuStrip();

            foreach (DataGridViewColumn column in dgvTests.Columns)
            {
                var menuItem = new ToolStripMenuItem(column.HeaderText)
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = column.Name
                };
                menuItem.CheckedChanged += ColumnVisibilityItem_CheckedChanged;
                columnVisibilityMenu.Items.Add(menuItem);
            }

            ApplySavedColumnVisibility();
        }

        private void ApplySavedColumnVisibility()
        {
            var savedColumns = UISettingsManager.Settings.BenchmarkVisibleColumns ?? new List<string>();
            if (savedColumns.Count == 0)
            {
                PersistColumnVisibility();
                return;
            }

            foreach (ToolStripMenuItem item in columnVisibilityMenu.Items)
            {
                string columnName = item.Tag as string;
                bool isVisible = savedColumns.Contains(columnName);
                item.Checked = isVisible;
                if (dgvTests.Columns.Contains(columnName))
                {
                    dgvTests.Columns[columnName].Visible = isVisible;
                }
            }
        }

        private void PersistColumnVisibility()
        {
            UISettingsManager.Settings.BenchmarkVisibleColumns = dgvTests.Columns
                .Cast<DataGridViewColumn>()
                .Where(c => c.Visible)
                .Select(c => c.Name)
                .ToList();
            UISettingsManager.Save();
        }

        private void ColumnVisibilityItem_CheckedChanged(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item) || !(item.Tag is string columnName) || !dgvTests.Columns.Contains(columnName))
            {
                return;
            }

            if (!item.Checked && dgvTests.Columns.Cast<DataGridViewColumn>().Count(c => c.Visible) == 1)
            {
                item.CheckedChanged -= ColumnVisibilityItem_CheckedChanged;
                item.Checked = true;
                item.CheckedChanged += ColumnVisibilityItem_CheckedChanged;
                return;
            }

            dgvTests.Columns[columnName].Visible = item.Checked;
            PersistColumnVisibility();
        }

        private RichTextBox CreateLogBox(Color? fg = null)
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = fg ?? Color.LightGray,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                WordWrap = true
            };
        }

        private Button CreateStyledButton(string text, Color bgColor)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Right,
                Width = 110,
                BackColor = bgColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private void BtnLoadJson_Click(object sender, EventArgs e)
        {
            using (System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog { Filter = "Zestaw Benchmarków (*.json)|*.json", Title = "Wybierz plik testowy" })
            {
                ofd.Multiselect = true;
                ofd.Title = "Wybierz pliki testowe";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        foreach (string fileName in ofd.FileNames)
                        {
                            AddBenchmarkToQueue(fileName);
                        }

                        if (ofd.FileNames.Length > 0)
                        {
                            SaveLastPath(ofd.FileNames[0]);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd wczytywania: {ex.Message}");
                    }
                }
            }
        }

        private void AddBenchmarkToQueue(string path)
        {
            string normalizedPath = Path.GetFullPath(path);
            if (_benchmarkQueue.Any(i => string.Equals(i.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var item = LoadBenchmarkQueueItem(normalizedPath);
            _benchmarkQueue.Add(item);
            RefreshQueueGrid();

            if (_activeQueueItem == null)
            {
                SetActiveQueueItem(item, true);
            }
        }

        private void RefreshTestsList(bool clearLogPane)
        {
            if (_currentConfig == null)
            {
                dgvTests.Rows.Clear();
                txtTaskDesc.Clear();
                txtErrorLog.Clear();
                txtDetails.Clear();
                btnSendToChat.Enabled = false;
                btnSendToChat.Tag = null;
                progressBar.Value = 0;
                btnResetTest.Enabled = false;
                return;
            }

            dgvTests.Rows.Clear();
            txtDetails.Clear();
            txtTaskDesc.Clear();
            txtErrorLog.Clear();
            btnSendToChat.Enabled = false;
            btnSendToChat.Tag = null;
            progressBar.Value = 0;

            if (clearLogPane)
            {
                txtLogs.Clear();
            }

            txtTaskDesc.SelectionFont = new Font(txtTaskDesc.Font, FontStyle.Bold);
            txtTaskDesc.AppendText("LISTA ZADAŃ W ZESTAWIE:\n\n");

            foreach (var test in _currentConfig.Tests)
            {
                int rowIndex = dgvTests.Rows.Add(
                    test.Id,
                    test.Category,
                    test.TestName,
                    NormalizePromptForGrid(test.UserPrompt),
                    "Oczekuje",
                    string.Empty);
                dgvTests.Rows[rowIndex].Cells["Prompt"].ToolTipText = test.UserPrompt ?? string.Empty;
                if (test.Passed || test.ExecutionTimeMs > 0 || (test.FailedRulesErrors != null && test.FailedRulesErrors.Any()))
                {
                    dgvTests.Rows[rowIndex].Cells["Status"].Value = test.Passed ? "SUKCES" : "BLAD";
                    dgvTests.Rows[rowIndex].Cells["Time"].Value = (test.ExecutionTimeMs / 1000.0).ToString("F1") + "s";
                    dgvTests.Rows[rowIndex].DefaultCellStyle.BackColor = test.Passed ? Color.ForestGreen : Color.Maroon;
                    dgvTests.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;
                }
                
                txtTaskDesc.SelectionColor = Color.White;
                txtTaskDesc.AppendText($"[ID: {test.Id}] {test.TestName}\n");
                txtTaskDesc.SelectionColor = Color.LightGray;
                txtTaskDesc.AppendText($"PROMPT: {test.UserPrompt}\n");
                txtTaskDesc.AppendText(new string('-', 40) + "\n");
            }

            AppendErrorsFromCurrentConfig();
            btnStart.Enabled = _benchmarkQueue.Count > 0;
            btnResetTest.Enabled = _activeQueueItem != null;
            lblGlobalStatus.Text = $"Aktywny benchmark: {_currentConfig.Tests.Count} testow.";
        }

        private BenchmarkQueueItem LoadBenchmarkQueueItem(string path)
        {
            string content = File.ReadAllText(path);
            var rawConfig = JsonConvert.DeserializeObject<BenchmarkConfig>(content);
            if (rawConfig == null)
            {
                throw new InvalidOperationException("Nie udalo sie zdeserializowac pliku benchmarku.");
            }

            var cleanConfig = CreateCleanBenchmarkConfig(rawConfig);
            string cleanConfigJson = JsonConvert.SerializeObject(cleanConfig);

            return new BenchmarkQueueItem
            {
                FilePath = path,
                DisplayName = Path.GetFileName(path),
                BenchmarkName = !string.IsNullOrWhiteSpace(cleanConfig.RunMetadata?.BenchmarkName)
                    ? cleanConfig.RunMetadata.BenchmarkName
                    : Path.GetFileNameWithoutExtension(path),
                TestCount = cleanConfig.Tests?.Count ?? 0,
                CleanConfigJson = cleanConfigJson,
                LoadedConfig = JsonConvert.DeserializeObject<BenchmarkConfig>(cleanConfigJson),
                Status = "Oczekuje"
            };
        }

        private BenchmarkConfig CreateCleanBenchmarkConfig(BenchmarkConfig source)
        {
            var clone = JsonConvert.DeserializeObject<BenchmarkConfig>(
                JsonConvert.SerializeObject(source)) ?? new BenchmarkConfig();

            if (clone.RunMetadata == null)
            {
                clone.RunMetadata = new RunMetadata();
            }

            clone.RunMetadata.GlobalScore = 0;
            clone.RunMetadata.AverageExecutionTimeMs = 0;
            clone.RunMetadata.CategoriesScores = new Dictionary<string, double>();

            foreach (var test in clone.Tests ?? Enumerable.Empty<BenchmarkTest>())
            {
                test.Passed = false;
                test.ExecutionTimeMs = 0;
                test.RecordedToolCalls = new List<RecordedToolCall>();
                test.FailedRulesErrors = new List<string>();
            }

            return clone;
        }

        private string NormalizePromptForGrid(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return string.Empty;
            }

            return prompt
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private void RefreshQueueGrid()
        {
            dgvQueue.Rows.Clear();

            for (int i = 0; i < _benchmarkQueue.Count; i++)
            {
                var item = _benchmarkQueue[i];
                string scoreText = item.Score.HasValue ? item.Score.Value.ToString("F2") : string.Empty;
                string passText = item.TestCount > 0 ? $"{item.PassedCount}/{item.TestCount}" : string.Empty;
                string timeText = item.DurationMs > 0 ? (item.DurationMs / 1000.0).ToString("F1") + "s" : string.Empty;
                int rowIndex = dgvQueue.Rows.Add(
                    i + 1,
                    item.DisplayName,
                    item.BenchmarkName,
                    item.TestCount,
                    item.Status,
                    scoreText,
                    passText,
                    timeText,
                    item.FailedCount);

                dgvQueue.Rows[rowIndex].Tag = item;

                if (ReferenceEquals(item, _activeQueueItem))
                {
                    dgvQueue.Rows[rowIndex].Selected = true;
                }

                if (string.Equals(item.Status, "Zakonczono", StringComparison.OrdinalIgnoreCase))
                {
                    dgvQueue.Rows[rowIndex].DefaultCellStyle.BackColor = item.FailedCount == 0 ? Color.ForestGreen : Color.DarkOliveGreen;
                    dgvQueue.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;
                }
                else if (string.Equals(item.Status, "Blad krytyczny", StringComparison.OrdinalIgnoreCase))
                {
                    dgvQueue.Rows[rowIndex].DefaultCellStyle.BackColor = Color.Maroon;
                    dgvQueue.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;
                }
                else if (string.Equals(item.Status, "Przerwano", StringComparison.OrdinalIgnoreCase))
                {
                    dgvQueue.Rows[rowIndex].DefaultCellStyle.BackColor = Color.SaddleBrown;
                    dgvQueue.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;
                }
                else if (string.Equals(item.Status, "W trakcie", StringComparison.OrdinalIgnoreCase))
                {
                    dgvQueue.Rows[rowIndex].DefaultCellStyle.BackColor = Color.DarkSlateBlue;
                    dgvQueue.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.White;
                }
            }

            btnClearQueue.Enabled = _benchmarkQueue.Count > 0 && !_isBatchRunning;
            btnStart.Enabled = _benchmarkQueue.Count > 0 && !_isBatchRunning;
            lblBatchStatus.Text = $"Kolejka benchmarkow: {_benchmarkQueue.Count} plikow.";
            progressBarBatch.Maximum = Math.Max(_benchmarkQueue.Count, 1);
        }

        private void SetActiveQueueItem(BenchmarkQueueItem item, bool clearLogPane)
        {
            _activeQueueItem = item;
            if (item == null)
            {
                _currentConfig = null;
                RefreshTestsList(clearLogPane);
                return;
            }

            _currentConfig = CloneConfig(item.FinalConfig ?? item.LoadedConfig);
            this.Tag = item.FilePath;
            RefreshTestsList(clearLogPane);
            SelectQueueRow(item);
        }

        private void SelectQueueRow(BenchmarkQueueItem item)
        {
            if (item == null) return;

            foreach (DataGridViewRow row in dgvQueue.Rows)
            {
                if (ReferenceEquals(row.Tag, item))
                {
                    row.Selected = true;
                    break;
                }
            }
        }

        private BenchmarkConfig CloneConfig(BenchmarkConfig source)
        {
            if (source == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<BenchmarkConfig>(
                JsonConvert.SerializeObject(source));
        }

        private void AppendErrorsFromCurrentConfig()
        {
            if (_currentConfig?.Tests == null) return;

            foreach (var test in _currentConfig.Tests.Where(t => t.FailedRulesErrors != null && t.FailedRulesErrors.Any()))
            {
                txtErrorLog.SelectionFont = new Font(txtErrorLog.Font, FontStyle.Bold);
                txtErrorLog.AppendText($"[TEST {test.Id}: {test.TestName}]\n");
                foreach (string err in test.FailedRulesErrors)
                {
                    txtErrorLog.AppendText($"  - {err}\n");
                }
                txtErrorLog.AppendText("\n");
            }
        }

        private void RestoreLastBenchmarkProfileSelection()
        {
            string lastProfile = UISettingsManager.Settings.LastBenchmarkProfileName;
            if (!string.IsNullOrWhiteSpace(lastProfile))
            {
                int index = cbProfiles.Items.IndexOf(lastProfile);
                if (index >= 0)
                {
                    cbProfiles.SelectedIndex = index;
                    return;
                }
            }

            cbProfiles.SelectedIndex = 0;
        }

        private void CbProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbProfiles.SelectedIndex <= 0)
            {
                UISettingsManager.Settings.LastBenchmarkProfileName = string.Empty;
            }
            else
            {
                UISettingsManager.Settings.LastBenchmarkProfileName = cbProfiles.SelectedItem?.ToString() ?? string.Empty;
            }

            UISettingsManager.Save();
        }

        private void SaveLastPath(string path)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_PATH))
                {
                    key.SetValue(REG_KEY, path);
                }
            } catch { }
        }

        private void LoadLastPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_PATH))
                {
                    if (key != null)
                    {
                        string path = key.GetValue(REG_KEY) as string;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            AddBenchmarkToQueue(path);
                        }
                    }
                }
            } catch { }
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (_benchmarkQueue.Count == 0) return;

            _isBatchRunning = _benchmarkQueue.Count > 1;
            if (_isBatchRunning)
            {
                _engine.OnBenchmarkCompleted -= Engine_OnBenchmarkCompleted;
            }
            btnStart.Enabled = false;
            btnLoadJson.Enabled = false;
            btnResetTest.Enabled = false;
            btnClearQueue.Enabled = false;
            btnStop.Enabled = true;
            cbProfiles.Enabled = false;
            
            _cts = new CancellationTokenSource();
            progressBarBatch.Maximum = Math.Max(_benchmarkQueue.Count, 1);
            progressBarBatch.Value = 0;
            progressBar.Maximum = Math.Max(_currentConfig?.Tests?.Count ?? 0, 1);
            progressBar.Value = 0;
            string selectedProfile = null;
            if (cbProfiles.SelectedIndex > 0)
            {
                selectedProfile = cbProfiles.SelectedItem.ToString();
            }

            txtLogs.Clear();
            txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] URUCHAMIAM KOLEJKE BENCHMARKOW...\n");

            try
            {
                // Uruchomienie w wątku tła, aby UI pozostało responsywne
                var provider = LLMConfigManager.GetActiveProvider();
                int completedCount = 0;

                foreach (var item in _benchmarkQueue)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }

                    item.Status = "W trakcie";
                    item.ErrorMessage = null;
                    item.FinalConfig = null;
                    item.Score = null;
                    item.PassedCount = 0;
                    item.FailedCount = 0;
                    item.DurationMs = 0;
                    SetActiveQueueItem(item, false);
                    progressBar.Maximum = Math.Max(item.TestCount, 1);
                    progressBar.Value = 0;
                    lblBatchStatus.Text = $"Kolejka: {completedCount}/{_benchmarkQueue.Count} zakonczonych. Teraz: {item.DisplayName}";
                    RefreshQueueGrid();
                    txtLogs.AppendText($"\n=== START PLIKU: {item.DisplayName} ===\n");

                    try
                    {
                        BenchmarkConfig finalConfig = await Task.Run(async () =>
                        {
                            return await _engine.RunBenchmarkAsync(item.FilePath, selectedProfile, _cts.Token);
                        });

                        item.FinalConfig = finalConfig;
                        item.Score = finalConfig?.RunMetadata?.GlobalScore;
                        item.PassedCount = finalConfig?.Tests?.Count(t => t.Passed) ?? 0;
                        item.FailedCount = finalConfig?.Tests?.Count(t => !t.Passed) ?? 0;
                        item.DurationMs = finalConfig?.Tests?.Sum(t => t.ExecutionTimeMs) ?? 0;
                        item.Status = _cts.IsCancellationRequested ? "Przerwano" : "Zakonczono";
                    }
                    catch (Exception ex)
                    {
                        item.Status = "Blad krytyczny";
                        item.ErrorMessage = ex.Message;
                        txtLogs.SelectionColor = Color.Red;
                        txtLogs.AppendText($"\nBLAD KRYTYCZNY ({item.DisplayName}): {ex.Message}\n");
                        txtLogs.SelectionColor = txtLogs.ForeColor;
                    }

                    completedCount++;
                    progressBarBatch.Value = Math.Min(completedCount, progressBarBatch.Maximum);
                    RefreshQueueGrid();
                    SetActiveQueueItem(item, false);

                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }
                }

                provider = LLMConfigManager.GetActiveProvider();
                if (_benchmarkQueue.Count > 1)
                {
                    _lastBatchSummary = _engine.SaveBatchSummaryReport(
                        _benchmarkQueue,
                        selectedProfile,
                        provider?.Name,
                        provider?.ModelName);

                    if (_lastBatchSummary != null)
                    {
                        lblBatchStatus.Text = $"Kolejka zakonczona: {_lastBatchSummary.CompletedBenchmarks}/{_lastBatchSummary.TotalBenchmarks}, laczny wynik {_lastBatchSummary.WeightedGlobalScore:F2}%";
                        txtLogs.AppendText($"\n=== PODSUMOWANIE BATCHA: {_lastBatchSummary.WeightedGlobalScore:F2}% ({_lastBatchSummary.PassedTests}/{_lastBatchSummary.TotalTests}) ===\n");
                    }
                }
            }
            catch (Exception ex)
            {
                txtLogs.SelectionColor = Color.Red;
                txtLogs.AppendText($"\nBŁĄD KRYTYCZNY: {ex.Message}\n");
            }
            finally
            {
                FinishRun();
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                btnStop.Enabled = false;
                btnStop.Text = "Przerwanie...";
            }
        }

        private void FinishRun()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(FinishRun));
                return;
            }

            btnStart.Enabled = true;
            btnLoadJson.Enabled = true;
            btnResetTest.Enabled = _activeQueueItem != null;
            btnClearQueue.Enabled = _benchmarkQueue.Count > 0;
            btnStop.Enabled = false;
            btnStop.Text = "⏹ Stop";
            cbProfiles.Enabled = true;
            _isBatchRunning = false;
            _engine.OnBenchmarkCompleted -= Engine_OnBenchmarkCompleted;
            _engine.OnBenchmarkCompleted += Engine_OnBenchmarkCompleted;
            
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
        }

        private void Engine_OnLogMessage(object sender, string message)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, string>(Engine_OnLogMessage), sender, message);
                return;
            }

            txtLogs.AppendText($"{message}\n");
            txtLogs.SelectionStart = txtLogs.Text.Length;
            txtLogs.ScrollToCaret();
        }

        private void Engine_OnTestFinished(object sender, BenchmarkProgressEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, BenchmarkProgressEventArgs>(Engine_OnTestFinished), sender, e);
                return;
            }

            progressBar.Value = e.CurrentTestIndex;
            lblGlobalStatus.Text = $"Postęp: {e.CurrentTestIndex}/{e.TotalTests} ({Math.Round((double)e.CurrentTestIndex/e.TotalTests*100)}%)";

            int rowIndex = e.CurrentTestIndex - 1;
            if (rowIndex >= 0 && rowIndex < dgvTests.Rows.Count)
            {
                var row = dgvTests.Rows[rowIndex];
                row.Cells["Status"].Value = e.TestResult.Passed ? "SUKCES" : "BŁĄD";
                
                double seconds = e.TestResult.ExecutionTimeMs / 1000.0;
                row.Cells["Time"].Value = seconds.ToString("F1") + "s";
                
                row.DefaultCellStyle.BackColor = e.TestResult.Passed ? Color.ForestGreen : Color.Maroon;
                row.DefaultCellStyle.ForeColor = Color.White;

                // Logujemy błędy do sumarycznej zakładki
                if (!e.TestResult.Passed)
                {
                    txtErrorLog.SelectionFont = new Font(txtErrorLog.Font, FontStyle.Bold);
                    txtErrorLog.AppendText($"[TEST {e.TestResult.Id}: {e.TestResult.TestName}]\n");
                    foreach(var err in e.TestResult.FailedRulesErrors)
                        txtErrorLog.AppendText($"  ✗ {err}\n");
                    txtErrorLog.AppendText("\n");
                }
            }
        }

        private void Engine_OnBenchmarkCompleted(object sender, BenchmarkCompletedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, BenchmarkCompletedEventArgs>(Engine_OnBenchmarkCompleted), sender, e);
                return;
            }

            string resultMsg = e.WasCancelled ? "PRZERWANO" : $"ZAKOŃCZONO: {e.FinalConfig.RunMetadata.GlobalScore}%";
            lblGlobalStatus.Text = resultMsg;
            
            _currentConfig = e.FinalConfig; // Aktualizujemy referencję o wyniki dla DgvTests_SelectionChanged

            MessageBox.Show($"Zakończono Benchmark V2!\n\nSkuteczność: {e.FinalConfig.RunMetadata.GlobalScore}%\nCzas średni: {e.FinalConfig.RunMetadata.AverageExecutionTimeMs}ms", 
                "Bielik AI V2 GOLD", MessageBoxButtons.OK, 
                e.FinalConfig.RunMetadata.GlobalScore > 70 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void BtnResetTest_Click(object sender, EventArgs e)
        {
            if (_activeQueueItem == null || string.IsNullOrWhiteSpace(_activeQueueItem.CleanConfigJson))
            {
                return;
            }

            _activeQueueItem.LoadedConfig = JsonConvert.DeserializeObject<BenchmarkConfig>(_activeQueueItem.CleanConfigJson);
            _activeQueueItem.FinalConfig = null;
            _activeQueueItem.Score = null;
            _activeQueueItem.PassedCount = 0;
            _activeQueueItem.FailedCount = 0;
            _activeQueueItem.DurationMs = 0;
            _activeQueueItem.ErrorMessage = null;
            _activeQueueItem.Status = "Oczekuje";
            SetActiveQueueItem(_activeQueueItem, false);
            RefreshQueueGrid();
            lblGlobalStatus.Text = $"Zresetowano stan {_currentConfig?.Tests?.Count ?? 0} testow.";
            txtLogs.AppendText($"\n[{DateTime.Now:HH:mm:ss}] Zresetowano wyniki benchmarku bez ponownego wczytywania pliku.");
        }

        private void BtnClearQueue_Click(object sender, EventArgs e)
        {
            if (_isBatchRunning)
            {
                return;
            }

            _benchmarkQueue.Clear();
            _activeQueueItem = null;
            _currentConfig = null;
            _lastBatchSummary = null;
            RefreshQueueGrid();
            RefreshTestsList(false);
            lblGlobalStatus.Text = "Oczekiwanie na pliki JSON...";
            lblBatchStatus.Text = "Kolejka benchmarkow: 0 plikow.";
            txtLogs.Clear();
        }

        private void BtnColumns_Click(object sender, EventArgs e)
        {
            columnVisibilityMenu?.Show(btnColumns, new Point(0, btnColumns.Height));
        }

        private void DgvQueue_SelectionChanged(object sender, EventArgs e)
        {
            if (_isBatchRunning || dgvQueue.SelectedRows.Count == 0)
            {
                return;
            }

            if (dgvQueue.SelectedRows[0].Tag is BenchmarkQueueItem item)
            {
                SetActiveQueueItem(item, false);
            }
        }

        private void DgvTests_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvTests.SelectedRows.Count == 0 || _currentConfig == null) return;

            int index = dgvTests.SelectedRows[0].Index;
            if (index < 0 || index >= _currentConfig.Tests.Count) return;

            var test = _currentConfig.Tests[index];
            btnSendToChat.Enabled = true;
            btnSendToChat.Tag = test.UserPrompt;

            txtDetails.Clear();
            txtDetails.SelectionFont = new Font(txtDetails.Font, FontStyle.Bold);
            txtDetails.SelectionColor = Color.White;
            txtDetails.AppendText($"TEST: {test.TestName}\n");
            txtDetails.AppendText($"POMOC: {test.Description}\n\n");

            txtDetails.SelectionColor = Color.LightSkyBlue;
            txtDetails.AppendText($"--- PROMPT ---\n{test.UserPrompt}\n\n");

            if (test.RecordedToolCalls != null && test.RecordedToolCalls.Any())
            {
                txtDetails.SelectionColor = Color.LightGreen;
                txtDetails.AppendText($"--- WYWOŁANE NARZĘDZIA ({test.RecordedToolCalls.Count}) ---\n");
                foreach (var call in test.RecordedToolCalls)
                {
                    txtDetails.AppendText($"  • {call.ToolName}({call.Arguments?.ToString(Formatting.None)})\n");
                }
            }

            if (test.FailedRulesErrors != null && test.FailedRulesErrors.Any())
            {
                txtDetails.SelectionColor = Color.LightCoral;
                txtDetails.AppendText($"\n--- ZNALEZIONE BŁĘDY ({test.FailedRulesErrors.Count}) ---\n");
                foreach (var err in test.FailedRulesErrors)
                {
                    txtDetails.AppendText($"  ✗ {err}\n");
                }
            }
            else if (test.Passed)
            {
                txtDetails.SelectionColor = Color.LimeGreen;
                txtDetails.AppendText("\n--- STATUS: WSZYSTKIE REGUŁY SPEŁNIONE ---");
            }
        }

        private void BtnSendToChat_Click(object sender, EventArgs e)
        {
            if (btnSendToChat.Tag is string prompt && AgentControl.Instance != null)
            {
                AgentControl.Instance.SwitchToChat();
                _ = AgentControl.Instance.ProcessInputAsync(prompt);
            }
        }

        // ========== Provider / Model picker logic ==========

        private void OnExternalConfigChanged()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(OnExternalConfigChanged));
                return;
            }
            RefreshProviderDropdown();
            _ = RefreshModelsForCurrentProviderAsync();
            _ = UpdateLiveStatusAsync();
        }

        private void RefreshProviderDropdown()
        {
            if (cbProviders == null) return;
            try
            {
                _suppressModelChangedEvent = true;
                var config = LLMConfigManager.Current;
                if (config == null) return;
                var active = LLMConfigManager.GetActiveProvider();

                cbProviders.DataSource = null;
                cbProviders.DataSource = config.Providers;
                cbProviders.DisplayMember = "Name";
                if (active != null)
                {
                    int idx = config.Providers.FindIndex(p => p.Id == active.Id);
                    if (idx >= 0) cbProviders.SelectedIndex = idx;
                }
            }
            finally
            {
                _suppressModelChangedEvent = false;
            }
        }

        private void CbProviders_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressModelChangedEvent) return;
            if (cbProviders.SelectedItem is LLMProviderConfig selected)
            {
                LLMConfigManager.SetActiveProvider(selected.Id);
                _ = RefreshModelsForCurrentProviderAsync();
                _ = UpdateLiveStatusAsync();
            }
        }

        private async System.Threading.Tasks.Task RefreshModelsForCurrentProviderAsync()
        {
            if (cbModels == null) return;
            var active = LLMConfigManager.GetActiveProvider();
            if (active == null) return;

            _suppressModelChangedEvent = true;
            try
            {
                if (!LLMClient.SupportsLocalModelManagement(active))
                {
                    cbModels.Visible = false;
                    btnRefreshModels.Visible = false;
                    chkLiveStatus.Visible = false;
                    btnUnloadModel.Visible = false;
                    lblModelStatus.Text = $"Aktualny: {active.ModelName ?? "(brak)"}  (remote – brak VRAM API)";
                    lblModelStatus.ForeColor = Color.Gray;
                    lblModelStatus.Visible = true;
                    cbModels.Items.Clear();
                    return;
                }

                cbModels.Visible = true;
                btnRefreshModels.Visible = true;
                chkLiveStatus.Visible = true;
                btnUnloadModel.Visible = true;
                cbModels.Items.Clear();
                cbModels.Items.Add("(pobieranie listy...)");
                cbModels.SelectedIndex = 0;
                btnRefreshModels.Enabled = false;
                btnRefreshModels.Text = "⏳";

                var client = new LLMClient(ToolOrchestrator.Instance);
                var models = await client.GetAvailableModelsAsync(active);

                btnRefreshModels.Enabled = true;
                btnRefreshModels.Text = "🔄";

                _suppressModelChangedEvent = true;
                cbModels.Items.Clear();
                if (models.Count == 0)
                {
                    cbModels.Items.Add("(brak modeli – kliknij 🔄)");
                    cbModels.SelectedIndex = 0;
                }
                else
                {
                    foreach (var m in models) cbModels.Items.Add(m);
                    if (!string.IsNullOrEmpty(active.ModelName) && cbModels.Items.Contains(active.ModelName))
                        cbModels.Text = active.ModelName;
                    else
                        cbModels.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                cbModels.Items.Clear();
                cbModels.Items.Add("(błąd pobierania)");
                cbModels.SelectedIndex = 0;
                btnRefreshModels.Text = "🔄";
                btnRefreshModels.Enabled = true;
                Engine_OnLogMessage(this, $"[ModelPicker] Błąd: {ex.Message}");
            }
            finally
            {
                _suppressModelChangedEvent = false;
            }
        }

        private void BtnRefreshModels_Click(object sender, EventArgs e)
        {
            _ = RefreshModelsForCurrentProviderAsync();
        }

        private async void BtnUnloadModel_Click(object sender, EventArgs e)
        {
            var active = LLMConfigManager.GetActiveProvider();
            if (active == null) return;
            if (!LLMClient.SupportsLocalModelManagement(active))
            {
                MessageBox.Show("Dostawcy chmurowi nie obsługują unload.",
                    "Wskazówka", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnUnloadModel.Enabled = false;
            btnUnloadModel.Text = "⏳";
            lblModelStatus.Text = "Rozładowywanie modelu...";
            lblModelStatus.ForeColor = Color.Khaki;

            try
            {
                var client = new LLMClient(ToolOrchestrator.Instance);
                var (ok, message) = await client.UnloadModelAsync(active);
                if (ok)
                {
                    Engine_OnLogMessage(this, $"[Unload] {message}");
                }
                else
                {
                    Engine_OnLogMessage(this, $"[Unload] {message}");
                    MessageBox.Show(message, "Błąd rozładowania", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                await UpdateLiveStatusAsync();
            }
            catch (Exception ex)
            {
                Engine_OnLogMessage(this, $"[Unload] Wyjątek: {ex.Message}");
                MessageBox.Show(ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnUnloadModel.Enabled = true;
                btnUnloadModel.Text = "⏏ Rozładuj";
            }
        }

        private void CbModels_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressModelChangedEvent) return;
            if (cbModels.SelectedItem is string modelName && !string.IsNullOrEmpty(modelName)
                && !modelName.StartsWith("(") && modelName != activeProviderModelName())
            {
                _ = SwitchToModelAsync(modelName);
            }
        }

        private string activeProviderModelName()
        {
            return LLMConfigManager.GetActiveProvider()?.ModelName;
        }

        private async System.Threading.Tasks.Task SwitchToModelAsync(string newModelName)
        {
            if (string.IsNullOrEmpty(newModelName)) return;

            var active = LLMConfigManager.GetActiveProvider();
            if (active == null) return;
            if (!LLMClient.SupportsLocalModelManagement(active))
            {
                MessageBox.Show("Dostawcy chmurowi nie obsługują dynamicznego ładowania modeli.",
                    "Wskazówka", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string previousModel = active.ModelName;
            cbModels.Enabled = false;
            cbProviders.Enabled = false;
            btnRefreshModels.Enabled = false;
            lblModelStatus.Text = "Przeładowanie modelu...";
            lblModelStatus.ForeColor = Color.Khaki;

            try
            {
                var client = new LLMClient(ToolOrchestrator.Instance);

                // 1) Unload poprzedniego
                if (!string.IsNullOrEmpty(previousModel) && previousModel != newModelName)
                {
                    var (okU, msgU) = await client.UnloadModelAsync(active);
                    if (okU)
                        Engine_OnLogMessage(this, $"[ModelSwitch] {msgU}");
                    else
                        Engine_OnLogMessage(this, $"[ModelSwitch] Unload: {msgU} (kontynuuję)");
                }

                // 2) Zapis nowego ModelName w configu
                string modelToLoad = newModelName;
                LLMConfigManager.UpdateActiveProvider(p => { p.ModelName = modelToLoad; return p; });

                // 3) Load nowego
                var fresh = LLMConfigManager.GetActiveProvider();
                var (okL, msgL) = await client.LoadModelAsync(fresh);
                if (okL)
                {
                    Engine_OnLogMessage(this, $"[ModelSwitch] {msgL}");
                }
                else
                {
                    Engine_OnLogMessage(this, $"[ModelSwitch] Błąd load: {msgL}");
                    // Cofnij ModelName
                    LLMConfigManager.UpdateActiveProvider(p => { p.ModelName = previousModel; return p; });
                    _suppressModelChangedEvent = true;
                    cbModels.Text = previousModel;
                    _suppressModelChangedEvent = false;
                    MessageBox.Show(msgL, "Błąd ładowania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                // 4) Odśwież etykietę statusu
                await UpdateLiveStatusAsync();
            }
            catch (Exception ex)
            {
                Engine_OnLogMessage(this, $"[ModelSwitch] Wyjątek: {ex.Message}");
                MessageBox.Show(ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cbModels.Enabled = true;
                cbProviders.Enabled = true;
                btnRefreshModels.Enabled = true;
            }
        }

        private void ChkLiveStatus_CheckedChanged(object sender, EventArgs e)
        {
            if (chkLiveStatus.Checked)
            {
                liveStatusTimer.Start();
                _ = UpdateLiveStatusAsync();
            }
            else
            {
                liveStatusTimer.Stop();
            }
        }

        private void LiveStatusTimer_Tick(object sender, EventArgs e)
        {
            _ = UpdateLiveStatusAsync();
        }

        private async System.Threading.Tasks.Task UpdateLiveStatusAsync()
        {
            if (lblModelStatus == null) return;
            var active = LLMConfigManager.GetActiveProvider();
            if (active == null) return;

            if (!LLMClient.SupportsLocalModelManagement(active))
            {
                lblModelStatus.Text = $"Aktualny: {active.ModelName ?? "(brak)"}  (remote)";
                lblModelStatus.ForeColor = Color.Gray;
                return;
            }

            try
            {
                var client = new LLMClient(ToolOrchestrator.Instance);
                var desc = await client.GetLoadedModelInfoAsync(active);
                if (desc != null)
                {
                    lblModelStatus.Text = "Załadowany: " + desc.FormatStatusLine();
                    lblModelStatus.ForeColor = Color.LightGreen;
                }
                else
                {
                    lblModelStatus.Text = "Brak załadowanego modelu (oczekuje na wybór)";
                    lblModelStatus.ForeColor = Color.Khaki;
                }
            }
            catch
            {
                lblModelStatus.Text = "Aktualny: " + (active.ModelName ?? "(brak)");
                lblModelStatus.ForeColor = Color.Gray;
            }
        }
    }
}
