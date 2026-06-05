import re
with open('src/UI/AgentControl.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# Fix tabChat = new TabPage
text = re.sub(r'(tabChat = new TabPage\("Czat \(V2 GOLD\)"\);)', r'\1\n\n            // --- Pasek Kontekstu (Context Bar) ---\n            panContextBar = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(5) };\n            lblContextTokens = new Label { Dock = DockStyle.Left, Width = 150, Text = "Kontekst: 0/8192 (0%)", TextAlign = ContentAlignment.MiddleLeft };\n            pbContext = new ProgressBar { Dock = DockStyle.Fill };\n            btnCompressContext = new Button { Dock = DockStyle.Right, Width = 40, Text = "🗜️", FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };\n            btnCompressContext.Click += BtnCompressContext_Click;\n            panContextBar.Controls.Add(pbContext);\n            panContextBar.Controls.Add(lblContextTokens);\n            panContextBar.Controls.Add(btnCompressContext);\n', text)

# Fix panInput.Controls.Add(btnSend); Panel panStats...
text = re.sub(r'(tabChat\.Controls\.Add\(txtHistory\);\s+tabChat\.Controls\.Add\(panInput\);)', r'tabChat.Controls.Add(panContextBar);\n            \1', text)

# Fix tabControl.TabPages.Add(tabChat);
text = re.sub(r'(tabControl\.TabPages\.Add\(tabChat\);)', r'InitializeSessionTab();\n            tabControl.TabPages.Add(tabSessions);\n            \1', text)

with open('src/UI/AgentControl.cs', 'w', encoding='utf-8') as f:
    f.write(text)
