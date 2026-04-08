using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Application = Bricscad.ApplicationServices.Application;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;

namespace Bricscad_AgentAI_V2.UI
{
    public class ToolSandboxControl : UserControl
    {
        private ComboBox cmbTools;
        private ComboBox cmbExamples;
        private RichTextBox txtArgs;
        private RichTextBox txtParamDoc;
        private RichTextBox txtLog;
        private Button btnExecute;
        private Button btnClearLog;
        private Button btnLoadSelection;
        private Label lblDescription;
        private SplitContainer splitHeader;

        public ToolSandboxControl()
        {
            InitializeComponent();
            PopulateTools();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Font = new Font("Segoe UI", 9.5f);

            // GŁÓWNY KONTENER (Spliter: góra Edytor, dół Logi)
            SplitContainer splitMain = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 450 };
            
            // --- GÓRA (Panel Sterowania i Edytor) ---
            Panel panTop = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            
            // SPLIT HEADER (Resizable: góra Wybór, dół Dokumentacja)
            splitHeader = new SplitContainer 
            { 
                Dock = DockStyle.Top, 
                Orientation = Orientation.Horizontal, 
                Height = UISettingsManager.Settings.ToolSandboxHeaderHeight,
                SplitterWidth = 4,
                BackColor = Color.FromArgb(50, 50, 50)
            };
            splitHeader.SplitterMoved += (s, e) => {
                UISettingsManager.Settings.ToolSandboxHeaderHeight = splitHeader.Height;
                UISettingsManager.Save();
            };

