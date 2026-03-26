using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public class DatasetManagerControl : UserControl
    {
        private ListBox listEntries;
        private TextBox txtContent;
        private Label lblStatus;
        private List<string> datasetLines = new List<string>();
        private string currentFilePath = "";

        public DatasetManagerControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9f);

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 150 };

            // --- GÓRA (LISTA) ---
            Panel panTop = new Panel { Dock = DockStyle.Fill };
            listEntries = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            listEntries.SelectedIndexChanged += ListEntries_SelectedIndexChanged;

            Panel panTopMenu = new Panel { Dock = DockStyle.Top, Height = 35, Padding = new Padding(5) };
            Button btnLoad = new Button { Text = "Wczytaj JSONL", Dock = DockStyle.Left, Width = 120 };
            btnLoad.Click += BtnLoad_Click;
            Button btnSaveFile = new Button { Text = "Zapisz do Pliku", Dock = DockStyle.Right, Width = 120, BackColor = Color.LightGreen };
            btnSaveFile.Click += BtnSaveFile_Click;

            panTopMenu.Controls.Add(btnLoad);
            panTopMenu.Controls.Add(btnSaveFile);
            panTop.Controls.Add(listEntries);
            panTop.Controls.Add(panTopMenu);

            // --- DÓŁ (EDYCJA I TEST) ---
            Panel panBottom = new Panel { Dock = DockStyle.Fill };
            txtContent = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10f) };

            Panel panBottomMenu = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5) };
            Button btnTest = new Button { Text = "TESTUJ TAGI", Dock = DockStyle.Left, Width = 120, BackColor = Color.LightSkyBlue };
            btnTest.Click += BtnTest_Click;
            Button btnUpdate = new Button { Text = "Zatwierdź Zmiany", Dock = DockStyle.Right, Width = 130 };
            btnUpdate.Click += BtnUpdate_Click;
            Button btnDelete = new Button { Text = "Usuń z Listy", Dock = DockStyle.Right, Width = 100, BackColor = Color.LightCoral };
            btnDelete.Click += BtnDelete_Click;

            panBottomMenu.Controls.Add(btnTest);
            panBottomMenu.Controls.Add(btnDelete); // Kolejność dockowania od prawej
            panBottomMenu.Controls.Add(btnUpdate);

            lblStatus = new Label { Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray };

            panBottom.Controls.Add(txtContent);
            panBottom.Controls.Add(panBottomMenu);
            panBottom.Controls.Add(lblStatus);

            split.Panel1.Controls.Add(panTop);
            split.Panel2.Controls.Add(panBottom);
            this.Controls.Add(split);
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Logi treningowe (*.jsonl)|*.jsonl|Wszystkie pliki (*.*)|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                currentFilePath = ofd.FileName;
                datasetLines = new List<string>(File.ReadAllLines(currentFilePath));
                RefreshList();
                lblStatus.Text = $"Wczytano plik: {Path.GetFileName(currentFilePath)} (Liczba wpisów: {datasetLines.Count})";
            }
        }

        private void RefreshList()
        {
            listEntries.Items.Clear();
            for (int i = 0; i < datasetLines.Count; i++)
            {
                // Wyciągamy na szybko polecenie użytkownika do ładnego wyświetlania na liście
                Match m = Regex.Match(datasetLines[i], @"\""role\""\s*:\s*\""user\""\s*,\s*\""content\""\s*:\s*\""([^\""]+)\""");
                string podglad = m.Success ? m.Groups[1].Value : "<Brak promptu user>";
                listEntries.Items.Add($"[{i + 1}] {podglad}");
            }
            txtContent.Clear();
        }

        private void ListEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listEntries.SelectedIndex >= 0 && listEntries.SelectedIndex < datasetLines.Count)
            {
                // Formatujemy ładnie do TextBoxa
                string linia = datasetLines[listEntries.SelectedIndex];
                // Rozbijamy escaped string dla lepszej czytelności w oknie
                txtContent.Text = linia.Replace("\\\"", "\"").Replace("\\n", "\r\n");
            }
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (listEntries.SelectedIndex >= 0)
            {
                // Z powrotem escapujemy do formatu JSONL
                string czystyTekst = txtContent.Text.Replace("\r\n", "\\n").Replace("\n", "\\n");
                // Uproszczone zabezpieczenie cudzysłowów (najlepiej działa, gdy edytujesz tylko treść tagów)
                datasetLines[listEntries.SelectedIndex] = czystyTekst;
                int sel = listEntries.SelectedIndex;
                RefreshList();
                listEntries.SelectedIndex = sel;
                lblStatus.Text = "Zaktualizowano wpis w pamięci (Pamiętaj o zapisie do pliku!).";
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

            // --- NOWY KOD: Przechwytujemy ręczne zaznaczenie z CAD-a przed testem ---
            PromptSelectionResult psr = ed.SelectImplied();
            if (psr.Status == PromptStatus.OK && psr.Value != null)
            {
                Komendy.AktywneZaznaczenie = psr.Value.GetObjectIds();
                ed.WriteMessage($"\n[System] Przechwycono {Komendy.AktywneZaznaczenie.Length} zaznaczonych obiektów przed testem.");
            }

            // Używamy Regex, aby wyłowić wszystkie tagi ukryte w tekście (SELECT, ACTION, itp.)
            MatchCollection znalezioneTagi = Regex.Matches(txtContent.Text, @"\[(SELECT|ACTION|GET_PROPERTIES|LISP)[^\]]*\]");

            if (znalezioneTagi.Count == 0)
            {
                MessageBox.Show("Nie znaleziono żadnych tagów do przetestowania w tym wpisie.");
                return;
            }

            // Blokujemy dokument dla interfejsu okienkowego!
            using (DocumentLock loc = doc.LockDocument())
            {
                ed.WriteMessage($"\n\n--- TESTOWANIE SEKWENCJI Z BAZY ({znalezioneTagi.Count} kroków) ---");

                foreach (Match m in znalezioneTagi)
                {
                    string tagDoWykonania = m.Value;
                    ed.WriteMessage($"\n[Wykonuję]: {tagDoWykonania}");

                    try
                    {
                        // Wywołujemy naszą publiczną metodę z TrainingStudio
                        string wynik = TrainingStudio.WykonywaczTagow(doc, tagDoWykonania);
                        ed.WriteMessage($"\n[Wynik]: {wynik}");
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n[BŁĄD]: {ex.Message}");
                    }
                }
                ed.WriteMessage("\n-----------------------------------\n");
            }
            lblStatus.Text = "Test zakończony. Sprawdź okno poleceń CAD.";
            Bricscad.ApplicationServices.Application.MainWindow.Focus(); // Cofa focus na BricsCAD, byś widział efekt
        }
    }
}