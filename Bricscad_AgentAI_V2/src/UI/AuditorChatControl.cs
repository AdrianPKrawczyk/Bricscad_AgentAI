using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Models.Session;

namespace Bricscad_AgentAI_V2.UI
{
    /// <summary>
    /// Zakladka UI do recznego testowania Agenta Rewidenta (AuditorProfile).
    /// Wzorowane na AgentQAChatControl, ale uproszczone - brak sesji.
    ///
    /// Uzycie:
    /// 1. Uzytkownik zaznacza obiekty w BricsCAD.
    /// 2. Wpisuje cel audytu, np. "Sprawdz czy warstwa to INST_WODA".
    /// 3. Naciska "Audytuj" - wysyla system prompt Auditora + cel + ActiveSelection
    ///    do AuditorProfile i wyswietla odpowiedz.
    ///
    /// Izoluje testowanie Rewidenta od koniecznosci testowania Workera (zgodnie z Q6).
    /// </summary>
    public class AuditorChatControl : UserControl
    {
        private LLMClient _llmClient;
        private RichTextBox txtHistory;
        private RichTextBox txtInput;
        private Button btnSend;
        private Button btnClear;
        private Label lblStatus;
        private Label lblContext;
        private CheckBox chkIncludeActiveSelection;
        private CheckBox chkIncludeBlackboard;
        private CheckBox chkIncludeListBlocks;

        public AuditorChatControl(LLMClient llmClient)
        {
            _llmClient = llmClient;
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);

            InitializeUI();
        }

        private void InitializeUI()
        {
            // === PANEL GORNY: opcje kontekstu ===
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.FromArgb(35, 35, 35), Padding = new Padding(10) };
            Label lblHeader = new Label
            {
                Text = "AUDYTOR (Agent Rewident) - reczne testowanie AuditorProfile",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font(this.Font, FontStyle.Bold),
                ForeColor = Color.LightGreen
            };
            panTop.Controls.Add(lblHeader);

