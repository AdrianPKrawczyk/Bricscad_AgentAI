using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
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
        private RichTextBox txtArgs;
        private RichTextBox txtLog;
        private Button btnExecute;
        private Button btnClearLog;
        private Button btnLoadSelection;
        private Label lblDescription;

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
            SplitContainer splitMain = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 350 };
            
            // --- GÓRA (Panel Sterowania i Edytor) ---
            Panel panTop = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            
            Panel panHeader = new Panel { Dock = DockStyle.Top, Height = 100 };
            
            Label lblPick = new Label { Text = "Wybierz narzędzie:", Dock = DockStyle.Top, Height = 20, ForeColor = Color.LightGray };
            cmbTools = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbTools.SelectedIndexChanged += CmbTools_SelectedIndexChanged;
            
            lblDescription = new Label { Text = "Opis narzędzia...", Dock = DockStyle.Fill, ForeColor = Color.DarkGray, Font = new Font(this.Font, FontStyle.Italic) };
            
            panHeader.Controls.Add(lblDescription);
            panHeader.Controls.Add(cmbTools);
            panHeader.Controls.Add(lblPick);

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
            panTop.Controls.Add(new Label { Text = "Argumenty JSON:", Dock = DockStyle.Top, Height = 25, ForeColor = Color.Orange, Margin = new Padding(0, 5, 0, 0) });
            panTop.Controls.Add(panHeader);

            splitMain.Panel1.Controls.Add(panTop);

            // --- DÓŁ (Logi) ---
            Panel panBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            
            Panel panLogHeader = new Panel { Dock = DockStyle.Top, Height = 30 };
            Label lblLog = new Label { Text = "Log wyników:", Dock = DockStyle.Left, Width = 150, ForeColor = Color.Cyan, TextAlign = ContentAlignment.MiddleLeft };
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

                // Generowanie szablonu JSON
                var template = GenerateJsonTemplate(schema.Function.Parameters);
                txtArgs.Text = JsonConvert.SerializeObject(template, Formatting.Indented);
            }
        }

        private JObject GenerateJsonTemplate(ParametersSchema parameters)
        {
            var obj = new JObject();
            if (parameters?.Properties == null) return obj;

            foreach (var prop in parameters.Properties)
            {
                obj[prop.Key] = GetDefaultValue(prop.Value);
            }
            return obj;
        }

        private JToken GetDefaultValue(ToolParameter param)
        {
            if (param.Properties != null && param.Properties.Count > 0)
            {
                var child = new JObject();
                foreach (var p in param.Properties) child[p.Key] = GetDefaultValue(p.Value);
                return child;
            }

            switch (param.Type?.ToLower())
            {
                case "string": return "";
                case "number":
                case "integer": return 0;
                case "boolean": return false;
                case "array": return new JArray();
                default: return null;
            }
        }

        private void BtnExecute_Click(object sender, EventArgs e)
        {
            if (cmbTools.SelectedItem is ToolItem item)
            {
                Log($"--- Uruchamianie: {item.Name} ---", Color.Gold);
                try
                {
                    var args = JObject.Parse(txtArgs.Text);
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
                Log("------------------------------------", Color.Gray);
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
