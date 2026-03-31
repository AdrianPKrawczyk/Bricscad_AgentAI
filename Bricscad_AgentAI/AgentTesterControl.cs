using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
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
    }

    public class AgentTesterControl : UserControl
    {
        private List<TestCase> tests = new List<TestCase>();
        private string baseFileName = "NieznanyTest";

        // Elementy UI
        private ListBox listTests;
        private TextBox txtQuestion, txtExpected, txtTag, txtUserReply, txtComment;
        private CheckBox chkPassed;
        private Label lblStatus, lblScore, lblQ;
        private Button btnRunTest, btnReply, btnSaveEvaluation;
        private ComboBox cmbModels; // Rozwijana lista z modelami!

        public AgentTesterControl()
        {
            InitializeUI();
            LoadModelsAsync(); // Od razu ładujemy listę z LM Studio
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(10);
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9f);

            // --- GÓRNY PASEK NARZĘDZI ---
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(2) };

            Button btnLoad = new Button { Text = "Wczytaj Pytania", Width = 110, Dock = DockStyle.Left, BackColor = Color.LightGray, ForeColor = Color.Black, Cursor = Cursors.Hand };
            Button btnSave = new Button { Text = "Zapisz Raport", Width = 110, Dock = DockStyle.Left, BackColor = Color.PaleGreen, ForeColor = Color.Black, Cursor = Cursors.Hand };

            Label lblModel = new Label { Text = " Model:", Width = 50, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleRight };
            cmbModels = new ComboBox { Width = 160, Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 0, 0) };
            cmbModels.SelectedIndexChanged += (s, e) => { Komendy.wybranyModel = cmbModels.SelectedItem.ToString(); };

            Button btnRefreshModels = new Button { Text = "↻", Width = 30, Dock = DockStyle.Left, Cursor = Cursors.Hand, BackColor = Color.LightYellow, ForeColor = Color.Black };
            btnRefreshModels.Click += (s, e) => LoadModelsAsync();

            lblScore = new Label { Text = "Wynik: 0/0 pkt", Dock = DockStyle.Right, Width = 150, TextAlign = ContentAlignment.MiddleRight, Font = new Font(this.Font, FontStyle.Bold), ForeColor = Color.Gold };
            lblStatus = new Label { Text = "Gotowy.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.LightSkyBlue };

            btnLoad.Click += BtnLoad_Click;
            btnSave.Click += BtnSave_Click;

            panTop.Controls.Add(lblStatus);
            panTop.Controls.Add(lblScore);
            panTop.Controls.Add(btnRefreshModels);
            panTop.Controls.Add(cmbModels);
            panTop.Controls.Add(lblModel);
            panTop.Controls.Add(btnSave);
            panTop.Controls.Add(btnLoad);

            // --- LISTA PYTAŃ (LEWA STRONA) ---
            listTests = new ListBox { Dock = DockStyle.Left, Width = 250, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, Font = new Font("Consolas", 9) };
            listTests.SelectedIndexChanged += ListTests_SelectedIndexChanged;

            // --- PANEL SZCZEGÓŁÓW (PRAWA STRONA) - BUDOWA OPARTA O ROZCIĄGALNE SPLITCONTAINERY ---

            // 1. Zewnętrzny SplitContainer (Pytanie vs Reszta)
            SplitContainer split1 = new SplitContainer { Orientation = Orientation.Horizontal, Dock = DockStyle.Fill, SplitterDistance = 48, SplitterWidth = 6, BackColor = Color.DimGray, FixedPanel = FixedPanel.Panel1 };
            split1.Panel1.BackColor = Color.FromArgb(40, 40, 40);
            split1.Panel2.BackColor = Color.FromArgb(40, 40, 40);

            txtQuestion = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            lblQ = new Label { Text = "Treść polecenia dla Agenta:", Dock = DockStyle.Top, Height = 20 };
            split1.Panel1.Controls.Add(txtQuestion);
            split1.Panel1.Controls.Add(lblQ);

            // 2. Wewnętrzny SplitContainer (Oczekiwane vs Tag z dołem)
            SplitContainer split2 = new SplitContainer { Orientation = Orientation.Horizontal, Dock = DockStyle.Fill, SplitterDistance = 48, SplitterWidth = 6, BackColor = Color.DimGray, FixedPanel = FixedPanel.Panel1 };
            split2.Panel1.BackColor = Color.FromArgb(40, 40, 40);
            split2.Panel2.BackColor = Color.FromArgb(40, 40, 40);
            split1.Panel2.Controls.Add(split2);

            txtExpected = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGoldenrodYellow, Font = new Font("Consolas", 9) };
            Label lblExpected = new Label { Text = "Prawidłowe Makro (Wzorzec):", Dock = DockStyle.Top, Height = 20 };
            split2.Panel1.Controls.Add(txtExpected);
            split2.Panel1.Controls.Add(lblExpected);

            // 3. Najgłębszy SplitContainer (Odpowiedź Agenta vs Oceny i Komentarz)
            SplitContainer split3 = new SplitContainer { Orientation = Orientation.Horizontal, Dock = DockStyle.Fill, SplitterDistance = 250, SplitterWidth = 6, BackColor = Color.DimGray, FixedPanel = FixedPanel.Panel2 };
            split3.Panel1.BackColor = Color.FromArgb(40, 40, 40);
            split3.Panel2.BackColor = Color.FromArgb(40, 40, 40);
            split2.Panel2.Controls.Add(split3);

            txtTag = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.LimeGreen, Font = new Font("Consolas", 10) };
            Label lblTag = new Label { Text = "Odpowiedź Agenta / Przechwycony Tag:", Dock = DockStyle.Top, Height = 20 };

            Panel panAgentControls = new Panel { Dock = DockStyle.Top, Height = 75 };
            btnRunTest = new Button { Text = "🚀 WYŚLIJ POLECENIE DO AGENTA (Start Testu)", Dock = DockStyle.Top, Height = 40, BackColor = Color.LightSkyBlue, ForeColor = Color.Black, Cursor = Cursors.Hand, Font = new Font(this.Font, FontStyle.Bold) };
            btnRunTest.Click += BtnRunTest_Click; // <--- DODAJ TĘ LINIJKĘ

            Panel panReply = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(0, 5, 0, 0) };
            btnReply = new Button { Text = "Wyślij odpowiedź", Dock = DockStyle.Right, Width = 120, BackColor = Color.Khaki, ForeColor = Color.Black, Cursor = Cursors.Hand };
            btnReply.Click += BtnReply_Click;
            txtUserReply = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            panReply.Controls.Add(txtUserReply);
            panReply.Controls.Add(btnReply);

            panAgentControls.Controls.Add(btnRunTest);
            panAgentControls.Controls.Add(panReply);

            split3.Panel1.Controls.Add(txtTag);
            split3.Panel1.Controls.Add(panAgentControls);
            split3.Panel1.Controls.Add(lblTag);

            txtComment = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            Label lblComm = new Label { Text = "Twój komentarz / Analiza błędu:", Dock = DockStyle.Top, Height = 20 };

            Panel panEval = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(0, 10, 0, 0) };
            chkPassed = new CheckBox { Text = "TEST ZALICZONY", Width = 150, Dock = DockStyle.Left, Font = new Font(this.Font, FontStyle.Bold), ForeColor = Color.LimeGreen };
            btnSaveEvaluation = new Button { Text = "Zatwierdź Ocenę", Dock = DockStyle.Left, Width = 150, BackColor = Color.Plum, ForeColor = Color.Black, Cursor = Cursors.Hand };
            btnSaveEvaluation.Click += BtnSaveEvaluation_Click;
            panEval.Controls.Add(btnSaveEvaluation);
            panEval.Controls.Add(chkPassed);

            split3.Panel2.Controls.Add(txtComment);
            split3.Panel2.Controls.Add(lblComm);
            split3.Panel2.Controls.Add(panEval);

            // Składanie w całość
            this.Controls.Add(split1);
            this.Controls.Add(listTests);
            this.Controls.Add(panTop);
        }

        // ==========================================
        // POBIERANIE MODELI Z LM STUDIO BEZPOŚREDNIO
        // ==========================================
        private async void LoadModelsAsync()
        {
            try
            {
                var response = await Komendy.client.GetAsync("http://127.0.0.1:1234/v1/models");
                string json = await response.Content.ReadAsStringAsync();

                List<string> modele = new List<string>();
                int p = 0;
                while ((p = json.IndexOf("\"id\":", p)) != -1)
                {
                    int s = json.IndexOf("\"", p + 5) + 1;
                    int e = json.IndexOf("\"", s);
                    modele.Add(json.Substring(s, e - s));
                    p = e;
                }

                cmbModels.Items.Clear();
                if (modele.Count > 0)
                {
                    foreach (var m in modele) cmbModels.Items.Add(m);

                    if (cmbModels.Items.Contains(Komendy.wybranyModel))
                        cmbModels.SelectedItem = Komendy.wybranyModel;
                    else
                        cmbModels.SelectedIndex = 0;

                    // ZABEZPIECZENIE: Wymuszamy aktualizację zmiennej modelu tuż po pobraniu!
                    Komendy.wybranyModel = cmbModels.SelectedItem.ToString();

                    lblStatus.Text = "Pobrano listę modeli z LM Studio.";
                }
            }
            catch { lblStatus.Text = "Brak połączenia z LM Studio!"; }
        }

        private void RefreshList()
        {
            int selected = listTests.SelectedIndex;
            listTests.Items.Clear();
            foreach (var t in tests)
            {
                string status = t.IsTested ? (t.Passed ? "[OK] " : "[FAIL] ") : "[?] ";
                listTests.Items.Add($"{status} {t.Id}. {t.Question.Substring(0, Math.Min(t.Question.Length, 20))}... ({t.Weight}p)");
            }
            if (selected >= 0 && selected < listTests.Items.Count) listTests.SelectedIndex = selected;

            CalculateScore();
        }

        private void CalculateScore()
        {
            int maxScore = 0;
            int earnedScore = 0;
            int testedCount = 0;

            foreach (var t in tests)
            {
                maxScore += t.Weight;
                if (t.IsTested)
                {
                    testedCount++;
                    if (t.Passed) earnedScore += t.Weight;
                }
            }
            lblScore.Text = $"Postęp: {testedCount}/{tests.Count} | Wynik: {earnedScore}/{maxScore} pkt";
        }

        private void ListTests_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listTests.SelectedIndex < 0) return;
            TestCase t = tests[listTests.SelectedIndex];

            lblQ.Text = $"Treść polecenia dla Agenta (Wartość: {t.Weight} pkt):";
            txtQuestion.Text = t.Question;
            txtExpected.Text = t.ExpectedTag;
            txtTag.Text = t.GeneratedTag;
            txtComment.Text = t.Comment;
            chkPassed.Checked = t.Passed;

            chkPassed.ForeColor = t.Passed ? Color.LimeGreen : Color.LightCoral;
        }

        // ==========================================
        // SILNIK TESTOWANIA I INTERAKCJI
        // ==========================================
        private async void BtnRunTest_Click(object sender, EventArgs e)
        {
            if (listTests.SelectedIndex < 0) return;
            TestCase t = tests[listTests.SelectedIndex];

            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            btnRunTest.Text = "⏳ AGENT MYŚLI...";
            btnRunTest.Enabled = false;

            try
            {
                Komendy.historiaRozmowy.Clear();

                // NAPRAWA BŁĘDU KOMUNIKACJI: Wymagana blokada dokumentu dla zmiany zaznaczenia z poziomu okienka!
                using (DocumentLock loc = doc.LockDocument())
                {
                    if (Komendy.AktywneZaznaczenie != null) Komendy.AktywneZaznaczenie = new Teigha.DatabaseServices.ObjectId[0];
                    doc.Editor.SetImpliedSelection(new Teigha.DatabaseServices.ObjectId[0]);
                }

                string odpowiedz = await Komendy.ZapytajAgentaAsync(t.Question, doc, null);
                txtTag.Text = $"[Polecenie]: {t.Question}\r\n--- AGENT ---\r\n{odpowiedz}";
                lblStatus.Text = "Agent odpowiedział.";
            }
            catch (Exception ex)
            {
                txtTag.Text = $"BŁĄD: {ex.Message}";
                lblStatus.Text = "Wystąpił błąd przed wysłaniem!";
            }
            finally
            {
                btnRunTest.Text = "🚀 WYŚLIJ POLECENIE DO AGENTA (Start Testu)";
                btnRunTest.Enabled = true;
            }
        }

        private async void BtnReply_Click(object sender, EventArgs e)
        {
            if (listTests.SelectedIndex < 0 || string.IsNullOrWhiteSpace(txtUserReply.Text)) return;

            string reply = txtUserReply.Text;
            txtUserReply.Clear();

            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            btnReply.Text = "Wysyłanie...";
            btnReply.Enabled = false;

            try
            {
                txtTag.Text += $"\r\n--- TY ---\r\n{reply}\r\n--- AGENT ---\r\n";
                string odpowiedz = await Komendy.ZapytajAgentaAsync(reply, doc, Komendy.AktywneZaznaczenie);
                txtTag.Text += odpowiedz;
                lblStatus.Text = "Odpowiedź odebrana.";
            }
            catch (Exception ex) { txtTag.Text += $"BŁĄD: {ex.Message}"; }
            finally
            {
                btnReply.Text = "Wyślij odpowiedź";
                btnReply.Enabled = true;
            }
        }

        private void BtnSaveEvaluation_Click(object sender, EventArgs e)
        {
            if (listTests.SelectedIndex < 0) return;
            TestCase t = tests[listTests.SelectedIndex];

            t.GeneratedTag = txtTag.Text.Replace("\r\n", " ").Replace("\n", " ");
            t.Comment = txtComment.Text.Replace("\r\n", " ").Replace("\n", " ");
            t.Passed = chkPassed.Checked;
            t.IsTested = true;

            lblStatus.Text = $"Zapisano ocenę dla testu {t.Id}.";
            RefreshList();
        }

        // ==========================================
        // ROZBUDOWANY PARSER JSON 
        // ==========================================
        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Wczytaj plik z pytaniami" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    baseFileName = Path.GetFileNameWithoutExtension(ofd.FileName);
                    LoadJson(ofd.FileName);
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (tests.Count == 0) return;

            // DYNAMICZNA NAZWA Z NAZWĄ MODELU POBRANĄ BEZPOŚREDNIO Z COMBOBOXA
            string modelName = cmbModels.SelectedItem != null ? cmbModels.SelectedItem.ToString() : Komendy.wybranyModel;
            string safeModelName = string.Join("_", modelName.Split(Path.GetInvalidFileNameChars()));
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string defaultName = $"{baseFileName}-{safeModelName}-{dateStr}.json";

            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Zapisz podsumowanie testów", FileName = defaultName })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SaveJson(sfd.FileName);
                    lblStatus.Text = "Zapisano raport ewaluacyjny!";
                }
            }
        }

        private void LoadJson(string path)
        {
            try
            {
                tests.Clear();
                string content = File.ReadAllText(path, Encoding.UTF8);

                string strPattern = @"((?:[^""\\]|\\.)*)";
                string pattern =
                    @"\{\s*" +
                    @"\""Id\""\s*:\s*(\d+)\s*,\s*" +
                    @"\""Weight\""\s*:\s*(\d+)\s*,\s*" +
                    @"\""Question\""\s*:\s*\""" + strPattern + @"\""\s*,\s*" +
                    @"\""ExpectedTag\""\s*:\s*\""" + strPattern + @"\""\s*,\s*" +
                    @"\""GeneratedTag\""\s*:\s*\""" + strPattern + @"\""\s*,\s*" +
                    @"\""Comment\""\s*:\s*\""" + strPattern + @"\""\s*,\s*" +
                    @"\""Passed\""\s*:\s*(true|false)\s*,\s*" +
                    @"\""IsTested\""\s*:\s*(true|false)\s*" +
                    @"\}";

                MatchCollection matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match m in matches)
                {
                    tests.Add(new TestCase
                    {
                        Id = int.Parse(m.Groups[1].Value),
                        Weight = int.Parse(m.Groups[2].Value),
                        Question = UnescapeJson(m.Groups[3].Value),
                        ExpectedTag = UnescapeJson(m.Groups[4].Value),
                        GeneratedTag = UnescapeJson(m.Groups[5].Value),
                        Comment = UnescapeJson(m.Groups[6].Value),
                        Passed = m.Groups[7].Value.ToLower() == "true",
                        IsTested = m.Groups[8].Value.ToLower() == "true"
                    });
                }
                RefreshList();
                lblStatus.Text = $"Wczytano {tests.Count} testów.";
            }
            catch (Exception ex) { MessageBox.Show("Błąd wczytywania: " + ex.Message); }
        }

        private void SaveJson(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < tests.Count; i++)
            {
                var t = tests[i];
                string comma = (i == tests.Count - 1) ? "" : ",";

                sb.AppendLine($"  {{");
                sb.AppendLine($"    \"Id\": {t.Id},");
                sb.AppendLine($"    \"Weight\": {t.Weight},");
                sb.AppendLine($"    \"Question\": \"{EscapeJson(t.Question)}\",");
                sb.AppendLine($"    \"ExpectedTag\": \"{EscapeJson(t.ExpectedTag)}\",");
                sb.AppendLine($"    \"GeneratedTag\": \"{EscapeJson(t.GeneratedTag)}\",");
                sb.AppendLine($"    \"Comment\": \"{EscapeJson(t.Comment)}\",");
                sb.AppendLine($"    \"Passed\": {t.Passed.ToString().ToLower()},");
                sb.AppendLine($"    \"IsTested\": {t.IsTested.ToString().ToLower()}");
                sb.AppendLine($"  }}{comma}");
            }
            sb.AppendLine("]");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private string EscapeJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
        private string UnescapeJson(string s) => s.Replace("\\\"", "\"").Replace("\\n", "\r\n").Replace("\\\\", "\\");
    }
}