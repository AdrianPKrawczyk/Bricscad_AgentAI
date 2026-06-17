using System;
using System.Drawing;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Models.Session;

namespace Bricscad_AgentAI_V2.UI
{
    public class LightChatControl : UserControl
    {
        private RichTextBox txtHistory;
        private TextBox txtInput;
        private Button btnSend;
        private Label lblStatus;
        private Label lblSessionInfo;
        private Label lblStats;
        private Label lblContextTokens;

        public LightChatControl()
        {
            InitializeComponent();
            ApplyTheme();
            this.Load += LightChatControl_Load;
        }

        private void LightChatControl_Load(object sender, EventArgs e)
        {
            SubscribeToEvents();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // --- HUD Panel (Top) ---
            Panel panHUD = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(5) };

            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Ładowanie..."
            };

            lblSessionInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.Yellow,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Sesja: Brak"
            };

            lblStats = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "0ms | 0 tkn | 0 t/s"
            };

            lblContextTokens = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.DodgerBlue,
                Font = new Font("Segoe UI", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Kontekst: 0/8192 (0%)"
            };

            panHUD.Controls.Add(lblContextTokens);
            panHUD.Controls.Add(lblStats);
            panHUD.Controls.Add(lblSessionInfo);
            panHUD.Controls.Add(lblStatus);

            // --- History Panel (Center) ---
            txtHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            // --- Input Panel (Bottom) ---
            Panel panInput = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 45) };

            txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            txtInput.KeyDown += TxtInput_KeyDown;

            btnSend = new Button
            {
                Text = "Wyślij\n(Ctrl+Enter)",
                Dock = DockStyle.Right,
                Width = 100,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += BtnSend_Click;

            panInput.Controls.Add(txtInput);
            panInput.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 5 });
            panInput.Controls.Add(btnSend);

            // --- Assemble ---
            this.Controls.Add(txtHistory);
            this.Controls.Add(panHUD);
            this.Controls.Add(panInput);

            this.Name = "LightChatControl";
            this.Size = new Size(300, 400);
            this.ResumeLayout(false);
        }

        private void ApplyTheme()
        {
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;
        }

        private void SubscribeToEvents()
        {
            if (AgentControl.Instance == null) return;

            if (AgentControl.Instance.LlmClient != null)
            {
                AgentControl.Instance.LlmClient.OnStatusUpdate += LlmClient_OnStatusUpdate;
                AgentControl.Instance.LlmClient.OnStatsUpdate += LlmClient_OnStatsUpdate;
            }

            AgentControl.Instance.OnHistoryAppended += AgentControl_OnHistoryAppended;
            AgentControl.Instance.OnSessionReloaded += AgentControl_OnSessionReloaded;

            // Initial state load
            AgentControl_OnSessionReloaded();
        }

        private void UnsubscribeFromEvents()
        {
            if (AgentControl.Instance == null) return;

            if (AgentControl.Instance.LlmClient != null)
            {
                AgentControl.Instance.LlmClient.OnStatusUpdate -= LlmClient_OnStatusUpdate;
                AgentControl.Instance.LlmClient.OnStatsUpdate -= LlmClient_OnStatsUpdate;
            }

            AgentControl.Instance.OnHistoryAppended -= AgentControl_OnHistoryAppended;
            AgentControl.Instance.OnSessionReloaded -= AgentControl_OnSessionReloaded;
        }

        private void AgentControl_OnHistoryAppended(string sender, string message, Color color)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, string, Color>(AgentControl_OnHistoryAppended), sender, message, color);
                return;
            }

            AppendToHistory(sender, message, color);
        }

        private void AppendToHistory(string sender, string message, Color color)
        {
            try
            {
                txtHistory.SelectionStart = txtHistory.TextLength;
                txtHistory.SelectionLength = 0;
                txtHistory.SelectionColor = color;
                txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Bold);
                txtHistory.AppendText($"{sender}:\n");

                txtHistory.SelectionColor = this.ForeColor;
                txtHistory.SelectionFont = new Font(txtHistory.Font, FontStyle.Regular);
                txtHistory.AppendText($"{message}\n\n");

                txtHistory.SelectionStart = txtHistory.TextLength;
                txtHistory.ScrollToCaret();
            }
            catch { }
        }

        private void LlmClient_OnStatusUpdate(string status)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(LlmClient_OnStatusUpdate), status);
                return;
            }

            lblStatus.Text = status;
        }

        private void LlmClient_OnStatsUpdate(LLMStats stats)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<LLMStats>(LlmClient_OnStatsUpdate), stats);
                return;
            }

            double tokensPerSecond = stats.TotalTimeMs > 0 ? (stats.CompletionTokens / (stats.TotalTimeMs / 1000.0)) : 0;
            lblStats.Text = $"{stats.TotalTimeMs}ms | {stats.CompletionTokens} tkn | {tokensPerSecond:F1} t/s";
            lblContextTokens.Text = $"Kontekst: {stats.TotalTokens}/8192 ({(stats.TotalTokens / 8192.0 * 100):F1}%)";
        }

        private void AgentControl_OnSessionReloaded()
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(AgentControl_OnSessionReloaded));
                return;
            }

            var session = SessionManager.CurrentSession;
            lblSessionInfo.Text = $"Sesja: {session?.Description ?? session?.Id ?? "Brak"}";

            // Reload history to match main view
            txtHistory.Clear();
            if (session != null && session.Messages != null)
            {
                foreach (var msg in session.Messages)
                {
                    if (msg.Role == "user") AppendToHistory("TY", msg.Content?.ToString() ?? "", Color.LightSkyBlue);
                    else AppendToHistory("BIELIK", msg.Content?.ToString() ?? "", Color.LightGreen);
                }
            }
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            string userMsg = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(userMsg)) return;

            txtInput.Clear();
            btnSend.Enabled = false;

            try
            {
                if (AgentControl.Instance != null)
                {
                    await AgentControl.Instance.ProcessInputAsync(userMsg);
                }
                else
                {
                    AppendToHistory("BŁĄD", "Instancja AgentControl nie jest uruchomiona.", Color.Red);
                }
            }
            finally
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    btnSend.Enabled = true;
                    txtInput.Focus();
                }
            }
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Zapobiega systemowemu "ding"
                BtnSend_Click(sender, e);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnsubscribeFromEvents();
            }
            base.Dispose(disposing);
        }
    }
}
