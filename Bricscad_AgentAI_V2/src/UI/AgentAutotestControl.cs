using System;
using Bricscad.ApplicationServices;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Application = Bricscad.ApplicationServices.Application;

namespace Bricscad_AgentAI_V2.UI
{
    public class AgentAutotestControl : UserControl
    {
        private LLMClient _client;
        private ListView lvAutoTools;
        private Button btnRunAutotest;
        private RichTextBox txtAutoConsole;
        private RadioButton rbStatic;
        private RadioButton rbInteractive;

        public AgentAutotestControl(LLMClient client)
        {
            _client = client;
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(30, 30, 30);

            SplitContainer splitAuto = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };
            
            // PANEL LEWY (Lista narzÄ™dzi)
            Panel panLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            Label lblTools = new Label { Text = "DostÄ™pne narzÄ™dzia (IToolV2):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LightSkyBlue, TextAlign = ContentAlignment.MiddleLeft };
            panLeft.Controls.Add(lblTools);

            lvAutoTools = new ListView {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CheckBoxes = true,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            lvAutoTools.Columns.Add("NarzÄ™dzie", 250);
            lvAutoTools.Columns.Add("Statyczne", 85);
            lvAutoTools.Columns.Add("Interaktywne", 100);
            lvAutoTools.Columns.Add("Status", 180);
            ContextMenuStrip ctxMenu = new ContextMenuStrip();
            var showHistoryItem = ctxMenu.Items.Add("PokaĹĽ historiÄ™ testĂłw");
            showHistoryItem.Click += (s, e) => {
                if (lvAutoTools.SelectedItems.Count > 0)
                {
                    string toolName = lvAutoTools.SelectedItems[0].Text;
                    string logPath = Path.Combine(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", "..")), "Autotesty", "Logs", $"{toolName}_History.md");
                    if (File.Exists(logPath))
                        System.Diagnostics.Process.Start(logPath);
                    else
                        MessageBox.Show("Brak historii dla tego narzÄ™dzia.");
                }
            };
            var markFixedItem = ctxMenu.Items.Add("Oznacz jako poprawione (RÄ™cznie)");
            markFixedItem.Click += (s, e) => {
                if (lvAutoTools.SelectedItems.Count > 0)
                {
                    string toolName = lvAutoTools.SelectedItems[0].Text;
                    AutotestRegistry.MarkAsFixed(toolName);
                    string logPath = Path.Combine(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", "..")), "Autotesty", "Logs", $"{toolName}_History.md");
                    if (!Directory.Exists(Path.GetDirectoryName(logPath))) Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    File.AppendAllText(logPath, $"\n\n## [USER FIX] - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nUĹĽytkownik rÄ™cznie oznaczyĹ‚ narzÄ™dzie jako poprawione.\n---\n");
                    LoadAutotestTools();
                }
            };
            lvAutoTools.ContextMenuStrip = ctxMenu;
            panLeft.Controls.Add(lvAutoTools);

            Panel panButtons = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(0, 5, 0, 0) };
            
            Button btnSelectAll = new Button { Text = "Zaznacz wszystko", Dock = DockStyle.Top, Height = 25, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 60, 60), Cursor = Cursors.Hand };
            btnSelectAll.Click += (s, e) => { foreach (ListViewItem item in lvAutoTools.Items) item.Checked = true; };
            
            Button btnDeselectAll = new Button { Text = "Odznacz wszystko", Dock = DockStyle.Top, Height = 25, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 60, 60), Cursor = Cursors.Hand };
            btnDeselectAll.Click += (s, e) => { foreach (ListViewItem item in lvAutoTools.Items) item.Checked = false; };
            
            panButtons.Controls.Add(btnDeselectAll);
            panButtons.Controls.Add(btnSelectAll);
            panLeft.Controls.Add(panButtons);

            btnRunAutotest = new Button { Text = "đźš€ URUCHOM AUTOTEST", Dock = DockStyle.Bottom, Height = 45, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(0, 122, 204), Font = new Font(this.Font, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(0, 5, 0, 0) };
            btnRunAutotest.Click += BtnRunAutotest_Click;
            panLeft.Controls.Add(btnRunAutotest);

            Panel panModes = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(0, 5, 0, 5) };
            rbStatic = new RadioButton { Text = "Analiza Statyczna (Zalecane)", Dock = DockStyle.Top, Checked = true, ForeColor = Color.LightGray };
            rbInteractive = new RadioButton { Text = "Test Interaktywny (UI)", Dock = DockStyle.Top, ForeColor = Color.Orange };
            panModes.Controls.Add(rbInteractive);
            panModes.Controls.Add(rbStatic);
            panLeft.Controls.Add(panModes);

            splitAuto.Panel1.Controls.Add(panLeft);

            // PANEL PRAWY (Konsola)
            Panel panRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            Label lblConsole = new Label { Text = "Konsola Diagnostyczna (Logi Autotestu):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LimeGreen, TextAlign = ContentAlignment.MiddleLeft };
            panRight.Controls.Add(lblConsole);

            txtAutoConsole = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.White, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None, ReadOnly = true };
            panRight.Controls.Add(txtAutoConsole);

            splitAuto.Panel2.Controls.Add(panRight);

            this.Controls.Add(splitAuto);

            LoadAutotestTools();
        }