            // Panel wyboru (Góra splitera nagłówka)
            Panel panTools = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 5), BackColor = Color.FromArgb(30, 30, 30) };
            Label lblPick = new Label { Text = "🛠️ Wybierz narzędzie:", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LightGray };
            cmbTools = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbTools.SelectedIndexChanged += CmbTools_SelectedIndexChanged;
            
            lblDescription = new Label { Text = "Opis narzędzia...", Dock = DockStyle.Top, Height = 45, ForeColor = Color.DarkGray, Font = new Font(this.Font, FontStyle.Italic), Padding = new Padding(0, 5, 0, 5) };

            Label lblExampleTag = new Label { Text = "📋 Wczytaj przykład (Snippets):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LightGray, Margin = new Padding(0, 10, 0, 0) };
            cmbExamples = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.Cyan, FlatStyle = FlatStyle.Flat };
            cmbExamples.SelectedIndexChanged += CmbExamples_SelectedIndexChanged;
            
            panTools.Controls.Add(cmbExamples);
            panTools.Controls.Add(lblExampleTag);
            panTools.Controls.Add(lblDescription);
            panTools.Controls.Add(cmbTools);
            panTools.Controls.Add(lblPick);

            // Panel dokumentacji (Dół splitera nagłówka)
            Panel panDoc = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0), BackColor = Color.FromArgb(30, 30, 30) };
            txtParamDoc = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10f),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtParamDoc.DoubleClick += TxtParamDoc_DoubleClick;

            panDoc.Controls.Add(txtParamDoc);
            panDoc.Controls.Add(new Label { Text = "📖 Dokumentacja (Discovery) - Kliknij 2x by wstawić parametr:", Dock = DockStyle.Top, Height = 25, ForeColor = Color.DarkGray, Font = new Font(this.Font, FontStyle.Bold) });

            splitHeader.Panel1.Controls.Add(panTools);
            splitHeader.Panel2.Controls.Add(panDoc);

            Panel panButtons = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(0, 5, 0, 0) };
            btnExecute = CreateButton("🚀 Execute Tool", Color.FromArgb(0, 122, 204), DockStyle.Right, 150);
            btnExecute.Click += BtnExecute_Click;
            
            btnLoadSelection = CreateButton("🎯 Load CAD Selection", Color.FromArgb(60, 60, 60), DockStyle.Left, 180);
            btnLoadSelection.Click += BtnLoadSelection_Click;

            panButtons.Controls.Add(btnExecute);
            panButtons.Controls.Add(btnLoadSelection);

            txtArgs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10.5f),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true
            };
            txtArgs.TextChanged += (s, e) => JsonSyntaxHighlighter.Highlight(txtArgs);

            panTop.Controls.Add(txtArgs);
            panTop.Controls.Add(panButtons);
            panTop.Controls.Add(new Label { Text = "⌨️ Edytor Argumentów JSON (komentarze // są dozwolone):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.Orange, Margin = new Padding(0, 5, 0, 0) });
            panTop.Controls.Add(splitHeader);

            splitMain.Panel1.Controls.Add(panTop);

            // --- DÓŁ (Logi) ---
            Panel panBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            
            Panel panLogHeader = new Panel { Dock = DockStyle.Top, Height = 30 };
            Label lblLog = new Label { Text = "📜 Log wyników:", Dock = DockStyle.Left, Width = 150, ForeColor = Color.Cyan, TextAlign = ContentAlignment.MiddleLeft };
            btnClearLog = CreateButton("🗑️ Wyczyść", Color.FromArgb(80, 40, 40), DockStyle.Right, 80);
            btnClearLog.Click += (s, e) => txtLog.Clear();
            panLogHeader.Controls.Add(lblLog);
            panLogHeader.Controls.Add(btnClearLog);

            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Consolas", 9f),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            panBottom.Controls.Add(txtLog);
            panBottom.Controls.Add(panLogHeader);
            
            splitMain.Panel2.Controls.Add(panBottom);

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
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private void PopulateTools()
        {
            if (ToolOrchestrator.Instance == null) return;
            var tools = ToolOrchestrator.Instance.GetRegisteredTools()
                .OrderBy(t => t.GetToolSchema()?.Function?.Name)
                .ToList();

            cmbTools.DisplayMember = "Name";
            foreach (var tool in tools)
            {
                var schema = tool.GetToolSchema();
                if (schema?.Function != null)
                {
                    cmbTools.Items.Add(new ToolItem { Name = schema.Function.Name, Tool = tool });
                }
            }
        }

        private void CmbTools_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbTools.SelectedItem is ToolItem item)
            {
                var schema = item.Tool.GetToolSchema();
                lblDescription.Text = schema.Function.Description ?? "Brak opisu.";

                UpdateParamDoc(schema.Function.Parameters);
                LoadExamples(item.Tool.Examples);

                // Generowanie MINIMALISTYCZNEGO szablonu JSON (tylko pola Required)
                var template = GenerateJsonTemplate(schema.Function.Parameters);
                txtArgs.Text = template;
            }
        }

        private void LoadExamples(List<string> examples)
        {
            cmbExamples.Items.Clear();
            if (examples == null || examples.Count == 0)
            {
                cmbExamples.Items.Add("Brak dostępnych przykładów");
                cmbExamples.SelectedIndex = 0;
                cmbExamples.Enabled = false;
                return;
            }

            cmbExamples.Enabled = true;
            foreach (var ex in examples)
            {
                cmbExamples.Items.Add(ex);
            }
            cmbExamples.SelectedIndex = -1;
        }

        private void CmbExamples_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbExamples.SelectedIndex >= 0 && cmbExamples.Enabled)
            {
                txtArgs.Text = cmbExamples.SelectedItem.ToString();
            }
        }

        private void UpdateParamDoc(ParametersSchema parameters)
        {
            txtParamDoc.Clear();
            if (parameters?.Properties == null) return;

            foreach (var prop in parameters.Properties)
            {
                bool isRequired = parameters.Required != null && parameters.Required.Contains(prop.Key);

                txtParamDoc.SelectionColor = isRequired ? Color.LightCoral : Color.LightSteelBlue;
                txtParamDoc.SelectionFont = new Font(txtParamDoc.Font, FontStyle.Bold);
                txtParamDoc.AppendText($"• {prop.Key} ");

                txtParamDoc.SelectionColor = Color.Gray;
                txtParamDoc.SelectionFont = new Font(txtParamDoc.Font, FontStyle.Regular);
                txtParamDoc.AppendText($"({prop.Value.Type})");
                
                if (isRequired)
                {
                    txtParamDoc.SelectionColor = Color.Coral;
                    txtParamDoc.AppendText(" [REQUIRED]");
                }

                txtParamDoc.AppendText(": ");

                txtParamDoc.SelectionColor = Color.White;
                txtParamDoc.AppendText(prop.Value.Description + Environment.NewLine);
            }
        }

        private void TxtParamDoc_DoubleClick(object sender, EventArgs e)
        {
            int pos = txtParamDoc.SelectionStart;
            int lineIdx = txtParamDoc.GetLineFromCharIndex(pos);
            if (lineIdx < 0 || lineIdx >= txtParamDoc.Lines.Length) return;

            string line = txtParamDoc.Lines[lineIdx];
            var match = Regex.Match(line, @"•\s*(\w+)");
            if (match.Success)
            {
                string paramName = match.Groups[1].Value;
                InsertParameterToEditor(paramName);
            }
        }

        private void InsertParameterToEditor(string paramName)
        {
            string current = txtArgs.Text.Trim();
            if (current.Contains($"\"{paramName}\""))
            {
                int idx = txtArgs.Text.IndexOf($"\"{paramName}\"");
                txtArgs.Focus();
                txtArgs.SelectionStart = idx;
                txtArgs.SelectionLength = paramName.Length + 2;
                return;
            }

            string marker = "[[CURSOR]]";
            string valTemplate = $"\"{marker}\"";

            if (string.IsNullOrEmpty(current) || !current.Contains("{"))
            {
                txtArgs.Text = "{\n  \"" + paramName + "\": " + valTemplate + "\n}";
            }
            else
            {
                int lastBrace = txtArgs.Text.LastIndexOf('}');
                if (lastBrace >= 0)
                {
                    string before = txtArgs.Text.Substring(0, lastBrace).TrimEnd();
                    bool needsComma = before.Length > 1 && !before.EndsWith("{") && !before.EndsWith(",");
                    string comma = needsComma ? "," : "";
                    
                    string newContent = before + comma + "\n  \"" + paramName + "\": " + valTemplate + "\n}";
                    txtArgs.Text = newContent;
                }
            }

            int cursorIdx = txtArgs.Text.IndexOf(marker);
            if (cursorIdx >= 0)
            {
                txtArgs.Text = txtArgs.Text.Remove(cursorIdx, marker.Length);
                txtArgs.Focus();
                txtArgs.SelectionStart = cursorIdx;
                txtArgs.SelectionLength = 0;
            }
        }

        private string GenerateJsonTemplate(ParametersSchema parameters)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            if (parameters?.Properties != null && parameters.Required != null)
            {
                var requiredProps = parameters.Properties
                    .Where(p => parameters.Required.Contains(p.Key))
                    .ToList();

                for (int i = 0; i < requiredProps.Count; i++)
                {
                    var prop = requiredProps[i];
                    string val = GetPlaceholderValue(prop.Value);
                    string comma = (i < requiredProps.Count - 1) ? "," : "";
                    sb.AppendLine($"  \"{prop.Key}\": {val}{comma} // {prop.Value.Description}");
                }
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GetPlaceholderValue(ToolParameter param)
        {
            if (param.Properties != null && param.Properties.Count > 0)
            {
                return "{ }";
            }

            switch (param.Type?.ToLower())
            {
                case "string": return "\"\"";
                case "number":
                case "integer": return "0";
                case "boolean": return "false";
                case "array": return "[ ]";
                default: return "null";
            }
        }

        private void BtnExecute_Click(object sender, EventArgs e)
        {
            if (cmbTools.SelectedItem is ToolItem item)
            {
                Log($"--- Uruchamianie: {item.Name} ---", Color.Gold);
                try
                {
                    string cleanJson = Regex.Replace(txtArgs.Text, @"//.*$", "", RegexOptions.Multiline);
                    var args = JObject.Parse(cleanJson);
                    Document doc = Application.DocumentManager.MdiActiveDocument;
                    
                    using (var loc = doc.LockDocument())
                    {
                        string result = ToolOrchestrator.Instance.ExecuteTool(item.Name, args, doc);
                        Log($"[WYNIK]: {result}", result.Contains("BŁĄD") ? Color.OrangeRed : Color.LimeGreen);
                    }
                }
                catch (Exception ex)
                {
                    Log($"[BŁĄD PARSOWANIA/WYKONANIA]: {ex.Message}", Color.Red);
                }
                Log("------------------------------------", Color.Gold);
            }
        }

        private void BtnLoadSelection_Click(object sender, EventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            PromptSelectionResult selRes = ed.SelectImplied();
            
            if (selRes.Status == PromptStatus.OK)
            {
                var ids = selRes.Value.GetObjectIds();
                AgentMemoryState.Update(ids);
                Log($"[INFO] Załadowano {ids.Length} obiektów do AgentMemoryState.ActiveSelection.", Color.LightSkyBlue);
            }
            else
            {
                Log("[OSTRZEŻENIE] Brak zaznaczonych obiektów w CAD. Pamięć ActiveSelection została wyczyszczona.", Color.Orange);
                AgentMemoryState.Clear();
            }
        }

        private void Log(string message, Color color)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => Log(message, color)));
                return;
            }

            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = Color.Gray;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ");
            
            txtLog.SelectionColor = color;
            txtLog.AppendText(message + Environment.NewLine);
            
            txtLog.ScrollToCaret();
        }

        private class ToolItem
        {
            public string Name { get; set; }
            public IToolV2 Tool { get; set; }
            public override string ToString() => Name;
        }
    }
}
