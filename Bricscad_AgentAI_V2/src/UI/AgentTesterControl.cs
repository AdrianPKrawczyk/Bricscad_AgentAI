using System;
using Bricscad.ApplicationServices;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Application = Bricscad.ApplicationServices.Application;

namespace Bricscad_AgentAI_V2.UI
{
    /// <summary>
    /// Model danych testowych dla Workbench V2
    /// </summary>
    public class TestCase
    {
        public int Id { get; set; }
        public int Weight { get; set; }
        public string Question { get; set; }
        public string ExpectedTag { get; set; }
        public string GeneratedTag { get; set; }
        public string Comment { get; set; }
        public bool Passed { get; set; }
        public bool IsTested { get; set; }
        public double ResponseTimeSec { get; set; }
    }

    public class AgentTesterControl : UserControl
    {
        private ListBox lbTests;
        private RichTextBox txtPrompt;
        private TextBox txtExpectedV1;
        private RichTextBox txtGeneratedV2;
        private TextBox txtComment;
        private CheckBox chkPassed;
        private ProgressBar pbProgress;
        private Label lblScore, lblTotalTime;
        private Button btnLoad, btnTest, btnSaveReport;
        
        // --- Interakcja ---
        private TextBox txtUserReply;
        private Button btnReply;
        private List<ChatMessage> _currentHistory;

        // --- HUD ---
        private Label lblStats, lblStatus;
        
        private LLMClient _client;
        private List<TestCase> _currentTests = new List<TestCase>();
        private bool _isUpdating = false;

        public AgentTesterControl(LLMClient client)
        {
            _client = client;
            InitializeUI();
            
            if (_client != null)
            {
                _client.OnToolCallLogged += CaptureToolCall;
                _client.OnStatusUpdate += (s) => UpdateStatus(s);
                _client.OnStatsUpdate += (stats) => UpdateStats(stats);
            }
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(30, 30, 30);

            // --- PANEL GÓRNY (Statystyki i Postęp) ---
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(40, 40, 40), Padding = new Padding(10) };
            lblScore = new Label { Text = "Wynik: 0/0 pkt", Dock = DockStyle.Left, Width = 150, ForeColor = Color.Gold, Font = new Font(this.Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            lblTotalTime = new Label { Text = "Łączny Czas: 0.0 s", Dock = DockStyle.Left, Width = 180, ForeColor = Color.LightSkyBlue, Font = new Font(this.Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            
            pbProgress = new ProgressBar { Dock = DockStyle.Fill, Height = 20, Style = ProgressBarStyle.Continuous };
            panTop.Controls.Add(pbProgress);
            panTop.Controls.Add(lblTotalTime);
            panTop.Controls.Add(lblScore);

            // --- PASEK STATYSTYK DOLNY (HUD) ---
            Panel panHUD = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(5, 2, 5, 2) };
            lblStatus = new Label { Dock = DockStyle.Top, Height = 20, ForeColor = Color.Cyan, Font = new Font("Segoe UI", 8, FontStyle.Bold), Text = "Gotowy." };
            lblStats = new Label { Dock = DockStyle.Top, Height = 20, ForeColor = Color.LightGray, Font = new Font("Consolas", 8), Text = "⏱ 0.0s | 🧠 0 tkn | ⚡ 0 t/s" };
            panHUD.Controls.Add(lblStats);
            panHUD.Controls.Add(lblStatus);

            // --- GŁÓWNY KONTENER (PODZIAŁ PIONOWY) ---
            SplitContainer splitMain = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 300 };

            // PANEL LEWY (LISTA)
            Panel panLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            lbTests = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, ItemHeight = 25 };
            lbTests.SelectedIndexChanged += LbTests_SelectedIndexChanged;
            
            btnLoad = new Button { Text = "📂 Wczytaj JSON", Dock = DockStyle.Bottom, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 60, 60), Cursor = Cursors.Hand };
            btnLoad.Click += BtnLoad_Click;
            
            btnSaveReport = new Button { Text = "💾 Zapisz Raport", Dock = DockStyle.Bottom, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.PaleGreen, BackColor = Color.FromArgb(50, 70, 50), Cursor = Cursors.Hand, Margin = new Padding(0, 5, 0, 0) };
            btnSaveReport.Click += BtnSaveReport_Click;

