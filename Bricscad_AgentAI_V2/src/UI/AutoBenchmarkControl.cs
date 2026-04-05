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

        // Kontrolki UI
        private Button btnLoadJson, btnStart, btnStop, btnSendToChat;
        private DataGridView dgvTests;
        private RichTextBox txtLogs, txtDetails, txtTaskDesc, txtErrorLog;
        private ProgressBar progressBar;
        private Label lblGlobalStatus;
        private TabControl tabLogs;
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
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f);

            // Pasek górny (Przyciski)
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(5) };
            
            btnLoadJson = CreateStyledButton("📂 Wczytaj JSON", Color.FromArgb(60, 60, 60));
            btnLoadJson.Click += BtnLoadJson_Click;
            
            btnStart = CreateStyledButton("▶ Start", Color.FromArgb(0, 122, 204));
            btnStart.Enabled = false;
            btnStart.Click += BtnStart_Click;

            btnStop = CreateStyledButton("⏹ Stop", Color.Crimson);
            btnStop.Enabled = false;
            btnStop.Click += BtnStop_Click;

            panTop.Controls.Add(btnStop);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panTop.Controls.Add(btnStart);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panTop.Controls.Add(btnLoadJson);

            // Stopka (Progres)
            Panel panFooter = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(5) };
            lblGlobalStatus = new Label { Text = "Oczekiwanie na plik JSON...", Dock = DockStyle.Top, Height = 20, Font = new Font(this.Font, FontStyle.Bold) };
            progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 10 };
            panFooter.Controls.Add(lblGlobalStatus);
            panFooter.Controls.Add(progressBar);

            // Tabela testów
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

            pageResults.Controls.Add(dgvTests);
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
            this.Controls.Add(panTop);
        }

        private void dgvTestsColumnsInit()
        {
            dgvTests.Columns.Clear();
            dgvTests.Columns.Add("Id", "ID");
            dgvTests.Columns["Id"].Width = 25;
            dgvTests.Columns.Add("Category", "Kategoria");
            dgvTests.Columns.Add("Name", "Nazwa Testu");
            dgvTests.Columns.Add("Status", "Status");
            dgvTests.Columns["Status"].Width = 80;
            dgvTests.Columns.Add("Time", "Czas (s)");
            dgvTests.Columns["Time"].Width = 70;
            
            dgvTests.ColumnHeadersHeight = 45;
            dgvTests.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
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
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string content = File.ReadAllText(ofd.FileName);
                        _currentConfig = JsonConvert.DeserializeObject<BenchmarkConfig>(content);
                        this.Tag = ofd.FileName;
                        SaveLastPath(ofd.FileName);
                        RefreshTestsList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd wczytywania: {ex.Message}");
                    }
                }
            }
        }

        private void RefreshTestsList()
        {
            if (_currentConfig == null) return;

            dgvTests.Rows.Clear();
            txtTaskDesc.Clear();
            txtErrorLog.Clear();

            txtTaskDesc.SelectionFont = new Font(txtTaskDesc.Font, FontStyle.Bold);
            txtTaskDesc.AppendText("LISTA ZADAŃ W ZESTAWIE:\n\n");

            foreach (var test in _currentConfig.Tests)
            {
                dgvTests.Rows.Add(test.Id, test.Category, test.TestName, "Oczekuje", "");
                
                txtTaskDesc.SelectionColor = Color.White;
                txtTaskDesc.AppendText($"[ID: {test.Id}] {test.TestName}\n");
                txtTaskDesc.SelectionColor = Color.LightGray;
                txtTaskDesc.AppendText($"PROMPT: {test.UserPrompt}\n");
                txtTaskDesc.AppendText(new string('-', 40) + "\n");
            }

            btnStart.Enabled = true;
            lblGlobalStatus.Text = $"Wczytano {_currentConfig.Tests.Count} testów.";
            txtLogs.AppendText($"\n[{DateTime.Now:HH:mm:ss}] Zainicjowano zestaw: {this.Tag}");
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
                            string content = File.ReadAllText(path);
                            _currentConfig = JsonConvert.DeserializeObject<BenchmarkConfig>(content);
                            this.Tag = path;
                            RefreshTestsList();
                        }
                    }
                }
            } catch { }
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (_currentConfig == null || this.Tag == null) return;

            btnStart.Enabled = false;
            btnLoadJson.Enabled = false;
            btnStop.Enabled = true;
            
            _cts = new CancellationTokenSource();
            progressBar.Maximum = _currentConfig.Tests.Count;
            progressBar.Value = 0;

            string filePath = this.Tag.ToString();
            txtLogs.Clear();
            txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] URUCHAMIAM BENCHMARK V2...\n");

            try
            {
                // Uruchomienie w wątku tła, aby UI pozostało responsywne
                _ = await Task.Run(async () =>
                {
                    return await _engine.RunBenchmarkAsync(filePath, _cts.Token);
                });
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
            btnStop.Enabled = false;
            btnStop.Text = "⏹ Stop";
            
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
    }
}
