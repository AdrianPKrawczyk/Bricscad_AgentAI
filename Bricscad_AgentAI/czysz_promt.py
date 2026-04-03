import re

def wyczysc_prompt_csharp(input_file, output_file):
    try:
        with open(input_file, 'r', encoding='utf-8') as f:
            content = f.read()

        # 1. Znajdź blok tekstu przypisany do systemPrompt
        # Szukamy wszystkiego między cudzysłowem po '=' a średnikiem na końcu
        match = re.search(r'systemPrompt\s*=\s*"(.*?)";', content, re.DOTALL)
        
        if not match:
            print("BŁĄD: Nie znaleziono zmiennej systemPrompt w pliku!")
            return

        raw_prompt = match.group(1)

        # 2. Usuń formatowanie C#: " + " oraz \n (jako tekst)
        # Zamieniamy fizyczne sekwencje " \n" + " na prawdziwe przejścia do nowej linii
        clean_prompt = raw_prompt.replace('\\n" +', '')
        clean_prompt = clean_prompt.replace('"', '')
        clean_prompt = clean_prompt.strip()

        # 3. Naprawa znaków ucieczki (np. \\P w C# to \P w tekście)
        clean_prompt = clean_prompt.replace('\\\\', '\\')

        # Zapisz wynik
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(clean_prompt)
            
        print(f"SUKCES! Czysty prompt został zapisany w: {output_file}")
        print("-" * 30)
        print("Możesz go teraz wkleić do Colaba w potrójny cudzysłów: SYSTEM_PROMPT = \"\"\"...\"\"\"")

    except Exception as e:
        print(f"Wystąpił błąd: {e}")

# Uruchomienie
wyczysc_prompt_csharp('AgentCommand.cs', 'czysty_prompt.txt')