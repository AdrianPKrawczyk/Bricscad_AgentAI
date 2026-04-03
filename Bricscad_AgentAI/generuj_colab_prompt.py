import os
import re

def generuj_prompt_dla_colab():
    # Ścieżki do plików (zakładam, że skrypt jest w tym samym folderze co AgentCommand.cs)
    base_dir = os.path.dirname(os.path.abspath(__file__))
    csharp_file_path = os.path.join(base_dir, 'AgentCommand.cs')
    output_file_path = os.path.join(base_dir, 'COLAB_PROMPT_GOTOWY.py')

    if not os.path.exists(csharp_file_path):
        print(f"BŁĄD: Nie znaleziono pliku {csharp_file_path}")
        return

    with open(csharp_file_path, 'r', encoding='utf-8') as f:
        kod_csharp = f.read()

    # Szukamy bloku pomiędzy naszymi specjalnymi komentarzami
    wzorzec = r'// --- SYSTEM PROMPT START ---(.*?)// --- SYSTEM PROMPT END ---'
    dopasowanie = re.search(wzorzec, kod_csharp, re.DOTALL)

    if not dopasowanie:
        print("BŁĄD: Nie znaleziono znaczników // --- SYSTEM PROMPT START --- i END w pliku AgentCommand.cs")
        return

    blok_kodu = dopasowanie.group(1)

    # Wyciągamy sam tekst ze zmiennej verbatim (pomiędzy @" a ";)
    wzorzec_stringa = r'@"(.*?)"\s*;'
    dopasowanie_stringa = re.search(wzorzec_stringa, blok_kodu, re.DOTALL)

    if not dopasowanie_stringa:
        print("BŁĄD: Nie znaleziono zmiennej z promptem zaczynającej się od @\" wewnątrz bloków.")
        return

    surowy_prompt = dopasowanie_stringa.group(1)

    # C# używa dwóch cudzysłowów ("") wewnątrz verbatim string do oznaczenia jednego (").
    # W Pythonie musimy to zamienić z powrotem na jeden cudzysłów.
    czysty_prompt = surowy_prompt.replace('""', '"')

    # Generujemy kod gotowy do wklejenia do Google Colab
    # Używamy r"""...""" (Raw String w Pythonie), aby Colab zignorował \C1 czy \n jako znaki specjalne
    kod_colab = f"""# ==========================================
# SKOPIUJ PONIŻSZY KOD DO KOMÓRKI W COLABIE
# ==========================================

system_prompt = r\"\"\"
{czysty_prompt.strip()}
\"\"\"

# Sprawdzenie, czy załadowało się poprawnie:
print("Długość promptu systemowego:", len(system_prompt))
"""

    with open(output_file_path, 'w', encoding='utf-8') as f:
        f.write(kod_colab)

    print(f"SUKCES! Wygenerowano plik do Colaba:")
    print(f"-> {output_file_path}")

if __name__ == "__main__":
    generuj_prompt_dla_colab()