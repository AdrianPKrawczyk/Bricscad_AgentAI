using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Application = Bricscad.ApplicationServices.Application;

namespace Bricscad_AgentAI_V2.UI
{
    public class SubAgentChatControl : UserControl
    {
        private readonly ToolOrchestrator _orchestrator;
        private readonly LLMClient _client;
        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        private readonly List<string> _toolLogEntries = new List<string>();

        private ComboBox cbProfiles;
        private RichTextBox txtHistory;
        private RichTextBox txtInput;
        private RichTextBox txtToolJson;
        private Label lblStatus;
        private Label lblProfileInfo;
        private Button btnSend;
        private Button btnClear;
        private Button btnExportChat;
        private Button btnExportTools;
        private Button btnCopyDebug;

        public SubAgentChatControl()
        {
            _orchestrator = ToolOrchestrator.Instance;
            _client = new LLMClient(_orchestrator);

            _client.OnStatusUpdate += UpdateStatus;
            _client.OnToolCallLogged += AppendToolCallLog;

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            InitializeUi();
            LoadProfiles();
            ResetConversationForSelectedProfile();
        }

        private void InitializeUi()
        {
            var panTop = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8), BackColor = Color.FromArgb(37, 37, 38) };
            var lblProfile = new Label
            {
                Text = "Subagent:",
                Dock = DockStyle.Left,
                Width = 70,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White
            };

            cbProfiles = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cbProfiles.SelectedIndexChanged += CbProfiles_SelectedIndexChanged;

            lblProfileInfo = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.Silver,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            panTop.Controls.Add(lblProfileInfo);
            panTop.Controls.Add(cbProfiles);
            panTop.Controls.Add(lblProfile);

