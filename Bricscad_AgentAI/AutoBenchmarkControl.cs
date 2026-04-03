using Bricscad.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace BricsCAD_Agent
{
    public class AutoBenchmarkControl : UserControl
    {
        // 1. Zmienne dla silnika i anulowania
        private AutoBenchmarkEngine backendEngine;
        private CancellationTokenSource cts;
        private BenchmarkConfig currentConfig;
        
        // 2. Kontrolki UI
        private Label lblGlobalStatus;
        private Label lblTotalTime;
        private ProgressBar progressBar;
        private Button btnLoadJson, btnRunBenchmark, btnStopBenchmark, btnSendToChat;
        private ComboBox cmbModels;
        
        // Tabele i wizualizatory
        private DataGridView dgvTests;
        private Chart chartScores;
        
        // Panele szczegółów
        private TextBox txtPrompt, txtExpectedRules, txtAIResponse, txtErrors;
        
        public AutoBenchmarkControl()
        {
            backendEngine = new AutoBenchmarkEngine();
            backendEngine.OnTestFinished += BackendEngine_OnTestFinished;
            backendEngine.OnBenchmarkCompleted += BackendEngine_OnBenchmarkCompleted;
            backendEngine.OnLogMessage += BackendEngine_OnLogMessage;

            InitializeUI();
            LoadModelsAsync();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.ForeColor = Color.White;

            // ==========================================
            // --- Pasek Górny ---
            // ==========================================
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(5) };

            // Przyciski lewa strona
            FlowLayoutPanel flpControls = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 350, WrapContents = false };
            
            btnLoadJson = new Button { Text = "Wczytaj JSON", Width = 110, Height = 35, BackColor = Color.LightGray, ForeColor = Color.Black, Cursor = Cursors.Hand };
            btnLoadJson.Click += BtnLoadJson_Click;
            
            btnRunBenchmark = new Button { Text = "▶ URUCHOM Ocenę LLM", Width = 150, Height = 35, BackColor = Color.LightSkyBlue, ForeColor = Color.Black, Cursor = Cursors.Hand, Enabled = false };
            btnRunBenchmark.Click += BtnRunBenchmark_Click;

            btnStopBenchmark = new Button { Text = "⏹ ZATRZYMAJ", Width = 110, Height = 35, BackColor = Color.LightCoral, ForeColor = Color.Black, Cursor = Cursors.Hand, Enabled = false };
            btnStopBenchmark.Click += BtnStopBenchmark_Click;

            flpControls.Controls.Add(btnLoadJson);
            flpControls.Controls.Add(btnRunBenchmark);
            flpControls.Controls.Add(btnStopBenchmark);

            // Wybór modelu pod spodem "Wczytaj JSON"
            Panel panModels = new Panel { Dock = DockStyle.Top, Height = 30, Width = 350, Padding = new Padding(0, 5, 0, 0) };
            Label lblModel = new Label { Text = "Model AI:", Width = 60, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleRight };
            cmbModels = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbModels.SelectedIndexChanged += (s, e) => { Komendy.wybranyModel = cmbModels.SelectedItem.ToString(); };
            panModels.Controls.Add(cmbModels);
            panModels.Controls.Add(lblModel);

            Panel panLeftCombined = new Panel { Dock = DockStyle.Left, Width = 390 };
            panLeftCombined.Controls.Add(panModels);
            panLeftCombined.Controls.Add(flpControls);
            flpControls.Dock = DockStyle.Top;

            // Pasek Postępu i Prawa strona
            Panel panProgress = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 5, 10, 5) };
            lblGlobalStatus = new Label { Text = "Oczekuję na wczytanie jsona...", Dock = DockStyle.Top, Height = 20, Font = new Font(this.Font, FontStyle.Bold) };
            progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 15, Minimum = 0, Maximum = 100, Value = 0 };
            lblTotalTime = new Label { Text = "Skuteczność: --%", Dock = DockStyle.Top, Height = 20, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.LimeGreen, Font = new Font(this.Font, FontStyle.Bold) };
            
            panProgress.Controls.Add(progressBar);
            panProgress.Controls.Add(lblGlobalStatus);
            panProgress.Controls.Add(lblTotalTime);

            panTop.Controls.Add(panProgress);
            panTop.Controls.Add(panLeftCombined);

            // ==========================================
            // --- Główny Podział Ekrany (Lista testów vs Wyniki) ---
            // ==========================================
            SplitContainer splitMain = new SplitContainer { Orientation = Orientation.Horizontal, Dock = DockStyle.Fill, SplitterDistance = 300, SplitterWidth = 6, BackColor = Color.DimGray };

            // Górna sekcja z tabelą i wykresem
            SplitContainer splitUpper = new SplitContainer { Orientation = Orientation.Vertical, Dock = DockStyle.Fill, SplitterDistance = 450, SplitterWidth = 6, BackColor = Color.DimGray };
            
            // Tabela po lewej (Lista)
            dgvTests = new DataGridView 
            { 
                Dock = DockStyle.Fill, 
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.Black,
                GridColor = Color.Gray,
                MultiSelect = false
            };
            dgvTests.Columns.Add("Id", "ID");
            dgvTests.Columns["Id"].Width = 40;
            dgvTests.Columns.Add("Kategoria", "Kategoria");
            dgvTests.Columns.Add("Nazwa", "Nazwa Testu");
            dgvTests.Columns.Add("Status", "Status");
            dgvTests.Columns["Status"].Width = 80;
            dgvTests.SelectionChanged += DgvTests_SelectionChanged;

            splitUpper.Panel1.Controls.Add(dgvTests);

            // WYKRES po prawej
            chartScores = new Chart { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40,40,40) };
            ChartArea ca = new ChartArea("Kategorie") { BackColor = Color.FromArgb(30,30,30) };
            ca.AxisX.LabelStyle.ForeColor = Color.White;
            ca.AxisY.LabelStyle.ForeColor = Color.White;
            ca.AxisX.LineColor = Color.Gray;
            ca.AxisY.LineColor = Color.Gray;
            ca.AxisY.Maximum = 100;
            ca.AxisX.Interval = 1;
            ca.AxisX.IsLabelAutoFit = true;
            chartScores.ChartAreas.Add(ca);

            Series sCategory = new Series("Skuteczność %");
            sCategory.ChartType = SeriesChartType.Bar; // Poziome słupki żeby czytać nazwy kategorii
            sCategory.IsValueShownAsLabel = true;
            sCategory.LabelForeColor = Color.White;
            chartScores.Series.Add(sCategory);
            splitUpper.Panel2.Controls.Add(chartScores);

            splitMain.Panel1.Controls.Add(splitUpper);

            // ==========================================
            // --- Dolna sekcja szczegółów testu ---
            // ==========================================
            TableLayoutPanel tlpDetails = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            tlpDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // Prompt + Reguły
            tlpDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 60F)); // Response + Błędy

            txtPrompt = CreateDetailTextBox("Prompt Użytkownika");
            txtExpectedRules = CreateDetailTextBox("Oczekiwano (Reguły)");
            txtExpectedRules.ForeColor = Color.LightGoldenrodYellow;
            
            Panel panAIResponse = new Panel { Dock = DockStyle.Fill };
            txtAIResponse = CreateDetailTextBox("Zarejestrowana Odpowiedź Agenta");
            txtAIResponse.ForeColor = Color.LightSkyBlue;
            btnSendToChat = new Button { Text = "Wyślij ten Prompt do Czatu", Dock = DockStyle.Bottom, Height = 30, BackColor = Color.Khaki, ForeColor = Color.Black, Cursor = Cursors.Hand };
            btnSendToChat.Click += BtnSendToChat_Click;
            panAIResponse.Controls.Add(txtAIResponse);
            panAIResponse.Controls.Add(btnSendToChat);

            txtErrors = CreateDetailTextBox("Wykryte błędy w odpowiedzi (Puste = Sukces)");
            txtErrors.ForeColor = Color.LightCoral;

            tlpDetails.Controls.Add(txtPrompt, 0, 0);
            tlpDetails.Controls.Add(txtExpectedRules, 1, 0);
            tlpDetails.Controls.Add(panAIResponse, 0, 1);
            tlpDetails.Controls.Add(txtErrors, 1, 1);

            splitMain.Panel2.Controls.Add(tlpDetails);

            this.Controls.Add(splitMain);
            this.Controls.Add(panTop);
        }

        private TextBox CreateDetailTextBox(string title)
        {
            TextBox tb = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, Font = new Font("Consolas", 9f) };
            // Obramowanie tekstowe (Hack na "Title" via Label w parent panel, lub po prostu nie uzywamy Labela tylko wrzucamy jako text)
            tb.Text = $"--- {title} ---\r\n\r\n";
            return tb;
        }

        private void SetDetailText(TextBox tb, string title, string content)
        {
            tb.Text = $"--- {title} ---\r\n\r\n{content}";
        }

        // =======================================================
        // METODY POŁĄCZENIOWE
        // =======================================================
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
                    if (cmbModels.Items.Contains(Komendy.wybranyModel)) cmbModels.SelectedItem = Komendy.wybranyModel;
                    else cmbModels.SelectedIndex = 0;
                }
            }
            catch { lblGlobalStatus.Text = "Brak połączenia z LM Studio!"; }
        }

        private void BtnLoadJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "JSON Benchmark (*.json)|*.json", Title = "Wczytaj Plik Benchmarku" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string content = File.ReadAllText(ofd.FileName);
                        currentConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<BenchmarkConfig>(content);
                        // Save path for the engine
                        this.Tag = ofd.FileName;
                        
                        dgvTests.Rows.Clear();
                        foreach(var t in currentConfig.Tests)
                        {
                            dgvTests.Rows.Add(t.Id, t.Category, t.TestName, "Oczekuje");
                        }
                        
                        btnRunBenchmark.Enabled = true;
                        lblGlobalStatus.Text = $"Wczytano gotowy do testów zbiór danych. Ilość zadań: {currentConfig.Tests.Count}";
                        UpdateChart(currentConfig.RunMetadata?.CategoriesScores);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Błąd parsowania: " + ex.Message);
                    }
                }
            }
        }

        private async void BtnRunBenchmark_Click(object sender, EventArgs e)
        {
            if (currentConfig == null || this.Tag == null) return;
            string sciezka = this.Tag.ToString();

            btnRunBenchmark.Enabled = false;
            btnStopBenchmark.Enabled = true;
            cts = new CancellationTokenSource();
            progressBar.Maximum = currentConfig.Tests.Count;
            progressBar.Value = 0;

            // Zerowanie wizualne w tabeli
            foreach(DataGridViewRow row in dgvTests.Rows) { row.Cells[3].Value = "Uruchamiam..."; row.DefaultCellStyle.BackColor = Color.FromArgb(50, 50, 50); }

            try
            {
                await backendEngine.UruchomBenchmarkAsync(sciezka, cts.Token);
            }
            catch(Exception ex)
            {
                lblGlobalStatus.Text = "BŁĄD ZAWIESZENIA: " + ex.Message;
            }
            finally
            {
                btnRunBenchmark.Enabled = true;
                btnStopBenchmark.Enabled = false;
            }
        }

        private void BtnStopBenchmark_Click(object sender, EventArgs e)
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                btnStopBenchmark.Text = "Przerywanie...";
                cts.Cancel();
            }
        }
        
        // Pchanie testu do rzeczywistego czata aplikacji
        private void BtnSendToChat_Click(object sender, EventArgs e)
        {
            if (dgvTests.SelectedRows.Count == 0 || currentConfig == null) return;
            int idx = dgvTests.SelectedRows[0].Index;
            var testObj = currentConfig.Tests[idx];

            if (Bricscad_AgentAI.AgentControl.Instance != null)
            {
                Bricscad_AgentAI.AgentControl.Instance.WyslijZapytanieZKonsoli(testObj.UserPrompt);
                lblGlobalStatus.Text = "Wysłano prompt to zakładki Czat!";
            }
            else
            {
                MessageBox.Show("Moduł głównego czatu nie został jeszcze zainicjalizowany w BricsCAD.");
            }
        }

        // =======================================================
        // OBSŁUGA ZDARZEŃ Z SILNIKA BENCHMARKU (Update UI)
        // =======================================================

        private void BackendEngine_OnLogMessage(object sender, string e)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => BackendEngine_OnLogMessage(sender, e))); return; }
            lblGlobalStatus.Text = e;
        }

        private void BackendEngine_OnTestFinished(object sender, BenchmarkProgressEventArgs e)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => BackendEngine_OnTestFinished(sender, e))); return; }
            
            progressBar.Value = e.CurrentTestIndex;
            int rowIdx = e.CurrentTestIndex - 1;

            if (rowIdx >= 0 && rowIdx < dgvTests.Rows.Count)
            {
                var row = dgvTests.Rows[rowIdx];
                row.Cells[3].Value = e.TestResult.Passed ? "ZALICZONY" : "OBLANY";
                row.DefaultCellStyle.BackColor = e.TestResult.Passed ? Color.ForestGreen : Color.Maroon;
                row.DefaultCellStyle.ForeColor = Color.White;
            }

            // Odswież zaznaczone tło jeśli aktualnie użytkownik na to patrzy
            if (dgvTests.SelectedRows.Count > 0 && dgvTests.SelectedRows[0].Index == rowIdx)
            {
                DgvTests_SelectionChanged(null, null);
            }
        }

        private void BackendEngine_OnBenchmarkCompleted(object sender, BenchmarkCompletedEventArgs e)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => BackendEngine_OnBenchmarkCompleted(sender, e))); return; }
            
            lblTotalTime.Text = $"Skuteczność: {e.FinalConfig.RunMetadata.GlobalScore}%";
            
            btnStopBenchmark.Text = "⏹ ZATRZYMAJ";
            currentConfig = e.FinalConfig; // odświeżamy referencję po wynikach

            if (e.WasCancelled) MessageBox.Show("Benchmark został przerwany w trakcie jego trwania.", "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else MessageBox.Show($"Zakończono Benchmark!\nWynik końcowy: {e.FinalConfig.RunMetadata.GlobalScore}%", "Gotowe", MessageBoxButtons.OK, MessageBoxIcon.Information);

            UpdateChart(e.FinalConfig.RunMetadata.CategoriesScores);
        }

        // =======================================================
        // POZOSTAŁE METODY UI
        // =======================================================
        private void UpdateChart(Dictionary<string, double> categoryScores)
        {
            if (categoryScores == null) return;
            chartScores.Series[0].Points.Clear();
            foreach (var kvp in categoryScores)
            {
                int p = chartScores.Series[0].Points.AddXY(kvp.Key, kvp.Value);
                if (kvp.Value == 100) chartScores.Series[0].Points[p].Color = Color.LimeGreen;
                else if (kvp.Value > 50) chartScores.Series[0].Points[p].Color = Color.Orange;
                else chartScores.Series[0].Points[p].Color = Color.Red;
            }
        }

        private void DgvTests_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvTests.SelectedRows.Count == 0 || currentConfig == null) return;
            
            int idx = dgvTests.SelectedRows[0].Index;
            var testObj = currentConfig.Tests[idx];

            SetDetailText(txtPrompt, "Prompt Użytkownika", testObj.UserPrompt);
            SetDetailText(txtAIResponse, "Zarejestrowana Odpowiedź Agenta", string.IsNullOrEmpty(testObj.GeneratedTag) ? "[Brak wykonanego cyklu]" : testObj.GeneratedTag);
            
            string rulesFormat = string.Join("\r\n\r\n", testObj.ValidationRules.Select(r => $"{r.RuleType}: {r.Value} \r\n(Oczekiwano: {r.ExpectedOutput ?? "N/A"})"));
            SetDetailText(txtExpectedRules, "Oczekiwano (Reguły)", rulesFormat);

            string errs = string.Join("\r\n", testObj.FailedRulesErrors);
            SetDetailText(txtErrors, "Wykryte błędy w odpowiedzi", string.IsNullOrEmpty(errs) ? (testObj.Passed ? "[Brak błędów - SUKCES]" : "[Nie odpalono]") : errs);
        }
    }
}
