using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
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
                ShowLines = true,
                ShowPlusMinus = false,
                ItemHeight = 32,
                Indent = 14
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

            this.Resize += (s, e) => UpdateSplitLayout();
        }

        private bool _splitLayoutApplied;

        private void UpdateSplitLayout()
        {
            if (split == null || this.Width <= 0) return;

            int target = Math.Max(200, Math.Min(400, (int)(this.Width * 0.22)));
            int panel2Min = 300;

            if (this.Width - target < panel2Min)
            {
                target = this.Width - panel2Min;
                if (target < 100) target = 100;
            }

            if (split.Panel1MinSize != 100) split.Panel1MinSize = 100;
            if (split.Panel2MinSize != panel2Min) split.Panel2MinSize = panel2Min;

            int desired = Math.Max(split.Panel1MinSize, Math.Min(target, this.Width - panel2Min));
            if (desired != split.SplitterDistance) split.SplitterDistance = desired;

            _splitLayoutApplied = true;
        }

        private static Encoding DetectFileEncoding(string filePath)
        {
            try
            {
                using (var reader = new StreamReader(filePath, Encoding.Default, true))
                {
                    reader.ReadToEnd();
                    return reader.CurrentEncoding;
                }
            }
            catch
            {
                return Encoding.UTF8;
            }
        }

        private static string ReadHelpFile(string filePath)
        {
            Encoding detected = DetectFileEncoding(filePath);
            return File.ReadAllText(filePath, detected);
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

                string json = ReadHelpFile(indexFile);
                var items = JsonConvert.DeserializeObject<List<HelpIndexItem>>(json);

                if (items != null)
                {
                    items.Sort((a, b) => a.Order.CompareTo(b.Order));
                    foreach (var item in items)
                    {
                        var node = new TreeNode(item.Title);
                        node.NodeFont = new Font("Segoe UI", 10f);
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
                ShowErrorHtml($"Blad ladowania indeksu pomocy: {ex.Message}");
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
                    string mdContent = ReadHelpFile(filePath);
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
                ShowErrorHtml($"Blad wczytywania pliku: {ex.Message}");
            }
        }

        private void ShowErrorHtml(string message)
        {
            string html = $@"
            <html><head><meta charset=""UTF-8""><style>
                body {{ background-color: #1e1e1e; color: #ff5555; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; }}
            </style></head>
            <body><h3>Blad</h3><p>{message}</p></body></html>";
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
            html = Regex.Replace(html, @"(?<=</li>)\r?\n(?=<li>)", "");
            html = Regex.Replace(html, @"(<li>.*?</li>)", "<ul>$1</ul>", RegexOptions.Singleline);
            html = html.Replace("</ul><ul>", "");

            // --- Tabele ---
            html = Regex.Replace(html, @"^\|(.*)\| *$", m => {
                var row = m.Groups[1].Value;
                if (row.Contains("---")) return "";
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

            // --- Akapity ---
            html = Regex.Replace(html, @"\r?\n\r?\n", "<br/><br/>");

            string wrapper = $@"
            <html>
            <head>
                <meta charset=""UTF-8"">
                <style>
                    body {{
                        background-color: #1e1e1e;
                        color: #e8e8e8;
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        font-size: 15px;
                        line-height: 1.7;
                        padding: 28px 36px;
                        margin: 0;
                        word-wrap: break-word;
                    }}
                    h1, h2, h3 {{ color: #ffffff; font-weight: 600; margin-top: 1.6em; margin-bottom: 0.6em; }}
                    h1 {{ border-bottom: 2px solid #007acc; padding-bottom: 8px; color: #4daafc; font-size: 1.8em; }}
                    h2 {{ color: #4daafc; font-size: 1.35em; border-left: 4px solid #007acc; padding-left: 10px; }}
                    h3 {{ color: #b8d8ff; font-size: 1.1em; }}
                    p {{ margin: 0.8em 0; }}
                    strong {{ color: #ffffff; font-weight: 600; }}
                    em {{ color: #dcdcaa; }}
                    code {{ background-color: #2d2d30; color: #dcdcaa; padding: 2px 6px; border-radius: 3px; font-family: Consolas, 'Courier New', monospace; font-size: 0.9em; }}
                    pre {{ background-color: #1a1a1a; padding: 14px 18px; border-radius: 5px; overflow-x: auto; border: 1px solid #333; }}
                    pre code {{ background-color: transparent; padding: 0; color: #d4d4d4; }}
                    hr {{ border: 0; border-top: 1px solid #444; margin: 24px 0; }}
                    ul, ol {{ padding-left: 26px; margin: 0.8em 0; }}
                    li {{ margin-bottom: 6px; line-height: 1.6; }}
                    blockquote {{ border-left: 4px solid #007acc; margin: 12px 0; padding: 6px 0 6px 16px; color: #9cdcfe; background-color: #1a1a1a; }}
                    table {{ border-collapse: collapse; width: 100%; margin: 18px 0; }}
                    table, th, td {{ border: 1px solid #444; }}
                    th {{ background-color: #2d2d30; color: #ffffff; }}
                    th, td {{ padding: 10px 12px; text-align: left; }}
                    .alert {{ padding: 12px 16px; margin: 16px 0; border-left: 5px solid; border-radius: 4px; background-color: #252526; }}
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