            var splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 720,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            var panChat = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            txtHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f)
            };

            var panInput = new Panel { Dock = DockStyle.Bottom, Height = 122, Padding = new Padding(0, 8, 0, 0) };
            txtInput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f)
            };
            txtInput.KeyDown += TxtInput_KeyDown;

            var panInputButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 122,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 0, 0, 0)
            };

            btnSend = CreateButton("WyĹ›lij", Color.FromArgb(0, 122, 204));
            btnSend.Click += BtnSend_Click;

            btnClear = CreateButton("WyczyĹ›Ä‡", Color.FromArgb(90, 90, 90));
            btnClear.Click += BtnClear_Click;

            panInputButtons.Controls.Add(btnSend);
            panInputButtons.Controls.Add(btnClear);

            panInput.Controls.Add(txtInput);
            panInput.Controls.Add(panInputButtons);

            lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                ForeColor = Color.Gray,
                Text = "Gotowy."
            };

            panChat.Controls.Add(txtHistory);
            panChat.Controls.Add(panInput);
            panChat.Controls.Add(lblStatus);

            var panJson = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            var lblJson = new Label
            {
                Text = "Tool Calls / JSON",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.Orange,
                Font = new Font(Font, FontStyle.Bold)
            };

            txtToolJson = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.LightGreen,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f)
            };

            var panExport = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 74,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0, 8, 0, 0)
            };

            btnExportChat = CreateWideButton("Eksport czatu", Color.FromArgb(0, 153, 102));
            btnExportChat.Click += BtnExportChat_Click;

            btnExportTools = CreateWideButton("Eksport JSON", Color.FromArgb(153, 102, 0));
            btnExportTools.Click += BtnExportTools_Click;

            btnCopyDebug = CreateWideButton("Kopiuj pakiet", Color.FromArgb(96, 60, 186));
            btnCopyDebug.Click += BtnCopyDebug_Click;

            panExport.Controls.Add(btnExportChat);
            panExport.Controls.Add(btnExportTools);
            panExport.Controls.Add(btnCopyDebug);

            panJson.Controls.Add(txtToolJson);
            panJson.Controls.Add(panExport);
            panJson.Controls.Add(lblJson);

            splitMain.Panel1.Controls.Add(panChat);
            splitMain.Panel2.Controls.Add(panJson);

            Controls.Add(splitMain);
            Controls.Add(panTop);
        }

        private Button CreateButton(string text, Color backColor)
        {
            return new Button
            {
                Text = text,
                Width = 100,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 8)
            };
        }

        private Button CreateWideButton(string text, Color backColor)
        {
            return new Button
            {
                Text = text,
                Width = 118,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 8)
            };
        }

        private void LoadProfiles()
        {
            cbProfiles.Items.Clear();
            foreach (var profileName in ToolConfigManager.GetProfiles().Keys.OrderBy(k => k))
            {
                cbProfiles.Items.Add(profileName);
            }

            if (cbProfiles.Items.Count > 0)
            {
                int preferredIndex = cbProfiles.Items.IndexOf("CadProfile");
                cbProfiles.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
            }
        }

        private void CbProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetConversationForSelectedProfile();
        }

        private void ResetConversationForSelectedProfile()
        {
            _history.Clear();
            _toolLogEntries.Clear();
            txtHistory.Clear();
            txtToolJson.Clear();

            string profileName = GetSelectedProfileName();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                lblProfileInfo.Text = "Brak profili.";
                return;
            }

            var profiles = ToolConfigManager.GetProfiles();
            if (!profiles.TryGetValue(profileName, out var profile))
            {
                lblProfileInfo.Text = "Nie znaleziono konfiguracji profilu.";
                return;
            }

            string systemPrompt = ToolConfigManager.LoadEffectivePromptForProfile(profileName);
            _history.Add(new ChatMessage { Role = "system", Content = systemPrompt });

            int toolCount = _orchestrator.GetToolsPayloadForProfile(profileName).Count;
            bool hasUserOverride = !string.IsNullOrWhiteSpace(ToolConfigManager.GetUserPromptOverride(profileName));
            lblProfileInfo.Text = $"Prompt: {profile.SystemPromptFile} | UserPrompt: {(hasUserOverride ? "Tak" : "Nie")} | Tools: {toolCount}";
            AppendSystemMessage($"Tryb testowy aktywny dla profilu `{profileName}`.");
            AppendSystemMessage($"ZaĹ‚adowano prompt `{profile.SystemPromptFile}` i {toolCount} narzÄ™dzi.");
            AppendToolSection(BuildProfileSnapshot(profileName, profile, toolCount));
            UpdateStatus("Gotowy.");
        }

        private string BuildProfileSnapshot(string profileName, AgentProfileConfig profile, int toolCount)
        {
            var payload = new
            {
                Profile = profileName,
                profile.SystemPromptFile,
                UserPromptOverride = !string.IsNullOrWhiteSpace(ToolConfigManager.GetUserPromptOverride(profileName)),
                ToolCount = toolCount,
                AllowedTools = profile.AllowedTools ?? new List<string>(),
                AllowedTags = profile.AllowedTags ?? new List<string>()
            };

            return JsonConvert.SerializeObject(payload, Formatting.Indented);
        }

        private string GetSelectedProfileName()
        {
            return cbProfiles.SelectedItem?.ToString();
        }


        private async void BtnSend_Click(object sender, EventArgs e)
        {
            string text = txtInput.Text.Trim();
            string profileName = GetSelectedProfileName();
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(profileName))
            {
                return;
            }

            txtInput.Clear();
            AppendUserMessage(text);

            int historyStartIndex = _history.Count;

            btnSend.Enabled = false;
            UpdateStatus($"Przetwarzanie przez {profileName}...");

            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                SyncImpliedSelectionToAgentMemory(doc);

                string payload = BuildContextualPayload(text, doc);
                _history.Add(new ChatMessage { Role = "user", Content = payload });

                var result = await Task.Run(async () =>
                    await _client.SendMessageReActAsync(
                        _history,
                        new CadExecutionContext(doc),
                        null,
                        true,
                        10,
                        profileName));

                RenderNewHistoryEntries(historyStartIndex);

                if (result != null)
                {
                    AppendAgentMessage(profileName, result.DisplayMessage ?? "(Brak odpowiedzi)");
                }
            }
            catch (Exception ex)
            {
                AppendSystemMessage($"BĹ‚Ä…d: {ex.Message}");
            }
            finally
            {
                btnSend.Enabled = true;
                UpdateStatus("Gotowy.");
            }
        }

        private string BuildContextualPayload(string userText, Document doc)
        {
            int selectionCount = AgentMemoryState.ActiveSelection?.Length ?? 0;
            string drawingPath = doc?.Name ?? string.Empty;
            string lowerText = userText.ToLowerInvariant();

            bool selectionScopedIntent =
                lowerText.Contains("zaznaczon") ||
                lowerText.Contains("wybran") ||
                lowerText.Contains("selection") ||
                lowerText.Contains("active") ||
                lowerText.Contains("ten blok") ||
                lowerText.Contains("tego bloku") ||
                lowerText.Contains("tym bloku") ||
                lowerText.Contains("ten obiekt") ||
                lowerText.Contains("tego obiektu") ||
                lowerText.Contains("tym obiekcie");

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(drawingPath))
            {
                sb.Append($"[Kontekst: aktywny plik to {drawingPath}; ActiveSelection zawiera {selectionCount} obiekt(Ăłw)]");
            }
            else
            {
                sb.Append($"[Kontekst: ActiveSelection zawiera {selectionCount} obiekt(Ăłw)]");
            }

            if (selectionScopedIntent)
            {
                sb.Append(" [ReguĹ‚a: uĹĽytkownik odnosi siÄ™ do zaznaczonych lub wybranych obiektĂłw. Operuj wyĹ‚Ä…cznie na obecnym ActiveSelection. Nie uĹĽywaj SelectEntities z Mode=New i nie rozszerzaj zakresu na caĹ‚y model, chyba ĹĽe uĹĽytkownik wyraĹşnie poprosi o nowe wyszukiwanie.]");
            }

            sb.AppendLine();
            sb.Append(userText);
            return sb.ToString();
        }

        private void SyncImpliedSelectionToAgentMemory(Document doc)
        {
            if (doc == null)
            {
                AgentMemoryState.Clear();
                return;
            }

            try
            {
                PromptSelectionResult selRes = doc.Editor.SelectImplied();
                if (selRes.Status == PromptStatus.OK && selRes.Value != null)
                {
                    AgentMemoryState.Update(selRes.Value.GetObjectIds());
                }
                else
                {
                    AgentMemoryState.Clear();
                }
            }
            catch (Exception ex)
            {
                BielikLogger.LogError("BĹ‚Ä…d synchronizacji zaznaczenia BricsCAD w Agent-Czat", ex);
            }
        }

        private void RenderNewHistoryEntries(int startIndex)
        {
            if (startIndex < 0 || startIndex >= _history.Count)
            {
                return;
            }

            var newMessages = _history.Skip(startIndex).ToList();
            if (newMessages.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (var message in newMessages)
            {
                if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                {
                    sb.AppendLine("ASSISTANT TOOL CALLS:");
                    sb.AppendLine(JsonConvert.SerializeObject(message.ToolCalls, Formatting.Indented));
                    sb.AppendLine();
                }
                else if (message.Role == "tool")
                {
                    sb.AppendLine("TOOL RESULT:");
                    sb.AppendLine(JsonConvert.SerializeObject(new
                    {
                        message.ToolCallId,
                        message.Content
                    }, Formatting.Indented));
                    sb.AppendLine();
                }
            }

            if (sb.Length > 0)
            {
                AppendToolSection(sb.ToString().TrimEnd());
            }
        }

        private void AppendToolCallLog(string json)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendToolCallLog), json);
                return;
            }

            _toolLogEntries.Add(json);
        }

        private void AppendToolSection(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendToolSection), text);
                return;
            }

            if (txtToolJson.TextLength > 0)
            {
                txtToolJson.AppendText(Environment.NewLine + Environment.NewLine);
            }

            txtToolJson.AppendText(text);
            txtToolJson.SelectionStart = txtToolJson.TextLength;
            txtToolJson.ScrollToCaret();
        }

        private void AppendUserMessage(string message)
        {
            AppendHistoryLine("TY", message, Color.LightSkyBlue);
        }

        private void AppendAgentMessage(string profileName, string message)
        {
            AppendHistoryLine(profileName, message, Color.LightGreen);
        }

        private void AppendSystemMessage(string message)
        {
            AppendHistoryLine("SYSTEM", message, Color.Orange);
        }

        private void AppendHistoryLine(string author, string message, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, string, Color>(AppendHistoryLine), author, message, color);
                return;
            }

            txtHistory.SelectionStart = txtHistory.TextLength;
            txtHistory.SelectionLength = 0;
            txtHistory.SelectionColor = color;
            txtHistory.AppendText($"[{author}]: ");
            txtHistory.SelectionColor = Color.White;
            txtHistory.AppendText(message + Environment.NewLine + Environment.NewLine);
            txtHistory.SelectionStart = txtHistory.TextLength;
            txtHistory.ScrollToCaret();
        }

        private void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(UpdateStatus), status);
                return;
            }

            lblStatus.Text = status;
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            ResetConversationForSelectedProfile();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Control)
            {
                e.SuppressKeyPress = true;
                BtnSend_Click(btnSend, EventArgs.Empty);
            }
        }

        private void BtnExportChat_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Markdown (*.md)|*.md|JSON (*.json)|*.json";
                dialog.FileName = $"agent-chat-{GetSelectedProfileName()?.ToLowerInvariant() ?? "profile"}-{DateTime.Now:yyyyMMdd-HHmmss}.md";
                if (dialog.ShowDialog() != DialogResult.OK) return;

                string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                string content = ext == ".json" ? BuildChatJsonExport() : BuildChatMarkdownExport();
                File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
                UpdateStatus($"Wyeksportowano czat: {Path.GetFileName(dialog.FileName)}");
            }
        }

        private void BtnExportTools_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "JSON (*.json)|*.json|Text (*.txt)|*.txt";
                dialog.FileName = $"agent-tools-{GetSelectedProfileName()?.ToLowerInvariant() ?? "profile"}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                if (dialog.ShowDialog() != DialogResult.OK) return;

                File.WriteAllText(dialog.FileName, BuildToolsJsonExport(), Encoding.UTF8);
                UpdateStatus($"Wyeksportowano log JSON: {Path.GetFileName(dialog.FileName)}");
            }
        }

        private void BtnCopyDebug_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(BuildDebugPacket());
            UpdateStatus("Pakiet debugowy skopiowany do schowka.");
        }

        private string BuildChatMarkdownExport()
        {
            var sb = new StringBuilder();
            string profileName = GetSelectedProfileName() ?? "(brak)";

            sb.AppendLine("# Agent-Czat");
            sb.AppendLine();
            sb.AppendLine($"- Profil: `{profileName}`");
            sb.AppendLine($"- Data eksportu: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            sb.AppendLine();
            sb.AppendLine("## Rozmowa");
            sb.AppendLine();

            foreach (var msg in _history.Where(m => m.Role != "system"))
            {
                sb.AppendLine($"### {msg.Role}");
                sb.AppendLine();
                sb.AppendLine("```text");
                sb.AppendLine(msg.Content?.ToString() ?? string.Empty);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string BuildChatJsonExport()
        {
            return JsonConvert.SerializeObject(new
            {
                Profile = GetSelectedProfileName(),
                ExportedAt = DateTime.Now,
                Messages = _history
            }, Formatting.Indented);
        }

        private string BuildToolsJsonExport()
        {
            return JsonConvert.SerializeObject(new
            {
                Profile = GetSelectedProfileName(),
                ExportedAt = DateTime.Now,
                ToolLogEntries = _toolLogEntries,
                Transcript = txtToolJson.Text
            }, Formatting.Indented);
        }

        private string BuildDebugPacket()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Profil: {GetSelectedProfileName()}");
            sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("=== CZAT ===");
            sb.AppendLine(BuildChatMarkdownExport());
            sb.AppendLine();
            sb.AppendLine("=== TOOL JSON ===");
            sb.AppendLine(txtToolJson.Text);
            return sb.ToString();
        }
    }
}
