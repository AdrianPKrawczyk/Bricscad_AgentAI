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
using System.Data;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.UI.KnowledgeBase
{
    public class KnowledgeBaseControl : UserControl
    {
        private TabControl mainTabControl;
        private TabPage tabPageFormulas;
        private TabPage tabPageMacros;
        private TabPage tabPageDatasets;

        private Panel pnlTopFilters;
        private TextBox txtTagFilter;
        private string currentTagFilter = "";

        // --- Formulas ---
        private TreeView tvFormulas;
        private RichTextBox rtbFormulaMetadata;
        private RichTextBox rtbFormulaCode;
        private Button btnReloadFormulas;
        private Button btnAddFormula;
        private Button btnDeleteFormula;
        private Button btnSaveFormula;

        // --- Macros ---
        private TreeView tvMacros;
        private RichTextBox rtbMacroJson;
        private Button btnExecuteMacro;
        private Button btnAddMacro;
        private Button btnDeleteMacro;
        private Button btnSaveMacro;

        // --- Datasets ---
        private TreeView tvDatasets;
        private DataGridView dgvDataset;
        private Button btnSaveDataset;

        // --- Skills ---
        private TabPage tabPageSkills;
        private TreeView tvSkills;
        private RichTextBox rtbSkillMarkdown;
        private Button btnAddSkill;
        private Button btnDeleteSkill;
        private Button btnSaveSkill;

        // --- Lisps ---
        private TabPage tabPageLisps;
        private TreeView tvLisps;
        private RichTextBox rtbLispCode;
        private Button btnExecuteLisp;
        private Button btnAddLisp;
        private Button btnDeleteLisp;
        private Button btnSaveLisp;

        public KnowledgeBaseControl()
        {
            InitializeComponent();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.VisibleChanged += KnowledgeBaseControl_VisibleChanged;

            pnlTopFilters = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5) };
            Label lblTagFilter = new Label { Text = "Filtruj wg tagów (po przecinku):", AutoSize = true, Location = new Point(10, 12), ForeColor = Color.LightGray };
            txtTagFilter = new TextBox { Location = new Point(190, 9), Width = 300, Font = new Font("Segoe UI", 9.5f) };
            txtTagFilter.TextChanged += TxtTagFilter_TextChanged;
            
            pnlTopFilters.Controls.Add(lblTagFilter);
            pnlTopFilters.Controls.Add(txtTagFilter);
            this.Controls.Add(pnlTopFilters);

            mainTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };

            // =========================
            // Zakładka: Formuły (Roslyn)
            // =========================
            tabPageFormulas = new TabPage("Formuły Inżynierskie (Roslyn)");

            Panel pnlFormulasLeft = new Panel { Dock = DockStyle.Left, Width = 250 };
            tvFormulas = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, HideSelection = false };
            tvFormulas.AfterSelect += TvFormulas_AfterSelect;
            
            Panel pnlFormulasLeftBtns = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            btnAddFormula = new Button { Text = "➕ Dodaj", Left = 5, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnAddFormula.Click += BtnAddFormula_Click;
            btnDeleteFormula = new Button { Text = "🗑️ Usuń", Left = 125, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnDeleteFormula.Click += BtnDeleteFormula_Click;
            pnlFormulasLeftBtns.Controls.Add(btnAddFormula);
            pnlFormulasLeftBtns.Controls.Add(btnDeleteFormula);

            btnReloadFormulas = new Button { Text = "🔄 Przeładuj Bazy (Hot Reload)", Dock = DockStyle.Bottom, Height = 40, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnReloadFormulas.Click += BtnReloadFormulas_Click;
            
            pnlFormulasLeft.Controls.Add(tvFormulas);
            pnlFormulasLeft.Controls.Add(pnlFormulasLeftBtns);
            pnlFormulasLeft.Controls.Add(btnReloadFormulas);

            Panel pnlFormulasRight = new Panel { Dock = DockStyle.Fill };
            
            SplitContainer splitFormulasRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            
            rtbFormulaMetadata = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = false, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
            Label lblMeta = new Label { Text = "Metadane (JSON):", Dock = DockStyle.Top, Height = 20, ForeColor = Color.LightGray };
            splitFormulasRight.Panel1.Controls.Add(lblMeta);
            splitFormulasRight.Panel1.Controls.Add(rtbFormulaMetadata);

            rtbFormulaCode = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = false, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
            Label lblCode = new Label { Text = "Kod wykonywalny (CSX):", Dock = DockStyle.Top, Height = 20, ForeColor = Color.LightGray };
            splitFormulasRight.Panel2.Controls.Add(lblCode);
            splitFormulasRight.Panel2.Controls.Add(rtbFormulaCode);

            Panel pnlFormulasRightBtns = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnSaveFormula = new Button { Text = "💾 Zapisz Formułę", Dock = DockStyle.Right, Width = 150, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSaveFormula.Click += BtnSaveFormula_Click;
            pnlFormulasRightBtns.Controls.Add(btnSaveFormula);

            pnlFormulasRight.Controls.Add(pnlFormulasRightBtns);
            pnlFormulasRight.Controls.Add(splitFormulasRight);

            tabPageFormulas.Controls.Add(pnlFormulasRight);
            tabPageFormulas.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageFormulas.Controls.Add(pnlFormulasLeft);

            // =========================
            // Zakładka: Makra (JSON)
            // =========================
            tabPageMacros = new TabPage("Makra (JSON)");

            Panel pnlMacrosLeft = new Panel { Dock = DockStyle.Left, Width = 250 };
            tvMacros = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, HideSelection = false };
            tvMacros.AfterSelect += TvMacros_AfterSelect;

            Panel pnlMacrosLeftBtns = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            btnAddMacro = new Button { Text = "➕ Dodaj", Left = 5, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnAddMacro.Click += BtnAddMacro_Click;
            btnDeleteMacro = new Button { Text = "🗑️ Usuń", Left = 125, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnDeleteMacro.Click += BtnDeleteMacro_Click;
            pnlMacrosLeftBtns.Controls.Add(btnAddMacro);
            pnlMacrosLeftBtns.Controls.Add(btnDeleteMacro);

            btnExecuteMacro = new Button { Text = "▶️ Wykonaj Wybrane Makro", Dock = DockStyle.Bottom, Height = 40, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnExecuteMacro.Click += BtnExecuteMacro_Click;

            pnlMacrosLeft.Controls.Add(tvMacros);
            pnlMacrosLeft.Controls.Add(pnlMacrosLeftBtns);
            pnlMacrosLeft.Controls.Add(btnExecuteMacro);

            Panel pnlMacrosRight = new Panel { Dock = DockStyle.Fill };
            rtbMacroJson = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = false, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
            Panel pnlMacrosRightBtns = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnSaveMacro = new Button { Text = "💾 Zapisz Makro", Dock = DockStyle.Right, Width = 150, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSaveMacro.Click += BtnSaveMacro_Click;
            pnlMacrosRightBtns.Controls.Add(btnSaveMacro);

            pnlMacrosRight.Controls.Add(pnlMacrosRightBtns);
            pnlMacrosRight.Controls.Add(rtbMacroJson);

            tabPageMacros.Controls.Add(pnlMacrosRight);
            tabPageMacros.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageMacros.Controls.Add(pnlMacrosLeft);

            // =========================
            // Zakładka: Bazy Danych (Katalogi)
            // =========================
            tabPageDatasets = new TabPage("Bazy Danych (Katalogi)");

            Panel pnlDatasetsLeft = new Panel { Dock = DockStyle.Left, Width = 250 };
            tvDatasets = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, HideSelection = false };
            tvDatasets.AfterSelect += TvDatasets_AfterSelect;

            pnlDatasetsLeft.Controls.Add(tvDatasets);

            Panel pnlDatasetsRight = new Panel { Dock = DockStyle.Fill };
            dgvDataset = new DataGridView 
            { 
                Dock = DockStyle.Fill, 
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Black
            };
            
            Panel pnlDatasetsRightBtns = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnSaveDataset = new Button { Text = "💾 Zapisz zmiany w tabeli", Dock = DockStyle.Right, Width = 200, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSaveDataset.Click += BtnSaveDataset_Click;
            pnlDatasetsRightBtns.Controls.Add(btnSaveDataset);

            pnlDatasetsRight.Controls.Add(pnlDatasetsRightBtns);
            pnlDatasetsRight.Controls.Add(dgvDataset);

            tabPageDatasets.Controls.Add(pnlDatasetsRight);
            tabPageDatasets.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageDatasets.Controls.Add(pnlDatasetsLeft);

            mainTabControl.TabPages.Add(tabPageFormulas);
            mainTabControl.TabPages.Add(tabPageMacros);
            mainTabControl.TabPages.Add(tabPageDatasets);

            // =========================
            // Zakładka: Skille (Markdown)
            // =========================
            tabPageSkills = new TabPage("Skille Inżynierskie (Markdown)");

            Panel pnlSkillsLeft = new Panel { Dock = DockStyle.Left, Width = 250 };
            tvSkills = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, HideSelection = false };
            tvSkills.AfterSelect += TvSkills_AfterSelect;

            Panel pnlSkillsLeftBtns = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnAddSkill = new Button { Text = "➕ Dodaj", Dock = DockStyle.Left, Width = 125, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnAddSkill.Click += BtnAddSkill_Click;
            btnDeleteSkill = new Button { Text = "🗑️ Usuń", Dock = DockStyle.Right, Width = 125, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnDeleteSkill.Click += BtnDeleteSkill_Click;
            pnlSkillsLeftBtns.Controls.Add(btnAddSkill);
            pnlSkillsLeftBtns.Controls.Add(btnDeleteSkill);

            pnlSkillsLeft.Controls.Add(tvSkills);
            pnlSkillsLeft.Controls.Add(pnlSkillsLeftBtns);

            Panel pnlSkillsRight = new Panel { Dock = DockStyle.Fill };
            rtbSkillMarkdown = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = false, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, WordWrap = true, ScrollBars = RichTextBoxScrollBars.Both };
            Panel pnlSkillsRightBtns = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnSaveSkill = new Button { Text = "💾 Zapisz Skill", Dock = DockStyle.Right, Width = 150, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSaveSkill.Click += BtnSaveSkill_Click;
            pnlSkillsRightBtns.Controls.Add(btnSaveSkill);

            pnlSkillsRight.Controls.Add(pnlSkillsRightBtns);
            pnlSkillsRight.Controls.Add(rtbSkillMarkdown);

            tabPageSkills.Controls.Add(pnlSkillsRight);
            tabPageSkills.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageSkills.Controls.Add(pnlSkillsLeft);

            mainTabControl.TabPages.Add(tabPageSkills);

            // =========================
            // Zakładka: Skrypty LISP
            // =========================
            tabPageLisps = new TabPage("Skrypty LISP");

            Panel pnlLispsLeft = new Panel { Dock = DockStyle.Left, Width = 250 };
            tvLisps = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, HideSelection = false };
            tvLisps.AfterSelect += TvLisps_AfterSelect;

            Panel pnlLispsLeftBtns = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            btnAddLisp = new Button { Text = "➕ Dodaj", Left = 5, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnAddLisp.Click += BtnAddLisp_Click;
            btnDeleteLisp = new Button { Text = "🗑️ Usuń", Left = 125, Top = 5, Width = 115, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnDeleteLisp.Click += BtnDeleteLisp_Click;
            pnlLispsLeftBtns.Controls.Add(btnAddLisp);
            pnlLispsLeftBtns.Controls.Add(btnDeleteLisp);

            btnExecuteLisp = new Button { Text = "▶️ Wykonaj Skrypt LISP", Dock = DockStyle.Bottom, Height = 40, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnExecuteLisp.Click += BtnExecuteLisp_Click;

            pnlLispsLeft.Controls.Add(tvLisps);
            pnlLispsLeft.Controls.Add(pnlLispsLeftBtns);
            pnlLispsLeft.Controls.Add(btnExecuteLisp);

            Panel pnlLispsRight = new Panel { Dock = DockStyle.Fill };
            rtbLispCode = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = false, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
            Panel pnlLispsRightBtns = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnSaveLisp = new Button { Text = "💾 Zapisz Skrypt LISP", Dock = DockStyle.Right, Width = 180, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSaveLisp.Click += BtnSaveLisp_Click;
            pnlLispsRightBtns.Controls.Add(btnSaveLisp);

            pnlLispsRight.Controls.Add(pnlLispsRightBtns);
            pnlLispsRight.Controls.Add(rtbLispCode);

            tabPageLisps.Controls.Add(pnlLispsRight);
            tabPageLisps.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageLisps.Controls.Add(pnlLispsLeft);

            mainTabControl.TabPages.Add(tabPageLisps);

            this.Controls.Add(mainTabControl);
            pnlTopFilters.BringToFront();
        }

        private void ApplyTheme()
        {
            Color bgDark = Color.FromArgb(30, 30, 30);
            Color fgLight = Color.LightGray;
            Color panelBg = Color.FromArgb(45, 45, 45);
            Color btnBg = Color.FromArgb(0, 122, 204);

            this.BackColor = panelBg;
            pnlTopFilters.BackColor = bgDark;
            txtTagFilter.BackColor = panelBg; txtTagFilter.ForeColor = Color.White;
            txtTagFilter.BorderStyle = BorderStyle.FixedSingle;

            tabPageFormulas.BackColor = panelBg;
            tabPageMacros.BackColor = panelBg;
            tabPageDatasets.BackColor = panelBg;
            tabPageSkills.BackColor = panelBg;
            tabPageLisps.BackColor = panelBg;

            tvFormulas.BackColor = bgDark; tvFormulas.ForeColor = fgLight;
            rtbFormulaMetadata.BackColor = bgDark; rtbFormulaMetadata.ForeColor = Color.Orange;
            rtbFormulaCode.BackColor = bgDark; rtbFormulaCode.ForeColor = Color.LightGreen;

            tvMacros.BackColor = bgDark; tvMacros.ForeColor = fgLight;
            rtbMacroJson.BackColor = bgDark; rtbMacroJson.ForeColor = Color.Cyan;

            tvDatasets.BackColor = bgDark; tvDatasets.ForeColor = fgLight;

            btnReloadFormulas.BackColor = btnBg; btnReloadFormulas.ForeColor = Color.White; btnReloadFormulas.FlatAppearance.BorderSize = 0;
            btnExecuteMacro.BackColor = Color.SeaGreen; btnExecuteMacro.ForeColor = Color.White; btnExecuteMacro.FlatAppearance.BorderSize = 0;

            btnAddFormula.BackColor = panelBg; btnAddFormula.ForeColor = Color.White;
            btnDeleteFormula.BackColor = Color.Brown; btnDeleteFormula.ForeColor = Color.White;
            btnSaveFormula.BackColor = btnBg; btnSaveFormula.ForeColor = Color.White;

            btnAddMacro.BackColor = panelBg; btnAddMacro.ForeColor = Color.White;
            btnDeleteMacro.BackColor = Color.Brown; btnDeleteMacro.ForeColor = Color.White;
            btnSaveMacro.BackColor = btnBg; btnSaveMacro.ForeColor = Color.White;

            btnAddSkill.BackColor = panelBg; btnAddSkill.ForeColor = Color.White;
            btnDeleteSkill.BackColor = Color.Brown; btnDeleteSkill.ForeColor = Color.White;
            btnSaveSkill.BackColor = btnBg; btnSaveSkill.ForeColor = Color.White;
            tvSkills.BackColor = bgDark; tvSkills.ForeColor = fgLight;
            rtbSkillMarkdown.BackColor = bgDark; rtbSkillMarkdown.ForeColor = Color.LightSkyBlue;

            tvLisps.BackColor = bgDark; tvLisps.ForeColor = fgLight;
            rtbLispCode.BackColor = bgDark; rtbLispCode.ForeColor = Color.LightGoldenrodYellow;
            btnExecuteLisp.BackColor = Color.SeaGreen; btnExecuteLisp.ForeColor = Color.White; btnExecuteLisp.FlatAppearance.BorderSize = 0;
            btnAddLisp.BackColor = panelBg; btnAddLisp.ForeColor = Color.White;
            btnDeleteLisp.BackColor = Color.Brown; btnDeleteLisp.ForeColor = Color.White;
            btnSaveLisp.BackColor = btnBg; btnSaveLisp.ForeColor = Color.White;

            btnSaveDataset.BackColor = btnBg; btnSaveDataset.ForeColor = Color.White;
        }

        private void PopulateTreeView(TreeView tv, IEnumerable<string> items, Func<string, string> getCategory)
        {
            tv.Nodes.Clear();

            foreach (var item in items)
            {
                string category = getCategory(item);
                if (string.IsNullOrWhiteSpace(category)) category = "Uncategorized";

                var parts = category.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                TreeNodeCollection currentCollection = tv.Nodes;

                string currentPath = "";
                foreach (var part in parts)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                    
                    var existingNodes = currentCollection.Find(currentPath, false);
                    if (existingNodes.Length == 0)
                    {
                        var newNode = new TreeNode(part) { Name = currentPath, Tag = "FOLDER" };
                        currentCollection.Add(newNode);
                        currentCollection = newNode.Nodes;
                    }
                    else
                    {
                        currentCollection = existingNodes[0].Nodes;
                    }
                }

                var leafNode = new TreeNode(item) { Name = item, Tag = item };
                currentCollection.Add(leafNode);
            }
            tv.ExpandAll();
        }

        public void LoadData()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(LoadData));
                return;
            }

            var tags = currentTagFilter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(t => t.Trim().ToLower())
                                       .ToList();

            var formulas = DynamicFormulaManager.GetAvailableFormulas();
            if (tags.Any())
            {
                formulas = formulas.Where(f => 
                {
                    var meta = DynamicFormulaManager.GetMetadata(f);
                    if (meta == null || meta.Tags == null) return false;
                    return tags.All(tag => meta.Tags.Any(mt => mt.ToLower().Contains(tag)));
                });
            }
            PopulateTreeView(tvFormulas, formulas, f => DynamicFormulaManager.GetMetadata(f)?.Category);
            rtbFormulaMetadata.Clear();
            rtbFormulaCode.Clear();

            var macros = MacroManager.GetAvailableMacros();
            if (tags.Any())
            {
                macros = macros.Where(m => 
                {
                    var macro = MacroManager.GetMacro(m);
                    if (macro == null || macro.Tags == null) return false;
                    return tags.All(tag => macro.Tags.Any(mt => mt.ToLower().Contains(tag)));
                });
            }
            PopulateTreeView(tvMacros, macros, m => MacroManager.GetMacro(m)?.Category);
            rtbMacroJson.Clear();

            var datasets = DatasetManager.GetAvailableDatasets();
            if (tags.Any())
            {
                datasets = datasets.Where(d => 
                {
                    if (d.Tags == null) return false;
                    return tags.All(tag => d.Tags.Any(dt => dt.ToLower().Contains(tag)));
                });
            }
            PopulateTreeView(tvDatasets, datasets.Select(d => d.DatasetId), id => datasets.FirstOrDefault(d => d.DatasetId == id)?.Category);
            dgvDataset.DataSource = null;

            var skills = SkillManager.GetAvailableSkills();
            if (tags.Any())
            {
                skills = skills.Where(s => 
                {
                    if (s.Tags == null) return false;
                    return tags.All(tag => s.Tags.Any(st => st.ToLower().Contains(tag)));
                });
            }
            PopulateTreeView(tvSkills, skills.Select(s => s.Id), id => SkillManager.GetSkill(id)?.Category);
            rtbSkillMarkdown.Clear();

            var lisps = LispManager.GetAvailableLisps();
            if (tags.Any())
            {
                lisps = lisps.Where(l => 
                {
                    var meta = LispManager.GetMetadata(l);
                    if (meta == null || meta.Tags == null) return false;
                    return tags.All(tag => meta.Tags.Any(mt => mt.ToLower().Contains(tag)));
                });
            }
            PopulateTreeView(tvLisps, lisps, l => LispManager.GetMetadata(l)?.Category);
            rtbLispCode.Clear();
        }

        private void TxtTagFilter_TextChanged(object sender, EventArgs e)
        {
            currentTagFilter = txtTagFilter.Text;
            LoadData();
        }

        private void KnowledgeBaseControl_VisibleChanged(object sender, EventArgs e)
        {
            if (this.Visible)
            {
                try
                {
                    DynamicFormulaManager.LoadAndCompileAll();
                    MacroManager.LoadAllMacros();
                    SkillManager.LoadAllSkills();
                    LispManager.LoadAllLisps();
                    LoadData();
                }
                catch (Exception ex)
                {
                    BielikLogger.LogError($"Błąd podczas automatycznego przeładowywania bazy wiedzy: {ex.Message}");
                }
            }
        }

        private void BtnReloadFormulas_Click(object sender, EventArgs e)
        {
            try
            {
                DynamicFormulaManager.LoadAndCompileAll();
                MacroManager.LoadAllMacros();
                SkillManager.LoadAllSkills();
                LispManager.LoadAllLisps();
                LoadData();
                MessageBox.Show("Baza wiedzy została pomyślnie przeładowana.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TvFormulas_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Tag?.ToString() == "FOLDER") { rtbFormulaCode.Clear(); rtbFormulaMetadata.Clear(); return; }
            string formulaId = e.Node.Tag.ToString();
            
            var formulasPath = AppPaths.GetFormulasPath();
            var csxFiles = Directory.GetFiles(formulasPath, $"{formulaId}.csx", SearchOption.AllDirectories);
            
            if (csxFiles.Length > 0)
            {
                string csxPath = csxFiles[0];
                string jsonPath = Path.ChangeExtension(csxPath, ".json");
                
                rtbFormulaCode.Text = File.ReadAllText(csxPath);
                if (File.Exists(jsonPath)) rtbFormulaMetadata.Text = File.ReadAllText(jsonPath);
                else rtbFormulaMetadata.Text = "{\n  \"formulaId\": \"" + formulaId + "\",\n  \"description\": \"Brak opisu.\",\n  \"requiredInputs\": [],\n  \"outputDescription\": \"\"\n}";
            }
            else
            {
                rtbFormulaCode.Text = $"// Plik skryptu dla {formulaId} nie istnieje.";
            }
        }

        private void TvMacros_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Tag?.ToString() == "FOLDER") { rtbMacroJson.Clear(); return; }
            string macroId = e.Node.Tag.ToString();
            
            string macrosPath = AppPaths.GetMacrosPath();
            var files = Directory.GetFiles(macrosPath, $"{macroId}.json", SearchOption.AllDirectories);
            
            if (files.Length > 0) rtbMacroJson.Text = File.ReadAllText(files[0]);
            else rtbMacroJson.Text = $"// Plik dla {macroId} nie istnieje.";
        }

        private void TvDatasets_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Tag?.ToString() == "FOLDER") { dgvDataset.DataSource = null; return; }
            string datasetName = e.Node.Tag.ToString();
            string datasetsPath = AppPaths.GetDatasetsPath();
            var files = Directory.GetFiles(datasetsPath, $"{datasetName}.data.json", SearchOption.AllDirectories);
            
            if (files.Length == 0)
            {
                // Fallback to legacy
                files = Directory.GetFiles(datasetsPath, $"{datasetName}.json", SearchOption.AllDirectories);
            }
            
            if (files.Length > 0)
            {
                try
                {
                    string json = File.ReadAllText(files[0]);
                    DataTable dt = JsonConvert.DeserializeObject<DataTable>(json);
                    dgvDataset.DataSource = dt;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"BĹ‚Ä…d odczytu bazy: {ex.Message}");
                }
            }
            else
            {
                dgvDataset.DataSource = null;
            }
        }

        private void BtnAddFormula_Click(object sender, EventArgs e)
        {
            string id = ShowInputDialog("Podaj ID nowej formuĹ‚y (bez spacji):", "Nowa formuĹ‚a");
            if (string.IsNullOrWhiteSpace(id)) return;
            
            string templateCode = "return \"0 mm\";";
            var templateMeta = new FormulaMetadata { FormulaId = id, Description = "Nowa formuĹ‚a inĹĽynierska", OutputDescription = "WartoĹ›Ä‡ z jednostkÄ…", Category = "Uncategorized" };
            string templateJson = JsonConvert.SerializeObject(templateMeta, Formatting.Indented);

            try
            {
                DynamicFormulaManager.SaveFormula(id, templateCode, templateJson);
                LoadData();
                // Opcjonalnie moĹĽna rozwinÄ…Ä‡ drzewo i zaznaczyÄ‡ nowo dodany element.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹ‚Ä…d podczas dodawania: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteFormula_Click(object sender, EventArgs e)
        {
            if (tvFormulas.SelectedNode == null || tvFormulas.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string id = tvFormulas.SelectedNode.Tag.ToString();
            if (MessageBox.Show($"Czy na pewno chcesz usunÄ…Ä‡ formuĹ‚Ä™ '{id}' (obie czÄ™Ĺ›ci)?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    DynamicFormulaManager.DeleteFormula(id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"BĹ‚Ä…d: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSaveFormula_Click(object sender, EventArgs e)
        {
            if (tvFormulas.SelectedNode == null || tvFormulas.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string id = tvFormulas.SelectedNode.Tag.ToString();
            string code = rtbFormulaCode.Text;
            string json = rtbFormulaMetadata.Text;
            try
            {
                DynamicFormulaManager.SaveFormula(id, code, json);
                MessageBox.Show("Zapisano pomyĹ›lnie. Kompilacja i walidacja JSON OK.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹÄ„D:\n{ex.Message}", "BĹ‚Ä…d Zapisywania", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddMacro_Click(object sender, EventArgs e)
        {
            string id = ShowInputDialog("Podaj ID nowego makra (bez spacji):", "Nowe Makro");
            if (string.IsNullOrWhiteSpace(id)) return;

            string template = "{\n  \"id\": \"" + id + "\",\n  \"description\": \"Nowe makro CAD\",\n  \"category\": \"Uncategorized\",\n  \"steps\": [\n    {\n      \"actionType\": \"CreateObject\",\n      \"parameters\": {\n        \"ObjectType\": \"Circle\",\n        \"Radius\": 100.0\n      }\n    }\n  ]\n}";
            try
            {
                MacroManager.SaveMacro(id, template);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹ‚Ä…d podczas dodawania: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteMacro_Click(object sender, EventArgs e)
        {
            if (tvMacros.SelectedNode == null || tvMacros.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string id = tvMacros.SelectedNode.Tag.ToString();
            if (MessageBox.Show($"Czy na pewno chcesz usunÄ…Ä‡ makro '{id}'?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    MacroManager.DeleteMacro(id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"BĹ‚Ä…d: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSaveMacro_Click(object sender, EventArgs e)
        {
            if (tvMacros.SelectedNode == null || tvMacros.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string id = tvMacros.SelectedNode.Tag.ToString();
            string json = rtbMacroJson.Text;
            try
            {
                MacroManager.SaveMacro(id, json);
                MessageBox.Show("Zapisano pomyĹ›lnie. JSON zwalidowany.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹÄ„D JSON:\n{ex.Message}", "BĹ‚Ä…d Zapisywania", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSaveDataset_Click(object sender, EventArgs e)
        {
            if (tvDatasets.SelectedNode == null || tvDatasets.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string datasetName = tvDatasets.SelectedNode.Tag.ToString();
            try
            {
                if (dgvDataset.DataSource is DataTable dt)
                {
                    string json = JsonConvert.SerializeObject(dt, Formatting.Indented);
                    var metadata = DatasetManager.GetAvailableDatasets().FirstOrDefault(d => d.DatasetId == datasetName);
                    if (metadata == null) metadata = new DatasetMetadata { DatasetId = datasetName, Description = "Zaktualizowano w UI", Category = "Uncategorized" };
                    DatasetManager.SaveDataset(metadata, json);
                    MessageBox.Show("Baza zostaĹ‚a zapisana i uaktualniona.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹ‚Ä…d zapisu baza: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TvSkills_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Tag?.ToString() == "FOLDER") { rtbSkillMarkdown.Clear(); return; }
            string skillId = e.Node.Tag.ToString();
            
            try
            {
                var skill = SkillManager.GetSkill(skillId);
                rtbSkillMarkdown.Text = skill.Content;
            }
            catch
            {
                rtbSkillMarkdown.Text = $"// Plik dla skilla {skillId} nie istnieje lub nie moĹĽna go zaĹ‚adowaÄ‡.";
            }
        }

        private void BtnAddSkill_Click(object sender, EventArgs e)
        {
            string id = ShowInputDialog("Podaj ID nowego skilla (bez spacji):", "Nowy Skill");
            if (string.IsNullOrWhiteSpace(id)) return;

            string template = $"---\ncategory: Uncategorized\ndescription: Nowy inĹĽynierski skill\ntags: []\n---\n\n## {id}\n\nZasady postÄ™powania dla tego zadania...\n";
            try
            {
                var skill = new AgentSkill { Id = id, Content = template, Category = "Uncategorized" };
                SkillManager.SaveSkill(skill);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹ‚Ä…d podczas dodawania: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteSkill_Click(object sender, EventArgs e)
        {
            if (tvSkills.SelectedNode == null || tvSkills.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string id = tvSkills.SelectedNode.Tag.ToString();
            if (MessageBox.Show($"Czy na pewno chcesz usunÄ…Ä‡ skilla '{id}'?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    SkillManager.DeleteSkill(id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"BĹ‚Ä…d: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSaveSkill_Click(object sender, EventArgs e)
        {
            if (tvSkills.SelectedNode == null || tvSkills.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string id = tvSkills.SelectedNode.Tag.ToString();
            string markdown = rtbSkillMarkdown.Text;
            try
            {
                var parsed = SkillManager.ParseSkill(id, markdown);
                SkillManager.SaveSkill(parsed);
                MessageBox.Show("Zapisano pomyĹ›lnie. Frontmatter zwalidowany.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹÄ„D ZAPISU:\n{ex.Message}", "BĹ‚Ä…d Zapisywania", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnExecuteMacro_Click(object sender, EventArgs e)
        {
            if (tvMacros.SelectedNode == null || tvMacros.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string macroId = tvMacros.SelectedNode.Tag.ToString();
            btnExecuteMacro.Enabled = false;
            btnExecuteMacro.Text = "âŹł Wykonywanie...";

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
                MessageBox.Show($"Makro '{macroId}' zostaĹ‚o wykonane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BĹ‚Ä…d: {ex.Message}", "BĹ‚Ä…d", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() => { btnExecuteMacro.Enabled = true; btnExecuteMacro.Text = "â–¶ď¸Ź Wykonaj Wybrane Makro"; }));
                }
            }
        }

        private void TvLisps_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Tag?.ToString() == "FOLDER") { rtbLispCode.Clear(); return; }
            string lispId = e.Node.Tag.ToString();
            
            try
            {
                string code = LispManager.GetLispCode(lispId);
                rtbLispCode.Text = code;
            }
            catch
            {
                rtbLispCode.Text = $"// Plik dla LISP "{lispId}" nie istnieje lub nie można go załadować.";
            }
        }

        private void BtnAddLisp_Click(object sender, EventArgs e)
        {
            string id = ShowInputDialog("Podaj ID nowego skryptu LISP (bez spacji):", "Nowy LISP");
            if (string.IsNullOrWhiteSpace(id)) return;

            string template = "(defun c:" + id + " (/)\n  (princ "\nNowy skrypt LISP")\n  (princ)\n)";
            try
            {
                var meta = new LispMetadata { LispId = id, Description = "Nowy skrypt LISP", Category = "Uncategorized", CreatedAt = DateTime.Now };
                LispManager.SaveLisp(meta, template);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas dodawania: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteLisp_Click(object sender, EventArgs e)
        {
            if (tvLisps.SelectedNode == null || tvLisps.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string id = tvLisps.SelectedNode.Tag.ToString();
            if (MessageBox.Show("Czy na pewno chcesz usunąć skrypt LISP '" + id + "'?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    LispManager.DeleteLisp(id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSaveLisp_Click(object sender, EventArgs e)
        {
            if (tvLisps.SelectedNode == null || tvLisps.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string id = tvLisps.SelectedNode.Tag.ToString();
            string code = rtbLispCode.Text;
            try
            {
                var meta = LispManager.GetMetadata(id);
                if (meta == null) meta = new LispMetadata { LispId = id, Description = "Zaktualizowano w UI", Category = "Uncategorized", CreatedAt = DateTime.Now };
                LispManager.SaveLisp(meta, code);
                MessageBox.Show("Zapisano skrypt LISP pomyślnie.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("BŁĄD ZAPISU:\n" + ex.Message, "Błąd Zapisywania", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnExecuteLisp_Click(object sender, EventArgs e)
        {
            if (tvLisps.SelectedNode == null || tvLisps.SelectedNode.Tag?.ToString() == "FOLDER") return;
            string lispId = tvLisps.SelectedNode.Tag.ToString();
            
            try
            {
                string code = rtbLispCode.Text;
                LispManager.TriggerLispExecution(lispId, code);
                MessageBox.Show("Polecenie uruchomienia skryptu LISP '" + lispId + "' zostało przesłane do BricsCAD.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

