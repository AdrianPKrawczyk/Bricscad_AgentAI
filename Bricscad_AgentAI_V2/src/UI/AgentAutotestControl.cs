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
            
            // PANEL LEWY (Lista narzędzi)
            Panel panLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            Label lblTools = new Label { Text = "Dostępne narzędzia (IToolV2):", Dock = DockStyle.Top, Height = 25, ForeColor = Color.LightSkyBlue, TextAlign = ContentAlignment.MiddleLeft };
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
            lvAutoTools.Columns.Add("Narzędzie", 250);
            lvAutoTools.Columns.Add("Statyczne", 85);
            lvAutoTools.Columns.Add("Interaktywne", 100);
            lvAutoTools.Columns.Add("Status", 180);
            ContextMenuStrip ctxMenu = new ContextMenuStrip();
            var showHistoryItem = ctxMenu.Items.Add("Pokaż historię testów");
            showHistoryItem.Click += (s, e) => {
                if (lvAutoTools.SelectedItems.Count > 0)
                {
                    string toolName = lvAutoTools.SelectedItems[0].Text;
                    string logPath = Path.Combine(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", "..")), "Autotesty", "Logs", $"{toolName}_History.md");
                    if (File.Exists(logPath))
                        System.Diagnostics.Process.Start(logPath);
                    else
                        MessageBox.Show("Brak historii dla tego narzędzia.");
                }
            };
            var markFixedItem = ctxMenu.Items.Add("Oznacz jako poprawione (Ręcznie)");
            markFixedItem.Click += (s, e) => {
                if (lvAutoTools.SelectedItems.Count > 0)
                {
                    string toolName = lvAutoTools.SelectedItems[0].Text;
                    AutotestRegistry.MarkAsFixed(toolName);
                    string logPath = Path.Combine(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", "..")), "Autotesty", "Logs", $"{toolName}_History.md");
                    if (!Directory.Exists(Path.GetDirectoryName(logPath))) Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    File.AppendAllText(logPath, $"\n\n## [USER FIX] - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nUżytkownik ręcznie oznaczył narzędzie jako poprawione.\n---\n");
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

            btnRunAutotest = new Button { Text = "🚀 URUCHOM AUTOTEST", Dock = DockStyle.Bottom, Height = 45, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(0, 122, 204), Font = new Font(this.Font, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(0, 5, 0, 0) };
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
            var profiles = ToolConfigManager.GetProfiles();
            if (profiles.TryGetValue(profileName, out var profile) && !string.IsNullOrEmpty(profile.SystemPromptFile))
            {
                string path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), profile.SystemPromptFile);
                if (File.Exists(path))
                {
                    return File.ReadAllText(path, System.Text.Encoding.UTF8);
                }
            }
            return "Jesteś asystentem BricsCAD V2. Odpowiadaj z Tool Calling. Wykonuj zadania precyzyjnie.";
        }

        private async void BtnRunAutotest_Click(object sender, EventArgs e)
        {
            if (lvAutoTools.CheckedItems.Count == 0)
            {
                MessageBox.Show("Zaznacz co najmniej jedno narzędzie do przetestowania.");
                return;
            }

            btnRunAutotest.Enabled = false;
            btnRunAutotest.Text = "⏳ TRWA AUTOTEST...";
            txtAutoConsole.Clear();
            AppendAutoLog($"[SYSTEM] Rozpoczynanie autotestu dla {lvAutoTools.CheckedItems.Count} narzędzi o godzinie {DateTime.Now:HH:mm:ss}...", Color.Cyan);

            string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
            string logsDir = Path.Combine(rootDir, "Autotesty", "Logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

            try
            {
                foreach (ListViewItem item in lvAutoTools.CheckedItems)
                {
                    string toolName = item.Text;
                    AppendAutoLog($"\n[TEST] ---> Narzędzie: {toolName}", Color.Yellow);
                    
                    AgentMemoryState.Clear();
                    AgentMemoryState.Variables.Clear();

                    string systemPrompt = LoadSystemPromptForProfile("AuditorProfile");
                    if (string.IsNullOrEmpty(systemPrompt) || systemPrompt.Contains("Jesteś asystentem BricsCAD"))
                    {
                        systemPrompt = "Jesteś Rewidentem (QA Agent). Twoim zadaniem jest przeprowadzenie rygorystycznego testu logiki narzędzia.";
                    }

                    string prompt = "";
                    string[] toolsPayload;

                    if (rbStatic.Checked)
                    {
                        string sourceCode = GetToolSourceCode(toolName);
                        prompt = $@"Przeprowadź pełny audyt statyczny (Static Analysis) dla narzędzia: {toolName}.

Oto kod źródłowy C# tego narzędzia:
```csharp
{sourceCode}
```

Zadanie:
1. Przeanalizuj logikę, walidację parametrów i bezpieczeństwo.
2. Zidentyfikuj ewentualne błędy i luki.
3. Wygeneruj krótki Raport Audytu (Markdown).
UWAGA: Zablokowałem Ci możliwość fizycznego wywołania narzędzi (brak flagi #all), więc testuj wyłącznie statycznie.";
                        toolsPayload = new string[0]; // Brak narzędzi do wykonania
                    }
                    else
                    {
                        prompt = $"Zleć Rewidentowi przetestowanie logiki polecenia {toolName}. Zwaliduj jego parametry i wywołaj je. UWAGA: To jest TEST INTERAKTYWNY, możesz fizycznie modyfikować rysunek lub wywołać monity w konsoli. Wygeneruj krótki raport.";
                        toolsPayload = new[] { "#all" }; // Pełen dostęp
                    }

                    var history = new List<ChatMessage> {
                        new ChatMessage { Role = "system", Content = systemPrompt },
                        new ChatMessage { Role = "user", Content = prompt }
                    };

                    Document doc = Application.DocumentManager.MdiActiveDocument;
                    var result = await _client.SendMessageReActAsync(history, new CadExecutionContext(doc), toolsPayload, true, 10, "AuditorProfile");
                    
                    string response = result.DisplayMessage;
                    
                    bool passed = true;
                    if (response.ToLower().Contains("błąd") || response.ToLower().Contains("error") || response.ToLower().Contains("niepowodzenie") || response.Contains("FAILED"))
                    {
                        AppendAutoLog($"[WYNIK] {toolName}: Znaleziono potencjalne błędy.", Color.OrangeRed);
                        passed = false;
                    }
                    else
                    {
                        AppendAutoLog($"[WYNIK] {toolName}: Zakończono pomyślnie.", Color.LimeGreen);
                    }
                    AppendAutoLog(response, Color.LightGray);

                    AutotestRegistry.UpdateRecord(toolName, rbStatic.Checked, passed);

                    string logPath = Path.Combine(logsDir, $"{toolName}_History.md");
                    string logContent = $"\n\n## Raport Autotestu - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n**Tryb:** {(rbStatic.Checked ? "Statyczny" : "Interaktywny")}\n**Wynik:** {(passed ? "Sukces" : "Błędy")}\n\n{response}\n---";
                    File.AppendAllText(logPath, logContent);
                }

                // Odśwież tabelę po zakończeniu testów
                LoadAutotestTools();

                AppendAutoLog($"\n[SYSTEM] Zakończono wszystkie testy. Logi zapisane w folderze Autotesty/Logs/", Color.Cyan);
            }
            catch (Exception ex)
            {
                AppendAutoLog($"\n[KRYTYCZNY BŁĄD AUTOTESTU]: {ex.Message}", Color.Red);
            }
            finally
            {
                btnRunAutotest.Enabled = true;
                btnRunAutotest.Text = "🚀 URUCHOM AUTOTEST";
            }
        }

        private string GetToolSourceCode(string toolName)
        {
            try
            {
                string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                // Szukamy w folderze src (wyżej w drzewie katalogów z bin/Debug)
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
                return "// NIE ZNALEZIONO KODU ŹRÓDŁOWEGO NARZĘDZIA W FOLDERZE SRC/TOOLS";
            }
            catch (Exception ex)
            {
                return $"// BŁĄD PODCZAS ODCZYTU KODU ŹRÓDŁOWEGO: {ex.Message}";
            }
        }
    }
}
