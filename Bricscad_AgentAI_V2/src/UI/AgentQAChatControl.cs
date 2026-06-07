using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Models.Session;

namespace Bricscad_AgentAI_V2.UI
{
    public class AgentQAChatControl : UserControl
    {
        private LLMClient _llmClient;
        private ListBox lbSessions;
        private RichTextBox txtHistory;
        private RichTextBox txtInput;
        private Button btnSend;
        private Button btnNewSession;
        private Button btnCompressContext;
        private Label lblStatus;

        public AgentQAChatControl(LLMClient llmClient)
        {
            _llmClient = llmClient;
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);

            InitializeUI();
            LoadSessions();
        }

        private void InitializeUI()
        {
            // Panel Lewy - Sesje
            Panel panLeft = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = Color.FromArgb(35, 35, 35), Padding = new Padding(10) };
            
            Label lblSessions = new Label { Text = "SESJE QA (AUDYT)", Dock = DockStyle.Top, Height = 30, Font = new Font(this.Font, FontStyle.Bold), ForeColor = Color.Orange };
            panLeft.Controls.Add(lblSessions);

            lbSessions = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                DisplayMember = "Description",
                ValueMember = "Id",
                Font = new Font("Segoe UI", 11)
            };
            lbSessions.SelectedIndexChanged += LbSessions_SelectedIndexChanged;
            panLeft.Controls.Add(lbSessions);

            Panel panLeftBottom = new Panel { Dock = DockStyle.Bottom, Height = 90, Padding = new Padding(0, 10, 0, 0) };
            
            btnNewSession = new Button { Text = "NOWA SESJA QA", Dock = DockStyle.Top, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(0, 122, 204), Cursor = Cursors.Hand };
            btnNewSession.Click += BtnNewSession_Click;
            panLeftBottom.Controls.Add(btnNewSession);

            btnCompressContext = new Button { Text = "KOMPRESJA KONTEKSTU", Dock = DockStyle.Top, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.DarkGoldenrod, Cursor = Cursors.Hand, Margin = new Padding(0, 5, 0, 0) };
            btnCompressContext.Click += BtnCompressContext_Click;
            panLeftBottom.Controls.Add(btnCompressContext);

            panLeft.Controls.Add(panLeftBottom);

            // Panel Prawy - Czat
            Panel panRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            txtHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Consolas", 10)
            };
            panRight.Controls.Add(txtHistory);

            Panel panInput = new Panel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(0, 10, 0, 0) };
            
            txtInput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 11)
            };
            txtInput.KeyDown += TxtInput_KeyDown;
            panInput.Controls.Add(txtInput);

            Panel panSend = new Panel { Dock = DockStyle.Right, Width = 100, Padding = new Padding(10, 0, 0, 0) };
            btnSend = new Button { Text = "WYŚLIJ", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.ForestGreen, Cursor = Cursors.Hand, Font = new Font(this.Font, FontStyle.Bold) };
            btnSend.Click += BtnSend_Click;
            panSend.Controls.Add(btnSend);

            panInput.Controls.Add(panSend);
            panRight.Controls.Add(panInput);

            lblStatus = new Label { Text = "Gotowy.", Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.Gray };
            panRight.Controls.Add(lblStatus);

            this.Controls.Add(panRight);
            this.Controls.Add(panLeft);
        }

        private void LoadSessions()
        {
            var sessions = QASessionManager.GetAllSessions();
            lbSessions.Items.Clear();
            foreach (var session in sessions)
            {
                lbSessions.Items.Add(session);
            }
            if (QASessionManager.CurrentSession != null)
            {
                lbSessions.SelectedItem = sessions.FirstOrDefault(s => s.Id == QASessionManager.CurrentSession.Id);
            }
        }

        private void LbSessions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbSessions.SelectedItem is ChatSession session)
            {
                if (QASessionManager.CurrentSession == null || QASessionManager.CurrentSession.Id != session.Id)
                {
                    QASessionManager.LoadSession(session.Id);
                    RefreshChatHistory();
                }
            }
        }

        private void BtnNewSession_Click(object sender, EventArgs e)
        {
            QASessionManager.CreateNewSession();
            LoadSessions();
            RefreshChatHistory();
        }

        private void RefreshChatHistory()
        {
            txtHistory.Clear();
            var msgs = QASessionManager.CurrentSession.Messages.Where(m => 
                m.Role != "system" && 
                m.Role != "tool" && 
                !string.IsNullOrWhiteSpace(m.Content?.ToString()));
                
            foreach (var msg in msgs)
            {
                AppendToHistory(msg.Role, msg.Content.ToString());
            }
        }

        private void AppendToHistory(string role, string content)
        {
            txtHistory.SelectionStart = txtHistory.TextLength;
            txtHistory.SelectionLength = 0;
            
            if (role == "user")
            {
                txtHistory.SelectionColor = Color.Cyan;
                txtHistory.AppendText($"\n[TY]: ");
            }
            else
            {
                txtHistory.SelectionColor = Color.Orange;
                txtHistory.AppendText($"\n[MÓZG QA]: ");
            }

            txtHistory.SelectionColor = Color.White;
            txtHistory.AppendText($"{content}\n");
            
            txtHistory.SelectionStart = txtHistory.Text.Length;
            txtHistory.ScrollToCaret();
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            string text = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            txtInput.Clear();
            AppendToHistory("user", text);

            var userMsg = new ChatMessage { Role = "user", Content = text };
            QASessionManager.CurrentSession.Messages.Add(userMsg);
            
            btnSend.Enabled = false;
            lblStatus.Text = "Przetwarzanie (Audyt QA)...";

            try
            {
                // QA Agent używa specjalnego profilu (AuditorProfile), i dysponuje wszystkimi narzędziami
                var response = await _llmClient.SendMessageReActAsync(QASessionManager.CurrentSession.Messages, null, new[] { "#all" }, true, 5, "AuditorProfile");

                if (response != null && response.IsSuccess)
                {
                    QASessionManager.SaveSession(); // Zapisz przed aktualizacją UI
                    RefreshChatHistory();

                    QASessionManager.TriggerAutoNaming(_llmClient, QASessionManager.CurrentSession);
                    LoadSessions();
                }
                else
                {
                    AppendToHistory("system", $"[BŁĄD]: {response?.DisplayMessage ?? "Nieznany błąd"}");
                }
            }
            catch (Exception ex)
            {
                AppendToHistory("system", $"[WYJĄTEK]: {ex.Message}");
            }
            finally
            {
                QASessionManager.SaveSession();
                btnSend.Enabled = true;
                lblStatus.Text = "Gotowy.";
            }
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                btnSend.PerformClick();
            }
        }

        private void BtnCompressContext_Click(object sender, EventArgs e)
        {
            if (QASessionManager.CurrentSession.Messages.Count < 4)
            {
                MessageBox.Show("Sesja jest zbyt krótka do kompresji.", "Kompresja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult res = MessageBox.Show("Czy chcesz zachować tylko 3 ostatnie wiadomości, aby zmniejszyć zużycie tokenów i oczyścić kontekst?", "Kompresja", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                var sysMsg = QASessionManager.CurrentSession.Messages.FirstOrDefault(m => m.Role == "system");
                var lastMsgs = QASessionManager.CurrentSession.Messages.Where(m => m.Role != "system").Skip(Math.Max(0, QASessionManager.CurrentSession.Messages.Count - 3)).ToList();
                
                QASessionManager.CurrentSession.Messages.Clear();
                if (sysMsg != null) QASessionManager.CurrentSession.Messages.Add(sysMsg);
                QASessionManager.CurrentSession.Messages.AddRange(lastMsgs);
                
                QASessionManager.SaveSession();
                RefreshChatHistory();
                lblStatus.Text = "Skonpresowano kontekst.";
            }
        }
    }
}
