using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace BricsCAD_Agent
{
    public class DatasetManagerControl : UserControl
    {
        private SplitContainer split;
        private ListBox listEntries;
        private RichTextBox txtContent;
        private Label lblStatus;

        private List<string> datasetLines = new List<string>();
        private string currentFilePath = "";

        // Zmienne ustawień
        private bool isDarkMode = true;
        private bool isFormattedView = true; // Domyślnie formatowanie włączone

        // Przyciski do interakcji
        private Button btnThemeToggle;
        private Button btnFormatToggle;
        private Button btnViewToggle;

        private const string RegistryPath = @"Software\BricsCADAgentAI\Settings";

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        private const int WM_SETREDRAW = 0x0b;

        public DatasetManagerControl()
        {
            LoadSettingsFromRegistry();
            InitializeUI();
            if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
            {
                LoadDataFromFile();
            }
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9f);

            // 1. Główne menu na samej górze (Zawsze widoczne, nie wchodzi do SplitContainera!)
            FlowLayoutPanel panTopMenu = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 35, Padding = new Padding(2), WrapContents = false };

            Button btnLoad = new Button { Text = "Wczytaj JSONL", Width = 100, Height = 28 };
            btnLoad.Click += BtnLoad_Click;

            Button btnRefresh = new Button { Text = "Odśwież", Width = 70, Height = 28 };
            btnRefresh.Click += BtnRefresh_Click;

            btnThemeToggle = new Button { Text = isDarkMode ? "Motyw: Ciemny" : "Motyw: Jasny", Width = 110, Height = 28 };
            btnThemeToggle.Click += BtnThemeToggle_Click;

            btnFormatToggle = new Button { Text = isFormattedView ? "Formatuj: Wł" : "Formatuj: Wył", Width = 100, Height = 28 };
            btnFormatToggle.Click += BtnFormatToggle_Click;

            btnViewToggle = new Button { Text = "Zwiń Listę", Width = 90, Height = 28 };
            btnViewToggle.Click += BtnViewToggle_Click;

            Button btnSaveFile = new Button { Text = "Zapisz do Pliku", Width = 110, Height = 28, BackColor = Color.LightGreen };
            btnSaveFile.Click += BtnSaveFile_Click;

            panTopMenu.Controls.Add(btnLoad);
            panTopMenu.Controls.Add(btnSaveFile);
            panTopMenu.Controls.Add(new Label { Width = 10 }); // Odstęp
            panTopMenu.Controls.Add(btnRefresh);
            panTopMenu.Controls.Add(btnThemeToggle);
            panTopMenu.Controls.Add(btnFormatToggle);
            panTopMenu.Controls.Add(btnViewToggle);

            // 2. Kontener dzielący resztę okna na Listę i Edytor
            split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 150 };

            // --- Panel 1 (Tylko Lista - to ona będzie ukrywana) ---
            listEntries = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            listEntries.SelectedIndexChanged += ListEntries_SelectedIndexChanged;
            split.Panel1.Controls.Add(listEntries);

            // --- Panel 2 (Edytor i Dolne Menu) ---
            Panel panBottom = new Panel { Dock = DockStyle.Fill };

            txtContent = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11f),
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                WordWrap = true
            };
            ApplyThemeColorsToTextBox();
            txtContent.TextChanged += TxtContent_TextChanged;

            Panel panBottomMenu = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5) };
            Button btnTest = new Button { Text = "TESTUJ TAGI", Dock = DockStyle.Left, Width = 120, BackColor = Color.LightSkyBlue };
            btnTest.Click += BtnTest_Click;
            Button btnUpdate = new Button { Text = "Zatwierdź Zmiany", Dock = DockStyle.Right, Width = 130 };
            btnUpdate.Click += BtnUpdate_Click;
            Button btnDelete = new Button { Text = "Usuń z Listy", Dock = DockStyle.Right, Width = 100, BackColor = Color.LightCoral };
            btnDelete.Click += BtnDelete_Click;

            panBottomMenu.Controls.Add(btnTest);
            panBottomMenu.Controls.Add(btnDelete);
            panBottomMenu.Controls.Add(btnUpdate);

            lblStatus = new Label { Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray };

            panBottom.Controls.Add(txtContent);
            panBottom.Controls.Add(panBottomMenu);
            panBottom.Controls.Add(lblStatus);
            split.Panel2.Controls.Add(panBottom);

            // 3. Dodawanie głównych sekcji do kontrolki (kolejność ma znaczenie dla Dockingu!)
            this.Controls.Add(split); // Najpierw kontener
            this.Controls.Add(panTopMenu); // Potem menu na samą górę
        }

        // =========================================================
        // USTAWIENIA I ZMIANY WIDOKU
        // =========================================================

        private void BtnThemeToggle_Click(object sender, EventArgs e)
        {
            isDarkMode = !isDarkMode;
            btnThemeToggle.Text = isDarkMode ? "Motyw: Ciemny" : "Motyw: Jasny";
            SaveSettingsToRegistry();
            ApplyThemeColorsToTextBox();
            ApplySyntaxHighlighting();
        }

        private void BtnFormatToggle_Click(object sender, EventArgs e)
        {
            isFormattedView = !isFormattedView;
            btnFormatToggle.Text = isFormattedView ? "Formatuj: Wł" : "Formatuj: Wył";
            SaveSettingsToRegistry();

            // Przeładuj aktualnie otwartą linię do edytora w nowym formacie
            if (listEntries.SelectedIndex >= 0) LoadLineToEditor(listEntries.SelectedIndex);
        }

        private void BtnViewToggle_Click(object sender, EventArgs e)
        {
            split.Panel1Collapsed = !split.Panel1Collapsed;
            btnViewToggle.Text = split.Panel1Collapsed ? "Pokaż Listę" : "Zwiń Listę";
        }

        private void ApplyThemeColorsToTextBox()
        {
            if (isDarkMode)
            {
                txtContent.BackColor = Color.FromArgb(30, 30, 30);
                txtContent.ForeColor = Color.FromArgb(212, 212, 212);
            }
            else
            {
                txtContent.BackColor = Color.White;
                txtContent.ForeColor = Color.Black;
            }
        }

        private void LoadSettingsFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        object fileVal = key.GetValue("LastDatasetFile");
                        if (fileVal != null) currentFilePath = fileVal.ToString();

                        object themeVal = key.GetValue("IsDarkMode");
                        if (themeVal != null) isDarkMode = Convert.ToBoolean(themeVal);

                        object formatVal = key.GetValue("IsFormattedView");
                        if (formatVal != null) isFormattedView = Convert.ToBoolean(formatVal);
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
                        key.SetValue("LastDatasetFile", currentFilePath);
                        key.SetValue("IsDarkMode", isDarkMode);
                        key.SetValue("IsFormattedView", isFormattedView);
                    }
                }
            }
            catch { }
        }

        // =========================================================
        // OBSŁUGA PLIKÓW I DANYCH
        // =========================================================

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Logi treningowe (*.jsonl)|*.jsonl|Wszystkie pliki (*.*)|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                currentFilePath = ofd.FileName;
                SaveSettingsToRegistry();
                LoadDataFromFile();
                lblStatus.Text = $"Wczytano plik: {Path.GetFileName(currentFilePath)}";
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath) || !File.Exists(currentFilePath))
            {
                MessageBox.Show("Brak pliku do odświeżenia lub plik został usunięty z dysku.");
                return;
            }
            LoadDataFromFile();
            lblStatus.Text = $"Odświeżono plik: {Path.GetFileName(currentFilePath)}";
        }

        private void LoadDataFromFile()
        {
            datasetLines = new List<string>(File.ReadAllLines(currentFilePath));
            RefreshList();
        }

        private void RefreshList()
        {
            int oldIndex = listEntries.SelectedIndex;
            listEntries.Items.Clear();
            for (int i = 0; i < datasetLines.Count; i++)
            {
                Match m = Regex.Match(datasetLines[i], @"\""role\""\s*:\s*\""user\""\s*,\s*\""content\""\s*:\s*\""([^\""]+)\""");
                string podglad = m.Success ? m.Groups[1].Value : "<Brak promptu user>";
                listEntries.Items.Add($"[{i + 1}] {podglad}");
            }

            if (oldIndex >= 0 && oldIndex < listEntries.Items.Count) listEntries.SelectedIndex = oldIndex;
            else txtContent.Clear();
        }

        // =========================================================
        // PARSOWANIE JSON: FORMATOWANIE I KOMPRESJA (MINIFY)
        // =========================================================

        private bool IsQuoteEscaped(string text, int quoteIndex)
        {
            int slashCount = 0;
            for (int i = quoteIndex - 1; i >= 0; i--)
            {
                if (text[i] == '\\') slashCount++;
                else break;
            }
            // Zwraca prawde, jesli liczba backslashy jest nieparzysta (czyli znak ucieczki chroni cudzyslow)
            return slashCount % 2 != 0;
        }

        private string FormatJsonLikeCode(string text)
        {
            int indent = 0;
            bool inString = false;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && !IsQuoteEscaped(text, i)) inString = !inString;

                if (!inString)
                {
                    if (c == '{' || c == '[')
                    {
                        sb.Append(c);
                        sb.Append("\r\n");
                        indent++;
                        sb.Append(new string(' ', indent * 3)); // Wcięcie na 3 spacje - czytelne i nierozwleczone
                    }
                    else if (c == '}' || c == ']')
                    {
                        sb.Append("\r\n");
                        indent--;
                        sb.Append(new string(' ', indent * 3));
                        sb.Append(c);
                    }
                    else if (c == ',')
                    {
                        sb.Append(c);
                        sb.Append("\r\n");
                        sb.Append(new string(' ', indent * 3));
                    }
                    else if (c == ':')
                    {
                        sb.Append(c);
                        sb.Append(" ");
                    }
                    else if (char.IsWhiteSpace(c)) { /* Ignorujemy zewnętrzne białe znaki */ }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            // Na koniec podmieniamy wirtualne znaki nowej linii w stringach na prawdziwe dla wygody edycji
            return sb.ToString().Replace("\\n", "\r\n");
        }

        private string MinifyJsonAndEscape(string text)
        {
            bool inString = false;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && !IsQuoteEscaped(text, i)) inString = !inString;

                if (!inString)
                {
                    // Poza stringami usuwamy wszystkie białe znaki, w tym zbudowane wcięcia!
                    if (!char.IsWhiteSpace(c)) sb.Append(c);
                }
                else
                {
                    // Wewnątrz stringa zamieniamy fizyczne wcisnięcie ENTER z powrotem na "\n" dla formatu JSONL
                    if (c == '\r') continue;
                    if (c == '\n') sb.Append("\\n");
                    else sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private void LoadLineToEditor(int index)
        {
            isFormatting = true;
            string linia = datasetLines[index];

            if (isFormattedView)
            {
                txtContent.Text = FormatJsonLikeCode(linia);
            }
            else
            {
                txtContent.Text = linia.Replace("\\n", "\r\n");
            }

            isFormatting = false;
            ApplySyntaxHighlighting();
        }

        private void ListEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listEntries.SelectedIndex >= 0 && listEntries.SelectedIndex < datasetLines.Count)
            {
                LoadLineToEditor(listEntries.SelectedIndex);
            }
        }

        // =========================================================
        // KOLOROWANIE SKŁADNI (SYNTAX HIGHLIGHTING)
        // =========================================================

        private bool isFormatting = false;

        private void TxtContent_TextChanged(object sender, EventArgs e)
        {
            if (isFormatting) return;
            ApplySyntaxHighlighting();
        }

        private void ApplySyntaxHighlighting()
        {
            if (string.IsNullOrEmpty(txtContent.Text)) return;

            isFormatting = true;
            SendMessage(txtContent.Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);

            int selStart = txtContent.SelectionStart;
            int selLen = txtContent.SelectionLength;

            Color colDefault = isDarkMode ? Color.FromArgb(212, 212, 212) : Color.Black;
            Color colString = isDarkMode ? Color.FromArgb(206, 145, 120) : Color.FromArgb(163, 21, 21);
            Color colNumberBool = isDarkMode ? Color.FromArgb(181, 206, 168) : Color.FromArgb(9, 134, 88);
            Color colKey = isDarkMode ? Color.FromArgb(156, 220, 254) : Color.FromArgb(4, 81, 165);
            Color colRole = isDarkMode ? Color.FromArgb(86, 156, 214) : Color.FromArgb(0, 0, 255);
            Color colTagPrefix = isDarkMode ? Color.FromArgb(197, 134, 192) : Color.FromArgb(128, 0, 128);
            Color colBraces = isDarkMode ? Color.FromArgb(220, 220, 170) : Color.FromArgb(139, 69, 19);
            Color colBrackets = isDarkMode ? Color.FromArgb(78, 201, 176) : Color.FromArgb(0, 128, 128);

            txtContent.SelectAll();
            txtContent.SelectionColor = colDefault;
            txtContent.SelectionFont = new Font("Consolas", 11f, FontStyle.Regular);

            HighlightRegex(@"\""(.*?)\""", colString, FontStyle.Regular);
            HighlightRegex(@"\b(true|false|null)\b", colNumberBool, FontStyle.Regular);
            HighlightRegex(@"\b\d+(\.\d+)?\b", colNumberBool, FontStyle.Regular);
            HighlightRegex(@"\""([^\""]+)\""(?=\s*:)", colKey, FontStyle.Regular);
            HighlightRegex(@"\""(user|assistant|system)\""", colRole, FontStyle.Bold);
            HighlightRegex(@"\{|\}", colBraces, FontStyle.Bold);
            HighlightRegex(@"\[|\]", colBrackets, FontStyle.Bold);
            HighlightRegex(@"\b(ACTION|SELECT|MSG|SYSTEM|LISP):", colTagPrefix, FontStyle.Bold);

            txtContent.SelectionStart = selStart;
            txtContent.SelectionLength = selLen;

            SendMessage(txtContent.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            txtContent.Invalidate();

            isFormatting = false;
        }

        private void HighlightRegex(string pattern, Color color, FontStyle style)
        {
            MatchCollection matches = Regex.Matches(txtContent.Text, pattern);
            foreach (Match m in matches)
            {
                txtContent.SelectionStart = m.Index;
                txtContent.SelectionLength = m.Length;
                txtContent.SelectionColor = color;
                if (style != FontStyle.Regular)
                    txtContent.SelectionFont = new Font("Consolas", 11f, style);
            }
        }

        // =========================================================
        // AKCJE I TESTOWANIE
        // =========================================================

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (listEntries.SelectedIndex >= 0)
            {
                // Magia: zawsze formatuje wpis w dół do bezpiecznej linii JSONL!
                string minified = MinifyJsonAndEscape(txtContent.Text);
                datasetLines[listEntries.SelectedIndex] = minified;
                lblStatus.Text = "Zaktualizowano wpis w pamięci (Pamiętaj o zapisie do pliku!).";

                // Odśwież widok na liście (nie ruszając aktualnego indeksu okna)
                int tempIdx = listEntries.SelectedIndex;
                isFormatting = true;
                RefreshList();
                listEntries.SelectedIndex = tempIdx;
                isFormatting = false;
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (listEntries.SelectedIndex >= 0)
            {
                datasetLines.RemoveAt(listEntries.SelectedIndex);
                RefreshList();
                lblStatus.Text = "Usunięto wpis z pamięci.";
            }
        }

        private void BtnSaveFile_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath)) return;
            try
            {
                File.WriteAllLines(currentFilePath, datasetLines);
                lblStatus.Text = $"Zapisano {datasetLines.Count} wpisów do: {Path.GetFileName(currentFilePath)}";
            }
            catch (Exception ex) { MessageBox.Show("Błąd zapisu: " + ex.Message); }
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtContent.Text)) return;

            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptSelectionResult psr = ed.SelectImplied();
            if (psr.Status == PromptStatus.OK && psr.Value != null)
            {
                Komendy.AktywneZaznaczenie = psr.Value.GetObjectIds();
                ed.WriteMessage($"\n[System] Przechwycono {Komendy.AktywneZaznaczenie.Length} zaznaczonych obiektów przed testem.");
            }

            using (DocumentLock loc = doc.LockDocument())
            {
                ed.WriteMessage($"\n\n--- INTELIGENTNE TESTOWANIE SEKWENCJI Z BAZY ---");

                string pelnyTekst = txtContent.Text;
                int currentIndex = 0;
                int wykonaneKroki = 0;

                while (currentIndex < pelnyTekst.Length)
                {
                    int idxAction = pelnyTekst.IndexOf("[ACTION:", currentIndex);
                    int idxSelect = pelnyTekst.IndexOf("[SELECT:", currentIndex);

                    int firstMatch = -1;
                    if (idxAction != -1 && idxSelect != -1) firstMatch = Math.Min(idxAction, idxSelect);
                    else if (idxAction != -1) firstMatch = idxAction;
                    else if (idxSelect != -1) firstMatch = idxSelect;

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

                    wykonaneKroki++;
                    ed.WriteMessage($"\n[Krok {wykonaneKroki} Wykonuję]: {tagDoWykonania}");

                    try
                    {
                        string wynik = TrainingStudio.WykonywaczTagow(doc, tagDoWykonania);
                        ed.WriteMessage($"\n[Wynik]: {wynik}");

                        if (wynik.StartsWith("WYNIK: Użytkownik") && !wynik.Contains("anulował"))
                        {
                            Match mExpected = Regex.Match(pelnyTekst.Substring(currentIndex), @"WYNIK: Użytkownik[^\r\n\""\\]*");
                            if (mExpected.Success)
                            {
                                string expectedLine = mExpected.Value;
                                int lastColonExpected = expectedLine.LastIndexOf(':');
                                int lastColonActual = wynik.LastIndexOf(':');

                                if (lastColonExpected != -1 && lastColonActual != -1)
                                {
                                    string staryWybor = expectedLine.Substring(lastColonExpected + 1).Trim();
                                    string nowyWybor = wynik.Substring(lastColonActual + 1).Trim();

                                    if (!string.IsNullOrEmpty(staryWybor) && staryWybor != nowyWybor)
                                    {
                                        int matchIdx = currentIndex + mExpected.Index;
                                        string head = pelnyTekst.Substring(0, matchIdx + mExpected.Length);
                                        string tail = pelnyTekst.Substring(matchIdx + mExpected.Length);

                                        tail = tail.Replace(staryWybor, nowyWybor);
                                        pelnyTekst = head + tail;
                                        pelnyTekst = pelnyTekst.Remove(matchIdx, mExpected.Length).Insert(matchIdx, wynik);

                                        ed.WriteMessage($"\n[Inteligentny Tester AI]: Zaktualizowano scenariusz -> Zmienną '{staryWybor}' podmieniono na '{nowyWybor}'.");

                                        isFormatting = true;
                                        txtContent.Text = pelnyTekst;
                                        isFormatting = false;
                                        ApplySyntaxHighlighting();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n[BŁĄD WYKONANIA]: {ex.Message}");
                    }
                }
                ed.WriteMessage($"\n-----------------------------------\nZakończono. Wykonano {wykonaneKroki} poleceń.");
            }
            lblStatus.Text = "Test zakończony. Sprawdź okno poleceń CAD.";
            Bricscad.ApplicationServices.Application.MainWindow.Focus();
        }
    }
}