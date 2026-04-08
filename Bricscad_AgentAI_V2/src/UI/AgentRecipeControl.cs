using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.UI
{
    public class AgentRecipeControl : UserControl
    {
        private ListBox lstRecipes;
        private TextBox txtTriggerName;
        private RichTextBox txtRecipeDescription;
        private RichTextBox txtRecipeJson;
        private CheckedListBox clbCategories;
        private Button btnSave;
        private Button btnDelete;
        private Button btnTestInSandbox;
        private Button btnRunSequence;
        private SplitContainer splitMain;

        public AgentRecipeControl()
        {
            InitializeComponent();
            RefreshList();
            LoadCategories();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Font = new Font("Segoe UI", 9.5f);

            splitMain = new SplitContainer 
            { 
                Dock = DockStyle.Fill, 
                Orientation = Orientation.Vertical, 
                SplitterDistance = UISettingsManager.Settings.AgentRecipeSplitterDistance,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            splitMain.SplitterMoved += (s, e) => {
                UISettingsManager.Settings.AgentRecipeSplitterDistance = splitMain.SplitterDistance;
                UISettingsManager.Save();
            };

            // LEWY PANEL: Lista
            lstRecipes = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI Semibold", 10f)
            };
            lstRecipes.SelectedIndexChanged += LstRecipes_SelectedIndexChanged;
            splitMain.Panel1.Controls.Add(lstRecipes);

            // PRAWY PANEL: Edytor
            Panel panEditor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            
            // Trigger
            Label lblTrigger = new Label { Text = "Trigger Name (starts with $):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LightGray };
            txtTriggerName = new TextBox 
            { 
                Dock = DockStyle.Top, 
                BackColor = Color.FromArgb(45, 45, 45), 
                ForeColor = Color.Orange, 
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 11f, FontStyle.Bold)
            };
            
            // Opis
            Label lblDesc = new Label { Text = "Recipe Description (Instructions for Agent):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LightGray, Margin = new Padding(0, 10, 0, 0) };
            txtRecipeDescription = new RichTextBox 
            { 
                Dock = DockStyle.Top, 
                Height = 80, 
                BackColor = Color.FromArgb(40, 40, 40), 
                ForeColor = Color.White, 
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 0, 0, 10)
            };

            // Kategorie
            Label lblCats = new Label { Text = "Auto-Load Categories (Tags):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LightGray };
            clbCategories = new CheckedListBox
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.LightSteelBlue,
                BorderStyle = BorderStyle.None,
                CheckOnClick = true
            };

            // JSON Editor
            Label lblJson = new Label { Text = "Tool Calls Sequence (JSON Array):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LightGray, Margin = new Padding(0, 10, 0, 0) };
            txtRecipeJson = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true
            };
            txtRecipeJson.TextChanged += (s, e) => JsonSyntaxHighlighter.Highlight(txtRecipeJson);

            // Dolne przyciski
            Panel panBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(0, 10, 0, 0) };
            btnSave = CreateButton("💾 Zapisz Przepis", Color.FromArgb(0, 122, 204), DockStyle.Right, 150);
            btnSave.Click += BtnSave_Click;
            
            btnDelete = CreateButton("🗑️ Usuń", Color.FromArgb(120, 40, 40), DockStyle.Left, 100);
            btnDelete.Click += BtnDelete_Click;

            btnTestInSandbox = CreateButton("🧪 Testuj w Sandboxie", Color.FromArgb(60, 60, 60), DockStyle.Right, 180);
            btnTestInSandbox.Click += BtnTestInSandbox_Click;

            btnRunSequence = CreateButton("🚀 Testuj Sekwencję", Color.SeaGreen, DockStyle.Right, 160);
            btnRunSequence.Click += BtnRunSequence_Click;

            panBottom.Controls.Add(btnSave);
            panBottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 }); // Spacer
            panBottom.Controls.Add(btnRunSequence);
            panBottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 }); // Spacer
            panBottom.Controls.Add(btnTestInSandbox);
            panBottom.Controls.Add(btnDelete);

            panEditor.Controls.Add(txtRecipeJson);
            panEditor.Controls.Add(lblJson);
            panEditor.Controls.Add(clbCategories);
            panEditor.Controls.Add(lblCats);
            panEditor.Controls.Add(txtRecipeDescription);
            panEditor.Controls.Add(lblDesc);
            panEditor.Controls.Add(txtTriggerName);
            panEditor.Controls.Add(lblTrigger);
            panEditor.Controls.Add(panBottom);

            splitMain.Panel2.Controls.Add(panEditor);
            this.Controls.Add(splitMain);
        }

        private Button CreateButton(string text, Color backColor, DockStyle dock, int width)
        {
            return new Button
            {
                Text = text,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Dock = dock,
                Width = width,
                Cursor = Cursors.Hand
            };
        }

        private void LoadCategories()
        {
            clbCategories.Items.Clear();
            var cats = ToolConfigManager.GetAvailableCategories();
            foreach (var cat in cats) clbCategories.Items.Add(cat);
        }

        private void RefreshList()
        {
            lstRecipes.Items.Clear();
            var recipes = RecipeManager.GetAll();
            foreach (var r in recipes) lstRecipes.Items.Add("$" + r.Trigger);
        }

        private void LstRecipes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstRecipes.SelectedIndex < 0) return;
            string trigger = lstRecipes.SelectedItem.ToString();
            var recipe = RecipeManager.GetByTrigger(trigger);
            if (recipe != null)
            {
                txtTriggerName.Text = "$" + recipe.Trigger;
                txtRecipeDescription.Text = recipe.Description;
                txtRecipeJson.Text = JsonConvert.SerializeObject(recipe.ToolExample, Formatting.Indented);
                
                for (int i = 0; i < clbCategories.Items.Count; i++)
                {
                    clbCategories.SetItemChecked(i, recipe.AutoLoadCategories.Contains(clbCategories.Items[i].ToString()));
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTriggerName.Text)) return;
            
            try
            {
                string trigger = txtTriggerName.Text.TrimStart('$');
                var recipe = new AgentRecipe
                {
                    Trigger = trigger,
                    Description = txtRecipeDescription.Text,
                    ToolExample = JArray.Parse(txtRecipeJson.Text),
                    AutoLoadCategories = clbCategories.CheckedItems.Cast<string>().ToList()
                };
                
                RecipeManager.AddOrUpdate(recipe);
                RefreshList();
                MessageBox.Show("Przepis zapisany pomyślnie.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu JSON: " + ex.Message);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (lstRecipes.SelectedIndex < 0) return;
            string trigger = lstRecipes.SelectedItem.ToString().TrimStart('$');
            if (MessageBox.Show($"Usunąć przepis {trigger}?", "Usuwanie", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                RecipeManager.Delete(trigger);
                RefreshList();
                ClearFields();
            }
        }

        private void ClearFields()
        {
            txtTriggerName.Clear();
            txtRecipeDescription.Clear();
            txtRecipeJson.Clear();
            for (int i = 0; i < clbCategories.Items.Count; i++) clbCategories.SetItemChecked(i, false);
        }

        private void BtnTestInSandbox_Click(object sender, EventArgs e)
        {
            try
            {
                var array = JArray.Parse(txtRecipeJson.Text);
                if (array.Count == 0) return;

                if (array.Count == 1)
                {
                    SendToSandbox(array[0]);
                }
                else
                {
                    ContextMenuStrip menu = new ContextMenuStrip();
                    for (int i = 0; i < array.Count; i++)
                    {
                        var call = array[i];
                        string name = call["function"]?["name"]?.ToString() ?? $"Krok {i+1}";
                        int idx = i;
                        menu.Items.Add($"{i+1}. {name}", null, (s, ev) => SendToSandbox(call));
                    }
                    menu.Show(btnTestInSandbox, new Point(0, btnTestInSandbox.Height));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd składni JSON: " + ex.Message);
            }
        }

        private void SendToSandbox(JToken toolCall)
        {
            try
            {
                string name = toolCall["function"]?["name"]?.ToString();
                var args = toolCall["function"]?["arguments"] as JObject;

                if (string.IsNullOrEmpty(name) || args == null)
                {
                    // Fallback dla uproszczonego formatu (jeśli ktoś wpisał bez wrapperów)
                    MessageBox.Show("Niepoprawny format kroku. Krok musi zawierać 'function' z 'name' i 'arguments'.");
                    return;
                }

                if (ToolSandboxControl.Instance != null)
                {
                    ToolSandboxControl.Instance.LoadToolCall(name, args);
                    // Switch tab
                    var studio = this.Parent.Parent as DatasetStudioControl; // Recipes -> TabPage -> TabControl -> DatasetStudio
                    if (studio != null)
                    {
                        foreach (TabPage tp in (studio.Controls[0] as TabControl).TabPages)
                        {
                            if (tp.Text.Contains("Sandbox"))
                            {
                                (studio.Controls[0] as TabControl).SelectedTab = tp;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd przesyłania do Sandboxa: " + ex.Message);
            }
        }

        private void BtnRunSequence_Click(object sender, EventArgs e)
        {
            try
            {
                var array = JArray.Parse(txtRecipeJson.Text);
                if (array.Count == 0) return;

                var doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                int successCount = 0;
                var logs = new System.Text.StringBuilder();
                logs.AppendLine($"--- Rozpoczynanie sekwencji ({DateTime.Now:HH:mm:ss}) ---");

                using (var loc = doc.LockDocument())
                {
                    foreach (var call in array)
                    {
                        string name = call["function"]?["name"]?.ToString();
                        var args = call["function"]?["arguments"] as JObject;

                        if (string.IsNullOrEmpty(name) || args == null)
                        {
                            logs.AppendLine($"[BŁĄD] Nieprawidłowy format kroku: {call.ToString(Formatting.None)}");
                            break;
                        }

                        logs.AppendLine($"[WYKONUJĘ]: {name}...");
                        string result = ToolOrchestrator.Instance.ExecuteTool(name, args, doc);
                        logs.AppendLine($"[WYNIK]: {result}");

                        if (result.Contains("BŁĄD")) break;
                        successCount++;
                    }
                }

                logs.AppendLine($"--- Zakończono. Wykonano pomyślnie {successCount}/{array.Count} kroków. ---");
                MessageBox.Show(logs.ToString(), "Wynik testu sekwencji");
            }
            catch (JsonException jex)
            {
                MessageBox.Show($"Błąd składni JSON: {jex.Message}\n\nSugestia: Sprawdź czy nie brakuje przecinków między krokami lub cudzysłowów.", "Błąd Walidacji");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd wykonania: " + ex.Message);
            }
        }

        public void InitFromCapture(JArray toolCalls)
        {
            ClearFields();
            txtTriggerName.Text = "$nowy_przepis";
            txtRecipeJson.Text = JsonConvert.SerializeObject(toolCalls, Formatting.Indented);
            txtRecipeDescription.Text = "Przechwycono z sesji dnia " + DateTime.Now.ToString("g");
        }
    }
}
