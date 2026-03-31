using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Teigha.DatabaseServices;
using Application = Bricscad.ApplicationServices.Application;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace Bricscad_AgentAI
{
    public partial class AgentControl : UserControl
    {
        private TabControl tabControl;

        // --- UI Czat ---
        private RichTextBox txtHistory;
        private RichTextBox txtInput;
        private Button btnSend;
        private Button btnReset;

        // --- UI Logi Tagów ---
        private ListBox listTags;
        private RichTextBox txtCodeView;
        private Button btnSaveJsonl;

        // --- UI DB Manager ---
        private BricsCAD_Agent.DatasetManagerControl dbManagerControl;

        // --- UI Ustawienia ---
        private Button btnThemeToggle;
        private bool isDarkMode = true;
        private const string RegistryPath = @"Software\BricsCADAgentAI\Settings";

        // --- Pamięć ---
        private List<string> sessionJsonlLines = new List<string>();
        private string lastUserPrompt = "";

        // ---- Statystyki
        private Label lblStats;

        public AgentControl()
        {
            LoadSettingsFromRegistry();
            InitializeModernUI();
            ApplyTheme();

            // Podpinamy nasłuchiwacz tagów z głównego silnika Agenta
            BricsCAD_Agent.Komendy.OnTagGenerated -= CatchTagForTraining;
            BricsCAD_Agent.Komendy.OnTagGenerated += CatchTagForTraining;
            BricsCAD_Agent.Komendy.OnModelStatsUpdated += UpdateStatsUI;
        }

        private void InitializeModernUI()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9.5f);

            tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(95, 25) };

            // ==========================================
            // ZAKŁADKA 1: CZAT Z AI
            // ==========================================
            TabPage tabChat = new TabPage("💬 Czat");

            txtHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None
            };

            Panel panInput = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(5) };

            txtInput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            txtInput.KeyDown += TxtInput_KeyDown;

            btnSend = new Button
            {
                Text = "Wyślij\n(Ctrl+Enter)",
                Dock = DockStyle.Right,
                Width = 100,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += btnSend_Click;

            // --- NOWY PRZYCISK RESET ---
            btnReset = new Button
            {
                Text = "Reset\nPamięci",
                Dock = DockStyle.Right,
                Width = 80,
                BackColor = Color.Crimson,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += BtnReset_Click;
            // --------------------------

            Panel inputBorder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Color.Gray };
            inputBorder.Controls.Add(txtInput);

            // --- ZMIENIONA KOLEJNOŚĆ (Aby Reset był obok Wyślij) ---
            panInput.Controls.Add(inputBorder);
            panInput.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panInput.Controls.Add(btnReset); // <-- Dodajemy Reset
            panInput.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panInput.Controls.Add(btnSend);  // <-- Dodajemy Wyślij na samym końcu

            // --- PASEK STATYSTYK (HUD) ---
            Panel panStats = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(5, 0, 0, 0) };
            lblStats = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Gotowy. Czekam na połączenie z LM Studio..."
            };
            panStats.Controls.Add(lblStats);

            // UPEWNIJ SIĘ, że dodajesz panele w tej kolejności do tabChat, aby pasek ułożył się nad oknem wpisywania:
            tabChat.Controls.Add(panStats);

            tabChat.Controls.Add(txtHistory);
            tabChat.Controls.Add(panInput);

            // ==========================================
            // ZAKŁADKA 2: LOGI TAGÓW (DAWNIEJ TRENING)
            // ==========================================
            TabPage tabDev = new TabPage("📜 Logi tagów");

            SplitContainer splitDev = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 150
            };

            listTags = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 9f),
                BorderStyle = BorderStyle.None
            };
            listTags.SelectedIndexChanged += ListTags_SelectedIndexChanged;

            txtCodeView = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None
            };

            // ---- PASEK Z PRZYCISKAMI DO EDYCJI TAGÓW ----
            Panel panDevMenu = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5) };

            Button btnTest = new Button { Text = "TESTUJ TAG", Dock = DockStyle.Left, Width = 90, BackColor = Color.LightSkyBlue, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnTest.FlatAppearance.BorderSize = 0;
            btnTest.Click += BtnTest_Click;

            Button btnValidateTags = new Button { Text = "Sprawdź Składnię", Dock = DockStyle.Left, Width = 110, BackColor = Color.Khaki, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnValidateTags.FlatAppearance.BorderSize = 0;
            btnValidateTags.Click += BtnValidateTags_Click;

            Button btnDelete = new Button { Text = "Usuń z listy", Dock = DockStyle.Right, Width = 90, BackColor = Color.LightCoral, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += BtnDelete_Click;

            Button btnUpdate = new Button { Text = "Zatwierdź", Dock = DockStyle.Right, Width = 90, BackColor = Color.Gray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.Click += BtnUpdate_Click;

            btnSaveJsonl = new Button { Text = "Zapisz .JSONL", Dock = DockStyle.Right, Width = 110, BackColor = Color.MediumSeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSaveJsonl.FlatAppearance.BorderSize = 0;
            btnSaveJsonl.Click += BtnSaveJsonl_Click;

            // Dodawanie od lewej
            panDevMenu.Controls.Add(btnValidateTags);
            panDevMenu.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panDevMenu.Controls.Add(btnTest);

            // Dodawanie od prawej (zaczynamy od tego, co ma być najbardziej z prawej)
            panDevMenu.Controls.Add(btnSaveJsonl);
            panDevMenu.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panDevMenu.Controls.Add(btnUpdate);
            panDevMenu.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panDevMenu.Controls.Add(btnDelete);

            splitDev.Panel1.Controls.Add(listTags);
            splitDev.Panel2.Controls.Add(txtCodeView);
            splitDev.Panel2.Controls.Add(panDevMenu);

            tabDev.Controls.Add(splitDev);

            // ==========================================
            // ZAKŁADKA 3: DB MANAGER (ZINTEGROWANA BAZA)
            // ==========================================
            TabPage tabDbManager = new TabPage("📂 DB Manager");

            dbManagerControl = new BricsCAD_Agent.DatasetManagerControl();
            dbManagerControl.Dock = DockStyle.Fill;
            tabDbManager.Controls.Add(dbManagerControl);

            // ==========================================
            // ZAKŁADKA 4: USTAWIENIA
            // ==========================================
            TabPage tabSettings = new TabPage("⚙️ Ustawienia");

            FlowLayoutPanel flowSettings = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15),
                FlowDirection = System.Windows.Forms.FlowDirection.TopDown
            };

            btnThemeToggle = new Button
            {
                Text = isDarkMode ? "Włącz Jasny Motyw" : "Włącz Ciemny Motyw",
                Width = 200,
                Height = 35,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnThemeToggle.Click += BtnThemeToggle_Click;

            Label lblInfo = new Label
            {
                Text = "Ustawienia interfejsu Agenta. Panel DB Manager posiada własne, niezależne ustawienia wyglądu.",
                AutoSize = true,
                Margin = new Padding(0, 20, 0, 0),
                ForeColor = Color.Gray
            };

            flowSettings.Controls.Add(btnThemeToggle);
            flowSettings.Controls.Add(lblInfo);

            // ==========================================
            // NARZĘDZIE DIAGNOSTYCZNE GETPOINT
            // ==========================================
            Button btnDebug = new Button
            {
                Text = "🛠 URUCHOM DIAGNOSTYKĘ GETPOINT",
                Width = 250,
                Height = 45,
                BackColor = Color.DarkOrange,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 30, 0, 0),
                Cursor = Cursors.Hand
            };

            btnDebug.Click += async (s, e) =>
            {
                Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                Editor ed = doc.Editor;
                ed.WriteMessage("\n\n=============== START DIAGNOSTYKI ===============");

                // TEST 1: Zwykły GetPoint (Zapewne anuluje od razu)
                try
                {
                    ed.WriteMessage("\n[TEST 1] Zwykły GetPoint... ");
                    var res1 = ed.GetPoint("\nKliknij punkt (TEST 1): ");
                    ed.WriteMessage($"Wynik: {res1.Status}");
                }
                catch (Exception ex) { ed.WriteMessage($"Błąd: {ex.Message}"); }

                // TEST 2: Wymuszony Focus + StartUserInteraction (Test przejęcia kontroli)
                try
                {
                    ed.WriteMessage("\n[TEST 2] Focus() + StartUserInteraction... ");
                    Bricscad.ApplicationServices.Application.MainWindow.Focus();
                    using (ed.StartUserInteraction(this))
                    {
                        var res2 = ed.GetPoint("\nKliknij punkt (TEST 2): ");
                        ed.WriteMessage($"Wynik: {res2.Status}");
                    }
                }
                catch (Exception ex) { ed.WriteMessage($"Błąd: {ex.Message}"); }

                // TEST 3: Odtworzenie zachowania LLM (Asynchroniczna pułapka!)
                try
                {
                    ed.WriteMessage("\n[TEST 3] Symulacja zapytania do AI (Czekam 1 sek)... ");

                    // Ta linijka zachowuje się dokładnie tak jak await client.PostAsync
                    await Task.Delay(1000);

                    Bricscad.ApplicationServices.Application.MainWindow.Focus();
                    using (ed.StartUserInteraction(this))
                    {
                        var res3 = ed.GetPoint("\nKliknij punkt (TEST 3): ");
                        ed.WriteMessage($"Wynik: {res3.Status}");
                    }
                }
                catch (Exception ex) { ed.WriteMessage($"Błąd: {ex.Message}"); }

                ed.WriteMessage("\n=============== KONIEC DIAGNOSTYKI ===============\n");
            };

            flowSettings.Controls.Add(btnDebug);

            tabSettings.Controls.Add(flowSettings);

            // Dodajemy zakładki do kontrolki głównej
            tabControl.TabPages.Add(tabChat);
            tabControl.TabPages.Add(tabDev);
            tabControl.TabPages.Add(tabDbManager);
            tabControl.TabPages.Add(tabSettings);

            // --- NOWA ZAKŁADKA: MOJE MAKRA ---
            TabPage tabMakra = new TabPage("💻 Moje Makra");
            tabMakra.Controls.Add(new BricsCAD_Agent.UserMacroControl());
            tabControl.TabPages.Add(tabMakra);
            // ---------------------------------

            this.Controls.Add(tabControl);
        }

        // =========================================================
        // AKCJA RESETU PAMIĘCI
        // =========================================================
        private void BtnReset_Click(object sender, EventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                // Wywołujemy publiczną metodę resetu z AgentCommand.cs
                BricsCAD_Agent.Komendy.ResetujPamiec(doc);

                // Zostawiamy ślad w oknie czatu
                AppendToHistory("SYSTEM", "Pamięć Agenta została wyczyszczona. Persona zresetowana.", isDarkMode ? Color.Orange : Color.DarkOrange);

                // Czyścimy ewentualny rozgrzebany tekst
                txtInput.Clear();
                txtInput.Focus();
            }
        }

        // =========================================================
        // AKTUALIZACJA HUD (STATYSTYK MODELU)
        // =========================================================
        private void UpdateStatsUI(int promptTokens, int completionTokens, double timeSec)
        {
            // Wymuszenie wykonania w wątku głównym UI (zapobiega błędom zamrożenia)
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateStatsUI(promptTokens, completionTokens, timeSec)));
                return;
            }

            int totalTokens = promptTokens + completionTokens;

            // Zabezpieczenie przed dzieleniem przez zero
            double speed = timeSec > 0 ? (completionTokens / timeSec) : 0;

            // Formatowanie ułamków (F1 oznacza 1 miejsce po przecinku)
            lblStats.Text = $"⏱ Czas: {timeSec:F1}s | 🧠 Kontekst: {promptTokens} tk | ⚡ Prędkość: {speed:F1} t/s | 📝 Wysłano: {completionTokens} tk | 📦 Pamięć: {totalTokens} tk";
        }


        // =========================================================
        // AKCJE ZAKŁADKI "LOGI TAGÓW" (Testowanie, Update, Usuwanie)
        // =========================================================
        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (listTags.SelectedIndex >= 0)
            {
                string minified = MinifyJsonAndEscape(txtCodeView.Text);
                sessionJsonlLines[listTags.SelectedIndex] = minified;

                // Odświeżamy widok by udowodnić, że zmiany się zapisały
                int tempIdx = listTags.SelectedIndex;
                listTags.SelectedIndex = -1;
                listTags.SelectedIndex = tempIdx;

                MessageBox.Show("Zaktualizowano wpis w pamięci logów.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (listTags.SelectedIndex >= 0)
            {
                int idx = listTags.SelectedIndex;
                sessionJsonlLines.RemoveAt(idx);
                listTags.Items.RemoveAt(idx);
                txtCodeView.Clear();
            }
        }

        private void BtnValidateTags_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCodeView.Text)) return;

            List<string> errs = BricsCAD_Agent.TagValidator.ValidateSequence(txtCodeView.Text);

            if (errs.Count == 0)
            {
                MessageBox.Show("Świetnie! Składnia jest poprawna, a wszystkie klasy i właściwości istnieją w bazie API.", "Sprawdzenie Tagów", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                string msg = "Znaleziono potencjalne błędy lub halucynacje LLM:\n\n" + string.Join("\n", errs);
                MessageBox.Show(msg, "Błędy w Składni", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCodeView.Text)) return;

            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptSelectionResult psr = ed.SelectImplied();
            if (psr.Status == PromptStatus.OK && psr.Value != null)
            {
                BricsCAD_Agent.Komendy.AktywneZaznaczenie = psr.Value.GetObjectIds();
                ed.WriteMessage($"\n[System] Przechwycono {BricsCAD_Agent.Komendy.AktywneZaznaczenie.Length} zaznaczonych obiektów przed testem.");
            }

            using (DocumentLock loc = doc.LockDocument())
            {
                ed.WriteMessage($"\n\n--- TESTOWANIE TAGÓW Z LOGÓW ---");

                string pelnyTekst = txtCodeView.Text;
                int currentIndex = 0;

                while (currentIndex < pelnyTekst.Length)
                {
                    int idxAction = pelnyTekst.IndexOf("[ACTION:", currentIndex);
                    int idxSelect = pelnyTekst.IndexOf("[SELECT:", currentIndex);
                    int idxLisp = pelnyTekst.IndexOf("[LISP:", currentIndex); // <--- NOWOŚĆ: Dodano wykrywanie LISP

                    int firstMatch = -1;
                    int[] indices = { idxAction, idxSelect, idxLisp };

                    // Szukamy, który tag występuje jako pierwszy w tekście
                    foreach (int idx in indices)
                    {
                        if (idx != -1 && (firstMatch == -1 || idx < firstMatch))
                            firstMatch = idx;
                    }

                    if (firstMatch == -1) break;

                    int bracketCount = 0;
                    int endIndex = -1;
                    for (int i = firstMatch; i < pelnyTekst.Length; i++)
                    {
                        if (pelnyTekst[i] == '[') bracketCount++;
                        else if (pelnyTekst[i] == ']') bracketCount--;

                        if (bracketCount == 0)
                        {
                            endIndex = i;
                            break;
                        }
                    }

                    if (endIndex == -1) break;

                    string tagDoWykonania = pelnyTekst.Substring(firstMatch, endIndex - firstMatch + 1);
                    currentIndex = endIndex + 1;

                    if (tagDoWykonania.Contains("[ACTION: ]") || tagDoWykonania.Replace(" ", "") == "[ACTION:]") continue;

                    string czystyTag = tagDoWykonania.Replace("\\\"", "\"").Replace("\\n", "\n");

                    ed.WriteMessage($"\n[Wykonuję]: {czystyTag}");

                    try
                    {
                        string wynik = BricsCAD_Agent.TrainingStudio.WykonywaczTagow(doc, czystyTag);
                        ed.WriteMessage($"\n[Wynik]: {wynik}");
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n[BŁĄD WYKONANIA]: {ex.Message}");
                    }
                }
                ed.WriteMessage($"\n-----------------------------------\nZakończono.");
            }
            Bricscad.ApplicationServices.Application.MainWindow.Focus();
        }

        // =========================================================
        // MOTYWY I REJESTR
        // =========================================================
        private void LoadSettingsFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        object themeVal = key.GetValue("IsDarkMode");
                        if (themeVal != null) isDarkMode = Convert.ToBoolean(themeVal);
                    }
                }
            }
            catch { }
        }

        private void SaveSettingsToRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue("IsDarkMode", isDarkMode);
                    }
                }
            }
            catch { }
        }

        private void BtnThemeToggle_Click(object sender, EventArgs e)
        {
            isDarkMode = !isDarkMode;
            SaveSettingsToRegistry();
            ApplyTheme();
            btnThemeToggle.Text = isDarkMode ? "Włącz Jasny Motyw" : "Włącz Ciemny Motyw";
        }

        private void ApplyTheme()
        {
            Color bgMain = isDarkMode ? Color.FromArgb(30, 30, 30) : Color.White;
            Color bgControl = isDarkMode ? Color.FromArgb(45, 45, 48) : Color.WhiteSmoke;
            Color fgText = isDarkMode ? Color.White : Color.Black;

            this.BackColor = bgMain;

            foreach (TabPage page in tabControl.TabPages)
            {
                page.BackColor = bgMain;
            }

            txtHistory.BackColor = bgMain;
            txtCodeView.BackColor = bgMain;
            txtInput.BackColor = bgControl;
            listTags.BackColor = bgControl;

            txtHistory.ForeColor = fgText;
            txtCodeView.ForeColor = fgText;
            txtInput.ForeColor = fgText;
            listTags.ForeColor = fgText;

            btnThemeToggle.BackColor = bgControl;
            btnThemeToggle.ForeColor = fgText;

            if (listTags.SelectedIndex >= 0)
            {
                ApplySyntaxHighlighting();
            }
        }

        // =========================================================
        // OBSŁUGA KLAWISZA ENTER (CTRL + ENTER = WYŚLIJ)
        // =========================================================
        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                btnSend_Click(btnSend, EventArgs.Empty);
            }
        }

        // =========================================================
        // GŁÓWNA LOGIKA WYSYŁANIA
        // =========================================================
        private async void btnSend_Click(object sender, EventArgs e)
        {
            string userMsg = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(userMsg)) return;

            lastUserPrompt = userMsg;

            AppendToHistory("TY", userMsg, isDarkMode ? Color.LightSkyBlue : Color.Blue);
            txtInput.Clear();
            btnSend.Enabled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;

            ObjectId[] przechwyconeZaznaczenie = null;
            try
            {
                PromptSelectionResult selRes = doc.Editor.SelectImplied();
                if (selRes.Status == PromptStatus.OK)
                    przechwyconeZaznaczenie = selRes.Value.GetObjectIds();
            }
            catch { }

            try
            {
                AppendToHistory("SYSTEM", "Bielik myśli...", Color.Gray);

                string aiResponse = await BricsCAD_Agent.Komendy.ZapytajAgentaAsync(userMsg, doc, przechwyconeZaznaczenie);

                AppendToHistory("BIELIK", aiResponse, isDarkMode ? Color.LightGreen : Color.DarkGreen);
            }
            catch (Exception ex)
            {
                AppendToHistory("BŁĄD", ex.Message, Color.LightCoral);
            }
            finally
            {
                btnSend.Enabled = true;
                txtInput.Focus();
            }
        }

        // =========================================================
        // PRZECHWYTYWANIE TAGÓW DO BAZY W TLE
        // =========================================================
        private void CatchTagForTraining(string czystyTag)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(CatchTagForTraining), czystyTag);
                return;
            }

            if (czystyTag.StartsWith("[Wykonano narzędzie"))
            {
                int newLineIdx = czystyTag.IndexOf('\n');
                if (newLineIdx != -1)
                    czystyTag = czystyTag.Substring(newLineIdx + 1).Trim();
            }

            if (czystyTag.Contains("[ACTION:") || czystyTag.Contains("[SELECT:"))
            {
                string promptBezpieczny = BricsCAD_Agent.Komendy.SafeJson(lastUserPrompt);
                string tagBezpieczny = BricsCAD_Agent.Komendy.SafeJson(czystyTag);

                string jsonlLine = $"{{\"messages\": [{{\"role\": \"user\", \"content\": \"{promptBezpieczny}\"}}, {{\"role\": \"assistant\", \"content\": \"{tagBezpieczny}\"}}]}}";

                sessionJsonlLines.Add(jsonlLine);
                listTags.Items.Add($"[{sessionJsonlLines.Count}] {lastUserPrompt}");
            }
        }

        private void ListTags_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listTags.SelectedIndex >= 0)
            {
                txtCodeView.Text = FormatJsonLikeCode(sessionJsonlLines[listTags.SelectedIndex]);
                ApplySyntaxHighlighting();
            }
        }

        private void BtnSaveJsonl_Click(object sender, EventArgs e)
        {
            if (sessionJsonlLines.Count == 0)
            {
                MessageBox.Show("Brak nowych tagów do zapisania w tej sesji.", "Pusto", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Logi treningowe (*.jsonl)|*.jsonl",
                FileName = "Tagi_z_Sesji.jsonl",
                Title = "Zapisz wygenerowane tagi"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllLines(sfd.FileName, sessionJsonlLines);
                MessageBox.Show($"Pomyślnie zapisano {sessionJsonlLines.Count} tagów!\nMożesz je teraz wczytać w DB Managerze i dodać do swojej głównej bazy.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // =========================================================
        // POMOCNICZE FUNKCJE KOMPRESJI JSON (Do zapisu)
        // =========================================================
        private bool IsQuoteEscaped(string text, int quoteIndex)
        {
            int slashCount = 0;
            for (int i = quoteIndex - 1; i >= 0; i--)
            {
                if (text[i] == '\\') slashCount++;
                else break;
            }
            return slashCount % 2 != 0;
        }

        private string MinifyJsonAndEscape(string text)
        {
            bool inString = false;
            var sb = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && !IsQuoteEscaped(text, i)) inString = !inString;

                if (!inString)
                {
                    if (!char.IsWhiteSpace(c)) sb.Append(c);
                }
                else
                {
                    if (c == '\r') continue;
                    if (c == '\n') sb.Append("\\n");
                    else sb.Append(c);
                }
            }
            return sb.ToString();
        }

        // =========================================================
        // FORMATOWANIE HISTORII I KODU
        // =========================================================
        public void AppendToHistory(string sender, string message, Color color)
        {
            txtHistory.SelectionStart = txtHistory.TextLength;
            txtHistory.SelectionLength = 0;

            txtHistory.SelectionColor = color;
            txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Bold);
            txtHistory.AppendText($"[{sender}]: ");

            txtHistory.SelectionColor = isDarkMode ? Color.White : Color.Black;
            txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Regular);
            txtHistory.AppendText($"{message}\n\n");

            txtHistory.SelectionStart = txtHistory.Text.Length;
            txtHistory.ScrollToCaret();
        }

        private string FormatJsonLikeCode(string text)
        {
            int indent = 0;
            bool inString = false;
            var sb = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;

                if (!inString)
                {
                    if (c == '{' || c == '[') { sb.Append(c); sb.Append("\r\n"); indent++; sb.Append(new string(' ', indent * 3)); }
                    else if (c == '}' || c == ']') { sb.Append("\r\n"); indent--; sb.Append(new string(' ', indent * 3)); sb.Append(c); }
                    else if (c == ',') { sb.Append(c); sb.Append("\r\n"); sb.Append(new string(' ', indent * 3)); }
                    else if (c == ':') { sb.Append(c); sb.Append(" "); }
                    else if (!char.IsWhiteSpace(c)) sb.Append(c);
                }
                else sb.Append(c);
            }
            return sb.ToString().Replace("\\n", "\r\n");
        }

        private void ApplySyntaxHighlighting()
        {
            int selStart = txtCodeView.SelectionStart;
            txtCodeView.SelectAll();
            txtCodeView.SelectionColor = isDarkMode ? Color.FromArgb(212, 212, 212) : Color.Black;

            Color colString = isDarkMode ? Color.FromArgb(206, 145, 120) : Color.FromArgb(163, 21, 21);
            Color colKey = isDarkMode ? Color.FromArgb(156, 220, 254) : Color.FromArgb(4, 81, 165);
            Color colTagPrefix = isDarkMode ? Color.FromArgb(197, 134, 192) : Color.FromArgb(128, 0, 128);

            MatchCollection matches = Regex.Matches(txtCodeView.Text, @"\""(.*?)\""");
            foreach (Match m in matches)
            {
                txtCodeView.SelectionStart = m.Index;
                txtCodeView.SelectionLength = m.Length;
                txtCodeView.SelectionColor = colString;
            }

            MatchCollection keys = Regex.Matches(txtCodeView.Text, @"\""([^\""]+)\""(?=\s*:)");
            foreach (Match m in keys)
            {
                txtCodeView.SelectionStart = m.Index;
                txtCodeView.SelectionLength = m.Length;
                txtCodeView.SelectionColor = colKey;
            }

            MatchCollection tags = Regex.Matches(txtCodeView.Text, @"\b(ACTION|SELECT|MSG|SYSTEM|LISP):");
            foreach (Match m in tags)
            {
                txtCodeView.SelectionStart = m.Index;
                txtCodeView.SelectionLength = m.Length;
                txtCodeView.SelectionColor = colTagPrefix;
                txtCodeView.SelectionFont = new Font(txtCodeView.Font, FontStyle.Bold);
            }

            txtCodeView.SelectionStart = selStart;
            txtCodeView.SelectionLength = 0;
        }
    }
}