            FlowLayoutPanel flpOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight
            };
            chkIncludeActiveSelection = new CheckBox
            {
                Text = "Aktywna selekcja",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.LightGray,
                Margin = new Padding(0, 6, 15, 0)
            };
            chkIncludeBlackboard = new CheckBox
            {
                Text = "Blackboard (Chain of Evidence)",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.LightGray,
                Margin = new Padding(0, 6, 15, 0)
            };
            chkIncludeListBlocks = new CheckBox
            {
                Text = "Lista blokow",
                AutoSize = true,
                ForeColor = Color.LightGray,
                Margin = new Padding(0, 6, 15, 0)
            };
            flpOptions.Controls.Add(chkIncludeActiveSelection);
            flpOptions.Controls.Add(chkIncludeBlackboard);
            flpOptions.Controls.Add(chkIncludeListBlocks);
            panTop.Controls.Add(flpOptions);

            // === PANEL DOLNY: input + send ===
            Panel panBottom = new Panel { Dock = DockStyle.Bottom, Height = 130, Padding = new Padding(5) };

            txtInput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.None
            };
            txtInput.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    BtnSend_Click(null, null);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            Panel panButtons = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnSend = new Button
            {
                Text = "AUDYTUJ (Ctrl+Enter)",
                Dock = DockStyle.Right,
                Width = 200,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSend.Click += BtnSend_Click;
            btnClear = new Button
            {
                Text = "Wyczysc",
                Dock = DockStyle.Right,
                Width = 80,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClear.Click += (s, e) => { txtHistory.Clear(); lblStatus.Text = "Gotowy"; };

            lblContext = new Label
            {
                Text = "Kontekst: 0 obiekt(ow) w selekcji",
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = Color.LightGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 12, 0, 0)
            };

            panButtons.Controls.Add(btnSend);
            panButtons.Controls.Add(btnClear);
            panButtons.Controls.Add(lblContext);

            panBottom.Controls.Add(txtInput);
            panBottom.Controls.Add(panButtons);

            // === PANEL SRODKOWY: historia ===
            Panel panMiddle = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            txtHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            panMiddle.Controls.Add(txtHistory);

            // === STATUS BAR ===
            Panel panStatus = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Color.FromArgb(35, 35, 35) };
            lblStatus = new Label
            {
                Text = "Gotowy. Wpisz cel audytu i nacisnij AUDYTUJ.",
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            panStatus.Controls.Add(lblStatus);

            this.Controls.Add(panMiddle);
            this.Controls.Add(panTop);
            this.Controls.Add(panStatus);
            this.Controls.Add(panBottom);

            // Event aktualizacji kontekstu po kazdym renderze
            this.Load += (s, e) => UpdateContextLabel();
        }

        private void UpdateContextLabel()
        {
            int n = AgentMemoryState.ActiveSelection?.Length ?? 0;
            lblContext.Text = $"Kontekst: {n} obiekt(ow) w selekcji  |  Auditor: {(AgentMemoryState.AuditorEnabled ? "ON" : "OFF (wlacz w panelu glownym)")}  |  CB prog: {AgentMemoryState.CircuitBreakerThreshold}";
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            string goal = txtInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(goal))
            {
                lblStatus.Text = "Wpisz cel audytu.";
                return;
            }

            btnSend.Enabled = false;
            lblStatus.Text = "Wysylam do Auditora...";
            lblStatus.ForeColor = Color.Gold;
            UpdateContextLabel();

            try
            {
                // Zbuduj sesje: system prompt Auditora + kontekst + cel
                var messages = BuildAuditSession(goal);

                AppendHistory("USER", goal);
                AppendHistory("---", "Audytor mysli... (Ctrl+Enter wstrzymuje)");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await Task.Run(async () =>
                {
                    return await _llmClient.SendMessageReActAsync(
                        conversationHistory: messages,
                        context: null,
                        initialTags: null,
                        earlyExitEnabled: false,
                        maxIterations: 5,
                        profileName: "AuditorProfile");
                });
                sw.Stop();

                if (result.IsSuccess)
                {
                    AppendHistory("AUDYTOR", result.DisplayMessage);
                    lblStatus.Text = $"Audyt zakończony w {sw.ElapsedMilliseconds}ms.";
                    lblStatus.ForeColor = Color.LightGreen;
                }
                else
                {
                    AppendHistory("AUDYTOR (FAIL)", result.DisplayMessage);
                    lblStatus.Text = $"Audyt nieudany: {result.DisplayMessage?.Substring(0, System.Math.Min(80, result.DisplayMessage?.Length ?? 0))}";
                    lblStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                AppendHistory("EXCEPTION", ex.Message);
                lblStatus.Text = "Wyjatek: " + ex.Message;
                lblStatus.ForeColor = Color.Red;
            }
            finally
            {
                btnSend.Enabled = true;
                txtInput.Clear();
            }
        }

        private List<ChatMessage> BuildAuditSession(string goal)
        {
            var messages = new List<ChatMessage>();

            // 1. System prompt Auditora
            string auditorPrompt = ToolConfigManager.LoadEffectivePromptForProfile("AuditorProfile");
            if (!string.IsNullOrWhiteSpace(auditorPrompt))
            {
                messages.Add(new ChatMessage { Role = "system", Content = auditorPrompt });
            }

            // 2. Kontekst (zbudowany z checkboxow)
            var sb = new StringBuilder();
            sb.AppendLine("=== KONTEKST AUDYTU (wstrzykniety przez UI Audytora) ===");
            sb.AppendLine();
            sb.AppendLine($"Cel audytu: {goal}");
            sb.AppendLine();

            if (chkIncludeActiveSelection.Checked)
            {
                var sel = AgentMemoryState.ActiveSelection;
                sb.AppendLine($"--- Aktywna selekcja ({sel?.Length ?? 0} obiekt(ow)) ---");
                if (sel != null && sel.Length > 0)
                {
                    foreach (var id in sel)
                    {
                        if (id.IsNull) continue;
                        sb.AppendLine($"Handle: 0x{id.Handle.ToString()}");
                    }
                }
                else
                {
                    sb.AppendLine("(pusta)");
                }
                sb.AppendLine();
            }

            if (chkIncludeBlackboard.Checked)
            {
                sb.AppendLine("--- Blackboard (klucze z @evidence_* + inne) ---");
                var allEntries = SharedMemoryState.GetAll();
                if (allEntries.Count > 0)
                {
                    foreach (var kvp in allEntries)
                    {
                        if (kvp.Key.StartsWith("@evidence_"))
                        {
                            sb.AppendLine($"- {kvp.Key} (dlugosc: {kvp.Value?.Length ?? 0})");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("(pusty)");
                }
                sb.AppendLine();
            }

            if (chkIncludeListBlocks.Checked)
            {
                sb.AppendLine("--- Lista blokow w rysunku (nazwy) ---");
                sb.AppendLine("(uzyj ListBlocks aby pobrac pelna liste)");
                sb.AppendLine();
            }

            sb.AppendLine("INSTRUKCJA:");
            sb.AppendLine("Odpowiedz jako JSON: { \"isSuccess\": bool, \"feedback\": string, \"evidence\": dict, \"severity\": \"info|warn|error|critical\" }");
            sb.AppendLine("Jesli nie mozesz odpowiedziec JSON-em, opisz slowami co widzisz i co jest nie tak.");

            messages.Add(new ChatMessage { Role = "user", Content = sb.ToString() });
            return messages;
        }

        private void AppendHistory(string role, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            txtHistory.SelectionStart = txtHistory.TextLength;
            txtHistory.SelectionColor = role == "USER" ? Color.LightBlue :
                                         role == "AUDYTOR" ? Color.LightGreen :
                                         role == "AUDYTOR (FAIL)" ? Color.Red :
                                         role == "EXCEPTION" ? Color.Red : Color.Gray;
            txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Bold);
            txtHistory.AppendText($"[{DateTime.Now:HH:mm:ss}] {role}:\n");
            txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Regular);
            txtHistory.SelectionColor = Color.White;
            txtHistory.AppendText(text + "\n\n");
            txtHistory.ScrollToCaret();
        }
    }
}
