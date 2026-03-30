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

        // --- NOWOŚĆ: Zbiór pamiętający indeksy wpisów z błędami ---
        private HashSet<int> invalidEntries = new HashSet<int>();

        // Zmienne ustawień
        private bool isDarkMode = true;
        private bool isFormattedView = true;

        // Przyciski do interakcji
        private Button btnThemeToggle;
        private Button btnFormatToggle;
        private Button btnViewToggle;
        private Button btnCopyToExamples;


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

            FlowLayoutPanel panTopMenu = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 35, Padding = new Padding(2), WrapContents = false };

            Button btnLoad = new Button { Text = "Wczytaj JSONL", Width = 100, Height = 28 };
            btnLoad.Click += BtnLoad_Click;

            Button btnRefresh = new Button { Text = "Odśwież", Width = 70, Height = 28 };
            btnRefresh.Click += BtnRefresh_Click;

            // --- NOWOŚĆ: Przycisk globalnego sprawdzania bazy ---
            Button btnValidateAll = new Button { Text = "Sprawdź Całą Bazę", Width = 130, Height = 28, BackColor = Color.Khaki };
            btnValidateAll.Click += BtnValidateAll_Click;

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
            panTopMenu.Controls.Add(new Label { Width = 10 });
            panTopMenu.Controls.Add(btnRefresh);
            panTopMenu.Controls.Add(btnValidateAll); // Dodany do paska
            panTopMenu.Controls.Add(btnThemeToggle);
            panTopMenu.Controls.Add(btnFormatToggle);
            panTopMenu.Controls.Add(btnViewToggle);

            split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 150 };

            // --- NOWOŚĆ: Ręczne rysowanie ListBoxa (OwnerDraw), aby móc kolorować pojedyncze wpisy! ---
            listEntries = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 28
            };
            listEntries.DrawItem += ListEntries_DrawItem;
            listEntries.SelectedIndexChanged += ListEntries_SelectedIndexChanged;
            split.Panel1.Controls.Add(listEntries);

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

            Button btnValidateTags = new Button { Text = "Sprawdź Składnię", Dock = DockStyle.Left, Width = 120, BackColor = Color.Khaki };
            btnValidateTags.Click += BtnValidateTags_Click;

            // --- NOWE PRZYCISKI ---
            Button btnAddEmpty = new Button { Text = "Dodaj Pusty", Dock = DockStyle.Left, Width = 100, BackColor = Color.LightGreen };
            btnAddEmpty.Click += BtnAddEmpty_Click;

            Button btnClone = new Button { Text = "Klonuj Wpis", Dock = DockStyle.Left, Width = 100, BackColor = Color.Plum };
            btnClone.Click += BtnClone_Click;

            // --- NASZ NOWY PRZYCISK: Kopiuj do przykładów ---
            Button btnCopyToExamples = new Button { Text = "Kopiuj do przykładów", Dock = DockStyle.Left, Width = 140, BackColor = Color.PaleTurquoise };
            btnCopyToExamples.Click += BtnCopyToExamples_Click;
            // ----------------------

            Button btnUpdate = new Button { Text = "Zatwierdź Zmiany", Dock = DockStyle.Right, Width = 130 };
            btnUpdate.Click += BtnUpdate_Click;

            Button btnDelete = new Button { Text = "Usuń z Listy", Dock = DockStyle.Right, Width = 100, BackColor = Color.LightCoral };
            btnDelete.Click += BtnDelete_Click;

            panBottomMenu.Controls.Add(btnTest);
            panBottomMenu.Controls.Add(btnValidateTags);
            panBottomMenu.Controls.Add(btnAddEmpty);
            panBottomMenu.Controls.Add(btnClone);
            panBottomMenu.Controls.Add(btnCopyToExamples); // <--- Dodajemy do panelu, dokuje do lewej
            panBottomMenu.Controls.Add(btnDelete);
            panBottomMenu.Controls.Add(btnUpdate);

            lblStatus = new Label { Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray };

            panBottom.Controls.Add(txtContent);
            panBottom.Controls.Add(panBottomMenu);
            panBottom.Controls.Add(lblStatus);
            split.Panel2.Controls.Add(panBottom);

            this.Controls.Add(split);
            this.Controls.Add(panTopMenu);
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
            listEntries.Invalidate(); // Odśwież tło listy
        }

        private void BtnFormatToggle_Click(object sender, EventArgs e)
        {
            isFormattedView = !isFormattedView;
            btnFormatToggle.Text = isFormattedView ? "Formatuj: Wł" : "Formatuj: Wył";
            SaveSettingsToRegistry();

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
                if (listEntries != null)
                {
                    listEntries.BackColor = Color.FromArgb(45, 45, 48);
                    listEntries.ForeColor = Color.FromArgb(212, 212, 212);
                }
            }
            else
            {
                txtContent.BackColor = Color.White;
                txtContent.ForeColor = Color.Black;
                if (listEntries != null)
                {
                    listEntries.BackColor = Color.White;
                    listEntries.ForeColor = Color.Black;
                }
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
            invalidEntries.Clear(); // Przy nowym pliku czyścimy błędy
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

        // --- NOWOŚĆ: Logika rysowania elementów listy (Kolorowanie na czerwono) ---
        private void ListEntries_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= listEntries.Items.Count) return;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool isInvalid = invalidEntries.Contains(e.Index);

            Color backColor;
            Color foreColor;

            if (isSelected)
            {
                backColor = isDarkMode ? Color.FromArgb(0, 122, 204) : SystemColors.Highlight;
                foreColor = Color.White;
            }
            else if (isInvalid)
            {
                // Kolor czerwony dla błędnego tagu!
                backColor = Color.FromArgb(220, 53, 69);
                foreColor = Color.White;
            }
            else
            {
                backColor = listEntries.BackColor;
                foreColor = listEntries.ForeColor;
            }

            using (SolidBrush bgBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            using (SolidBrush textBrush = new SolidBrush(foreColor))
            {
                // Używamy StringFormat do idealnego wyśrodkowania tekstu w pionie, niezależnie od DPI ekranu!
                using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                {
                    Rectangle textRect = new Rectangle(e.Bounds.Left + 5, e.Bounds.Top, e.Bounds.Width - 10, e.Bounds.Height);
                    e.Graphics.DrawString(listEntries.Items[e.Index].ToString(), e.Font, textBrush, textRect, sf);
                }
            }
            e.DrawFocusRectangle();
        }

        // =========================================================
        // NOWE FUNKCJE: DODAWANIE I KLONOWANIE
        // =========================================================
        private void BtnAddEmpty_Click(object sender, EventArgs e)
        {
            // Generujemy czysty, poprawny szablon JSONL dla jednej interakcji
            string emptyTemplate = "{\"messages\": [{\"role\": \"user\", \"content\": \"Wpisz polecenie użytkownika...\"}, {\"role\": \"assistant\", \"content\": \"[ACTION:TWOJ_TAG_TUTAJ]\"}]}";

            datasetLines.Add(emptyTemplate);

            RefreshList();

            // Przewijamy listę na sam dół i zaznaczamy nowy wpis
            listEntries.SelectedIndex = datasetLines.Count - 1;
            lblStatus.Text = "Dodano nowy, pusty wpis na końcu listy. Pamiętaj o zapisie!";
            txtContent.Focus(); // Kursor od razu ląduje w polu tekstowym
        }

        private void BtnClone_Click(object sender, EventArgs e)
        {
            if (listEntries.SelectedIndex >= 0)
            {
                int currentIdx = listEntries.SelectedIndex;
                string clonedLine = datasetLines[currentIdx];

                int newIdx = currentIdx + 1;

                // Wstawiamy sklonowany wpis dokładnie POD zaznaczonym elementem
                datasetLines.Insert(newIdx, clonedLine);

                // --- INTELIGENTNE PRZESUWANIE KOLORÓW (BŁĘDÓW) ---
                // Ponieważ wcisnęliśmy nowy element w środek listy, musimy przesunąć
                // indeksy ewentualnych czerwonych podświetleń błędów, żeby się nie rozjechały!
                HashSet<int> newInvalids = new HashSet<int>();
                foreach (int idx in invalidEntries)
                {
                    if (idx < newIdx) newInvalids.Add(idx);
                    else newInvalids.Add(idx + 1); // Przesuwamy w dół wszystko, co jest pod spodem
                }

                // Jeśli klonowaliśmy wpis, który JUŻ MIEŁ błąd, klon też musi świecić na czerwono
                if (invalidEntries.Contains(currentIdx)) newInvalids.Add(newIdx);

                invalidEntries = newInvalids;

                RefreshList();
                listEntries.SelectedIndex = newIdx; // Program automatycznie przeskakuje na klona
                lblStatus.Text = "Pomyślnie sklonowano wpis.";
            }
            else
            {
                MessageBox.Show("Najpierw zaznacz wpis na liście po lewej stronie, aby móc go sklonować.", "Brak zaznaczenia", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }


        // =========================================================
        // PARSOWANIE JSON I KOLOROWANIE
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
                        sb.Append(c); sb.Append("\r\n"); indent++; sb.Append(new string(' ', indent * 3));
                    }
                    else if (c == '}' || c == ']')
                    {
                        sb.Append("\r\n"); indent--; sb.Append(new string(' ', indent * 3)); sb.Append(c);
                    }
                    else if (c == ',')
                    {
                        sb.Append(c); sb.Append("\r\n"); sb.Append(new string(' ', indent * 3));
                    }
                    else if (c == ':') { sb.Append(c); sb.Append(" "); }
                    else if (!char.IsWhiteSpace(c)) sb.Append(c);
                }
                else sb.Append(c);
            }
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

        private void LoadLineToEditor(int index)
        {
            isFormatting = true;
            string linia = datasetLines[index];
            if (isFormattedView) txtContent.Text = FormatJsonLikeCode(linia);
            else txtContent.Text = linia.Replace("\\n", "\r\n");
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
                if (style != FontStyle.Regular) txtContent.SelectionFont = new Font("Consolas", 11f, style);
            }
        }

        // =========================================================
        // AKCJE I WALIDACJA
        // =========================================================

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (listEntries.SelectedIndex >= 0)
            {
                string minified = MinifyJsonAndEscape(txtContent.Text);
                datasetLines[listEntries.SelectedIndex] = minified;
                lblStatus.Text = "Zaktualizowano wpis w pamięci (Pamiętaj o zapisie do pliku!).";

                // --- NOWOŚĆ: Po zatwierdzeniu zmian, sprawdź, czy usunąć czerwony kolor ---
                if (TagValidator.ValidateSequence(minified).Count == 0) invalidEntries.Remove(listEntries.SelectedIndex);
                else invalidEntries.Add(listEntries.SelectedIndex);

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
                int deletedIdx = listEntries.SelectedIndex;
                datasetLines.RemoveAt(deletedIdx);

                // --- NOWOŚĆ: Przesuwanie czerwonych oznaczeń po usunięciu wpisu ---
                HashSet<int> newInvalids = new HashSet<int>();
                foreach (int idx in invalidEntries)
                {
                    if (idx < deletedIdx) newInvalids.Add(idx);
                    else if (idx > deletedIdx) newInvalids.Add(idx - 1);
                }
                invalidEntries = newInvalids;

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

        private void BtnValidateTags_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtContent.Text)) return;

            List<string> errs = TagValidator.ValidateSequence(txtContent.Text);

            if (errs.Count == 0)
            {
                if (listEntries.SelectedIndex >= 0) { invalidEntries.Remove(listEntries.SelectedIndex); listEntries.Invalidate(); }
                MessageBox.Show("Świetnie! Składnia jest poprawna, a wszystkie klasy i właściwości istnieją w bazie API.", "Sprawdzenie Tagów", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Walidator: Składnia poprawna.";
            }
            else
            {
                if (listEntries.SelectedIndex >= 0) { invalidEntries.Add(listEntries.SelectedIndex); listEntries.Invalidate(); }
                string msg = "Znaleziono potencjalne błędy lub halucynacje LLM:\n\n" + string.Join("\n", errs);
                MessageBox.Show(msg, "Błędy w Składni", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                lblStatus.Text = $"Walidator: Znaleziono {errs.Count} problemów.";
            }
        }

        // --- NOWOŚĆ: Globalne sprawdzenie całego pliku JSONL ---
        private void BtnValidateAll_Click(object sender, EventArgs e)
        {
            if (datasetLines.Count == 0) return;

            invalidEntries.Clear();
            int errorCount = 0;

            for (int i = 0; i < datasetLines.Count; i++)
            {
                var errors = TagValidator.ValidateSequence(datasetLines[i]);
                if (errors.Count > 0)
                {
                    invalidEntries.Add(i);
                    errorCount += errors.Count;
                }
            }

            listEntries.Invalidate(); // Odrysowuje wszystkie pozycje z nowymi kolorami!

            if (invalidEntries.Count == 0)
            {
                MessageBox.Show("Świetnie! Cała baza (plik JSONL) jest w 100% poprawna. Brak halucynacji i błędów składni.", "Globalne Sprawdzenie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Walidacja globalna: Składnia w całej bazie poprawna.";
            }
            else
            {
                MessageBox.Show($"Znaleziono błędy w {invalidEntries.Count} wpisach (łącznie {errorCount} problemów).\n\nBłędne pozycje zostały podświetlone na CZERWONO na liście po lewej stronie.", "Błędy w Bazie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                lblStatus.Text = $"Walidacja globalna: Znaleziono błędy w {invalidEntries.Count} wpisach.";
            }
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
                ed.WriteMessage($"\n\n--- INTELIGENTNE TESTOWANIE SEKWENCJI ---");
                string input = txtContent.Text;
                int licznikPolecen = 0;

                int currentIndex = 0;
                while (currentIndex < input.Length)
                {
                    // Szukamy początku dowolnego znanego tagu prosto w tekście
                    int startIdx = -1;
                    string[] tagTypes = { "[ACTION:", "[SELECT:", "[LISP:" };

                    foreach (var t in tagTypes)
                    {
                        int found = input.IndexOf(t, currentIndex);
                        if (found != -1 && (startIdx == -1 || found < startIdx))
                        {
                            startIdx = found;
                        }
                    }

                    if (startIdx == -1) break; // Brak więcej tagów

                    // Szukamy domykającego nawiasu dla tego konkretnego tagu (uwzględniając zagnieżdżenie)
                    int bracketCount = 0;
                    int endIdx = -1;
                    for (int i = startIdx; i < input.Length; i++)
                    {
                        if (input[i] == '[') bracketCount++;
                        else if (input[i] == ']') bracketCount--;

                        if (bracketCount == 0)
                        {
                            endIdx = i;
                            break;
                        }
                    }

                    if (endIdx == -1) break;

                    string surowyTag = input.Substring(startIdx, endIdx - startIdx + 1);
                    currentIndex = endIdx + 1; // Przesuwamy wskaźnik ZA wykonany tag

                    // ==============================================================
                    // PANCERNE CZYSZCZENIE:
                    // Zdejmujemy wszelkie JSON-owe escape characters (\") z wewnątrz tagu
                    // Dzięki temu tag testowany staje się w 100% zgodny z tym z CADa
                    // ==============================================================
                    string czystyTag = surowyTag.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");

                    licznikPolecen++;
                    ed.WriteMessage($"\n[Krok {licznikPolecen}]: {czystyTag}");

                    try
                    {
                        // Wykonujemy wyczyszczony tag
                        string wynik = BricsCAD_Agent.TrainingStudio.WykonywaczTagow(doc, czystyTag);
                        ed.WriteMessage($"\n[Wynik]: {wynik}");
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n[BŁĄD]: {ex.Message}");
                    }
                }
                ed.WriteMessage($"\n-----------------------------------\nZakończono. Wykonano {licznikPolecen} poleceń.");
            }
            lblStatus.Text = "Test zakończony. Sprawdź okno poleceń CAD.";
            Bricscad.ApplicationServices.Application.MainWindow.Focus();
        }

        // ==========================================================
        // --- KOPIOWANIE TAGU DO PLIKU PRZYKŁADÓW (IN-CONTEXT) ---
        // ==========================================================
        private void BtnCopyToExamples_Click(object sender, EventArgs e)
        {
            if (listEntries.SelectedIndex < 0)
            {
                MessageBox.Show("Wybierz najpierw tag z listy, który chcesz skopiować!", "Brak zaznaczenia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string selectedLine = datasetLines[listEntries.SelectedIndex];

                // 1. ŚCIEŻKA ROBOCZA (Tam, gdzie wtyczka DLL aktualnie "żyje" w pamięci)
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string currentDir = System.IO.Path.GetDirectoryName(dllPath);
                string pathJsonl = System.IO.Path.Combine(currentDir, "Agent_Example_Data.jsonl");

                // AppendAllText automatycznie utworzy plik, jeśli go nie ma
                System.IO.File.AppendAllText(pathJsonl, selectedLine + Environment.NewLine, System.Text.Encoding.UTF8);

                string wiadomoscSukcesu = "Zapisano roboczo w:\n" + pathJsonl;

                // 2. ŚCIEŻKA ŹRÓDŁOWA (Tropiciel - szuka głównego folderu projektu z kodem)
                string sourceDir = currentDir;
                bool znalezionoZrodlo = false;

                // Cofamy się w górę drzewa katalogów tak długo, aż znajdziemy plik AgentCommand.cs
                while (sourceDir != null)
                {
                    if (System.IO.File.Exists(System.IO.Path.Combine(sourceDir, "AgentCommand.cs")))
                    {
                        znalezionoZrodlo = true;
                        break;
                    }
                    sourceDir = System.IO.Path.GetDirectoryName(sourceDir);
                }

                if (znalezionoZrodlo)
                {
                    string sourcePathJsonl = System.IO.Path.Combine(sourceDir, "Agent_Example_Data.jsonl");
                    System.IO.File.AppendAllText(sourcePathJsonl, selectedLine + Environment.NewLine, System.Text.Encoding.UTF8);
                    wiadomoscSukcesu += "\n\nZapisano źródłowo w:\n" + sourcePathJsonl;
                }
                else
                {
                    wiadomoscSukcesu += "\n\n(UWAGA: Nie odnaleziono folderu źródłowego z kodem!)";
                }

                if (lblStatus != null)
                {
                    lblStatus.Text = "Sukces: Dodano tag do Agent_Example_Data.jsonl!";
                    lblStatus.ForeColor = Color.LimeGreen;
                }

                // Pokazujemy dokładne ścieżki w okienku!
                MessageBox.Show(wiadomoscSukcesu, "Raport Kopiowania Tagu", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas kopiowania: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}