            panLeft.Controls.Add(lbTests);
            panLeft.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 5 });
            panLeft.Controls.Add(btnSaveReport);
            panLeft.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 5 });
            panLeft.Controls.Add(btnLoad);
            splitMain.Panel1.Controls.Add(panLeft);

            // PANEL PRAWY (WORKBENCH)
            Panel panRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };

            txtPrompt = CreateLabelledRichTextBox(panRight, "Pytanie (User Prompt):", 80, Color.Cyan);
            txtExpectedV1 = CreateLabelledTextBox(panRight, "Oczekiwany Tag (V1 Reference):", Color.Orange);
            
            btnTest = new Button { Text = "🚀 TESTUJ INTENCJĘ I WYKONAJ (V2)", Dock = DockStyle.Top, Height = 45, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(0, 122, 204), Font = new Font(this.Font, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(0, 10, 0, 10) };
            btnTest.Click += BtnTest_Click;
            panRight.Controls.Add(btnTest);

            // Panel Odpowiedzi
            Panel panReply = new Panel { Dock = DockStyle.Top, Height = 35, Padding = new Padding(0, 5, 0, 5) };
            txtUserReply = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, Font = new Font(this.Font, FontStyle.Italic) };
            btnReply = new Button { Text = "Wyślij Odpowiedź", Dock = DockStyle.Right, Width = 120, FlatStyle = FlatStyle.Flat, ForeColor = Color.Black, BackColor = Color.Khaki, Cursor = Cursors.Hand };
            btnReply.Click += BtnReply_Click;
            panReply.Controls.Add(txtUserReply);
            panReply.Controls.Add(btnReply);
            panRight.Controls.Add(panReply);
            panRight.Controls.Add(new Label { Text = "Interakcja (Reply):", Dock = DockStyle.Top, Height = 18, ForeColor = Color.Khaki });

            txtGeneratedV2 = CreateLabelledRichTextBox(panRight, "Logi Agenta i Tool Calls (JSON Indented):", 180, Color.LimeGreen);
            txtGeneratedV2.BackColor = Color.Black;
            txtGeneratedV2.Font = new Font("Consolas", 10f);

            txtComment = CreateLabelledTextBox(panRight, "Mój Komentarz / Analiza:", Color.White);
            txtComment.Multiline = true;
            txtComment.Height = 50;
            txtComment.TextChanged += (s, e) => { if (!_isUpdating && lbTests.SelectedIndex >= 0) _currentTests[lbTests.SelectedIndex].Comment = txtComment.Text; };

            chkPassed = new CheckBox { Text = "TEST ZALICZONY (PASSED)", Dock = DockStyle.Top, Height = 30, ForeColor = Color.LimeGreen, Font = new Font(this.Font, FontStyle.Bold) };
            chkPassed.CheckedChanged += (s, e) => { 
                if (!_isUpdating && lbTests.SelectedIndex >= 0) {
                    _currentTests[lbTests.SelectedIndex].Passed = chkPassed.Checked;
                    _currentTests[lbTests.SelectedIndex].IsTested = true;
                    RefreshScore();
                    UpdateTestListItem(lbTests.SelectedIndex);
                }
            };
            panRight.Controls.Add(chkPassed);

            splitMain.Panel2.Controls.Add(panRight);

            this.Controls.Add(splitMain);
            this.Controls.Add(panHUD);
            this.Controls.Add(panTop);
        }

        private RichTextBox CreateLabelledRichTextBox(Panel parent, string label, int height, Color labelColor)
        {
            Label lbl = new Label { Text = label, Dock = DockStyle.Top, Height = 20, ForeColor = labelColor, Margin = new Padding(0, 5, 0, 0) };
            RichTextBox rtb = new RichTextBox { Dock = DockStyle.Top, Height = height, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, BorderStyle = BorderStyle.None };
            parent.Controls.Add(rtb);
            parent.Controls.Add(lbl);
            return rtb;
        }

        private TextBox CreateLabelledTextBox(Panel parent, string label, Color labelColor)
        {
            Label lbl = new Label { Text = label, Dock = DockStyle.Top, Height = 20, ForeColor = labelColor, Margin = new Padding(0, 5, 0, 0) };
            TextBox tb = new TextBox { Dock = DockStyle.Top, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            parent.Controls.Add(tb);
            parent.Controls.Add(lbl);
            return tb;
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "JSON files|*.json|JSONL files|*.jsonl" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string content = File.ReadAllText(ofd.FileName);
                        _currentTests = JsonConvert.DeserializeObject<List<TestCase>>(content) ?? new List<TestCase>();
                        RefreshTestList();
                        RefreshScore();
                    }
                    catch (Exception ex) { MessageBox.Show("Błąd wczytywania: " + ex.Message); }
                }
            }
        }

        private void BtnSaveReport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "JSON files|*.json", Title = "Zapisz Raport V2" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try { File.WriteAllText(sfd.FileName, JsonConvert.SerializeObject(_currentTests, Newtonsoft.Json.Formatting.Indented)); MessageBox.Show("Raport zapisany!"); }
                    catch (Exception ex) { MessageBox.Show("Błąd zapisu: " + ex.Message); }
                }
            }
        }

        private void RefreshTestList()
        {
            lbTests.Items.Clear();
            for (int i = 0; i < _currentTests.Count; i++)
            {
                var t = _currentTests[i];
                string status = t.IsTested ? (t.Passed ? "[OK] " : "[FAIL] ") : "[?] ";
                lbTests.Items.Add($"{status}{t.Id}. {t.Question}");
            }
            if (lbTests.Items.Count > 0) lbTests.SelectedIndex = 0;
        }

        private void UpdateTestListItem(int index)
        {
            if (index < 0 || index >= _currentTests.Count) return;
            var t = _currentTests[index];
            string status = t.IsTested ? (t.Passed ? "[OK] " : "[FAIL] ") : "[?] ";
            lbTests.Items[index] = $"{status}{t.Id}. {testPreview(t.Question)}";
        }

        private string testPreview(string q) => q.Length > 40 ? q.Substring(0, 37) + "..." : q;

        private void RefreshScore()
        {
            int score = _currentTests.Where(t => t.Passed).Sum(t => t.Weight);
            int total = _currentTests.Sum(t => t.Weight);
            double time = _currentTests.Sum(t => t.ResponseTimeSec);
            int tested = _currentTests.Count(t => t.IsTested);

            lblScore.Text = $"Wynik: {score}/{total} pkt";
            lblTotalTime.Text = $"Łączny Czas: {time:F1} s";
            pbProgress.Maximum = _currentTests.Count;
            pbProgress.Value = tested;
        }

        private void LbTests_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbTests.SelectedIndex < 0) return;
            _isUpdating = true;
            var t = _currentTests[lbTests.SelectedIndex];
            txtPrompt.Text = t.Question;
            txtExpectedV1.Text = t.ExpectedTag;
            txtComment.Text = t.Comment;
            chkPassed.Checked = t.Passed;
            txtGeneratedV2.Clear();
            _isUpdating = false;
        }

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            if (lbTests.SelectedIndex < 0) return;
            var t = _currentTests[lbTests.SelectedIndex];
            
            btnTest.Enabled = false;
            btnTest.Text = "⏳ WYKONYWANIE...";
            txtGeneratedV2.Clear();

            try
            {
                AgentMemoryState.Clear();
                AgentMemoryState.Variables.Clear();

                _currentHistory = new List<ChatMessage> {
                    new ChatMessage { Role = "system", Content = "Jesteś asystentem BricsCAD V2. Odpowiadaj z Tool Calling. Wykonuj zadania precyzyjnie." },
                    new ChatMessage { Role = "user", Content = t.Question }
                };

                Document doc = Application.DocumentManager.MdiActiveDocument;
                string response = await _client.SendMessageReActAsync(_currentHistory, doc);
                
                txtGeneratedV2.SelectionColor = Color.LightGray;
                txtGeneratedV2.AppendText($"[FINAL RESPONSE]: {response}\n");
                
                t.GeneratedTag = response; // Uproszczone zapisanie intencji
            }
            catch (Exception ex) { txtGeneratedV2.AppendText("BŁĄD: " + ex.Message); }
            finally { btnTest.Enabled = true; btnTest.Text = "🚀 TESTUJ INTENCJĘ I WYKONAJ (V2)"; }
        }

        private async void BtnReply_Click(object sender, EventArgs e)
        {
            if (lbTests.SelectedIndex < 0 || _currentHistory == null) return;
            string reply = txtUserReply.Text;
            txtUserReply.Clear();

            btnReply.Enabled = false;
            try
            {
                _currentHistory.Add(new ChatMessage { Role = "user", Content = reply });
                Document doc = Application.DocumentManager.MdiActiveDocument;
                string response = await _client.SendMessageReActAsync(_currentHistory, doc);
                txtGeneratedV2.AppendText($"\n[REPLY RESPONSE]: {response}\n");
            }
            catch (Exception ex) { txtGeneratedV2.AppendText("\nBŁĄD REPLy: " + ex.Message); }
            finally { btnReply.Enabled = true; }
        }

        private void CaptureToolCall(string json)
        {
            if (this.InvokeRequired) { this.Invoke(new Action<string>(CaptureToolCall), json); return; }
            try
            {
                var parsed = JsonConvert.DeserializeObject(json);
                txtGeneratedV2.AppendText(JsonConvert.SerializeObject(parsed, Formatting.Indented) + "\n\n");
            }
            catch { txtGeneratedV2.AppendText(json + "\n\n"); }
            txtGeneratedV2.SelectionStart = txtGeneratedV2.Text.Length;
            txtGeneratedV2.ScrollToCaret();
        }

        private void UpdateStatus(string s) { if (this.InvokeRequired) this.Invoke(new Action(() => lblStatus.Text = s)); else lblStatus.Text = s; }

        private void UpdateStats(LLMStats stats)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => UpdateStats(stats))); return; }
            double sec = stats.TotalTimeMs / 1000.0;
            lblStats.Text = $"⏱ {sec:F1}s | 🧠 {stats.TotalTokens} tkn | ⚡ {stats.TokensPerSecond:F1} t/s";
            
            if (lbTests.SelectedIndex >= 0) {
                _currentTests[lbTests.SelectedIndex].ResponseTimeSec = sec;
                RefreshScore();
            }
        }
    }
}
