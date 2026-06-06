using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.UI
{
    public class HelpCenterControl : UserControl
    {
        private SplitContainer split;
        private TreeView treeMenu;
        private WebBrowser webView;
        private string helpDirectory;

        public HelpCenterControl()
        {
            InitializeComponent();
            LoadHelpIndex();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(45, 45, 48);

            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 250,
                BackColor = Color.FromArgb(28, 28, 28)
            };

            treeMenu = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None,
                HideSelection = false,
                ShowLines = false,
                ItemHeight = 30
            };
            treeMenu.AfterSelect += TreeMenu_AfterSelect;

            webView = new WebBrowser
            {
                Dock = DockStyle.Fill,
                ScriptErrorsSuppressed = true
            };

            split.Panel1.Controls.Add(treeMenu);
            split.Panel2.Controls.Add(webView);

            this.Controls.Add(split);
        }

        private void LoadHelpIndex()
        {
            try
            {
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                helpDirectory = Path.Combine(exeDir, "resources", "help");

                if (!Directory.Exists(helpDirectory))
                {
                    ShowErrorHtml($"Nie znaleziono folderu pomocy: {helpDirectory}");
                    return;
                }

                string indexFile = Path.Combine(helpDirectory, "index.json");
                if (!File.Exists(indexFile))
                {
                    ShowErrorHtml("Nie znaleziono pliku index.json w folderze pomocy.");
                    return;
                }

                string json = File.ReadAllText(indexFile);
                var items = JsonConvert.DeserializeObject<List<HelpIndexItem>>(json);

                if (items != null)
                {
                    items.Sort((a, b) => a.Order.CompareTo(b.Order));
                    foreach (var item in items)
                    {
                        var node = new TreeNode(item.Title);
                        node.Tag = item.File;
                        treeMenu.Nodes.Add(node);
                    }

                    if (treeMenu.Nodes.Count > 0)
                    {
                        treeMenu.SelectedNode = treeMenu.Nodes[0];
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorHtml($"Błąd ładowania indeksu pomocy: {ex.Message}");
            }
        }

        private void TreeMenu_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is string fileName)
            {
                LoadMarkdownFile(fileName);
            }
        }

        private void LoadMarkdownFile(string fileName)
        {
            try
            {
                string filePath = Path.Combine(helpDirectory, fileName);
                if (File.Exists(filePath))
                {
                    string mdContent = File.ReadAllText(filePath);
                    string html = ConvertMarkdownToHtml(mdContent);
                    webView.DocumentText = html;
                }
                else
                {
                    ShowErrorHtml($"Plik nie istnieje: {filePath}");
                }
            }
            catch (Exception ex)
            {
                ShowErrorHtml($"Błąd wczytywania pliku: {ex.Message}");
            }
        }

        private void ShowErrorHtml(string message)
        {
            string html = $@"
            <html><head><style>
                body {{ background-color: #1e1e1e; color: #ff5555; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; }}
            </style></head>
            <body><h3>Błąd</h3><p>{message}</p></body></html>";
            webView.DocumentText = html;
        }

        private string ConvertMarkdownToHtml(string markdown)
        {
            string html = markdown;

            // Zabezpieczenie HTML
            html = html.Replace("<", "&lt;").Replace(">", "&gt;");

            // --- Nagłówki ---
            html = Regex.Replace(html, @"^### (.*?)$", "<h3>$1</h3>", RegexOptions.Multiline);
            html = Regex.Replace(html, @"^## (.*?)$", "<h2>$1</h2>", RegexOptions.Multiline);
            html = Regex.Replace(html, @"^# (.*?)$", "<h1>$1</h1>", RegexOptions.Multiline);

            // --- Formatowanie tekstu ---
            html = Regex.Replace(html, @"\*\*(.*?)\*\*", "<strong>$1</strong>");
            html = Regex.Replace(html, @"\*(.*?)\*", "<em>$1</em>");
            html = Regex.Replace(html, @"`(.*?)`", "<code>$1</code>");

            // --- Bloki kodu ---
            html = Regex.Replace(html, @"```(?:.*?)\r?\n(.*?)\r?\n```", "<pre><code>$1</code></pre>", RegexOptions.Singleline);

            // --- Alerts/Cytaty ---
            html = Regex.Replace(html, @"^> \[!TIP\]\r?\n(?:> (.*?)\r?\n)+", m => "<div class='alert tip'>" + m.Value.Replace("> [!TIP]", "").Replace("> ", "") + "</div>", RegexOptions.Multiline);
            html = Regex.Replace(html, @"^> \[!IMPORTANT\]\r?\n(?:> (.*?)\r?\n)+", m => "<div class='alert important'>" + m.Value.Replace("> [!IMPORTANT]", "").Replace("> ", "") + "</div>", RegexOptions.Multiline);
            html = Regex.Replace(html, @"^> \[!WARNING\]\r?\n(?:> (.*?)\r?\n)+", m => "<div class='alert warning'>" + m.Value.Replace("> [!WARNING]", "").Replace("> ", "") + "</div>", RegexOptions.Multiline);
            html = Regex.Replace(html, @"^> (.*?)$", "<blockquote>$1</blockquote>", RegexOptions.Multiline);

            // --- Listy ---
            html = Regex.Replace(html, @"^\- (.*?)$", "<li>$1</li>", RegexOptions.Multiline);
            html = Regex.Replace(html, @"(?<=</li>)\r?\n(?=<li>)", ""); // Usuwanie przerw między elementami listy
            html = Regex.Replace(html, @"(<li>.*?</li>)", "<ul>$1</ul>", RegexOptions.Singleline);
            // Proste czyszczenie zagnieżdżonych ul tagów
            html = html.Replace("</ul><ul>", "");

            // --- Tabele (bardzo uproszczone, ignorujące alignment) ---
            html = Regex.Replace(html, @"^\|(.*)\| *$", m => {
                var row = m.Groups[1].Value;
                if (row.Contains("---")) return ""; // Ignorowanie wiersza separatora
                var cells = row.Split('|');
                string res = "<tr>";
                foreach (var c in cells) res += $"<td>{c.Trim()}</td>";
                res += "</tr>";
                return res;
            }, RegexOptions.Multiline);
            html = Regex.Replace(html, @"(<tr>.*?</tr>)", "<table border='1'>$1</table>", RegexOptions.Singleline);
            html = html.Replace("</table><table border='1'>", "");

            // --- Linie pionowe ---
            html = Regex.Replace(html, @"^--- *$", "<hr/>", RegexOptions.Multiline);

            // --- Akapity (Podwójny newline na <br/><br/>) ---
            html = Regex.Replace(html, @"\r?\n\r?\n", "<br/><br/>");

            string wrapper = $@"
            <html>
            <head>
                <style>
                    body {{
                        background-color: #1e1e1e;
                        color: #d4d4d4;
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        font-size: 14px;
                        line-height: 1.6;
                        padding: 20px;
                        margin: 0;
                    }}
                    h1, h2, h3 {{ color: #ffffff; font-weight: normal; margin-top: 1.5em; }}
                    h1 {{ border-bottom: 1px solid #333; padding-bottom: 5px; color: #007acc; }}
                    h2 {{ color: #4daafc; }}
                    code {{ background-color: #2d2d30; color: #dcdcaa; padding: 2px 5px; border-radius: 3px; font-family: Consolas, monospace; }}
                    pre {{ background-color: #2d2d30; padding: 10px; border-radius: 5px; overflow-x: auto; }}
                    pre code {{ background-color: transparent; padding: 0; }}
                    hr {{ border: 0; border-top: 1px solid #333; margin: 20px 0; }}
                    ul {{ padding-left: 20px; }}
                    li {{ margin-bottom: 5px; }}
                    blockquote {{ border-left: 4px solid #007acc; margin: 0; padding-left: 15px; color: #9cdcfe; }}
                    table {{ border-collapse: collapse; width: 100%; margin: 15px 0; }}
                    table, th, td {{ border: 1px solid #444; }}
                    th, td {{ padding: 8px; text-align: left; }}
                    .alert {{ padding: 10px; margin: 15px 0; border-left: 4px solid; border-radius: 3px; background-color: #252526; }}
                    .tip {{ border-color: #4CAF50; }}
                    .important {{ border-color: #2196F3; }}
                    .warning {{ border-color: #FF9800; }}
                </style>
            </head>
            <body>
                {html}
            </body>
            </html>";

            return wrapper;
        }
    }

    public class HelpIndexItem
    {
        public string Title { get; set; }
        public string File { get; set; }
        public int Order { get; set; }
    }
}
