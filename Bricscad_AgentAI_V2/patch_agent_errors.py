import re
with open('src/UI/AgentControl.cs', 'r', encoding='utf-8') as f:
    text = f.read()

text = text.replace('SessionManager.CreateNewSession(desc);', 'SessionManager.CreateNewSession(); SessionManager.CurrentSession.Description = desc; SessionManager.SaveSession();')
text = text.replace('AppendToHistory("TY", msg.Content, Color.LightSkyBlue);', 'AppendToHistory("TY", msg.Content.ToString(), Color.LightSkyBlue);')
text = text.replace('AppendToHistory("BIELIK", msg.Content, Color.LightGreen);', 'AppendToHistory("BIELIK", msg.Content.ToString(), Color.LightGreen);')
text = text.replace('SessionManager.SaveCurrentSession();', 'SessionManager.SaveSession();')

with open('src/UI/AgentControl.cs', 'w', encoding='utf-8') as f:
    f.write(text)
