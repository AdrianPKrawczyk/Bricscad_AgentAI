using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.UI
{
    public class BenchmarkAnalyticsControl : UserControl
    {
        private const string RegPath = @"Software\BricscadAgentAI";
        private const string LastBenchmarkKey = "LastBenchmarkPath";

        private TextBox txtRootPath;
        private ComboBox cbBenchmarkFilter;
        private ComboBox cbProfileFilter;
        private Button btnBrowseRoot;
        private Button btnRefresh;
        private Button btnClearModelFilter;
        private Button btnFitColumns;
        private Label lblStatus;

        private DataGridView dgvModels;
        private DataGridView dgvRuns;
        private DataGridView dgvFailures;
        private RichTextBox txtSummary;

        private List<BenchmarkRunRecord> _allRuns = new List<BenchmarkRunRecord>();
        private string _selectedModel;
        private bool _isRefreshing;

        public BenchmarkAnalyticsControl()
        {
            InitializeUi();
            txtRootPath.Text = ResolveInitialReportsRoot();
            LoadReports();
        }

        private void InitializeUi()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 104, Padding = new Padding(8) };

            var pathLabel = new Label
            {
                Text = "Folder raportow:",
                Dock = DockStyle.Left,
                Width = 110,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White
            };

            txtRootPath = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnBrowseRoot = CreateButton("Wybierz", 90, Color.FromArgb(70, 70, 70));
            btnBrowseRoot.Click += (s, e) => BrowseRootFolder();

            btnRefresh = CreateButton("Odswiez", 90, Color.FromArgb(0, 122, 204));
            btnRefresh.Click += (s, e) => LoadReports();

            btnFitColumns = CreateButton("Dopasuj kolumny", 150, Color.FromArgb(70, 70, 70));
            btnFitColumns.Click += (s, e) => FitAllGridColumns();

            var pathRow = new Panel { Dock = DockStyle.Top, Height = 30 };
            pathRow.Controls.Add(btnRefresh);
            pathRow.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 6 });
            pathRow.Controls.Add(btnFitColumns);
            pathRow.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 6 });
            pathRow.Controls.Add(btnBrowseRoot);
            pathRow.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 6 });
            pathRow.Controls.Add(txtRootPath);
            pathRow.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8 });
            pathRow.Controls.Add(pathLabel);

            var filterRow = new Panel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(0, 10, 0, 0) };

            cbBenchmarkFilter = CreateFilterCombo(220);
            cbBenchmarkFilter.SelectedIndexChanged += (s, e) => RefreshViews();

            cbProfileFilter = CreateFilterCombo(170);
            cbProfileFilter.SelectedIndexChanged += (s, e) => RefreshViews();

            btnClearModelFilter = CreateButton("Wyczysc filtr modelu", 170, Color.FromArgb(70, 70, 70));
            btnClearModelFilter.Click += (s, e) =>
            {
                _selectedModel = null;
                dgvModels.ClearSelection();
                RefreshViews();
            };

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.LightGray,
                Padding = new Padding(8, 0, 0, 0)
            };

            var filtersLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 720,
                AutoSize = false,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            filtersLeft.Controls.Add(MakeFlowLabel("Benchmark:", 82));
            filtersLeft.Controls.Add(cbBenchmarkFilter);
            filtersLeft.Controls.Add(MakeSpacer(8));
            filtersLeft.Controls.Add(MakeFlowLabel("Profil:", 50));
            filtersLeft.Controls.Add(cbProfileFilter);
            filtersLeft.Controls.Add(MakeSpacer(8));
            filtersLeft.Controls.Add(btnClearModelFilter);

            filterRow.Controls.Add(lblStatus);
            filterRow.Controls.Add(filtersLeft);

            topPanel.Controls.Add(filterRow);
            topPanel.Controls.Add(pathRow);

            dgvModels = CreateGrid();
            dgvModels.SelectionChanged += DgvModels_SelectionChanged;
            ConfigureModelsGrid();

            dgvRuns = CreateGrid();
            ConfigureRunsGrid();
            dgvRuns.SelectionChanged += (s, e) => RefreshSummary();

            dgvFailures = CreateGrid();
            ConfigureFailuresGrid();

            txtSummary = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                WordWrap = true
            };

            var rightTabs = new TabControl
            {
                Dock = DockStyle.Fill
            };
            rightTabs.SelectedIndexChanged += (s, e) => ScheduleFitAllGridColumns();

            var tabModels = new TabPage("Modele");
            tabModels.BackColor = Color.FromArgb(30, 30, 30);
            tabModels.Controls.Add(WrapWithCaption("Zestawienie modeli", dgvModels));

            var tabRuns = new TabPage("Przebiegi");
            tabRuns.BackColor = Color.FromArgb(30, 30, 30);
            tabRuns.Controls.Add(dgvRuns);

            var tabFailures = new TabPage("Porazki");
            tabFailures.BackColor = Color.FromArgb(30, 30, 30);
            tabFailures.Controls.Add(dgvFailures);

            var tabSummary = new TabPage("Podsumowanie");
            tabSummary.BackColor = Color.FromArgb(30, 30, 30);
            tabSummary.Controls.Add(txtSummary);

            rightTabs.TabPages.Add(tabModels);
            rightTabs.TabPages.Add(tabRuns);
            rightTabs.TabPages.Add(tabFailures);
            rightTabs.TabPages.Add(tabSummary);

            Controls.Add(rightTabs);
            Controls.Add(topPanel);

            Resize += (s, e) => ScheduleFitAllGridColumns();
            VisibleChanged += (s, e) =>
            {
                if (Visible)
                {
                    ScheduleFitAllGridColumns();
                }
            };
            Load += (s, e) => ScheduleFitAllGridColumns();
        }

        private Button CreateButton(string text, int width, Color backColor)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Right,
                Width = width,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
        }

        private ComboBox CreateFilterCombo(int width)
        {
            return new ComboBox
            {
                Dock = DockStyle.None,
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
        }

        private Label MakeInlineLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Right,
                Width = width,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White
            };
        }

        private DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(60, 60, 60),
                MultiSelect = false,
                ScrollBars = ScrollBars.Both,
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };

            grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35);
            grid.DefaultCellStyle.ForeColor = Color.WhiteSmoke;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(42, 42, 42);
            grid.AlternatingRowsDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55, 55, 55);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 55, 55);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;

            return grid;
        }

        private Control MakeSpacer(int width)
        {
            return new Panel { Width = width, Height = 1, Margin = new Padding(0) };
        }

        private Control MakeFlowLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                Width = width,
                Height = 28,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White,
                Margin = new Padding(0, 3, 0, 0)
            };
        }

        private Control WrapWithCaption(string title, Control content)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
            var label = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            };
            panel.Controls.Add(content);
            panel.Controls.Add(label);
            return panel;
        }

        private void ConfigureModelsGrid()
        {
            dgvModels.Columns.Clear();
            dgvModels.Columns.Add("Model", "Model");
            dgvModels.Columns.Add("Runs", "Przebiegi");
            dgvModels.Columns.Add("AvgScore", "Sredni %");
            dgvModels.Columns.Add("BestScore", "Best %");
            dgvModels.Columns.Add("AvgMs", "Sredni ms");
            dgvModels.Columns.Add("Calls", "Sr. calls");
            dgvModels.Columns.Add("LastRun", "Ostatni");
            ApplyProportionalWidths(dgvModels, new Dictionary<string, int>
            {
                ["Model"] = 210,
                ["Runs"] = 80,
                ["AvgScore"] = 80,
                ["BestScore"] = 80,
                ["AvgMs"] = 90,
                ["Calls"] = 80,
                ["LastRun"] = 140
            });
        }

        private void ConfigureRunsGrid()
        {
            dgvRuns.Columns.Clear();
            dgvRuns.Columns.Add("RunDate", "Data");
            dgvRuns.Columns.Add("Model", "Model");
            dgvRuns.Columns.Add("Benchmark", "Benchmark");
            dgvRuns.Columns.Add("Profile", "Profil");
            dgvRuns.Columns.Add("Score", "Wynik %");
            dgvRuns.Columns.Add("AvgMs", "Sredni ms");
            dgvRuns.Columns.Add("Failed", "Bledy");
            dgvRuns.Columns.Add("Calls", "Calls");
            dgvRuns.Columns.Add("Config", "Konfiguracja");
            ApplyProportionalWidths(dgvRuns, new Dictionary<string, int>
            {
                ["RunDate"] = 125,
                ["Model"] = 180,
                ["Benchmark"] = 220,
                ["Profile"] = 120,
                ["Score"] = 75,
                ["AvgMs"] = 85,
                ["Failed"] = 60,
                ["Calls"] = 60,
                ["Config"] = 260
            });
        }

        private void ConfigureFailuresGrid()
        {
            dgvFailures.Columns.Clear();
            dgvFailures.Columns.Add("Id", "ID");
            dgvFailures.Columns.Add("Name", "Test");
            dgvFailures.Columns.Add("Fails", "Porazki");
            dgvFailures.Columns.Add("FailRate", "Fail %");
            dgvFailures.Columns.Add("Models", "Modele");
            ApplyProportionalWidths(dgvFailures, new Dictionary<string, int>
            {
                ["Id"] = 45,
                ["Name"] = 320,
                ["Fails"] = 70,
                ["FailRate"] = 70,
                ["Models"] = 360
            });
        }

        private void FitAllGridColumns()
        {
            ApplyProportionalWidths(dgvModels, new Dictionary<string, int>
            {
                ["Model"] = 210,
                ["Runs"] = 80,
                ["AvgScore"] = 80,
                ["BestScore"] = 80,
                ["AvgMs"] = 90,
                ["Calls"] = 80,
                ["LastRun"] = 140
            });

            ApplyProportionalWidths(dgvRuns, new Dictionary<string, int>
            {
                ["RunDate"] = 125,
                ["Model"] = 180,
                ["Benchmark"] = 220,
                ["Profile"] = 120,
                ["Score"] = 75,
                ["AvgMs"] = 85,
                ["Failed"] = 60,
                ["Calls"] = 60,
                ["Config"] = 260
            });

            ApplyProportionalWidths(dgvFailures, new Dictionary<string, int>
            {
                ["Id"] = 45,
                ["Name"] = 320,
                ["Fails"] = 70,
                ["FailRate"] = 70,
                ["Models"] = 360
            });
        }

        private void ScheduleFitAllGridColumns()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            BeginInvoke(new Action(FitAllGridColumns));
        }

        private void ApplyProportionalWidths(DataGridView grid, Dictionary<string, int> baseWidths)
        {
            if (grid == null || grid.Columns.Count == 0 || baseWidths == null || baseWidths.Count == 0)
            {
                return;
            }

            int totalBaseWidth = baseWidths.Values.Sum();
            if (totalBaseWidth <= 0)
            {
                return;
            }

            int availableWidth = grid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
            if (availableWidth <= 0)
            {
                return;
            }

            double scale = (double)availableWidth / totalBaseWidth;
            int assigned = 0;
            int lastVisibleColumnIndex = -1;

            for (int i = 0; i < grid.Columns.Count; i++)
            {
                if (grid.Columns[i].Visible)
                {
                    lastVisibleColumnIndex = i;
                }
            }

            for (int i = 0; i < grid.Columns.Count; i++)
            {
                var column = grid.Columns[i];
                if (!column.Visible || !baseWidths.ContainsKey(column.Name))
                {
                    continue;
                }

                int width;
                if (i == lastVisibleColumnIndex)
                {
                    width = Math.Max(40, availableWidth - assigned);
                }
                else
                {
                    width = Math.Max(40, (int)Math.Round(baseWidths[column.Name] * scale));
                    assigned += width;
                }

                column.Width = width;
            }
        }

        private void BrowseRootFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Wybierz folder z raportami benchmarkow";
                dialog.SelectedPath = Directory.Exists(txtRootPath.Text) ? txtRootPath.Text : string.Empty;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtRootPath.Text = dialog.SelectedPath;
                    LoadReports();
                }
            }
        }

        private string ResolveInitialReportsRoot()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    var path = key?.GetValue(LastBenchmarkKey) as string;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        string rootFromRegistry = DetermineReportsRoot(path);
                        if (Directory.Exists(rootFromRegistry))
                        {
                            return rootFromRegistry;
                        }
                    }
                }
            }
            catch
            {
            }

            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            foreach (var dir in EnumerateSelfAndParents(assemblyDir, 6))
            {
                string candidate = Path.Combine(dir, "Bricscad_AgentAI_V2", "tests");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(assemblyDir, "tests");
        }

        private IEnumerable<string> EnumerateSelfAndParents(string start, int maxDepth)
        {
            string current = start;
            for (int i = 0; i < maxDepth && !string.IsNullOrWhiteSpace(current); i++)
            {
                yield return current;
                current = Directory.GetParent(current)?.FullName;
            }
        }

        private string DetermineReportsRoot(string path)
        {
            if (Directory.Exists(path))
            {
                return path;
            }

            if (!File.Exists(path))
            {
                return path;
            }

            string dir = Path.GetDirectoryName(path) ?? path;
            string fileName = Path.GetFileName(path) ?? string.Empty;
            if (fileName.IndexOf("_FULL_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("_ERRORS_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Directory.GetParent(dir)?.FullName ?? dir;
            }

            return dir;
        }

        private void LoadReports()
        {
            _allRuns.Clear();
            _selectedModel = null;

            string root = txtRootPath.Text;
            if (!Directory.Exists(root))
            {
                lblStatus.Text = "Nie znaleziono folderu raportow.";
                cbBenchmarkFilter.Items.Clear();
                cbProfileFilter.Items.Clear();
                dgvModels.Rows.Clear();
                dgvRuns.Rows.Clear();
                dgvFailures.Rows.Clear();
                txtSummary.Clear();
                return;
            }

            foreach (var file in Directory.GetFiles(root, "*_FULL_*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<BenchmarkConfig>(File.ReadAllText(file));
                    if (config?.RunMetadata == null || config.Tests == null)
                    {
                        continue;
                    }

                    _allRuns.Add(new BenchmarkRunRecord
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        FileTimestamp = File.GetLastWriteTime(file),
                        Config = config
                    });
                }
                catch
                {
                }
            }

            PopulateFilters();
            RefreshViews();
            ScheduleFitAllGridColumns();
        }

        private void PopulateFilters()
        {
            string selectedBenchmark = cbBenchmarkFilter.SelectedItem as string;
            string selectedProfile = cbProfileFilter.SelectedItem as string;

            cbBenchmarkFilter.Items.Clear();
            cbProfileFilter.Items.Clear();

            cbBenchmarkFilter.Items.Add("(Wszystkie benchmarki)");
            foreach (var benchmark in _allRuns
                .Select(r => SafeBenchmarkName(r.Config))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .OrderBy(v => v))
            {
                cbBenchmarkFilter.Items.Add(benchmark);
            }
            cbBenchmarkFilter.SelectedItem =
                !string.IsNullOrWhiteSpace(selectedBenchmark) && cbBenchmarkFilter.Items.Contains(selectedBenchmark)
                    ? selectedBenchmark
                    : "(Wszystkie benchmarki)";

            cbProfileFilter.Items.Add("(Wszystkie profile)");
            foreach (var profile in _allRuns
                .Select(r => SafeProfile(r.Config))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .OrderBy(v => v))
            {
                cbProfileFilter.Items.Add(profile);
            }
            cbProfileFilter.SelectedItem =
                !string.IsNullOrWhiteSpace(selectedProfile) && cbProfileFilter.Items.Contains(selectedProfile)
                    ? selectedProfile
                    : "(Wszystkie profile)";
        }

        private void RefreshViews()
        {
            _isRefreshing = true;
            try
            {
                var filteredRuns = GetFilteredRuns().ToList();

                lblStatus.Text = $"Raporty: {_allRuns.Count} | Po filtrach: {filteredRuns.Count} | Wybrany model: {(_selectedModel ?? "(brak)")}";

                RefreshModelsGrid(filteredRuns);
                RefreshRunsGrid(filteredRuns);
                RefreshFailuresGrid(filteredRuns);
                RefreshSummary();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private IEnumerable<BenchmarkRunRecord> GetFilteredRuns()
        {
            IEnumerable<BenchmarkRunRecord> query = _allRuns;

            string benchmark = cbBenchmarkFilter.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(benchmark) && benchmark != "(Wszystkie benchmarki)")
            {
                query = query.Where(r => string.Equals(SafeBenchmarkName(r.Config), benchmark, StringComparison.OrdinalIgnoreCase));
            }

            string profile = cbProfileFilter.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(profile) && profile != "(Wszystkie profile)")
            {
                query = query.Where(r => string.Equals(SafeProfile(r.Config), profile, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_selectedModel))
            {
                query = query.Where(r => string.Equals(SafeModel(r.Config), _selectedModel, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }

        private void RefreshModelsGrid(List<BenchmarkRunRecord> filteredRuns)
        {
            string modelToReselect = _selectedModel;
            dgvModels.Rows.Clear();

            var modelStats = _allRuns
                .Where(r =>
                    (cbBenchmarkFilter.SelectedItem as string == "(Wszystkie benchmarki)" || string.Equals(SafeBenchmarkName(r.Config), cbBenchmarkFilter.SelectedItem as string, StringComparison.OrdinalIgnoreCase)) &&
                    (cbProfileFilter.SelectedItem as string == "(Wszystkie profile)" || string.Equals(SafeProfile(r.Config), cbProfileFilter.SelectedItem as string, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(r => SafeModel(r.Config))
                .Select(g => new
                {
                    Model = g.Key,
                    Runs = g.Count(),
                    AvgScore = g.Average(r => r.Config.RunMetadata.GlobalScore),
                    BestScore = g.Max(r => r.Config.RunMetadata.GlobalScore),
                    AvgMs = g.Average(r => r.Config.RunMetadata.AverageExecutionTimeMs),
                    AvgCalls = g.Average(GetTotalToolCalls),
                    LastRun = g.Max(r => r.FileTimestamp)
                })
                .OrderByDescending(x => x.AvgScore)
                .ThenBy(x => x.AvgMs)
                .ToList();

            foreach (var stat in modelStats)
            {
                int row = dgvModels.Rows.Add(
                    stat.Model,
                    stat.Runs,
                    stat.AvgScore.ToString("F2"),
                    stat.BestScore.ToString("F2"),
                    Math.Round(stat.AvgMs, 0).ToString("F0"),
                    Math.Round(stat.AvgCalls, 1).ToString("F1"),
                    stat.LastRun.ToString("yyyy-MM-dd HH:mm"));

                dgvModels.Rows[row].DefaultCellStyle.BackColor =
                    modelToReselect == stat.Model ? Color.FromArgb(40, 70, 100) : Color.FromArgb(35, 35, 35);
            }

            if (!string.IsNullOrWhiteSpace(modelToReselect))
            {
                foreach (DataGridViewRow row in dgvModels.Rows)
                {
                    if (string.Equals(row.Cells["Model"].Value?.ToString(), modelToReselect, StringComparison.OrdinalIgnoreCase))
                    {
                        row.Selected = true;
                        break;
                    }
                }
            }
        }

        private void RefreshRunsGrid(List<BenchmarkRunRecord> filteredRuns)
        {
            dgvRuns.Rows.Clear();

            foreach (var run in filteredRuns
                .OrderByDescending(r => r.FileTimestamp))
            {
                string configText =
                    $"ctx={run.Config.RunMetadata.LoadContextLength}, temp={run.Config.RunMetadata.Temperature:0.##}, top_p={run.Config.RunMetadata.TopP:0.##}, max={run.Config.RunMetadata.MaxTokens}";

                int failed = run.Config.Tests.Count(t => !t.Passed);
                dgvRuns.Rows.Add(
                    run.Config.RunMetadata.RunDate,
                    SafeModel(run.Config),
                    SafeBenchmarkName(run.Config),
                    SafeProfile(run.Config),
                    run.Config.RunMetadata.GlobalScore.ToString("F2"),
                    run.Config.RunMetadata.AverageExecutionTimeMs.ToString("F0"),
                    failed,
                    GetTotalToolCalls(run).ToString(),
                    configText);
            }
        }

        private void RefreshFailuresGrid(List<BenchmarkRunRecord> filteredRuns)
        {
            dgvFailures.Rows.Clear();

            if (filteredRuns.Count == 0)
            {
                return;
            }

            var failures = filteredRuns
                .SelectMany(run => run.Config.Tests
                    .Where(t => !t.Passed)
                    .Select(t => new
                    {
                        t.Id,
                        t.TestName,
                        Model = SafeModel(run.Config)
                    }))
                .GroupBy(x => new { x.Id, x.TestName })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.TestName,
                    FailCount = g.Count(),
                    FailRate = (double)g.Count() / filteredRuns.Count * 100.0,
                    Models = string.Join(", ", g.Select(x => x.Model).Distinct().OrderBy(x => x))
                })
                .OrderByDescending(x => x.FailCount)
                .ThenBy(x => x.Id)
                .Take(30)
                .ToList();

            foreach (var failure in failures)
            {
                dgvFailures.Rows.Add(
                    failure.Id,
                    failure.TestName,
                    failure.FailCount,
                    failure.FailRate.ToString("F1"),
                    failure.Models);
            }
        }

        private void RefreshSummary()
        {
            var filteredRuns = GetFilteredRuns().OrderByDescending(r => r.FileTimestamp).ToList();
            txtSummary.Clear();

            if (filteredRuns.Count == 0)
            {
                txtSummary.Text = "Brak danych po aktualnych filtrach.";
                return;
            }

            var selectedRun = GetSelectedRun(filteredRuns);
            if (selectedRun != null)
            {
                WriteRunSummary(selectedRun);
                return;
            }

            WriteAggregateSummary(filteredRuns);
        }

        private BenchmarkRunRecord GetSelectedRun(List<BenchmarkRunRecord> filteredRuns)
        {
            if (dgvRuns.SelectedRows.Count == 0)
            {
                return null;
            }

            string runDate = dgvRuns.SelectedRows[0].Cells["RunDate"].Value?.ToString();
            string model = dgvRuns.SelectedRows[0].Cells["Model"].Value?.ToString();
            string benchmark = dgvRuns.SelectedRows[0].Cells["Benchmark"].Value?.ToString();

            return filteredRuns.FirstOrDefault(r =>
                string.Equals(r.Config.RunMetadata.RunDate, runDate, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SafeModel(r.Config), model, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SafeBenchmarkName(r.Config), benchmark, StringComparison.OrdinalIgnoreCase));
        }

        private void WriteAggregateSummary(List<BenchmarkRunRecord> filteredRuns)
        {
            var models = filteredRuns.Select(r => SafeModel(r.Config)).Distinct().OrderBy(x => x).ToList();
            var benchmarks = filteredRuns.Select(r => SafeBenchmarkName(r.Config)).Distinct().OrderBy(x => x).ToList();

            txtSummary.AppendText("ANALIZA ZBIORCZA\n");
            txtSummary.AppendText(new string('=', 64) + "\n\n");
            txtSummary.AppendText($"Raporty: {filteredRuns.Count}\n");
            txtSummary.AppendText($"Modele: {models.Count}\n");
            txtSummary.AppendText($"Benchmarki: {benchmarks.Count}\n");
            txtSummary.AppendText($"Sredni wynik: {filteredRuns.Average(r => r.Config.RunMetadata.GlobalScore):F2}%\n");
            txtSummary.AppendText($"Najlepszy wynik: {filteredRuns.Max(r => r.Config.RunMetadata.GlobalScore):F2}%\n");
            txtSummary.AppendText($"Sredni czas: {filteredRuns.Average(r => r.Config.RunMetadata.AverageExecutionTimeMs):F0} ms\n");
            txtSummary.AppendText($"Srednia liczba tool calls: {filteredRuns.Average(GetTotalToolCalls):F1}\n\n");

            txtSummary.AppendText("Ranking modeli (po srednim wyniku):\n");
            foreach (var group in filteredRuns.GroupBy(r => SafeModel(r.Config))
                .OrderByDescending(g => g.Average(r => r.Config.RunMetadata.GlobalScore))
                .ThenBy(g => g.Average(r => r.Config.RunMetadata.AverageExecutionTimeMs)))
            {
                txtSummary.AppendText(
                    $"- {group.Key}: avg {group.Average(r => r.Config.RunMetadata.GlobalScore):F2}% | " +
                    $"best {group.Max(r => r.Config.RunMetadata.GlobalScore):F2}% | " +
                    $"avg {group.Average(r => r.Config.RunMetadata.AverageExecutionTimeMs):F0} ms | " +
                    $"runs {group.Count()}\n");
            }

            txtSummary.AppendText("\nNajczestsze testy problematyczne:\n");
            var topFailures = filteredRuns
                .SelectMany(run => run.Config.Tests.Where(t => !t.Passed).Select(t => new { t.Id, t.TestName }))
                .GroupBy(x => new { x.Id, x.TestName })
                .OrderByDescending(g => g.Count())
                .Take(10);

            foreach (var failure in topFailures)
            {
                txtSummary.AppendText($"- [{failure.Key.Id}] {failure.Key.TestName}: {failure.Count()} porazek\n");
            }
        }

        private void WriteRunSummary(BenchmarkRunRecord run)
        {
            var meta = run.Config.RunMetadata;
            txtSummary.AppendText("SZCZEGOLY PRZEBIEGU\n");
            txtSummary.AppendText(new string('=', 64) + "\n\n");
            txtSummary.AppendText($"Plik: {run.FileName}\n");
            txtSummary.AppendText($"Model: {SafeModel(run.Config)}\n");
            txtSummary.AppendText($"Benchmark: {SafeBenchmarkName(run.Config)}\n");
            txtSummary.AppendText($"Profil: {SafeProfile(run.Config)}\n");
            txtSummary.AppendText($"Wynik: {meta.GlobalScore:F2}% ({run.Config.Tests.Count(t => t.Passed)}/{run.Config.Tests.Count})\n");
            txtSummary.AppendText($"Sredni czas: {meta.AverageExecutionTimeMs:F0} ms\n");
            txtSummary.AppendText($"Tool calls: {GetTotalToolCalls(run)}\n");
            txtSummary.AppendText($"Provider: {meta.ProviderName}\n");
            txtSummary.AppendText($"Endpoint: {meta.ProviderEndpoint}\n");
            txtSummary.AppendText($"Konfiguracja: temp={meta.Temperature:0.##}, top_p={meta.TopP:0.##}, top_k={meta.TopK}, min_p={meta.MinP:0.##}, rep={meta.RepetitionPenalty:0.##}, max={meta.MaxTokens}, ctx={meta.LoadContextLength}\n\n");

            txtSummary.AppendText("Kategorie:\n");
            foreach (var category in meta.CategoriesScores.OrderBy(kvp => kvp.Key))
            {
                txtSummary.AppendText($"- {category.Key}: {category.Value:F2}%\n");
            }

            var failedTests = run.Config.Tests.Where(t => !t.Passed).ToList();
            txtSummary.AppendText("\nPorazki:\n");
            if (failedTests.Count == 0)
            {
                txtSummary.AppendText("- brak, pelny sukces\n");
            }
            else
            {
                foreach (var test in failedTests)
                {
                    txtSummary.AppendText($"- [{test.Id}] {test.TestName}\n");
                    foreach (var err in test.FailedRulesErrors.Take(3))
                    {
                        txtSummary.AppendText($"    {err}\n");
                    }
                }
            }

            int noToolCallTests = run.Config.Tests.Count(t => (t.RecordedToolCalls == null || t.RecordedToolCalls.Count == 0));
            txtSummary.AppendText($"\nDiagnostyka: testy bez tool calls = {noToolCallTests}/{run.Config.Tests.Count}\n");
            if (noToolCallTests == run.Config.Tests.Count)
            {
                txtSummary.AppendText("Uwaga: ten przebieg wyglada na brak wejscia modelu w kontrakt tool-calling.\n");
            }
        }

        private void DgvModels_SelectionChanged(object sender, EventArgs e)
        {
            if (_isRefreshing)
            {
                return;
            }

            if (dgvModels.SelectedRows.Count == 0)
            {
                return;
            }

            _selectedModel = dgvModels.SelectedRows[0].Cells["Model"].Value?.ToString();
            RefreshViews();
        }

        private static int GetTotalToolCalls(BenchmarkRunRecord run)
        {
            return run.Config.Tests.Sum(t => t.RecordedToolCalls?.Count ?? 0);
        }

        private static string SafeBenchmarkName(BenchmarkConfig config)
        {
            return config?.RunMetadata?.BenchmarkName ?? "(brak)";
        }

        private static string SafeModel(BenchmarkConfig config)
        {
            return config?.RunMetadata?.ModelName ?? "(brak)";
        }

        private static string SafeProfile(BenchmarkConfig config)
        {
            return string.IsNullOrWhiteSpace(config?.RunMetadata?.ProfileName) ? "(brak)" : config.RunMetadata.ProfileName;
        }

        private class BenchmarkRunRecord
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
            public DateTime FileTimestamp { get; set; }
            public BenchmarkConfig Config { get; set; }
        }
    }
}
