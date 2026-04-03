import json
import re
import os
import tkinter as tk
from tkinter import filedialog, messagebox

def napraw_zmienne(tekst):
    # Wymuszenie wielkich liter dla argumentu SaveAs
    tekst = re.sub(r'("SaveAs"\s*:\s*")([^"]+)(")', lambda m: m.group(1) + m.group(2).upper() + m.group(3), tekst, flags=re.IGNORECASE)
    
    # Wymuszenie wielkich liter dla zmiennych z @ i $ (np. @pole -> @POLE, $pole -> $POLE)
    tekst = re.sub(r'([@$])([a-zA-Z0-9_]+)', lambda m: m.group(1) + m.group(2).upper(), tekst)
    return tekst

def napraw_foreach_rpn(tekst):
    # Jeśli to pętla FOREACH i zawiera wyrażenie RPN, upewnij się, że zaczyna się od CLEAR
    if "[ACTION:FOREACH" in tekst and "RPN:" in tekst:
        # Szukamy RPN: i jeśli nie ma po nim słowa CLEAR, dodajemy je
        tekst = re.sub(r'(RPN:\s*\'?[^\']*\'?\s*)(?!CLEAR\b)', r'\1CLEAR ', tekst, flags=re.IGNORECASE)
        # Czyszczenie ewentualnych duplikatów
        tekst = tekst.replace("CLEAR CLEAR", "CLEAR")
    return tekst

def przetwarzaj_plik(plik_wejsciowy, plik_wyjsciowy):
    sklejony_bufor = ""
    poprawne_obiekty = 0
    
    with open(plik_wejsciowy, 'r', encoding='utf-8') as wejscie, \
         open(plik_wyjsciowy, 'w', encoding='utf-8') as wyjscie:
        
        # Omijanie "twardych enterów" - sklejanie linii aż utworzą poprawny JSON
        for linia in wejscie:
            sklejony_bufor += linia.strip() + "\\n" if not linia.strip().endswith('}') else linia.strip()
            
            try:
                # Oczyszczanie bufora z nadmiarowych zakończeń na końcu
                if sklejony_bufor.endswith('\\n'):
                    sklejony_bufor = sklejony_bufor[:-2]
                    
                obiekt_json = json.loads(sklejony_bufor)
                
                # Przetwarzanie zawartości
                if 'messages' in obiekt_json:
                    for wiadomosc in obiekt_json['messages']:
                        if 'content' in wiadomosc:
                            zawartosc = wiadomosc['content']
                            zawartosc = napraw_zmienne(zawartosc)
                            zawartosc = napraw_foreach_rpn(zawartosc)
                            wiadomosc['content'] = zawartosc

                # Zapis naprawionego obiektu do jednej fizycznej linii
                json.dump(obiekt_json, wyjscie, ensure_ascii=False)
                wyjscie.write('\n')
                
                sklejony_bufor = ""
                poprawne_obiekty += 1
                
            except json.JSONDecodeError:
                # Kontynuuj sklejanie linii, jeśli JSON jest wciąż niekompletny
                sklejony_bufor += " "

    return poprawne_obiekty

def main():
    # Inicjalizacja i ukrycie głównego okna tkinter
    root = tk.Tk()
    root.withdraw()

    # Wywołanie okna wyboru pliku
    plik_wejsciowy = filedialog.askopenfilename(
        title="Wybierz plik .jsonl bazy wiedzy do naprawy",
        filetypes=[("JSON Lines", "*.jsonl"), ("Wszystkie pliki", "*.*")]
    )

    if not plik_wejsciowy:
        print("Nie wybrano żadnego pliku. Operacja anulowana.")
        return

    # Automatyczne generowanie nazwy pliku wyjściowego w tym samym folderze
    katalog = os.path.dirname(plik_wejsciowy)
    nazwa_pliku = os.path.basename(plik_wejsciowy)
    nazwa_bez_rozszerzenia, rozszerzenie = os.path.splitext(nazwa_pliku)
    
    plik_wyjsciowy = os.path.join(katalog, f"{nazwa_bez_rozszerzenia}_FIXED{rozszerzenie}")

    print(f"Rozpoczynam przetwarzanie pliku:\n{plik_wejsciowy}...")
    
    # Uruchomienie właściwego algorytmu naprawczego
    przetworzone_linie = przetwarzaj_plik(plik_wejsciowy, plik_wyjsciowy)
    
    komunikat = f"Pomyślnie przetworzono i naprawiono: {przetworzone_linie} instrukcji.\n\nWynik zapisano jako:\n{plik_wyjsciowy}"
    print("\n" + komunikat)
    
    # Wyświetlenie okienka z potwierdzeniem sukcesu
    messagebox.showinfo("Sukces - Agent Bielik AI", komunikat)

if __name__ == "__main__":
    main()