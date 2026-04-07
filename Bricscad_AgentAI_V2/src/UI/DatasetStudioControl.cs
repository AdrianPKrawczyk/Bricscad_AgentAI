using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.UI
{
    public class DatasetStudioControl : UserControl
    {
        private ListBox lstSessions;
        private RichTextBox txtJsonlEditor;
        private Button btnSave;
        private Label lblTotalTime;
        private Label lblTokens;
        private Label lblTokensPerSec;
        private CheckBox chkIsolateContext;
        private List<SessionRecord> _records = new List<SessionRecord>();
        
        private static string TrainingFilePath => Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), 
            "Agent_Training_Data_v2_DO_TRENINGU.jsonl"
        );

        public DatasetStudioControl()
        {
            InitializeComponent();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(45, 45, 48);

            // GÓRNA BELKA: Przełączniki i Statystyki
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(10, 5, 10, 5),
                BackColor = Color.FromArgb(28, 28, 28),
                BorderStyle = BorderStyle.FixedSingle
            };

            chkIsolateContext = new CheckBox
            {
                Text = "✂️ Izoluj polecenie (Single-Turn)",
                Checked = true,
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Padding = new Padding(0, 0, 0, 5)
            };
            chkIsolateContext.CheckedChanged += (s, e) => RefreshEditor();

            var statsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };

            lblTotalTime = CreateStatLabel("⏱ Czas: -");
            lblTokens = CreateStatLabel("🧠 Tokeny: -");
            lblTokensPerSec = CreateStatLabel("⚡ T/s: -");

            statsPanel.Controls.Add(lblTotalTime);
            statsPanel.Controls.Add(new Label { Text = " | ", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(5, 12, 5, 0) });
            statsPanel.Controls.Add(lblTokens);
            statsPanel.Controls.Add(new Label { Text = " | ", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(5, 12, 5, 0) });
            statsPanel.Controls.Add(lblTokensPerSec);

            headerPanel.Controls.Add(statsPanel);
            headerPanel.Controls.Add(chkIsolateContext);

            // GŁÓWNY PODZIAŁ (Poziomy)
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(45, 45, 48),
                SplitterWidth = 6
            };
            
            // Panel 1: Lista sesji (20%)
            lstSessions = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                ItemHeight = 25
            };
            lstSessions.SelectedIndexChanged += LstSessions_SelectedIndexChanged;
            mainSplit.Panel1.Controls.Add(lstSessions);

            // Panel 2: Edytor i Zapis (80%)
            var bottomContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

            txtJsonlEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true
            };

            btnSave = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                Text = "💾 Zapisz Złoty Standard do JSONL",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            bottomContainer.Controls.Add(txtJsonlEditor);
            bottomContainer.Controls.Add(btnSave);
            mainSplit.Panel2.Controls.Add(bottomContainer);

            // Inicjalizacja SplitterDistance (20% wysokości)
            this.Load += (s, e) => {
                int targetDistance = (int)(this.Height * 0.2);
                if (targetDistance > 50) mainSplit.SplitterDistance = targetDistance;
            };

            this.Controls.Add(mainSplit);
            this.Controls.Add(headerPanel);
        }

        private Label CreateStatLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, 10, 10, 0)
            };
        }

        private void ApplyTheme()
        {
            this.BackColor = Color.FromArgb(30, 30, 30);
        }

        public void AddSessionRecord(string displayName, List<ChatMessage> historySnapshot, List<ToolDefinition> toolsSnapshot, LLMStats stats)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AddSessionRecord(displayName, historySnapshot, toolsSnapshot, stats)));
                return;
            }
        
            var record = new SessionRecord
            {
                DisplayName = $"{DateTime.Now:HH:mm:ss} - {displayName}",
                Messages = historySnapshot,
                Tools = toolsSnapshot,
                Stats = stats
            };
        
            _records.Insert(0, record); // Najnowsze na górze
            lstSessions.Items.Insert(0, record.DisplayName);
        }

        private void LstSessions_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshEditor();
        }

        private void RefreshEditor()
        {
            if (lstSessions.SelectedIndex < 0) return;
            var record = _records[lstSessions.SelectedIndex];

            if (record.Stats != null)
            {
                UpdateStatsUI(record.Stats);
            }

            List<ChatMessage> exportList;
            if (chkIsolateContext.Checked && record.Messages != null)
            {
                exportList = new List<ChatMessage>();
                // 1. Wiadomość systemowa
                var sysMsg = record.Messages.FirstOrDefault(m => m.Role == "system");
                if (sysMsg != null) exportList.Add(sysMsg);
                
                // 2. Ostatni użytkownik i wszystko po nim
                int lastUserIdx = record.Messages.FindLastIndex(m => m.Role == "user");
                if (lastUserIdx >= 0)
                {
                    exportList.AddRange(record.Messages.Skip(lastUserIdx));
                }
                else
                {
                    exportList = record.Messages.ToList();
                }
            }
            else
            {
                exportList = record.Messages ?? new List<ChatMessage>();
            }

            // Formatowanie JSON do edytora (Indented)
            try
            {
                var wrapper = new { messages = exportList, tools = record.Tools };
                var settings = new JsonSerializerSettings 
                { 
                    NullValueHandling = NullValueHandling.Ignore, 
                    Formatting = Newtonsoft.Json.Formatting.Indented 
                };
                txtJsonlEditor.Text = JsonConvert.SerializeObject(wrapper, settings);
            }
            catch (Exception ex)
            {
                txtJsonlEditor.Text = $"BŁĄD REFRESH: {ex.Message}";
            }
        }

        private void UpdateStatsUI(LLMStats stats)
        {
            double seconds = stats.TotalTimeMs / 1000.0;
            lblTotalTime.Text = $"⏱ Czas: {seconds:F1}s";
            lblTokens.Text = $"🧠 Tokeny: {stats.PromptTokens} wysłane / {stats.CompletionTokens} odebrane (Suma: {stats.TotalTokens})";
            lblTokensPerSec.Text = $"⚡ Prędkość: {stats.TokensPerSecond:F1} t/s";
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtJsonlEditor.Text)) return;

            try
            {
                // Walidacja i unifikacja do jednej linii (Formatting.None)
                // KRYTYCZNE: Używamy settings ignorujących NULL, aby zachować czystość ChatML
                var settings = new JsonSerializerSettings 
                { 
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Newtonsoft.Json.Formatting.None 
                };

                var obj = JsonConvert.DeserializeObject(txtJsonlEditor.Text);
                string singleLineJson = JsonConvert.SerializeObject(obj, settings);

                File.AppendAllLines(TrainingFilePath, new[] { singleLineJson });

                MessageBox.Show($"Pomyślnie dodano Złoty Standard do pliku:\n{TrainingFilePath}", 
                    "Dataset Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisu JSONL:\n{ex.Message}", 
                    "Błąd Walidacji", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
