import re
with open('src/UI/AgentControl.cs', 'r', encoding='utf-8') as f:
    text = f.read()

methods = """
        private void InitializeSessionTab()
        {
            tabSessions = new TabPage("Sesje");
            
            Panel panTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
            
            btnNewSession = new Button { Text = "Nowa Sesja", Dock = DockStyle.Left, Width = 100, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnNewSession.Click += BtnNewSession_Click;
            
            btnLoadSession = new Button { Text = "Wczytaj", Dock = DockStyle.Left, Width = 80, Margin = new Padding(5, 0, 0, 0), BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnLoadSession.Click += BtnLoadSession_Click;

            btnDeleteSession = new Button { Text = "Usuń", Dock = DockStyle.Right, Width = 80, BackColor = Color.Crimson, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDeleteSession.Click += BtnDeleteSession_Click;

            panTop.Controls.Add(btnLoadSession);
            panTop.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5 });
            panTop.Controls.Add(btnNewSession);
            panTop.Controls.Add(btnDeleteSession);

            gridSessions = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Black,
                MultiSelect = false
            };

            tabSessions.Controls.Add(gridSessions);
            tabSessions.Controls.Add(panTop);

            RefreshSessionsGrid();
        }

        private void RefreshSessionsGrid()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(RefreshSessionsGrid));
                return;
            }

            var sessions = SessionManager.GetAllSessions();
            gridSessions.DataSource = sessions.Select(s => new { Id = s.Id, Tytuł = s.Description, Data = s.UpdatedAt }).ToList();
        }

        private void BtnNewSession_Click(object sender, EventArgs e)
        {
            string desc = Microsoft.VisualBasic.Interaction.InputBox("Podaj opis nowej sesji:", "Nowa Sesja", "Sesja " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            if (!string.IsNullOrWhiteSpace(desc))
            {
                SessionManager.CreateNewSession(desc);
                _supervisor?.ClearHistory();
                RefreshSessionsGrid();
                ReloadChatHistoryFromSession();
            }
        }

        private void BtnLoadSession_Click(object sender, EventArgs e)
        {
            if (gridSessions.SelectedRows.Count > 0)
            {
                string id = gridSessions.SelectedRows[0].Cells["Id"].Value.ToString();
                SessionManager.LoadSession(id);
                RefreshSessionsGrid();
                ReloadChatHistoryFromSession();
                tabControl.SelectedTab = tabChat;
            }
        }

        private void BtnDeleteSession_Click(object sender, EventArgs e)
        {
            if (gridSessions.SelectedRows.Count > 0)
            {
                string id = gridSessions.SelectedRows[0].Cells["Id"].Value.ToString();
                if (MessageBox.Show("Czy na pewno usunąć tę sesję?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    SessionManager.DeleteSession(id);
                    RefreshSessionsGrid();
                    ReloadChatHistoryFromSession();
                }
            }
        }

        private void ReloadChatHistoryFromSession()
        {
            txtHistory.Clear();
            var session = SessionManager.CurrentSession;
            foreach (var msg in session.Messages)
            {
                if (msg.Role == "user") AppendToHistory("TY", msg.Content, Color.LightSkyBlue);
                else AppendToHistory("BIELIK", msg.Content, Color.LightGreen);
            }
        }

        private void BtnCompressContext_Click(object sender, EventArgs e)
        {
            // Na razie czyszczenie starej historii - potem dodamy podsumowanie LLM
            if (_supervisor != null)
            {
                var history = _supervisor.GetHistory();
                if (history.Count > 2)
                {
                    history.RemoveRange(0, history.Count - 2); // Zostaw tylko 2 ostatnie
                    SessionManager.SaveCurrentSession();
                    ReloadChatHistoryFromSession();
                    MessageBox.Show("Skompresowano kontekst (pozostawiono 2 ostatnie wiadomości).", "Kompresja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void UpdateTokenBar(int totalTokens)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<int>(UpdateTokenBar), totalTokens);
                return;
            }

            int maxTokens = 8192; // Docelowo pobierane z ustawień
            int percent = (int)((double)totalTokens / maxTokens * 100);
            if (percent > 100) percent = 100;

            pbContext.Value = percent;
            lblContextTokens.Text = $"Kontekst: {totalTokens}/{maxTokens} ({percent}%)";

            if (percent >= 90) pbContext.ForeColor = Color.Red;
            else if (percent >= 70) pbContext.ForeColor = Color.Orange;
            else pbContext.ForeColor = Color.Green;
        }
"""

text = re.sub(r'(\s+)(}\s*})$', r'\n' + methods + r'\1\2', text)

with open('src/UI/AgentControl.cs', 'w', encoding='utf-8') as f:
    f.write(text)
