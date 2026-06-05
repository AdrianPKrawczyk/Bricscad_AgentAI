import codecs

with codecs.open('src/UI/AgentControl.cs', 'r', 'utf-8-sig') as f:
    text = f.read()

# Fix Context Bar ForeColor
text = text.replace('lblContextTokens = new Label { Dock = DockStyle.Left, Width = 150, Text = "Kontekst: 0/8192 (0%)", TextAlign = ContentAlignment.MiddleLeft };',
                    'lblContextTokens = new Label { Dock = DockStyle.Left, Width = 150, Text = "Kontekst: 0/8192 (0%)", TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.LightGray };')

# Fix UI Polish chars that got mangled
replacements = {
    'Logi NarzÄ™dzi': 'Logi Narzędzi',
    'Logi Narztdzi': 'Logi Narzędzi',
    'PrzeglÄ…d': 'Przegląd',
    'Przegl d': 'Przegląd',
    'Ścieżki i Dane': 'Ścieżki i Dane',
    'cieki i Dane': 'Ścieżki i Dane',
    'OtwÄ‚Ĺ‚rz w Notatniku': 'Otwórz w Notatniku',
    'Otwrz w Notatniku': 'Otwórz w Notatniku',
    'WypeÄąâ€šniamy listÄ„â„˘': 'Wypełniamy listę',
    'Wypeniamy list': 'Wypełniamy listę',
    'NarzÄ„â„˘dzia': 'Narzędzia',
    'Narz dzia': 'Narzędzia',
    'zostaÄąâ€š pomyÄąâ€şlnie zapisany': 'został pomyślnie zapisany',
    'BÄąâ€šĂ„â€¦d': 'Błąd',
    'BÄąÂ Ă„â€žD': 'BŁĄD',
    'Ä„': 'Ą', 'Ä†': 'Ć', 'Ä': 'Ę', 'Ä…': 'ą', 'Ä‡': 'ć', 'Ä™': 'ę', 'Ĺ': 'Ł', 'Ĺ„': 'ń', 'Ĺ“': 'ś', 'Ĺş': 'ź', 'ĹĽ': 'ż', 'Ĺ‚': 'ł', 'Ăł': 'ó'
}

for bad, good in replacements.items():
    text = text.replace(bad, good)

# Save with BOM
with codecs.open('src/UI/AgentControl.cs', 'w', 'utf-8-sig') as f:
    f.write(text)
