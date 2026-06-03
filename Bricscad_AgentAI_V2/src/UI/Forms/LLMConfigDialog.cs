using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.UI.Forms
{
    public class LLMConfigDialog : Form
    {
        private ComboBox cbProviders;
        private TextBox txtName, txtUrl, txtApiKey, txtSiteName;
        private NumericUpDown numTemp, numTokens;
        private NumericUpDown numTopP, numTopK, numMinP, numRepPenalty;
        private ComboBox cbReasoningEffort;

        // LM Studio Load Parameters
        private ComboBox cbGpu;
        private NumericUpDown numLoadCtx, numTtl;
        private CheckBox chkAutoLoad, chkFlashAtt, chkOffloadKv;
        private Button btnLoadModel;

        private ComboBox cbModels;
        private Button btnSave, btnAdd, btnRemove, btnFetchModels;

        private LLMConfig _config;
        private LLMProviderConfig _currentEditing;
        private bool _isUpdatingUI = false;

        public LLMConfigDialog()
        {
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "⚙️ Ustawienia Dostawców LLM";
            this.Size = new Size(500, 840);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f);

            var panTop = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            cbProviders = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
            cbProviders.SelectedIndexChanged += CbProviders_SelectedIndexChanged;
            
            btnAdd = new Button { Text = "➕ Nowy", Dock = DockStyle.Right, Width = 80, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 122, 204) };
            btnAdd.Click += BtnAdd_Click;
            btnRemove = new Button { Text = "🗑 Usuń", Dock = DockStyle.Right, Width = 80, FlatStyle = FlatStyle.Flat, BackColor = Color.Crimson };
            btnRemove.Click += BtnRemove_Click;

            panTop.Controls.Add(cbProviders);
            panTop.Controls.Add(btnAdd);
            panTop.Controls.Add(btnRemove);

            var panForm = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 18, Padding = new Padding(10) };
            panForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            panForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            txtName = AddFormField(panForm, 0, "Nazwa Profilu:");
            txtUrl = AddFormField(panForm, 1, "Endpoint URL:");
            txtApiKey = AddFormField(panForm, 2, "Klucz API:");
            txtApiKey.PasswordChar = '*';
            
            // Wiersz 3: Model
            panForm.Controls.Add(new Label { Text = "Model:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
            var panModel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            cbModels = new ComboBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
            cbModels.TextChanged += (s, e) => { if (!_isUpdatingUI && _currentEditing != null) _currentEditing.ModelName = cbModels.Text; };
            btnFetchModels = new Button { Text = "🔄", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60) };
            btnFetchModels.Click += BtnFetchModels_Click;
            panModel.Controls.Add(cbModels);
            panModel.Controls.Add(btnFetchModels);
            panForm.Controls.Add(panModel, 1, 3);

            numTemp = AddNumericField(panForm, 4, "Temperatura:", 0, 2, 2, 0.1m);
            numTokens = AddNumericField(panForm, 5, "Max Tokens:", 1, 128000, 0, 1000m);
            
            numTopP = AddNumericField(panForm, 6, "Top-P:", 0.0m, 1.0m, 2, 0.05m);
            numTopK = AddNumericField(panForm, 7, "Top-K:", 0, 200, 0, 1m);
            numMinP = AddNumericField(panForm, 8, "Min-P:", 0.0m, 1.0m, 2, 0.05m);
            numRepPenalty = AddNumericField(panForm, 9, "Repetition Penalty:", 1.0m, 2.0m, 2, 0.05m);

            // Wiersz 10: Reasoning Effort
            panForm.Controls.Add(new Label { Text = "Reasoning Effort:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 10);
            cbReasoningEffort = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
            cbReasoningEffort.Items.AddRange(new object[] { "none", "low", "medium", "high" });
            cbReasoningEffort.SelectedIndexChanged += (s, e) => {
                if (!_isUpdatingUI && _currentEditing != null)
                    _currentEditing.ReasoningEffort = cbReasoningEffort.Text;
            };
            panForm.Controls.Add(cbReasoningEffort, 1, 10);

            // Wiersz 11: Pusta linia jako separator sekcji LM Studio
            panForm.Controls.Add(new Label { Text = "--- PARAMETRY ŁADOWANIA (LM STUDIO) ---", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DarkGray, Font = new Font(this.Font, FontStyle.Italic | FontStyle.Bold) }, 0, 11);
            panForm.SetColumnSpan(panForm.GetControlFromPosition(0, 11), 2);

            // Wiersz 12: AutoLoad i przycisk LoadModel
            panForm.Controls.Add(new Label { Text = "Dynamiczne VRAM:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 12);
            var panAutoLoad = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            chkAutoLoad = new CheckBox { Text = "Auto-ładuj", AutoSize = true, Location = new Point(0, 5), ForeColor = Color.White };
            chkAutoLoad.CheckedChanged += (s, e) => {
                if (!_isUpdatingUI && _currentEditing != null)
                    _currentEditing.AutoLoadModel = chkAutoLoad.Checked;
            };
            btnLoadModel = new Button { Text = "⚡ Załaduj do VRAM", Location = new Point(100, 0), Width = 150, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 122, 204), Font = new Font(this.Font, FontStyle.Bold) };
            btnLoadModel.Click += BtnLoadModel_Click;
            panAutoLoad.Controls.Add(chkAutoLoad);
            panAutoLoad.Controls.Add(btnLoadModel);
            panForm.Controls.Add(panAutoLoad, 1, 12);

            // Wiersz 13: GPU Offload
            panForm.Controls.Add(new Label { Text = "GPU Offload (VRAM):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 13);
            cbGpu = new ComboBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
            cbGpu.Items.AddRange(new object[] { "default", "max", "off", "0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9" });
            cbGpu.TextChanged += (s, e) => {
                if (!_isUpdatingUI && _currentEditing != null)
                    _currentEditing.GpuOffload = cbGpu.Text;
            };
            panForm.Controls.Add(cbGpu, 1, 13);

            // Wiersz 14: LoadContextLength
            numLoadCtx = AddNumericField(panForm, 14, "Context Length (Load):", 0, 128000, 0, 1000m);

            // Wiersz 15: TtlSeconds
            numTtl = AddNumericField(panForm, 15, "Czas TTL (sekundy):", 0, 86400, 0, 60m);

            // Wiersz 16: FlashAttention i OffloadKvCache
            panForm.Controls.Add(new Label { Text = "Optymalizacje:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 16);
            var panOpts = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            chkFlashAtt = new CheckBox { Text = "Flash Att.", AutoSize = true, Location = new Point(0, 5), ForeColor = Color.White };
            chkFlashAtt.CheckedChanged += (s, e) => {
                if (!_isUpdatingUI && _currentEditing != null)
                    _currentEditing.FlashAttention = chkFlashAtt.Checked;
            };
            chkOffloadKv = new CheckBox { Text = "Offload KV", AutoSize = true, Location = new Point(100, 5), ForeColor = Color.White };
            chkOffloadKv.CheckedChanged += (s, e) => {
                if (!_isUpdatingUI && _currentEditing != null)
                    _currentEditing.OffloadKvCache = chkOffloadKv.Checked;
            };
            panOpts.Controls.Add(chkFlashAtt);
            panOpts.Controls.Add(chkOffloadKv);
            panForm.Controls.Add(panOpts, 1, 16);

            // Wiersz 17: Site Name
            txtSiteName = AddFormField(panForm, 17, "Site Name (OpenRouter):");

            // Konfiguracja podpowiedzi (ToolTips)
            var toolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 400,
                ReshowDelay = 400,
                ShowAlways = true
            };
            toolTip.SetToolTip(numTemp, "Temperatura (0.0 - 2.0):\nKontroluje stopień losowości generowania. Niska temperatura (np. 0.0 - 0.2) daje deterministyczne, powtarzalne odpowiedzi - idealne do generowania kodu i operacji CAD. Wyższa (np. 0.7 - 1.0) zwiększa kreatywność.");
            toolTip.SetToolTip(numTokens, "Max Tokens:\nOkreśla limit długości generowanej odpowiedzi (w tokenach). Dla modeli lokalnych standard to 4096 - 8000.");
            toolTip.SetToolTip(numTopP, "Top-P (Nucleus Sampling):\nAlternatywa dla temperatury. Model bierze pod uwagę tylko słowa z puli o skumulowanym prawdopodobieństwie P (np. 0.95 oznacza odrzucenie 5% najmniej prawdopodobnych słów).");
            toolTip.SetToolTip(numTopK, "Top-K:\nOgranicza pulę wyboru do K najbardziej prawdopodobnych słów na krok generowania (np. 40). Pomaga utrzymać logiczny wątek na małych modelach. Ustawienie 0 wyłącza parametr.");
            toolTip.SetToolTip(numMinP, "Min-P:\nDynamiczny próg prawdopodobieństwa. Odrzuca tokeny, których prawdopodobieństwo jest mniejsze niż np. 5% (0.05) w stosunku do najmocniejszego tokenu. Świetna, nowoczesna alternatywa dla Top-P.");
            toolTip.SetToolTip(numRepPenalty, "Repetition Penalty:\nKara za powtarzalność słów. Wartość > 1.0 (np. 1.1 - 1.2) skutecznie chroni mniejsze modele lokalne przed wpadaniem w pętle nieskończone.");
            toolTip.SetToolTip(cbReasoningEffort, "Reasoning Effort (np. gpt-oss-20b, o1, o3-mini):\nKontroluje stopień zaangażowania procesu myślowego modelu (Chain of Thought).");

            // Nowe Tooltipy dla parametrów ładowania LM Studio
            toolTip.SetToolTip(chkAutoLoad, "Auto-ładuj:\nJeśli włączone, wtyczka przed każdym zapytaniem wyśle żądanie /api/v1/models/load do LM Studio, upewniając się, że model jest w pamięci przed egzekucją czatu.");
            toolTip.SetToolTip(btnLoadModel, "⚡ Załaduj do VRAM:\nNatychmiast wysyła żądanie do LM Studio, aby załadować wybrany model z poniższymi parametrami VRAM.");
            toolTip.SetToolTip(cbGpu, "GPU Offload (VRAM):\n[Niedostępne w REST API] LM Studio nie pozwala na przekazywanie proporcji GPU bezpośrednio w żądaniu ładowania REST API. Model zostanie załadowany przy użyciu domyślnego profilu GPU ustawionego w GUI aplikacji LM Studio.");
            toolTip.SetToolTip(numLoadCtx, "Context Length (Load):\nRozmiar kontekstu modelu ustawiany przy ładowaniu w LM Studio (np. 8192). Wartość 0 używa domyślnej dla modelu.");
            toolTip.SetToolTip(numTtl, "Czas TTL (sekundy):\nCzas bezczynności w sekundach, po którym LM Studio automatycznie wyładuje model z pamięci VRAM i zwolni pamięć GPU (np. 300 sekund = 5 minut, 0 = wyłączony).");
            toolTip.SetToolTip(chkFlashAtt, "Flash Attention:\nOptymalizacja uwagi. Zmniejsza zużycie pamięci i przyspiesza generowanie na kompatybilnych kartach GPU.");
            toolTip.SetToolTip(chkOffloadKv, "Offload KV Cache:\nOdciąża cache klucz-wartość (KV Cache) do GPU, co dodatkowo przyspiesza generowanie przy długim kontekście.");

            var panBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
            btnSave = new Button { Text = "💾 Zapisz i Wybierz", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.SeaGreen, Font = new Font(this.Font, FontStyle.Bold) };
            btnSave.Click += BtnSave_Click;
            panBottom.Controls.Add(btnSave);

            this.Controls.Add(panForm);
            this.Controls.Add(panTop);
            this.Controls.Add(panBottom);
        }

        private TextBox AddFormField(TableLayoutPanel panel, int row, string labelText)
        {
            panel.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            var tb = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            tb.TextChanged += (s, e) => {
                if (_isUpdatingUI || _currentEditing == null) return;
                if (row == 0) _currentEditing.Name = tb.Text;
                else if (row == 1) _currentEditing.EndpointUrl = tb.Text;
                else if (row == 2) _currentEditing.ApiKey = tb.Text;
                else if (row == 17) _currentEditing.SiteName = tb.Text;
            };
            panel.Controls.Add(tb, 1, row);
            return tb;
        }

        private NumericUpDown AddNumericField(TableLayoutPanel panel, int row, string labelText, decimal min, decimal max, int decimalPlaces, decimal increment)
        {
            panel.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            var num = new NumericUpDown { Dock = DockStyle.Fill, Minimum = min, Maximum = max, DecimalPlaces = decimalPlaces, Increment = increment, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
            num.ValueChanged += (s, e) => {
                if (_isUpdatingUI || _currentEditing == null) return;
                if (row == 4) _currentEditing.Temperature = (double)num.Value;
                else if (row == 5) _currentEditing.MaxTokens = (int)num.Value;
                else if (row == 6) _currentEditing.TopP = (double)num.Value;
                else if (row == 7) _currentEditing.TopK = (int)num.Value;
                else if (row == 8) _currentEditing.MinP = (double)num.Value;
                else if (row == 9) _currentEditing.RepetitionPenalty = (double)num.Value;
                else if (row == 14) _currentEditing.LoadContextLength = (int)num.Value;
                else if (row == 15) _currentEditing.TtlSeconds = (int)num.Value;
            };
            panel.Controls.Add(num, 1, row);
            return num;
        }

        private void LoadData()
        {
            // Głęboka kopia, by można było anulować zmianę zamykając okno
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(LLMConfigManager.Current);
            _config = Newtonsoft.Json.JsonConvert.DeserializeObject<LLMConfig>(json);
            
            RefreshProviderList();
            
            var activeIdx = _config.Providers.FindIndex(p => p.Id == _config.ActiveProviderId);
            if (activeIdx >= 0) cbProviders.SelectedIndex = activeIdx;
            else if (cbProviders.Items.Count > 0) cbProviders.SelectedIndex = 0;
        }

        private void RefreshProviderList()
        {
            _isUpdatingUI = true;
            cbProviders.DataSource = null;
            cbProviders.DataSource = _config.Providers;
            cbProviders.DisplayMember = "Name";
            _isUpdatingUI = false;
        }

        private void CbProviders_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            _currentEditing = cbProviders.SelectedItem as LLMProviderConfig;
            UpdateFormFromCurrent();
        }

        private void UpdateFormFromCurrent()
        {
            if (_currentEditing == null) return;
            _isUpdatingUI = true;

            txtName.Text = _currentEditing.Name;
            txtUrl.Text = _currentEditing.EndpointUrl;
            txtApiKey.Text = _currentEditing.ApiKey;
            
            cbModels.Items.Clear();
            cbModels.Text = _currentEditing.ModelName;

            numTemp.Value = (decimal)_currentEditing.Temperature;
            numTokens.Value = _currentEditing.MaxTokens;
            
            numTopP.Value = (decimal)_currentEditing.TopP;
            numTopK.Value = _currentEditing.TopK;
            numMinP.Value = (decimal)_currentEditing.MinP;
            numRepPenalty.Value = (decimal)_currentEditing.RepetitionPenalty;
            
            cbReasoningEffort.Text = string.IsNullOrEmpty(_currentEditing.ReasoningEffort) ? "none" : _currentEditing.ReasoningEffort;

            // Parametry LM Studio
            chkAutoLoad.Checked = _currentEditing.AutoLoadModel;
            cbGpu.Text = string.IsNullOrEmpty(_currentEditing.GpuOffload) ? "default" : _currentEditing.GpuOffload;
            numLoadCtx.Value = _currentEditing.LoadContextLength;
            numTtl.Value = _currentEditing.TtlSeconds;
            chkFlashAtt.Checked = _currentEditing.FlashAttention;
            chkOffloadKv.Checked = _currentEditing.OffloadKvCache;

            txtSiteName.Text = _currentEditing.SiteName;

            _isUpdatingUI = false;
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var newProvider = new LLMProviderConfig { Name = "Nowy Dostawca " + (_config.Providers.Count + 1) };
            _config.Providers.Add(newProvider);
            RefreshProviderList();
            cbProviders.SelectedItem = newProvider;
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (_config.Providers.Count <= 1)
            {
                MessageBox.Show("Nie możesz usunąć ostatniego profilu.");
                return;
            }
            if (_currentEditing != null)
            {
                _config.Providers.Remove(_currentEditing);
                RefreshProviderList();
                cbProviders.SelectedIndex = 0;
            }
        }

        private async void BtnFetchModels_Click(object sender, EventArgs e)
        {
            if (_currentEditing == null) return;
            
            btnFetchModels.Enabled = false;
            btnFetchModels.Text = "...";

            try
            {
                // Konwersja URL: z np. https://.../v1/chat/completions na https://.../v1/models
                string url = _currentEditing.EndpointUrl.Replace("/chat/completions", "/models");
                
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    if (!string.IsNullOrEmpty(_currentEditing.ApiKey) && _currentEditing.ApiKey != "not-needed")
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_currentEditing.ApiKey}");

                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var body = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(body);
                    var modelsArray = json["data"] as JArray;

                    if (modelsArray != null)
                    {
                        var models = modelsArray.Select(m => m["id"]?.ToString()).Where(m => !string.IsNullOrEmpty(m)).OrderBy(m => m).ToList();
                        _isUpdatingUI = true;
                        cbModels.Items.Clear();
                        cbModels.Items.AddRange(models.ToArray());
                        _isUpdatingUI = false;
                        
                        if (models.Count > 0 && cbModels.Items.Contains(_currentEditing.ModelName))
                        {
                            cbModels.Text = _currentEditing.ModelName;
                        }
                        
                        MessageBox.Show($"Pobrano {models.Count} modeli.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania modeli: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnFetchModels.Enabled = true;
                btnFetchModels.Text = "🔄";
            }
        }

        private async void BtnLoadModel_Click(object sender, EventArgs e)
        {
            if (_currentEditing == null) return;

            btnLoadModel.Enabled = false;
            btnLoadModel.Text = "Ładowanie...";

            try
            {
                // Safeguard przed wysłaniem do chmury
                string url = _currentEditing.EndpointUrl;
                if (url.Contains("openrouter.ai") || url.Contains("api.openai.com"))
                {
                    MessageBox.Show("Dostawcy chmurowi (np. OpenRouter, OpenAI) nie obsługują dynamicznego ładowania modeli przez API.", "Wskazówka", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (url.Contains("/v1/chat/completions"))
                {
                    url = url.Replace("/v1/chat/completions", "");
                }
                else if (url.Contains("/chat/completions"))
                {
                    url = url.Replace("/chat/completions", "");
                }

                string loadUrl = url.TrimEnd('/') + "/api/v1/models/load";

                var loadPayload = new Dictionary<string, object>
                {
                    { "model", _currentEditing.ModelName }
                };

                if (_currentEditing.LoadContextLength > 0)
                {
                    loadPayload["context_length"] = _currentEditing.LoadContextLength;
                }

                if (_currentEditing.TtlSeconds > 0)
                {
                    loadPayload["ttl"] = _currentEditing.TtlSeconds;
                }

                if (_currentEditing.FlashAttention)
                {
                    loadPayload["flash_attention"] = true;
                }

                if (_currentEditing.OffloadKvCache)
                {
                    loadPayload["offload_kv_cache_to_gpu"] = true;
                }

                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(loadPayload);

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(3);
                    if (!string.IsNullOrEmpty(_currentEditing.ApiKey) && _currentEditing.ApiKey != "not-needed")
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_currentEditing.ApiKey}");

                    var request = new HttpRequestMessage(HttpMethod.Post, loadUrl)
                    {
                        Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                    };

                    var response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Model został pomyślnie załadowany do pamięci VRAM w LM Studio.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"Błąd serwera LM Studio ({response.StatusCode}): {responseBody}\n\nUpewnij się, że model o podanej nazwie jest zainstalowany.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wyjątek komunikacji: {ex.Message}\n\nUpewnij się, że serwer lokalny działa pod wskazanym adresem URL.", "Błąd połączenia", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLoadModel.Enabled = true;
                btnLoadModel.Text = "⚡ Załaduj do VRAM";
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_currentEditing != null)
            {
                _config.ActiveProviderId = _currentEditing.Id;
                
                // Kopiujemy z powrotem do aktualnej struktury i zapisujemy
                var configManagerType = typeof(LLMConfigManager);
                var field = configManagerType.GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                field.SetValue(null, _config);

                LLMConfigManager.Save();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}
