using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.UI
{
    public class DatasetStudioControl : UserControl
    {
        // KONTROLKI - TAB 1 (Sesja)
        private ListBox lstSessions;
        private RichTextBox txtJsonlEditor; // Aktualna sesja
        private Button btnSave;
        private Label lblTotalTime, lblTokens, lblTokensPerSec;
        private CheckBox chkIsolateContext;
        private List<SessionRecord> _records = new List<SessionRecord>();

        // KONTROLKI - TAB 2 (Edycja Plików)
        private ComboBox cmbFilePath;
        private ListBox lstDatasetLines;
        private RichTextBox txtFileEditor; 
        private List<string> _currentFileLines = new List<string>();
        private string _currentLoadedPath = "";

        private TabControl tabMain;

        public DatasetStudioControl()
        {
            InitializeComponent();
            ApplyTheme();
            LoadLastFile();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(45, 45, 48);

            tabMain = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal };
            var tabSession = new TabPage("🏠 Aktualna sesja");
            var tabEdit = new TabPage("📂 Edycja data setów");

            // --- TAB 1: AKTUALNA SESJA ---
            SetupSessionTab(tabSession);

            // --- TAB 2: EDYCJA DATA SETÓW ---
            SetupEditTab(tabEdit);

            tabMain.TabPages.Add(tabSession);
            tabMain.TabPages.Add(tabEdit);
            this.Controls.Add(tabMain);
        }

        private void SetupSessionTab(TabPage page)
        {
            page.BackColor = Color.FromArgb(30, 30, 30);
            
            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 85, BackColor = Color.FromArgb(28, 28, 28), Padding = new Padding(10) };
            chkIsolateContext = new CheckBox { Text = "✂️ Izoluj polecenie (Single-Turn)", Checked = true, Dock = DockStyle.Top, ForeColor = Color.White, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            chkIsolateContext.CheckedChanged += (s, e) => RefreshEditor();
            
            var stats = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            lblTotalTime = CreateStatLabel("⏱ Czas: -");
            lblTokens = CreateStatLabel("🧠 Tokeny: -");
            lblTokensPerSec = CreateStatLabel("⚡ T/s: -");
            stats.Controls.Add(lblTotalTime); stats.Controls.Add(lblTokens); stats.Controls.Add(lblTokensPerSec);
            header.Controls.Add(stats); header.Controls.Add(chkIsolateContext);

            // Splitter
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 150 };
            lstSessions = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, BorderStyle = BorderStyle.None };
            lstSessions.SelectedIndexChanged += (s, e) => RefreshEditor();
            
            txtJsonlEditor = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.LightGreen, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None };
            btnSave = new Button { Dock = DockStyle.Bottom, Height = 40, Text = "💾 Zapisz do historycznego JSONL", BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSave.Click += BtnSave_Click;

            split.Panel1.Controls.Add(lstSessions);
            split.Panel2.Controls.Add(txtJsonlEditor);
            split.Panel2.Controls.Add(btnSave);
            
            page.Controls.Add(split);
            page.Controls.Add(header);
        }

        private void SetupEditTab(TabPage page)
        {
            page.BackColor = Color.FromArgb(30, 30, 30);

            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Color.FromArgb(40, 40, 40), Padding = new Padding(10) };
            
            cmbFilePath = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDown };
            if (UISettingsManager.Settings.RecentDatasetFiles != null) cmbFilePath.Items.AddRange(UISettingsManager.Settings.RecentDatasetFiles.ToArray());

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            var btnLoad = CreateToolButton("Wczytaj", (s, e) => LoadFile(cmbFilePath.Text));
            var btnSaveFile = CreateToolButton("Zapisz", (s, e) => SaveFile(_currentLoadedPath));
            var btnSaveAs = CreateToolButton("Zapisz jako", (s, e) => SaveAs());
            var btnRefresh = CreateToolButton("Odśwież", (s, e) => LoadFile(_currentLoadedPath));
            btnPanel.Controls.AddRange(new Control[] { btnLoad, btnSaveFile, btnSaveAs, btnRefresh });

            header.Controls.Add(btnPanel);
            header.Controls.Add(cmbFilePath);

            // Splitter
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 150 };
            lstDatasetLines = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, BorderStyle = BorderStyle.None };
            lstDatasetLines.SelectedIndexChanged += LstDatasetLines_SelectedIndexChanged;

            // Editor + Bottom Buttons
            var editContainer = new Panel { Dock = DockStyle.Fill };
            txtFileEditor = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.LightGreen, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None };
            txtFileEditor.TextChanged += (s, e) => JsonSyntaxHighlighter.Highlight(txtFileEditor);

            var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, BackColor = Color.FromArgb(45, 45, 48), Padding = new Padding(5) };
            var btnCopy = CreateToolButton("📋 Duplikuj wpis", (s, e) => DuplicateEntry());
            var btnAdd = CreateToolButton("➕ Dodaj pusty", (s, e) => AddEmptyEntry());
            var btnRun = CreateToolButton("🚀 Uruchom makro", (s, e) => RunMacro());
            btnRun.BackColor = Color.SeaGreen;

            var btnDeleteInstruct = CreateToolButton("🗑️ Usuń instrukcje", (s, e) => DeleteInstructions());
            var btnReplaceInstruct = CreateToolButton("📋 Zamień instrukcję (Schowek)", (s, e) => ReplaceInstructions());
            btnReplaceInstruct.BackColor = Color.FromArgb(0, 122, 204);

            bottomPanel.Controls.AddRange(new Control[] { btnCopy, btnAdd, btnDeleteInstruct, btnReplaceInstruct, btnRun });

            editContainer.Controls.Add(txtFileEditor);
            editContainer.Controls.Add(bottomPanel);

            split.Panel1.Controls.Add(lstDatasetLines);
            split.Panel2.Controls.Add(editContainer);

            page.Controls.Add(split);
            page.Controls.Add(header);
        }

        private Button CreateToolButton(string text, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(10, 5, 10, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 5, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private Label CreateStatLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 10, 10, 0) };
        }

        private void ApplyTheme()
        {
            foreach (Control c in this.Controls) if (c is TabControl tc) tc.SizeMode = TabSizeMode.Fixed;
        }

        // --- LOGIKA TAB 1 ---
        public void AddSessionRecord(string displayName, List<ChatMessage> historySnapshot, List<ToolDefinition> toolsSnapshot, LLMStats stats)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => AddSessionRecord(displayName, historySnapshot, toolsSnapshot, stats))); return; }
            var record = new SessionRecord { DisplayName = $"{DateTime.Now:HH:mm:ss} - {displayName}", Messages = historySnapshot, Tools = toolsSnapshot, Stats = stats };
            _records.Insert(0, record);
            lstSessions.Items.Insert(0, record.DisplayName);
        }

        private void RefreshEditor()
        {
            if (lstSessions.SelectedIndex < 0) return;
            var record = _records[lstSessions.SelectedIndex];
            if (record.Stats != null) UpdateStatsUI(record.Stats);

            var exportList = record.Messages ?? new List<ChatMessage>();
            if (chkIsolateContext.Checked)
            {
                int lastIdx = exportList.FindLastIndex(m => m.Role == "user");
                if (lastIdx >= 0) exportList = exportList.Skip(lastIdx).ToList();
            }

            var wrapper = new { messages = exportList, tools = record.Tools };
            txtJsonlEditor.Text = JsonConvert.SerializeObject(wrapper, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            JsonSyntaxHighlighter.Highlight(txtJsonlEditor);
        }

        private void UpdateStatsUI(LLMStats stats)
        {
            lblTotalTime.Text = $"⏱ Czas: {stats.TotalTimeMs / 1000.0:F1}s";
            lblTokens.Text = $"🧠 Tokeny: {stats.TotalTokens}";
            lblTokensPerSec.Text = $"⚡ T/s: {stats.TokensPerSecond:F1}";
        }

        // --- LOGIKA TAB 2 ---
        private void LoadLastFile()
        {
            string path = UISettingsManager.Settings.LastDatasetFilePath;
            if (string.IsNullOrEmpty(path)) path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Agent_Training_Data_v2_DO_TRENINGU.jsonl");
            if (File.Exists(path)) LoadFile(path);
        }

        private void LoadFile(string path)
        {
            if (!File.Exists(path)) return;
            _currentLoadedPath = path;
            try {
                _currentFileLines = File.ReadAllLines(path).ToList();
                RefreshListUI();
                
                UISettingsManager.Settings.LastDatasetFilePath = path;
                if (!UISettingsManager.Settings.RecentDatasetFiles.Contains(path)) 
                    UISettingsManager.Settings.RecentDatasetFiles.Insert(0, path);
                UISettingsManager.Save();
                cmbFilePath.Text = path;
            } catch (Exception ex) { MessageBox.Show("Błąd wczytywania: " + ex.Message); }
        }

        private void RefreshListUI()
        {
            lstDatasetLines.BeginUpdate();
            lstDatasetLines.Items.Clear();

            for (int i = 0; i < _currentFileLines.Count; i++)
            {
                string label = $"Wpis {i + 1}: {(_currentFileLines[i].Length > 50 ? _currentFileLines[i].Substring(0, 50) + "..." : _currentFileLines[i])}";
                
                try
                {
                    var obj = JObject.Parse(_currentFileLines[i]);
                    var messages = obj["messages"] as JArray;
                    if (messages != null)
                    {
                        var userMsg = messages.FirstOrDefault(m => m["role"]?.ToString() == "user");
                        if (userMsg != null)
                        {
                            string content = userMsg["content"]?.ToString() ?? "";
                            if (content.Length > 80) content = content.Substring(0, 80) + "...";
                            label = $"{i + 1}. [User]: {content}";
                        }
                    }
                }
                catch { }

                lstDatasetLines.Items.Add(label);
            }
            lstDatasetLines.EndUpdate();
        }

        private void SaveFile(string path)
        {
            if (string.IsNullOrEmpty(path)) { SaveAs(); return; }
            SyncCurrentLine();
            File.WriteAllLines(path, _currentFileLines);
            MessageBox.Show("Zapisano pomyślnie.");
        }

        private void SaveAs()
        {
            using (var sfd = new SaveFileDialog { Filter = "JSONL files (*.jsonl)|*.jsonl|All files (*.*)|*.*" })
            {
                if (sfd.ShowDialog() == DialogResult.OK) SaveFile(sfd.FileName);
            }
        }

        private void SyncCurrentLine()
        {
            if (lstDatasetLines.SelectedIndex >= 0)
            {
                try {
                    var obj = JsonConvert.DeserializeObject(txtFileEditor.Text);
                    _currentFileLines[lstDatasetLines.SelectedIndex] = JsonConvert.SerializeObject(obj, Formatting.None);
                } catch { }
            }
        }

        private void LstDatasetLines_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstDatasetLines.SelectedIndex < 0) return;
            string raw = _currentFileLines[lstDatasetLines.SelectedIndex];
            try {
                var obj = JsonConvert.DeserializeObject(raw);
                txtFileEditor.Text = JsonConvert.SerializeObject(obj, Formatting.Indented);
            } catch { txtFileEditor.Text = raw; }
        }

        private void DuplicateEntry()
        {
            if (lstDatasetLines.SelectedIndex < 0) return;
            SyncCurrentLine(); // Zapisz ewentualne zmiany w obecnym wpisie
            _currentFileLines.Add(_currentFileLines[lstDatasetLines.SelectedIndex]);
            RefreshListUI();
            lstDatasetLines.SelectedIndex = lstDatasetLines.Items.Count - 1; // Zaznacz nowy wpis
        }

        private void AddEmptyEntry()
        {
            _currentFileLines.Add("{\"messages\":[],\"tools\":[]}");
            RefreshListUI();
            lstDatasetLines.SelectedIndex = lstDatasetLines.Items.Count - 1; // Zaznacz nowy wpis
        }

        private void RunMacro()
        {
            if (string.IsNullOrEmpty(txtFileEditor.Text)) return;
            try {
                var doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                var obj = JObject.Parse(txtFileEditor.Text);
                var messages = obj["messages"] as JArray;
                if (messages == null) return;

                foreach (var msg in messages)
                {
                    var toolCalls = msg["tool_calls"] as JArray;
                    if (toolCalls != null)
                    {
                        foreach (var call in toolCalls)
                        {
                            string name = call["function"]?["name"]?.ToString();
                            string argsRaw = call["function"]?["arguments"]?.ToString();
                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(argsRaw))
                            {
                                ToolOrchestrator.Instance.ExecuteTool(name, JObject.Parse(argsRaw), doc);
                            }
                        }
                    }
                }
                MessageBox.Show("Makro wykonane pomyślnie.");
            } catch (Exception ex) { MessageBox.Show("Błąd makra: " + ex.Message); }
        }

        private void DeleteInstructions()
        {
            if (string.IsNullOrEmpty(txtFileEditor.Text)) return;
            try {
                var obj = JObject.Parse(txtFileEditor.Text);
                var messages = obj["messages"] as JArray;
                if (messages == null) return;

                // Zachowaj tylko komunikaty SYSTEM, usuń całą interakcję (user/assistant/tool)
                var systemMessages = messages.Where(m => m["role"]?.ToString() == "system").ToList();
                obj["messages"] = new JArray(systemMessages);
                
                txtFileEditor.Text = JsonConvert.SerializeObject(obj, Formatting.Indented);
            } catch (Exception ex) { MessageBox.Show("Błąd parsowania: " + ex.Message); }
        }

        private void ReplaceInstructions()
        {
            if (string.IsNullOrEmpty(txtFileEditor.Text)) return;
            string clipboardText = Clipboard.GetText();
            if (string.IsNullOrEmpty(clipboardText)) { MessageBox.Show("Schowek jest pusty."); return; }

            try {
                JArray newMsgs;
                if (clipboardText.Trim().StartsWith("[")) {
                    newMsgs = JArray.Parse(clipboardText);
                } else {
                    newMsgs = new JArray(JObject.Parse(clipboardText));
                }

                var obj = JObject.Parse(txtFileEditor.Text);
                var currentMessages = obj["messages"] as JArray;

                // Logika inteligentnego łączenia:
                // 1. Jeśli schowek ma własny 'system', zastępujemy wszystko.
                // 2. Jeśli schowek nie ma 'system', zachowujemy 'system' z aktualnego wpisu i doklejamy resztę.
                
                bool clipboardHasSystem = newMsgs.Any(m => m["role"]?.ToString() == "system");
                
                if (clipboardHasSystem)
                {
                    obj["messages"] = newMsgs;
                }
                else
                {
                    var existingSystem = currentMessages?.Where(m => m["role"]?.ToString() == "system").ToList() ?? new List<JToken>();
                    var finalArray = new JArray(existingSystem);
                    foreach (var m in newMsgs) finalArray.Add(m);
                    obj["messages"] = finalArray;
                }

                txtFileEditor.Text = JsonConvert.SerializeObject(obj, Formatting.Indented);
                MessageBox.Show("Instrukcje zamienione (zachowano nagłówek systemowy jeśli brakowało go w schowku).");
            } catch (Exception ex) { MessageBox.Show("Błąd schowka lub parsowania: " + ex.Message); }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
             string trainingPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Agent_Training_Data_v2_DO_TRENINGU.jsonl");
             try {
                var obj = JsonConvert.DeserializeObject(txtJsonlEditor.Text);
                File.AppendAllLines(trainingPath, new[] { JsonConvert.SerializeObject(obj, Formatting.None) });
                MessageBox.Show("Zapisano.");
             } catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
        }
    }

    public class SessionRecord
    {
        public string DisplayName { get; set; }
        public List<ChatMessage> Messages { get; set; }
        public List<ToolDefinition> Tools { get; set; }
        public LLMStats Stats { get; set; }
    }
}
