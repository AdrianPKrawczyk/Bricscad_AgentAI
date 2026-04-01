using Bricscad.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        public double ResponseTimeSec { get; set; }
    }

    public class AgentTesterControl : UserControl
    {
        private List<TestCase> tests = new List<TestCase>();
        private string baseFileName = "NieznanyTest";

        // Elementy UI
        private ListBox listTests;
        private TextBox txtQuestion, txtExpected, txtCapturedTags, txtAgentResponse, txtUserReply, txtComment;
        private CheckBox chkPassed;
        private Label lblStatus, lblScore, lblTotalTime, lblQ;
        private Button btnRunTest, btnReply, btnSaveEvaluation, btnRunExpected;
        private NumericUpDown numWeight;
        private ComboBox cmbModels;
        private Label lblStats;
        private double lastResponseTime = 0;

        private bool isUpdatingUI = false;

        public AgentTesterControl()
        {
            InitializeUI();
            LoadModelsAsync();
            BricsCAD_Agent.Komendy.OnModelStatsUpdated += UpdateStatsUI;

            string autoSavePath = GetAutoSavePath();
            if (File.Exists(autoSavePath))
            {
                try { LoadJson(autoSavePath); lblStatus.Text = "Wznowiono sesję z autozapisu!"; }
                catch { }
            }
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(10);
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9f);

            // ==============================================================
            // --- GÓRNY PASEK NARZĘDZI ---
            // ==============================================================
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 75, Padding = new Padding(2), BackColor = Color.FromArgb(45, 45, 45) };

            // LEWA STRONA (Przyciski + Modele)
            Panel panTopLeft = new Panel { Dock = DockStyle.Left, Width = 390 };

            FlowLayoutPanel flpButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            Button btnLoad = new Button { Text = "Wczytaj JSON", Width = 95, Height = 28, BackColor = Color.LightGray, ForeColor = Color.Black, Cursor = Cursors.Hand };
            Button btnImportJsonl = new Button { Text = "+ Import JSONL", Width = 110, Height = 28, BackColor = Color.LightSkyBlue, ForeColor = Color.Black, Cursor = Cursors.Hand };
            Button btnSave = new Button { Text = "Zapisz Raport", Width = 100, Height = 28, BackColor = Color.PaleGreen, ForeColor = Color.Black, Cursor = Cursors.Hand };
            Button btnClear = new Button { Text = "Wyczyść", Width = 70, Height = 28, BackColor = Color.LightCoral, ForeColor = Color.Black, Cursor = Cursors.Hand };

            btnLoad.Click += BtnLoad_Click;
            btnImportJsonl.Click += BtnImportJsonl_Click;
            btnSave.Click += BtnSave_Click;
            btnClear.Click += (s, e) => { tests.Clear(); RefreshList(); SaveAutoSave(); };

            flpButtons.Controls.Add(btnLoad);
            flpButtons.Controls.Add(btnImportJsonl);
            flpButtons.Controls.Add(btnSave);
            flpButtons.Controls.Add(btnClear);

            Panel panModels = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(0, 4, 0, 0) };
            Label lblModel = new Label { Text = "Model AI:", Width = 60, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleRight };
            Button btnRefreshModels = new Button { Text = "↻", Width = 30, Dock = DockStyle.Left, Cursor = Cursors.Hand, BackColor = Color.LightYellow, ForeColor = Color.Black };
            btnRefreshModels.Click += (s, e) => LoadModelsAsync();
            cmbModels = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbModels.SelectedIndexChanged += (s, e) => { Komendy.wybranyModel = cmbModels.SelectedItem.ToString(); };

            panModels.Controls.Add(cmbModels);
            panModels.Controls.Add(btnRefreshModels);
            panModels.Controls.Add(lblModel);

            panTopLeft.Controls.Add(panModels);
            panTopLeft.Controls.Add(flpButtons);

            // PRAWA STRONA (Punkty i Czas)
            Panel panTopRight = new Panel { Dock = DockStyle.Right, Width = 220 };
            lblScore = new Label { Text = "Wynik: 0/0 pkt", Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleRight, Font = new Font(this.Font, FontStyle.Bold), ForeColor = Color.Gold };
            lblTotalTime = new Label { Text = "Całkowity czas: 0.0 s", Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleRight, Font = new Font(this.Font, FontStyle.Bold), ForeColor = Color.LightSkyBlue };
            panTopRight.Controls.Add(lblTotalTime);
            panTopRight.Controls.Add(lblScore);

            // ŚRODEK (Status)
            lblStatus = new Label { Text = "Gotowy.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGreen, Font = new Font(this.Font, FontStyle.Bold) };

            panTop.Controls.Add(lblStatus);
            panTop.Controls.Add(panTopRight);
            panTop.Controls.Add(panTopLeft);

            // ==============================================================
            // --- GŁÓWNY PODZIAŁ (Lista Zadań vs Szczegóły) ---
            // ==============================================================
            SplitContainer splitMain = new SplitContainer { Orientation = Orientation.Vertical, Dock = DockStyle.Fill, SplitterWidth = 6, BackColor = Color.DimGray };
            splitMain.Panel1.BackColor = Color.FromArgb(30, 30, 30);
            splitMain.Panel2.BackColor = Color.FromArgb(40, 40, 40);

            listTests = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, Font = new Font("Consolas", 9), IntegralHeight = false };
            listTests.SelectedIndexChanged += ListTests_SelectedIndexChanged;
            splitMain.Panel1.Controls.Add(listTests);

            // ==============================================================
            // --- PANEL SZCZEGÓŁÓW (Prawa strona - 5 poziomów) ---
            // ==============================================================
            SplitContainer split1 = new SplitContainer { Orientation = Orientation.Horizontal, Dock = DockStyle.Fill, SplitterWidth = 6, BackColor = Color.DimGray };
            SplitContainer split2 = new SplitContainer { Orientation = Orientation.Horizontal, Dock = DockStyle.Fill, SplitterWidth = 6, BackColor = Color.DimGray };
            SplitContainer split3 = new SplitContainer { Orientation = Orientation.Horizontal, Dock = DockStyle.Fill, SplitterWidth = 6, BackColor = Color.DimGray };
            SplitContainer split4 = new SplitContainer { Orientation = Orientation.Horizontal, Dock = DockStyle.Fill, SplitterWidth = 6, BackColor = Color.DimGray };

            split1.Panel1.BackColor = Color.FromArgb(40, 40, 40);
            split2.Panel1.BackColor = Color.FromArgb(40, 40, 40);
            split3.Panel1.BackColor = Color.FromArgb(40, 40, 40);
            split4.Panel1.BackColor = Color.FromArgb(40, 40, 40);
            split4.Panel2.BackColor = Color.FromArgb(40, 40, 40);

            splitMain.Panel2.Controls.Add(split1);
            split1.Panel2.Controls.Add(split2);
            split2.Panel2.Controls.Add(split3);
            split3.Panel2.Controls.Add(split4);

            // 1. Pytanie z wagą
            Panel panQHeader = new Panel { Dock = DockStyle.Top, Height = 25 };
            lblQ = new Label { Text = "Treść polecenia dla Agenta:", Dock = DockStyle.Left, Width = 250, TextAlign = ContentAlignment.MiddleLeft };
            numWeight = new NumericUpDown { Dock = DockStyle.Right, Width = 50, Minimum = 1, Maximum = 100, Value = 1, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            numWeight.ValueChanged += NumWeight_ValueChanged;
            Label lblW = new Label { Text = "Waga:", Dock = DockStyle.Right, Width = 45, TextAlign = ContentAlignment.MiddleRight };
            panQHeader.Controls.Add(numWeight);
            panQHeader.Controls.Add(lblW);
            panQHeader.Controls.Add(lblQ);

            txtQuestion = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            split1.Panel1.Controls.Add(txtQuestion);
            split1.Panel1.Controls.Add(panQHeader);

            // 2. Wzorzec
            Panel panExpHeader = new Panel { Dock = DockStyle.Top, Height = 30 };
            Label lblExpected = new Label { Text = "Prawidłowe Makro (Wzorzec):", Dock = DockStyle.Left, Width = 200, TextAlign = ContentAlignment.MiddleLeft };
            btnRunExpected = new Button { Text = "▶ Uruchom Wzorzec", Dock = DockStyle.Right, Width = 140, BackColor = Color.MediumSeaGreen, ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font(this.Font, FontStyle.Bold) };
            btnRunExpected.Click += BtnRunExpected_Click;
            panExpHeader.Controls.Add(btnRunExpected);
            panExpHeader.Controls.Add(lblExpected);

            txtExpected = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = false, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGoldenrodYellow, Font = new Font("Consolas", 9) };
            txtExpected.TextChanged += (s, e) => { if (!isUpdatingUI && listTests.SelectedIndex >= 0) tests[listTests.SelectedIndex].ExpectedTag = txtExpected.Text; };
            split2.Panel1.Controls.Add(txtExpected);
            split2.Panel1.Controls.Add(panExpHeader);

            // 3. Przechwycone Tagi (Tylko czyste akcje)
            Panel panCapHeader = new Panel { Dock = DockStyle.Top, Height = 25 };
            Label lblCaptured = new Label { Text = "Przechwycone Tagi (Do oceny):", Dock = DockStyle.Left, Width = 250, TextAlign = ContentAlignment.MiddleLeft };
            panCapHeader.Controls.Add(lblCaptured);
            txtCapturedTags = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.LimeGreen, Font = new Font("Consolas", 10) };
            txtCapturedTags.TextChanged += (s, e) => { if (!isUpdatingUI && listTests.SelectedIndex >= 0) tests[listTests.SelectedIndex].GeneratedTag = txtCapturedTags.Text; };
            split3.Panel1.Controls.Add(txtCapturedTags);
            split3.Panel1.Controls.Add(panCapHeader);

            // 4. Historia / Odpowiedź Agenta (Pełny łańcuch myślowy)
            Label lblAgentResp = new Label { Text = "Historia Konwersacji (Logi):", Dock = DockStyle.Top, Height = 20 };
            txtAgentResponse = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightSkyBlue, Font = new Font("Consolas", 9) };

            Panel panAgentControls = new Panel { Dock = DockStyle.Top, Height = 75 };
            btnRunTest = new Button { Text = "🚀 WYŚLIJ POLECENIE DO AGENTA (Start Testu)", Dock = DockStyle.Top, Height = 40, BackColor = Color.LightSkyBlue, ForeColor = Color.Black, Cursor = Cursors.Hand, Font = new Font(this.Font, FontStyle.Bold) };
            btnRunTest.Click += BtnRunTest_Click;

            Panel panReply = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(0, 5, 0, 0) };
            btnReply = new Button { Text = "Wyślij odpowiedź", Dock = DockStyle.Right, Width = 120, BackColor = Color.Khaki, ForeColor = Color.Black, Cursor = Cursors.Hand };
            btnReply.Click += BtnReply_Click;
            txtUserReply = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            panReply.Controls.Add(txtUserReply);
            panReply.Controls.Add(btnReply);

            panAgentControls.Controls.Add(btnRunTest);
            panAgentControls.Controls.Add(panReply);

            split4.Panel1.Controls.Add(txtAgentResponse);
            split4.Panel1.Controls.Add(panAgentControls);
            split4.Panel1.Controls.Add(lblAgentResp);

            // 5. Komentarz i Ocena 
            txtComment = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            txtComment.TextChanged += (s, e) => { if (!isUpdatingUI && listTests.SelectedIndex >= 0) tests[listTests.SelectedIndex].Comment = txtComment.Text; };
            Label lblComm = new Label { Text = "Twój komentarz / Analiza błędu:", Dock = DockStyle.Top, Height = 20 };

            Panel panEval = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(0, 10, 0, 0) };
            chkPassed = new CheckBox { Text = "TEST ZALICZONY", Width = 150, Dock = DockStyle.Left, Font = new Font(this.Font, FontStyle.Bold), ForeColor = Color.LimeGreen };
            btnSaveEvaluation = new Button { Text = "Zatwierdź Ocenę", Dock = DockStyle.Left, Width = 150, BackColor = Color.Plum, ForeColor = Color.Black, Cursor = Cursors.Hand };
            btnSaveEvaluation.Click += BtnSaveEvaluation_Click;
            panEval.Controls.Add(btnSaveEvaluation);
            panEval.Controls.Add(chkPassed);

            split4.Panel2.Controls.Add(txtComment);
            split4.Panel2.Controls.Add(lblComm);
            split4.Panel2.Controls.Add(panEval);

            // --- PASEK STATYSTYK DOLNY ---
            Panel panStats = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(5, 0, 0, 0) };
            lblStats = new Label { Dock = DockStyle.Fill, ForeColor = Color.LightGray, Font = new Font("Consolas", 8), TextAlign = ContentAlignment.MiddleLeft, Text = "Gotowy." };
            panStats.Controls.Add(lblStats);

            this.Controls.Add(splitMain);
            this.Controls.Add(panTop);
            this.Controls.Add(panStats);

            bool proporcjeUstawione = false;
            this.SizeChanged += (s, e) =>
            {
                if (!proporcjeUstawione && this.Height > 300)
                {
                    try
                    {
                        splitMain.SplitterDistance = 280; // Szerokość listy
                        split1.SplitterDistance = Math.Max(50, (int)(splitMain.Height * 0.10));
                        split2.SplitterDistance = Math.Max(60, (int)(splitMain.Height * 0.15));
                        split3.SplitterDistance = Math.Max(80, (int)(splitMain.Height * 0.20));
                        split4.SplitterDistance = (int)(split4.Height * 0.70);
                        proporcjeUstawione = true;
                    }
                    catch { }
                }
            };
        }

        // ==========================================
        // AUTOZAPIS
        // ==========================================
        private string GetAutoSavePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BricsCAD_Agent");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "autosave_tester.json");
        }

        private void SaveAutoSave()
        {
            if (tests.Count > 0) SaveJson(GetAutoSavePath(), true);
        }

        // ==========================================
        // EDYCJA WAGI
        // ==========================================
        private void NumWeight_ValueChanged(object sender, EventArgs e)
        {
            if (isUpdatingUI || listTests.SelectedIndex < 0) return;

            int idx = listTests.SelectedIndex;
            tests[idx].Weight = (int)numWeight.Value;
            CalculateScore();

            TestCase t = tests[idx];
            string status = t.IsTested ? (t.Passed ? "[OK] " : "[FAIL] ") : "[?] ";
            listTests.Items[idx] = $"{status} {t.Id}. {t.Question.Substring(0, Math.Min(t.Question.Length, 20))}... ({t.Weight}p)";

            SaveAutoSave();
        }

        // ==========================================
        // URUCHAMIANIE WZORCA
        // ==========================================
        private async void BtnRunExpected_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtExpected.Text)) return;
            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            string tag = txtExpected.Text;
            lblStatus.Text = "Uruchamiam wzorzec...";

            try
            {
                string wynik = await Komendy.WykonajWCADAsync(() => {
                    if (tag.Contains("[SELECT:"))
                    {
                        int cnt = Komendy.WykonajInteligentneZaznaczenie(doc, tag);
                        return $"Zaznaczono {cnt} obiektów.";
                    }
                    else
                    {
                        return TrainingStudio.WykonywaczTagow(doc, tag);
                    }
                });

                txtAgentResponse.Text = $"[WYNIK WZORCA]:\r\n{wynik}";
                lblStatus.Text = "Wzorzec wykonany pomyślnie.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Błąd wykonywania wzorca!";
                txtAgentResponse.Text = $"[BŁĄD WZORCA]: {ex.Message}";
            }
        }

        // ==========================================
        // IMPORT JSONL
        // ==========================================
        private void BtnImportJsonl_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "JSONL Files (*.jsonl)|*.jsonl", Title = "Importuj zbiór JSONL" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string[] lines = File.ReadAllLines(ofd.FileName, Encoding.UTF8);
                        int dodane = 0;
                        string currentQuestion = "";

                        string pattern = @"\""role\""\s*:\s*\""(user|assistant)\"".*?\""content\""\s*:\s*\""((?:[^""\\]|\\.)*)\""";

                        foreach (string line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            MatchCollection matches = Regex.Matches(line, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                            foreach (Match m in matches)
                            {
                                string role = m.Groups[1].Value.ToLower();
                                string content = UnescapeJson(m.Groups[2].Value);

                                if (role == "user")
                                {
                                    currentQuestion = content;
                                }
                                else if (role == "assistant" && !string.IsNullOrEmpty(currentQuestion))
                                {
                                    tests.Add(new TestCase
                                    {
                                        Id = tests.Count + 1,
                                        Weight = 1,
                                        Question = currentQuestion,
                                        ExpectedTag = content,
                                        GeneratedTag = "",
                                        Comment = "",
                                        Passed = false,
                                        IsTested = false,
                                        ResponseTimeSec = 0
                                    });
                                    dodane++;
                                    currentQuestion = "";
                                }
                            }
                        }

                        RefreshList();
                        SaveAutoSave();
                        lblStatus.Text = $"Zaimportowano {dodane} testów z pliku JSONL.";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Błąd importu JSONL: " + ex.Message);
                    }
                }
            }
        }

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

                    Komendy.wybranyModel = cmbModels.SelectedItem.ToString();
                    lblStatus.Text = "Pobrano listę modeli z LM Studio.";
                }
            }
            catch { lblStatus.Text = "Brak połączenia z LM Studio!"; }
        }

        private void RefreshList()
        {
            isUpdatingUI = true;
            int selected = listTests.SelectedIndex;
            listTests.Items.Clear();
            foreach (var t in tests)
            {
                string status = t.IsTested ? (t.Passed ? "[OK] " : "[FAIL] ") : "[?] ";
                listTests.Items.Add($"{status} {t.Id}. {t.Question.Substring(0, Math.Min(t.Question.Length, 20))}... ({t.Weight}p)");
            }
            if (selected >= 0 && selected < listTests.Items.Count) listTests.SelectedIndex = selected;
            isUpdatingUI = false;

            CalculateScore();
        }

        private void CalculateScore()
        {
            int maxScore = 0;
            int earnedScore = 0;
            int testedCount = 0;
            double totalTime = 0;

            foreach (var t in tests)
            {
                maxScore += t.Weight;
                if (t.IsTested)
                {
                    testedCount++;
                    if (t.Passed) earnedScore += t.Weight;
                    totalTime += t.ResponseTimeSec;
                }
            }
            lblScore.Text = $"Postęp: {testedCount}/{tests.Count} | Wynik: {earnedScore}/{maxScore} pkt";
            lblTotalTime.Text = $"Całkowity czas: {totalTime.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} s";
        }

        private void ListTests_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listTests.SelectedIndex < 0) return;

            isUpdatingUI = true;
            TestCase t = tests[listTests.SelectedIndex];

            numWeight.Value = t.Weight;
            txtQuestion.Text = t.Question;
            txtExpected.Text = t.ExpectedTag;
            txtCapturedTags.Text = t.GeneratedTag;
            txtAgentResponse.Text = ""; // Czyścimy historię przy przełączaniu zadań
            txtComment.Text = t.Comment;
            chkPassed.Checked = t.Passed;
            chkPassed.ForeColor = t.Passed ? Color.LimeGreen : Color.LightCoral;

            isUpdatingUI = false;
        }

        // ==========================================
        // SILNIK TESTOWANIA I INTELIGENTNE WYCIĄGANIE TAGÓW
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
                string odpowiedz = await Komendy.ZapytajAgentaAsync(t.Question, doc, null);

                t.ResponseTimeSec = lastResponseTime;
                ZaktualizujWidokPoOdpowiedzi(t, odpowiedz);

                lblStatus.Text = "Agent odpowiedział.";
            }
            catch (Exception ex)
            {
                txtAgentResponse.Text = $"BŁĄD: {ex.Message}";
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
            TestCase t = tests[listTests.SelectedIndex];

            string reply = txtUserReply.Text;
            txtUserReply.Clear();

            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            btnReply.Text = "Wysyłanie...";
            btnReply.Enabled = false;

            try
            {
                string odpowiedz = await Komendy.ZapytajAgentaAsync(reply, doc, Komendy.AktywneZaznaczenie);
                ZaktualizujWidokPoOdpowiedzi(t, odpowiedz);
                lblStatus.Text = "Odpowiedź odebrana.";
            }
            catch (Exception ex) { txtAgentResponse.Text += $"BŁĄD: {ex.Message}"; }
            finally
            {
                btnReply.Text = "Wyślij odpowiedź";
                btnReply.Enabled = true;
            }
        }

        // Metoda odpowiedzialna za budowanie logicznej historii i separację czystych tagów operacyjnych
        private void ZaktualizujWidokPoOdpowiedzi(TestCase t, string ostatniaOdpowiedz)
        {
            StringBuilder pelnaHistoria = new StringBuilder();
            string allTags = "";

            // Zaawansowany Regex wyciągający TYLKO [ACTION...] i [SELECT...] z uwzględnieniem zagnieżdżeń JSON!
            // Automatycznie ignoruje [MSG: ...]
            string tagPattern = @"\[(ACTION|SELECT)(?>[^\[\]]+|\[(?<Depth>)|\](?<-Depth>))*(?(Depth)(?!))\]";

            bool zacznijZapis = false;

            // Pobieramy początek pytania testowego, aby odnaleźć moment startu właściwej konwersacji 
            // (omijamy tym samym wstrzyknięte przykłady szkoleniowe LLM)
            string cleanQuestion = t.Question.Trim();
            if (cleanQuestion.Length > 20) cleanQuestion = cleanQuestion.Substring(0, 20);

            foreach (string wpis in Komendy.historiaRozmowy)
            {
                Match mRole = Regex.Match(wpis, @"\""role\""\s*:\s*\""([^\""]+)\""");
                Match mContent = Regex.Match(wpis, @"\""content\""\s*:\s*\""((?:[^""\\]|\\.)*)\""");

                if (mRole.Success && mContent.Success)
                {
                    string role = mRole.Groups[1].Value.ToLower();
                    string content = UnescapeJson(mContent.Groups[1].Value);

                    // Detekcja momentu, w którym kończą się przykłady systemowe, a zaczyna właściwe pytanie testowe
                    if (!zacznijZapis && role == "user")
                    {
                        if (content.Replace("\r\n", "\n").Contains(cleanQuestion.Replace("\r\n", "\n")))
                        {
                            zacznijZapis = true;
                        }
                    }

                    if (zacznijZapis)
                    {
                        if (role == "system")
                        {
                            // Ignorujemy "czyste" tagi systemowe, żeby nie śmiecić w UI testera
                        }
                        else if (role == "user")
                        {
                            if (content.StartsWith("Oto dane z narzędzia:") || content.StartsWith("[SYSTEM]"))
                                pelnaHistoria.AppendLine($"\r\n--- WYNIK NARZĘDZIA / SYSTEM ---\r\n{content}");
                            else
                                pelnaHistoria.AppendLine($"\r\n--- TY ---\r\n{content}");
                        }
                        else if (role == "assistant")
                        {
                            pelnaHistoria.AppendLine($"\r\n--- AGENT ---\r\n{content}");

                            // Wyodrębnienie TYLKO poleceń wykonawczych dla okna "Przechwycone Tagi"
                            MatchCollection matches = Regex.Matches(content, tagPattern, RegexOptions.Singleline);
                            foreach (Match m in matches)
                            {
                                allTags += m.Value + "\r\n";
                            }
                        }
                    }
                }
            }

            // Fallback (zabezpieczenie na wypadek braku tagów wykonawczych)
            if (string.IsNullOrWhiteSpace(allTags) && !string.IsNullOrWhiteSpace(ostatniaOdpowiedz))
            {
                MatchCollection matches = Regex.Matches(ostatniaOdpowiedz, tagPattern, RegexOptions.Singleline);
                foreach (Match m in matches)
                {
                    allTags += m.Value + "\r\n";
                }
            }

            allTags = allTags.Trim();
            t.GeneratedTag = allTags;
            txtCapturedTags.Text = allTags;
            txtAgentResponse.Text = pelnaHistoria.ToString().Trim();

            // Automatyczne przewijanie na dół logów
            txtAgentResponse.SelectionStart = txtAgentResponse.Text.Length;
            txtAgentResponse.ScrollToCaret();
        }

        private void BtnSaveEvaluation_Click(object sender, EventArgs e)
        {
            if (listTests.SelectedIndex < 0) return;
            TestCase t = tests[listTests.SelectedIndex];

            t.GeneratedTag = txtCapturedTags.Text.Replace("\r\n", " ").Replace("\n", " ");
            t.Comment = txtComment.Text.Replace("\r\n", " ").Replace("\n", " ");
            t.Passed = chkPassed.Checked;
            t.IsTested = true;

            lblStatus.Text = $"Zapisano ocenę dla testu {t.Id}.";
            RefreshList();
            SaveAutoSave(); // Wymuszenie autozapisu po każdej ocenie
        }

        // ==========================================
        // PARSER JSON 
        // ==========================================
        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Wczytaj plik z pytaniami" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    baseFileName = Path.GetFileNameWithoutExtension(ofd.FileName);
                    LoadJson(ofd.FileName);
                    SaveAutoSave();
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (tests.Count == 0) return;

            string modelName = cmbModels.SelectedItem != null ? cmbModels.SelectedItem.ToString() : Komendy.wybranyModel;
            string safeModelName = string.Join("_", modelName.Split(Path.GetInvalidFileNameChars()));
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string defaultName = $"{baseFileName}-{safeModelName}-{dateStr}.json";

            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Zapisz podsumowanie testów", FileName = defaultName })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SaveJson(sfd.FileName, false);
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
                    @"\""IsTested\""\s*:\s*(true|false)" +
                    @"(?:\s*,\s*\""ResponseTimeSec\""\s*:\s*([0-9.]+))?\s*" +
                    @"\}";

                MatchCollection matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match m in matches)
                {
                    double responseTime = 0;
                    if (m.Groups[9].Success && !string.IsNullOrEmpty(m.Groups[9].Value))
                    {
                        double.TryParse(m.Groups[9].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out responseTime);
                    }

                    tests.Add(new TestCase
                    {
                        Id = int.Parse(m.Groups[1].Value),
                        Weight = int.Parse(m.Groups[2].Value),
                        Question = UnescapeJson(m.Groups[3].Value),
                        ExpectedTag = UnescapeJson(m.Groups[4].Value),
                        GeneratedTag = UnescapeJson(m.Groups[5].Value),
                        Comment = UnescapeJson(m.Groups[6].Value),
                        Passed = m.Groups[7].Value.ToLower() == "true",
                        IsTested = m.Groups[8].Value.ToLower() == "true",
                        ResponseTimeSec = responseTime
                    });
                }
                RefreshList();
                lblStatus.Text = $"Wczytano {tests.Count} testów.";
            }
            catch (Exception ex) { MessageBox.Show("Błąd wczytywania: " + ex.Message); }
        }

        private void SaveJson(string path, bool isAutoSave = false)
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
                sb.AppendLine($"    \"IsTested\": {t.IsTested.ToString().ToLower()},");
                sb.AppendLine($"    \"ResponseTimeSec\": {t.ResponseTimeSec.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                sb.AppendLine($"  }}{comma}");
            }
            sb.AppendLine("]");

            if (isAutoSave)
            {
                try { File.WriteAllText(path, sb.ToString(), Encoding.UTF8); } catch { }
            }
            else
            {
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
        }

        private void UpdateStatsUI(int promptTokens, int completionTokens, double timeSec)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateStatsUI(promptTokens, completionTokens, timeSec)));
                return;
            }

            int totalTokens = promptTokens + completionTokens;
            double speed = timeSec > 0 ? (completionTokens / timeSec) : 0;

            lblStats.Text = $"⏱ Czas: {timeSec:F1}s | 🧠 Kontekst: {promptTokens} tk | ⚡ Prędkość: {speed:F1} t/s | 📝 Wysłano: {completionTokens} tk";

            lastResponseTime = timeSec;
        }
        private string EscapeJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
        private string UnescapeJson(string s) => s.Replace("\\\"", "\"").Replace("\\n", "\r\n").Replace("\\\\", "\\");
    }
}