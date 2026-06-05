using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using System.IO;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.UI.KnowledgeBase
{
    public class KnowledgeBaseControl : UserControl
    {
        private TabControl mainTabControl;
        private TabPage tabPageFormulas;
        private TabPage tabPageMacros;

        // --- Formulas ---
        private ListBox lstFormulas;
        private RichTextBox rtbFormulaMetadata;
        private RichTextBox rtbFormulaCode;
        private Button btnReloadFormulas;
        private Button btnAddFormula;
        private Button btnDeleteFormula;
        private Button btnSaveFormula;

        // --- Macros ---
        private ListBox lstMacros;
        private RichTextBox rtbMacroJson;
        private Button btnExecuteMacro;
        private Button btnAddMacro;
        private Button btnDeleteMacro;
        private Button btnSaveMacro;

        public KnowledgeBaseControl()
        {
            InitializeComponent();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;

            mainTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                ItemSize = new Size(150, 30),
                Font = new Font("Segoe UI", 9.5f)
            };

            // =========================
            // Zakładka: Formuły (Roslyn)
            // =========================
            tabPageFormulas = new TabPage("🧠 Formuły Inżynierskie (Roslyn)");

            Panel pnlFormulasLeft = new Panel { Dock = DockStyle.Left, Width = 250 };
            lstFormulas = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
            lstFormulas.SelectedIndexChanged += LstFormulas_SelectedIndexChanged;
            
            Panel pnlFormulasLeftBtns = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            btnAddFormula = new Button { Text = "➕ Dodaj", Left = 5, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnAddFormula.Click += BtnAddFormula_Click;
            btnDeleteFormula = new Button { Text = "🗑️ Usuń", Left = 125, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnDeleteFormula.Click += BtnDeleteFormula_Click;
            pnlFormulasLeftBtns.Controls.Add(btnAddFormula);
            pnlFormulasLeftBtns.Controls.Add(btnDeleteFormula);

            btnReloadFormulas = new Button { Text = "🔄 Przeładuj Bazy (Hot Reload)", Dock = DockStyle.Bottom, Height = 40, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnReloadFormulas.Click += BtnReloadFormulas_Click;
            
            pnlFormulasLeft.Controls.Add(lstFormulas);
            pnlFormulasLeft.Controls.Add(pnlFormulasLeftBtns);
            pnlFormulasLeft.Controls.Add(btnReloadFormulas);

            Panel pnlFormulasRight = new Panel { Dock = DockStyle.Fill };
            
            SplitContainer splitFormulasRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            
            rtbFormulaMetadata = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = false, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
            Label lblMeta = new Label { Text = "Metadane (JSON):", Dock = DockStyle.Top, Height = 20, ForeColor = Color.LightGray };
            splitFormulasRight.Panel1.Controls.Add(rtbFormulaMetadata);
            splitFormulasRight.Panel1.Controls.Add(lblMeta);

            rtbFormulaCode = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = false, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
            Label lblCode = new Label { Text = "Kod wykonywalny (CSX):", Dock = DockStyle.Top, Height = 20, ForeColor = Color.LightGray };
            splitFormulasRight.Panel2.Controls.Add(rtbFormulaCode);
            splitFormulasRight.Panel2.Controls.Add(lblCode);

            Panel pnlFormulasRightBtns = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnSaveFormula = new Button { Text = "💾 Zapisz Formułę", Dock = DockStyle.Right, Width = 150, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSaveFormula.Click += BtnSaveFormula_Click;
            pnlFormulasRightBtns.Controls.Add(btnSaveFormula);

            pnlFormulasRight.Controls.Add(splitFormulasRight);
            pnlFormulasRight.Controls.Add(pnlFormulasRightBtns);

            tabPageFormulas.Controls.Add(pnlFormulasRight);
            tabPageFormulas.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageFormulas.Controls.Add(pnlFormulasLeft);

            // =========================
            // Zakładka: Makra (JSON)
            // =========================
            tabPageMacros = new TabPage("📜 Makra (JSON)");

            Panel pnlMacrosLeft = new Panel { Dock = DockStyle.Left, Width = 250 };
            lstMacros = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
            lstMacros.SelectedIndexChanged += LstMacros_SelectedIndexChanged;

            Panel pnlMacrosLeftBtns = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            btnAddMacro = new Button { Text = "➕ Dodaj", Left = 5, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnAddMacro.Click += BtnAddMacro_Click;
            btnDeleteMacro = new Button { Text = "🗑️ Usuń", Left = 125, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnDeleteMacro.Click += BtnDeleteMacro_Click;
            pnlMacrosLeftBtns.Controls.Add(btnAddMacro);
            pnlMacrosLeftBtns.Controls.Add(btnDeleteMacro);

            btnExecuteMacro = new Button { Text = "▶️ Wykonaj Wybrane Makro", Dock = DockStyle.Bottom, Height = 40, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnExecuteMacro.Click += BtnExecuteMacro_Click;

            pnlMacrosLeft.Controls.Add(lstMacros);
            pnlMacrosLeft.Controls.Add(pnlMacrosLeftBtns);
            pnlMacrosLeft.Controls.Add(btnExecuteMacro);

            Panel pnlMacrosRight = new Panel { Dock = DockStyle.Fill };
            rtbMacroJson = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = false, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
            Panel pnlMacrosRightBtns = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnSaveMacro = new Button { Text = "💾 Zapisz Makro", Dock = DockStyle.Right, Width = 150, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSaveMacro.Click += BtnSaveMacro_Click;
            pnlMacrosRightBtns.Controls.Add(btnSaveMacro);

            pnlMacrosRight.Controls.Add(rtbMacroJson);
            pnlMacrosRight.Controls.Add(pnlMacrosRightBtns);

            tabPageMacros.Controls.Add(pnlMacrosRight);
            tabPageMacros.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageMacros.Controls.Add(pnlMacrosLeft);

            mainTabControl.TabPages.Add(tabPageFormulas);
            mainTabControl.TabPages.Add(tabPageMacros);
            this.Controls.Add(mainTabControl);
        }

        private void ApplyTheme()
        {
            Color bgDark = Color.FromArgb(30, 30, 30);
            Color fgLight = Color.LightGray;
            Color panelBg = Color.FromArgb(45, 45, 45);
            Color btnBg = Color.FromArgb(0, 122, 204);

            this.BackColor = panelBg;
            tabPageFormulas.BackColor = panelBg;
            tabPageMacros.BackColor = panelBg;

            lstFormulas.BackColor = bgDark; lstFormulas.ForeColor = fgLight;
            rtbFormulaMetadata.BackColor = bgDark; rtbFormulaMetadata.ForeColor = Color.Orange;
            rtbFormulaCode.BackColor = bgDark; rtbFormulaCode.ForeColor = Color.LightGreen;

            lstMacros.BackColor = bgDark; lstMacros.ForeColor = fgLight;
            rtbMacroJson.BackColor = bgDark; rtbMacroJson.ForeColor = Color.Cyan;

            btnReloadFormulas.BackColor = btnBg; btnReloadFormulas.ForeColor = Color.White; btnReloadFormulas.FlatAppearance.BorderSize = 0;
            btnExecuteMacro.BackColor = Color.SeaGreen; btnExecuteMacro.ForeColor = Color.White; btnExecuteMacro.FlatAppearance.BorderSize = 0;

            btnAddFormula.BackColor = panelBg; btnAddFormula.ForeColor = Color.White;
            btnDeleteFormula.BackColor = Color.Brown; btnDeleteFormula.ForeColor = Color.White;
            btnSaveFormula.BackColor = btnBg; btnSaveFormula.ForeColor = Color.White;

            btnAddMacro.BackColor = panelBg; btnAddMacro.ForeColor = Color.White;
            btnDeleteMacro.BackColor = Color.Brown; btnDeleteMacro.ForeColor = Color.White;
            btnSaveMacro.BackColor = btnBg; btnSaveMacro.ForeColor = Color.White;
        }

        public void LoadData()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(LoadData));
                return;
            }

            lstFormulas.Items.Clear();
            var formulas = DynamicFormulaManager.GetAvailableFormulas();
            foreach (var f in formulas) lstFormulas.Items.Add(f);
            rtbFormulaMetadata.Clear();
            rtbFormulaCode.Clear();

            lstMacros.Items.Clear();
            var macros = MacroManager.GetAvailableMacros();
            foreach (var m in macros) lstMacros.Items.Add(m);
            rtbMacroJson.Clear();
        }

        private void BtnReloadFormulas_Click(object sender, EventArgs e)
        {
            try
            {
                DynamicFormulaManager.LoadAndCompileAll();
                MacroManager.LoadAllMacros();
                LoadData();
                MessageBox.Show("Baza wiedzy została pomyślnie przeładowana.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LstFormulas_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstFormulas.SelectedItem == null) { rtbFormulaCode.Clear(); rtbFormulaMetadata.Clear(); return; }
            string formulaId = lstFormulas.SelectedItem.ToString();
            string formulasPath = AppPaths.GetFormulasPath();
            string csxPath = Path.Combine(formulasPath, $"{formulaId}.csx");
            string jsonPath = Path.Combine(formulasPath, $"{formulaId}.json");
            
            if (File.Exists(csxPath)) rtbFormulaCode.Text = File.ReadAllText(csxPath);
            else rtbFormulaCode.Text = $"// Plik {csxPath} nie istnieje.";

            if (File.Exists(jsonPath)) rtbFormulaMetadata.Text = File.ReadAllText(jsonPath);
            else rtbFormulaMetadata.Text = "{\n  \"formulaId\": \"" + formulaId + "\",\n  \"description\": \"Brak opisu.\",\n  \"requiredInputs\": [],\n  \"outputDescription\": \"\"\n}";
        }

        private void LstMacros_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstMacros.SelectedItem == null) { rtbMacroJson.Clear(); return; }
            string macroId = lstMacros.SelectedItem.ToString();
            string macrosPath = AppPaths.GetMacrosPath();
            string filePath = Path.Combine(macrosPath, $"{macroId}.json");
            
            if (File.Exists(filePath)) rtbMacroJson.Text = File.ReadAllText(filePath);
            else rtbMacroJson.Text = $"// Plik {filePath} nie istnieje.";
        }

        private void BtnAddFormula_Click(object sender, EventArgs e)
        {
            string id = ShowInputDialog("Podaj ID nowej formuły (bez spacji):", "Nowa formuła");
            if (string.IsNullOrWhiteSpace(id)) return;
            
            string templateCode = "return \"0 mm\";";
            var templateMeta = new FormulaMetadata { FormulaId = id, Description = "Nowa formuła inżynierska", OutputDescription = "Wartość z jednostką" };
            string templateJson = JsonConvert.SerializeObject(templateMeta, Formatting.Indented);

            try
            {
                DynamicFormulaManager.SaveFormula(id, templateCode, templateJson);
                LoadData();
                lstFormulas.SelectedItem = id;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteFormula_Click(object sender, EventArgs e)
        {
            if (lstFormulas.SelectedItem == null) return;
            string id = lstFormulas.SelectedItem.ToString();
            if (MessageBox.Show($"Czy na pewno chcesz usunąć formułę '{id}' (obie części)?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    DynamicFormulaManager.DeleteFormula(id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSaveFormula_Click(object sender, EventArgs e)
        {
            if (lstFormulas.SelectedItem == null) return;
            string id = lstFormulas.SelectedItem.ToString();
            string code = rtbFormulaCode.Text;
            string json = rtbFormulaMetadata.Text;
            try
            {
                DynamicFormulaManager.SaveFormula(id, code, json);
                MessageBox.Show("Zapisano pomyślnie. Kompilacja i walidacja JSON OK.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BŁĄD:\n{ex.Message}", "Błąd Zapisywania", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddMacro_Click(object sender, EventArgs e)
        {
            string id = ShowInputDialog("Podaj ID nowego makra (bez spacji):", "Nowe Makro");
            if (string.IsNullOrWhiteSpace(id)) return;

            string template = "{\n  \"id\": \"" + id + "\",\n  \"description\": \"Nowe makro CAD\",\n  \"steps\": [\n    {\n      \"actionType\": \"CreateObject\",\n      \"parameters\": {\n        \"ObjectType\": \"Circle\",\n        \"Radius\": 100.0\n      }\n    }\n  ]\n}";
            try
            {
                MacroManager.SaveMacro(id, template);
                LoadData();
                lstMacros.SelectedItem = id;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteMacro_Click(object sender, EventArgs e)
        {
            if (lstMacros.SelectedItem == null) return;
            string id = lstMacros.SelectedItem.ToString();
            if (MessageBox.Show($"Czy na pewno chcesz usunąć makro '{id}'?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    MacroManager.DeleteMacro(id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSaveMacro_Click(object sender, EventArgs e)
        {
            if (lstMacros.SelectedItem == null) return;
            string id = lstMacros.SelectedItem.ToString();
            string json = rtbMacroJson.Text;
            try
            {
                MacroManager.SaveMacro(id, json);
                MessageBox.Show("Zapisano pomyślnie. JSON zwalidowany.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BŁĄD JSON:\n{ex.Message}", "Błąd Zapisywania", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnExecuteMacro_Click(object sender, EventArgs e)
        {
            if (lstMacros.SelectedItem == null) return;
            string macroId = lstMacros.SelectedItem.ToString();
            btnExecuteMacro.Enabled = false;
            btnExecuteMacro.Text = "⏳ Wykonywanie...";

            try
            {
                await Task.Run(() =>
                {
                    var doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        var context = new CadExecutionContext(doc);
                        MacroManager.ExecuteMacro(macroId, context);
                    }
                });
                MessageBox.Show($"Makro '{macroId}' zostało wykonane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() => { btnExecuteMacro.Enabled = true; btnExecuteMacro.Text = "▶️ Wykonaj Wybrane Makro"; }));
                }
            }
        }

        public static string ShowInputDialog(string text, string caption)
        {
            Form prompt = new Form()
            {
                Width = 400, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = caption, StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 20, Top = 20, Width = 340, Text = text };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 340 };
            Button confirmation = new Button() { Text = "OK", Left = 260, Width = 100, Top = 80, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }
}