        private void LoadAutotestTools()
        {
            lvAutoTools.Items.Clear();
            var allTools = ToolOrchestrator.Instance.GetToolsPayload(new[] { "#all" });
            foreach (var t in allTools.OrderBy(x => x.Function.Name))
            {
                var record = AutotestRegistry.GetRecord(t.Function.Name);
                ListViewItem item = new ListViewItem(t.Function.Name);
                item.SubItems.Add(record.StaticTestsCount.ToString());
                item.SubItems.Add(record.InteractiveTestsCount.ToString());
                item.SubItems.Add(record.LastStatus);
                
                if (record.LastStatus.Contains("Sukces") || record.LastStatus.Contains("Poprawione"))
                    item.ForeColor = Color.LimeGreen;
                else if (record.LastStatus.Contains("Failed") || record.LastStatus.Contains("Wymaga"))
                    item.ForeColor = Color.OrangeRed;

                lvAutoTools.Items.Add(item);
            }
        }

        private void AppendAutoLog(string text, Color color)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendAutoLog(text, color)));
                return;
            }
            txtAutoConsole.SelectionStart = txtAutoConsole.TextLength;
            txtAutoConsole.SelectionLength = 0;
            txtAutoConsole.SelectionColor = color;
            txtAutoConsole.AppendText(text + "\n");
            txtAutoConsole.SelectionColor = txtAutoConsole.ForeColor;
            txtAutoConsole.ScrollToCaret();
        }

        private string LoadSystemPromptForProfile(string profileName)
        {
            string prompt = ToolConfigManager.LoadEffectivePromptForProfile(profileName);
            return string.IsNullOrWhiteSpace(prompt)
                ? "Jestes asystentem BricsCAD V2. Odpowiadaj z Tool Calling. Wykonuj zadania precyzyjnie."
                : prompt;
        }

        private async void BtnRunAutotest_Click(object sender, EventArgs e)
        {
            if (lvAutoTools.CheckedItems.Count == 0)
            {
                MessageBox.Show("Zaznacz co najmniej jedno narzÄ™dzie do przetestowania.");
                return;
            }

            btnRunAutotest.Enabled = false;
            btnRunAutotest.Text = "âŹł TRWA AUTOTEST...";
            txtAutoConsole.Clear();
            AppendAutoLog($"[SYSTEM] Rozpoczynanie autotestu dla {lvAutoTools.CheckedItems.Count} narzÄ™dzi o godzinie {DateTime.Now:HH:mm:ss}...", Color.Cyan);

            string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
            string logsDir = Path.Combine(rootDir, "Autotesty", "Logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

            try
            {
                foreach (ListViewItem item in lvAutoTools.CheckedItems)
                {
                    string toolName = item.Text;
                    AppendAutoLog($"\n[TEST] ---> NarzÄ™dzie: {toolName}", Color.Yellow);
                    
                    AgentMemoryState.Clear();
                    AgentMemoryState.Variables.Clear();

                    string systemPrompt = LoadSystemPromptForProfile("AuditorProfile");
                    if (string.IsNullOrEmpty(systemPrompt) || systemPrompt.Contains("JesteĹ› asystentem BricsCAD"))
                    {
                        systemPrompt = "JesteĹ› Rewidentem (QA Agent). Twoim zadaniem jest przeprowadzenie rygorystycznego testu logiki narzÄ™dzia.";
                    }

                    string prompt = "";
                    string[] toolsPayload;

                    if (rbStatic.Checked)
                    {
                        string sourceCode = GetToolSourceCode(toolName);
                        prompt = $@"PrzeprowadĹş peĹ‚ny audyt statyczny (Static Analysis) dla narzÄ™dzia: {toolName}.

Oto kod ĹşrĂłdĹ‚owy C# tego narzÄ™dzia:
```csharp
{sourceCode}
```

Zadanie:
1. Przeanalizuj logikÄ™, walidacjÄ™ parametrĂłw i bezpieczeĹ„stwo.
2. Zidentyfikuj ewentualne bĹ‚Ä™dy i luki.
3. Wygeneruj krĂłtki Raport Audytu (Markdown).
UWAGA: ZablokowaĹ‚em Ci moĹĽliwoĹ›Ä‡ fizycznego wywoĹ‚ania narzÄ™dzi (brak flagi #all), wiÄ™c testuj wyĹ‚Ä…cznie statycznie.";
                        toolsPayload = new string[0]; // Brak narzÄ™dzi do wykonania
                    }
                    else
                    {
                        prompt = $"ZleÄ‡ Rewidentowi przetestowanie logiki polecenia {toolName}. Zwaliduj jego parametry i wywoĹ‚aj je. UWAGA: To jest TEST INTERAKTYWNY, moĹĽesz fizycznie modyfikowaÄ‡ rysunek lub wywoĹ‚aÄ‡ monity w konsoli. Wygeneruj krĂłtki raport.";
                        toolsPayload = new[] { "#all" }; // PeĹ‚en dostÄ™p
                    }

                    var history = new List<ChatMessage> {
                        new ChatMessage { Role = "system", Content = systemPrompt },
                        new ChatMessage { Role = "user", Content = prompt }
                    };

                    Document doc = Application.DocumentManager.MdiActiveDocument;
                    var result = await _client.SendMessageReActAsync(history, new CadExecutionContext(doc), toolsPayload, true, 10, "AuditorProfile");
                    
                    string response = result.DisplayMessage;
                    
                    bool passed = true;
                    if (response.ToLower().Contains("bĹ‚Ä…d") || response.ToLower().Contains("error") || response.ToLower().Contains("niepowodzenie") || response.Contains("FAILED"))
                    {
                        AppendAutoLog($"[WYNIK] {toolName}: Znaleziono potencjalne bĹ‚Ä™dy.", Color.OrangeRed);
                        passed = false;
                    }
                    else
                    {
                        AppendAutoLog($"[WYNIK] {toolName}: ZakoĹ„czono pomyĹ›lnie.", Color.LimeGreen);
                    }
                    AppendAutoLog(response, Color.LightGray);

                    AutotestRegistry.UpdateRecord(toolName, rbStatic.Checked, passed);

                    string logPath = Path.Combine(logsDir, $"{toolName}_History.md");
                    string logContent = $"\n\n## Raport Autotestu - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n**Tryb:** {(rbStatic.Checked ? "Statyczny" : "Interaktywny")}\n**Wynik:** {(passed ? "Sukces" : "BĹ‚Ä™dy")}\n\n{response}\n---";
                    File.AppendAllText(logPath, logContent);
                }

                // OdĹ›wieĹĽ tabelÄ™ po zakoĹ„czeniu testĂłw
                LoadAutotestTools();

                AppendAutoLog($"\n[SYSTEM] ZakoĹ„czono wszystkie testy. Logi zapisane w folderze Autotesty/Logs/", Color.Cyan);
            }
            catch (Exception ex)
            {
                AppendAutoLog($"\n[KRYTYCZNY BĹÄ„D AUTOTESTU]: {ex.Message}", Color.Red);
            }
            finally
            {
                btnRunAutotest.Enabled = true;
                btnRunAutotest.Text = "đźš€ URUCHOM AUTOTEST";
            }
        }

        private string GetToolSourceCode(string toolName)
        {
            try
            {
                string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                // Szukamy w folderze src (wyĹĽej w drzewie katalogĂłw z bin/Debug)
                string rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..")); 
                string toolsDir = Path.Combine(rootDir, "src", "Tools");
                
                if (Directory.Exists(toolsDir))
                {
                    string[] files = Directory.GetFiles(toolsDir, "*Tool*.cs", SearchOption.AllDirectories);
                    // Najpierw szukamy po nazwie polecenia np. InsertBlockTool.cs
                    string match = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(toolName + "Tool", StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                    {
                        match = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).IndexOf(toolName, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    
                    if (match != null)
                    {
                        return File.ReadAllText(match);
                    }
                }
                return "// NIE ZNALEZIONO KODU ĹąRĂ“DĹOWEGO NARZÄDZIA W FOLDERZE SRC/TOOLS";
            }
            catch (Exception ex)
            {
                return $"// BĹÄ„D PODCZAS ODCZYTU KODU ĹąRĂ“DĹOWEGO: {ex.Message}";
            }
        }
    }
}
