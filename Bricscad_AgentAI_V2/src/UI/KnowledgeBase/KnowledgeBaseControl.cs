using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using System.IO;

namespace Bricscad_AgentAI_V2.UI.KnowledgeBase
{
    public class KnowledgeBaseControl : UserControl
    {
        private TabControl mainTabControl;
        private TabPage tabPageFormulas;
        private TabPage tabPageMacros;

        // --- Formulas ---
        private ListBox lstFormulas;
        private RichTextBox rtbFormulaCode;
        private Button btnReloadFormulas;

        // --- Macros ---
        private ListBox lstMacros;
        private RichTextBox rtbMacroJson;
        private Button btnExecuteMacro;

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
            
            lstFormulas = new ListBox
            {
                Dock = DockStyle.Left,
                Width = 250,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            lstFormulas.SelectedIndexChanged += LstFormulas_SelectedIndexChanged;

            rtbFormulaCode = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            btnReloadFormulas = new Button
            {
                Text = "🔄 Przeładuj Bazy (Hot Reload)",
                Dock = DockStyle.Bottom,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnReloadFormulas.Click += BtnReloadFormulas_Click;

            tabPageFormulas.Controls.Add(rtbFormulaCode);
            tabPageFormulas.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageFormulas.Controls.Add(lstFormulas);
            tabPageFormulas.Controls.Add(btnReloadFormulas);

            // =========================
            // Zakładka: Makra (JSON)
            // =========================
            tabPageMacros = new TabPage("📜 Makra (JSON)");

            lstMacros = new ListBox
            {
                Dock = DockStyle.Left,
                Width = 250,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            lstMacros.SelectedIndexChanged += LstMacros_SelectedIndexChanged;

            rtbMacroJson = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            btnExecuteMacro = new Button
            {
                Text = "▶️ Wykonaj Wybrane Makro",
                Dock = DockStyle.Bottom,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExecuteMacro.Click += BtnExecuteMacro_Click;

            tabPageMacros.Controls.Add(rtbMacroJson);
            tabPageMacros.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 5 });
            tabPageMacros.Controls.Add(lstMacros);
            tabPageMacros.Controls.Add(btnExecuteMacro);

            // Dodanie zakładek do głównego kontrolera
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

            lstFormulas.BackColor = bgDark;
            lstFormulas.ForeColor = fgLight;
            rtbFormulaCode.BackColor = bgDark;
            rtbFormulaCode.ForeColor = Color.LightGreen;

            lstMacros.BackColor = bgDark;
            lstMacros.ForeColor = fgLight;
            rtbMacroJson.BackColor = bgDark;
            rtbMacroJson.ForeColor = Color.Cyan;

            btnReloadFormulas.BackColor = btnBg;
            btnReloadFormulas.ForeColor = Color.White;
            btnReloadFormulas.FlatAppearance.BorderSize = 0;

            btnExecuteMacro.BackColor = Color.SeaGreen;
            btnExecuteMacro.ForeColor = Color.White;
            btnExecuteMacro.FlatAppearance.BorderSize = 0;
        }

        public void LoadData()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(LoadData));
                return;
            }

            // Ładowanie Formuł
            lstFormulas.Items.Clear();
            var formulas = DynamicFormulaManager.GetAvailableFormulas();
            foreach (var f in formulas)
            {
                lstFormulas.Items.Add(f);
            }
            rtbFormulaCode.Clear();

            // Ładowanie Makr
            lstMacros.Items.Clear();
            var macros = MacroManager.GetAvailableMacros();
            foreach (var m in macros)
            {
                lstMacros.Items.Add(m);
            }
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
                MessageBox.Show($"Błąd podczas ładowania bazy wiedzy: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LstFormulas_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstFormulas.SelectedItem == null) return;
            string formulaId = lstFormulas.SelectedItem.ToString();
            
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string filePath = Path.Combine(appData, "Bricscad_AgentAI", "CustomKnowledge", "Formulas", $"{formulaId}.csx");
            
            if (File.Exists(filePath))
            {
                rtbFormulaCode.Text = File.ReadAllText(filePath);
            }
            else
            {
                rtbFormulaCode.Text = $"// Plik {filePath} nie istnieje.";
            }
        }

        private void LstMacros_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstMacros.SelectedItem == null) return;
            string macroId = lstMacros.SelectedItem.ToString();
            
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string filePath = Path.Combine(appData, "Bricscad_AgentAI", "CustomKnowledge", "Macros", $"{macroId}.json");
            
            if (File.Exists(filePath))
            {
                rtbMacroJson.Text = File.ReadAllText(filePath);
            }
            else
            {
                rtbMacroJson.Text = $"// Plik {filePath} nie istnieje.";
            }
        }

        private async void BtnExecuteMacro_Click(object sender, EventArgs e)
        {
            if (lstMacros.SelectedItem == null)
            {
                MessageBox.Show("Proszę wybrać makro z listy.", "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string macroId = lstMacros.SelectedItem.ToString();
            btnExecuteMacro.Enabled = false;
            btnExecuteMacro.Text = "⏳ Wykonywanie...";

            try
            {
                await Task.Run(() =>
                {
                    // Używamy aktualnego dokumentu do kontekstu
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
                MessageBox.Show($"Błąd podczas wykonywania makra '{macroId}':\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        btnExecuteMacro.Enabled = true;
                        btnExecuteMacro.Text = "▶️ Wykonaj Wybrane Makro";
                    }));
                }
            }
        }
    }
}
