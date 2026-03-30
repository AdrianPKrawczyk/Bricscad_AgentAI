using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Application = Bricscad.ApplicationServices.Application;

namespace BricsCAD_Agent
{
    public class UserMacroControl : UserControl
    {
        private ListBox lstGlobal;
        private ListBox lstLocal;
        private Label lblLocalInfo;
        private TextBox txtGlobalPath;

        // Kontrolki Edytora
        private SplitContainer splitMain;
        private TextBox txtEditorName;
        private RichTextBox txtEditorCode;
        private ComboBox cmbEditorTarget;
        private Button btnToggleEditor;

        // Stan Edytora
        private MacroItem _editingItem = null;
        private bool _editingIsGlobal = true;

        // Kolory składni (Jasny motyw)
        private static readonly Color ColorJsonString = Color.FromArgb(163, 21, 21); // Ciemny czerwony
        private static readonly Color ColorJsonKey = Color.FromArgb(4, 81, 165); // Ciemny niebieski
        private static readonly Color ColorTagPrefix = Color.Purple;
        private static readonly Color ColorDefaultText = Color.Black;

        public UserMacroControl()
        {
            InitializeUI();
            RefreshLists();
            Application.DocumentManager.DocumentBecameCurrent += (s, e) => RefreshLists();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(5);

            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel2Collapsed = true // Domyślnie edytor jest ukryty!
            };

            // =========================================================
            // PANEL 1 (GÓRA): LISTY MAKR I USTAWIENIA
            // =========================================================
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 9, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 0: Label Global
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));   // 1: Lista Global
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 2: Btn Global
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 3: Label Local
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));   // 4: Lista Local
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 5: Btn Local
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 6: Btn Odśwież
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));  // 7: Ustawienia
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 8: Toggle Editor Btn

            layout.Controls.Add(new Label { Text = "🌍 Tagi Globalne:", Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 5) }, 0, 0);

            lstGlobal = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
            lstGlobal.DoubleClick += (s, e) => RunMacro(lstGlobal.SelectedItem as MacroItem);
            lstGlobal.SelectedIndexChanged += (s, e) => { if (lstGlobal.SelectedIndex != -1) { lstLocal.ClearSelected(); LoadToEditor(lstGlobal.SelectedItem as MacroItem, true); } };
            layout.Controls.Add(lstGlobal, 0, 1);

            Button btnRunGlobal = new Button { Text = "▶ Uruchom Globalne", Dock = DockStyle.Fill, BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 5, 0, 10) };
            btnRunGlobal.Click += (s, e) => RunMacro(lstGlobal.SelectedItem as MacroItem);
            layout.Controls.Add(btnRunGlobal, 0, 2);

            lblLocalInfo = new Label { Text = "📄 Tagi Rysunku:", Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 5) };
            layout.Controls.Add(lblLocalInfo, 0, 3);

            lstLocal = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
            lstLocal.DoubleClick += (s, e) => RunMacro(lstLocal.SelectedItem as MacroItem);
            lstLocal.SelectedIndexChanged += (s, e) => { if (lstLocal.SelectedIndex != -1) { lstGlobal.ClearSelected(); LoadToEditor(lstLocal.SelectedItem as MacroItem, false); } };
            layout.Controls.Add(lstLocal, 0, 4);

            Button btnRunLocal = new Button { Text = "▶ Uruchom z Rysunku", Dock = DockStyle.Fill, BackColor = Color.LightBlue, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 5, 0, 5) };
            btnRunLocal.Click += (s, e) => RunMacro(lstLocal.SelectedItem as MacroItem);
            layout.Controls.Add(btnRunLocal, 0, 5);

            Button btnRefresh = new Button { Text = "Odśwież Listy", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
            btnRefresh.Click += (s, e) => RefreshLists();
            layout.Controls.Add(btnRefresh, 0, 6);

            // RAMKA USTAWIEŃ
            GroupBox gbSettings = new GroupBox { Text = "⚙️ Plik Globalny", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8) };
            TableLayoutPanel setLay = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Padding = new Padding(2) };
            setLay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            setLay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35F));

            txtGlobalPath = new TextBox { Dock = DockStyle.Fill, Text = MacroManager.GlobalMacrosPath };
            Button btnBrw = new Button { Text = "...", Dock = DockStyle.Fill };
            btnBrw.Click += (s, e) => { OpenFileDialog ofd = new OpenFileDialog { Filter = "JSONL (*.jsonl)|*.jsonl|Wszystkie (*.*)|*.*", CheckFileExists = false }; if (ofd.ShowDialog() == DialogResult.OK) txtGlobalPath.Text = ofd.FileName; };
            Button btnSav = new Button { Text = "Zapisz Lokalizację", Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 0) };
            btnSav.Click += (s, e) => { MacroManager.GlobalMacrosPath = txtGlobalPath.Text; RefreshLists(); MessageBox.Show("Zapisano ścieżkę!", "Agent AI"); };

            setLay.Controls.Add(txtGlobalPath, 0, 0); setLay.Controls.Add(btnBrw, 1, 0); setLay.Controls.Add(btnSav, 0, 1);
            setLay.SetColumnSpan(btnSav, 2);
            gbSettings.Controls.Add(setLay);
            layout.Controls.Add(gbSettings, 0, 7);

            // PRZYCISK POKAŻ/UKRYJ EDYTOR
            btnToggleEditor = new Button { Text = "▼ Pokaż Edytor Makr ▼", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.LightGray, Margin = new Padding(0, 5, 0, 0) };
            btnToggleEditor.Click += (s, e) => ToggleEditor();
            layout.Controls.Add(btnToggleEditor, 0, 8);

            splitMain.Panel1.Controls.Add(layout);


            // =========================================================
            // PANEL 2 (DÓŁ): EDYTOR KODU (ULEPSZONY)
            // =========================================================
            TableLayoutPanel editorLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(0, 5, 0, 0) };
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));

            // Pasek nazwy i wyboru bazy
            TableLayoutPanel nameLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, Margin = new Padding(0) };
            nameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            nameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85F));

            txtEditorName = new TextBox { Dock = DockStyle.Fill, Text = "Nazwa_Makra" };
            cmbEditorTarget = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            cmbEditorTarget.Items.Add("Globalne"); cmbEditorTarget.Items.Add("Rysunek"); cmbEditorTarget.SelectedIndex = 0;

            nameLayout.Controls.Add(txtEditorName, 0, 0);
            nameLayout.Controls.Add(cmbEditorTarget, 1, 0);
            editorLayout.Controls.Add(nameLayout, 0, 0);

            // Pole na kod (RichTextBox) - NAPRAWIONE WIDOCZNOŚĆ I SCROLLBARE
            txtEditorCode = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.5f),
                Text = "[Kliknij makro lub stwórz nowe]",
                Multiline = true,
                WordWrap = false, // Wyłączamy zawijanie, by widzieć długie linie JSON
                ScrollBars = RichTextBoxScrollBars.Both, // Włączamy paski przewijania (poziomy i pionowy)
                AcceptsTab = true
            };
            // Podpinamy kolorowanie składni przy zmianie tekstu
            txtEditorCode.TextChanged += TxtEditorCode_TextChanged;
            editorLayout.Controls.Add(txtEditorCode, 0, 1);

            // Dolne przyciski
            TableLayoutPanel btnLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 3, Margin = new Padding(0) };
            btnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            btnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            btnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));

            Button btnNew = new Button { Text = "Nowe", Dock = DockStyle.Fill, BackColor = Color.LightSkyBlue, FlatStyle = FlatStyle.Flat };
            btnNew.Click += (s, e) => { _editingItem = null; txtEditorName.Text = "Nowe_Makro"; txtEditorCode.Text = "[ACTION: ...]"; lstGlobal.ClearSelected(); lstLocal.ClearSelected(); };

            Button btnSave = new Button { Text = "Zapisz", Dock = DockStyle.Fill, BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnSave.Click += (s, e) => SaveEditor();

            Button btnDelete = new Button { Text = "Usuń", Dock = DockStyle.Fill, BackColor = Color.LightCoral, FlatStyle = FlatStyle.Flat };
            btnDelete.Click += (s, e) => DeleteEditor();

            btnLayout.Controls.Add(btnNew, 0, 0); btnLayout.Controls.Add(btnSave, 1, 0); btnLayout.Controls.Add(btnDelete, 2, 0);
            editorLayout.Controls.Add(btnLayout, 0, 2);

            splitMain.Panel2.Controls.Add(editorLayout);
            this.Controls.Add(splitMain);
        }

        private void ToggleEditor()
        {
            splitMain.Panel2Collapsed = !splitMain.Panel2Collapsed;
            btnToggleEditor.Text = splitMain.Panel2Collapsed ? "▼ Pokaż Edytor Makr ▼" : "▲ Ukryj Edytor Makr ▲";

            if (!splitMain.Panel2Collapsed)
            {
                // Ustawiamy wysokość edytora na około połowę panelu
                splitMain.SplitterDistance = (int)(this.Height * 0.55);
            }
        }

        private void LoadToEditor(MacroItem item, bool isGlobal)
        {
            if (item == null) return;
            _editingItem = item;
            _editingIsGlobal = isGlobal;

            txtEditorName.Text = item.Name;
            txtEditorCode.Text = item.Tag; // TextChanged automatycznie nałoży formatowanie
            cmbEditorTarget.SelectedIndex = isGlobal ? 0 : 1;

            // NAPRAWIONE: Usunięto automatyczne otwieranie edytora, jeśli jest schowany.
        }

        private void SaveEditor()
        {
            if (string.IsNullOrWhiteSpace(txtEditorName.Text) || string.IsNullOrWhiteSpace(txtEditorCode.Text)) return;

            bool saveToGlobal = cmbEditorTarget.SelectedIndex == 0;
            string path = saveToGlobal ? MacroManager.GlobalMacrosPath : MacroManager.GetLocalMacrosPath(Application.DocumentManager.MdiActiveDocument);

            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Aby zapisać lokalnie, musisz najpierw zapisać rysunek DWG na dysku.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<MacroItem> macros = MacroManager.LoadMacros(path);

            if (_editingItem != null && _editingIsGlobal == saveToGlobal)
            {
                var existing = macros.Find(m => m.Name == _editingItem.Name);
                if (existing != null) { existing.Name = txtEditorName.Text; existing.Tag = txtEditorCode.Text; }
                else macros.Add(new MacroItem { Name = txtEditorName.Text, Tag = txtEditorCode.Text });
            }
            else
            {
                // Dodawanie jako nowe
                macros.Add(new MacroItem { Name = txtEditorName.Text, Tag = txtEditorCode.Text });
            }

            MacroManager.SaveAllMacros(path, macros);

            _editingItem = new MacroItem { Name = txtEditorName.Text, Tag = txtEditorCode.Text };
            _editingIsGlobal = saveToGlobal;

            RefreshLists();
            MessageBox.Show("Zapisano pomyślnie!", "Zapis Makra", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DeleteEditor()
        {
            if (_editingItem == null) return;
            if (MessageBox.Show($"Czy usunąć makro '{_editingItem.Name}'?", "Usuwanie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No) return;

            string path = _editingIsGlobal ? MacroManager.GlobalMacrosPath : MacroManager.GetLocalMacrosPath(Application.DocumentManager.MdiActiveDocument);
            if (string.IsNullOrEmpty(path)) return;

            List<MacroItem> macros = MacroManager.LoadMacros(path);
            macros.RemoveAll(m => m.Name == _editingItem.Name);
            MacroManager.SaveAllMacros(path, macros);

            _editingItem = null;
            txtEditorName.Text = "";
            txtEditorCode.Text = "";
            RefreshLists();
        }

        public void RefreshLists()
        {
            if (this.InvokeRequired) { this.Invoke(new Action(RefreshLists)); return; }

            lstGlobal.Items.Clear();
            foreach (var m in MacroManager.LoadMacros(MacroManager.GlobalMacrosPath)) lstGlobal.Items.Add(m);

            lstLocal.Items.Clear();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            string localPath = MacroManager.GetLocalMacrosPath(doc);

            if (string.IsNullOrEmpty(localPath)) lblLocalInfo.Text = "📄 Tagi Rysunku (Zapisz DWG, aby włączyć!):";
            else
            {
                lblLocalInfo.Text = $"📄 Tagi Rysunku ({Path.GetFileName(localPath)}):";
                foreach (var m in MacroManager.LoadMacros(localPath)) lstLocal.Items.Add(m);
            }
        }

        private void RunMacro(MacroItem item)
        {
            if (item == null) return;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            using (doc.LockDocument())
            {
                doc.Editor.WriteMessage($"\n\n--- URUCHAMIAM MAKRO: {item.Name} ---");
                try
                {
                    string input = item.Tag;
                    int currentIndex = 0; int licznik = 0;

                    while (currentIndex < input.Length)
                    {
                        int startIdx = -1;
                        string[] tagTypes = { "[ACTION:", "[SELECT:", "[LISP:" };
                        foreach (var t in tagTypes)
                        {
                            int found = input.IndexOf(t, currentIndex);
                            if (found != -1 && (startIdx == -1 || found < startIdx)) startIdx = found;
                        }
                        if (startIdx == -1) break;

                        int bracketCount = 0; int endIdx = -1;
                        for (int i = startIdx; i < input.Length; i++)
                        {
                            if (input[i] == '[') bracketCount++;
                            else if (input[i] == ']') bracketCount--;
                            if (bracketCount == 0) { endIdx = i; break; }
                        }
                        if (endIdx == -1) break;

                        string czystyTag = input.Substring(startIdx, endIdx - startIdx + 1);
                        currentIndex = endIdx + 1;
                        licznik++;

                        doc.Editor.WriteMessage($"\n[Krok {licznik}]: {czystyTag}");
                        string wynik = TrainingStudio.WykonywaczTagow(doc, czystyTag);
                        doc.Editor.WriteMessage($"\n[Wynik]: {wynik}");
                    }
                    doc.Editor.WriteMessage($"\n--- Zakończono ({licznik} kroków) ---\n");
                }
                catch (Exception ex) { doc.Editor.WriteMessage($"\n[BŁĄD KRYTYCZNY]: {ex.Message}\n"); }
            }
            Application.MainWindow.Focus();
        }

        // --- KOLOROWANIE SKŁADNI (LOGIKA EDYTORA) ---
        private void TxtEditorCode_TextChanged(object sender, EventArgs e)
        {
            ApplySyntaxHighlighting();
        }

        private void ApplySyntaxHighlighting()
        {
            if (string.IsNullOrEmpty(txtEditorCode.Text)) return;

            // Zapamiętujemy pozycję kursora
            int selStart = txtEditorCode.SelectionStart;
            int selLen = txtEditorCode.SelectionLength;

            // Resetujemy formatowanie
            txtEditorCode.SelectAll();
            txtEditorCode.SelectionColor = ColorDefaultText;
            txtEditorCode.SelectionFont = txtEditorCode.Font;

            // 1. Kolorowanie wartości tekstowych JSON: "wartość"
            MatchCollection strings = Regex.Matches(txtEditorCode.Text, @"\""(.*?)\""");
            foreach (Match m in strings)
            {
                txtEditorCode.SelectionStart = m.Index;
                txtEditorCode.SelectionLength = m.Length;
                txtEditorCode.SelectionColor = ColorJsonString;
            }

            // 2. Kolorowanie kluczy JSON: "klucz":
            MatchCollection keys = Regex.Matches(txtEditorCode.Text, @"\""([^\""]+)\""(?=\s*:)");
            foreach (Match m in keys)
            {
                txtEditorCode.SelectionStart = m.Index;
                txtEditorCode.SelectionLength = m.Length;
                txtEditorCode.SelectionColor = ColorJsonKey;
            }

            // 3. Kolorowanie głównych prefixów tagów (ACTION, SELECT, MSG, SYSTEM, LISP)
            MatchCollection tags = Regex.Matches(txtEditorCode.Text, @"\b(ACTION|SELECT|MSG|SYSTEM|LISP):");
            foreach (Match m in tags)
            {
                txtEditorCode.SelectionStart = m.Index;
                txtEditorCode.SelectionLength = m.Length;
                txtEditorCode.SelectionColor = ColorTagPrefix;
                txtEditorCode.SelectionFont = new Font(txtEditorCode.Font, FontStyle.Bold);
            }

            // Przywracamy pozycję kursora
            txtEditorCode.SelectionStart = selStart;
            txtEditorCode.SelectionLength = selLen;
            txtEditorCode.SelectionColor = ColorDefaultText; // Reset koloru dla nowego pisania
        }
    }
}