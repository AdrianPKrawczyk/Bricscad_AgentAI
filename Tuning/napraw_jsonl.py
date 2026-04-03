import json
import os
import tkinter as tk
from tkinter import filedialog

# Ukrycie głównego pustego okienka Tkinter
root = tk.Tk()
root.withdraw()

print("Oczekuję na wybór pliku...")

# Wywołanie okna wyboru pliku
input_path = filedialog.askopenfilename(
    title="Wybierz plik źródłowy JSONL",
    filetypes=[("Pliki JSONL", "*.jsonl"), ("Wszystkie pliki", "*.*")]
)

# Jeśli użytkownik zamknie okno bez wyboru pliku
if not input_path:
    print("Anulowano wybór pliku. Zamykam skrypt.")
    exit()

# Generowanie nazwy pliku wyjściowego
base_dir = os.path.dirname(input_path)
file_name = os.path.basename(input_path)
name_part, ext_part = os.path.splitext(file_name)
output_path = os.path.join(base_dir, f"{name_part}_DO_TRENINGU{ext_part}")

print(f"\nPrzetwarzam plik: {input_path}")

finalne_linie = []

with open(input_path, 'r', encoding='utf-8') as f:
    for nr_linii, linia in enumerate(f, 1):
        linia = linia.strip()
        if not linia: continue
        
        try:
            data = json.loads(linia)
            stare_wiadomosci = data.get('messages', [])
            nowe_wiadomosci = []
            
            for msg in stare_wiadomosci:
                rola = msg['role']
                tresc = msg['content']
                
                if rola == 'assistant':
                    ma_akcje = '[' in tresc and ']' in tresc
                    ma_msg = '[MSG:' in tresc
                    
                    if ma_akcje and ma_msg:
                        # Szukamy miejsca, gdzie zaczyna się [MSG:]
                        msg_index = tresc.find('[MSG:')
                        
                        akcja_tresc = tresc[:msg_index].strip()
                        wiadomosc_tresc = tresc[msg_index:].strip()
                        
                        if akcja_tresc and wiadomosc_tresc:
                            nowe_wiadomosci.append({"role": "assistant", "content": akcja_tresc})
                            nowe_wiadomosci.append({"role": "user", "content": "WYNIK: Akcja wykonana pomyślnie. Kontynuuj."})
                            nowe_wiadomosci.append({"role": "assistant", "content": wiadomosc_tresc})
                            continue

                nowe_wiadomosci.append({"role": rola, "content": tresc})
            
            finalne_linie.append({"messages": nowe_wiadomosci})
            
        except json.JSONDecodeError:
            print(f"Pominięto uszkodzoną linię nr {nr_linii}")

with open(output_path, 'w', encoding='utf-8') as f:
    for wpis in finalne_linie:
        f.write(json.dumps(wpis, ensure_ascii=False) + '\n')
        
print(f"\nSUKCES! Stworzono nowy, zoptymalizowany plik do treningu:")
print(f"-> {output_path}")
print(f"Ilość przetworzonych linii: {len(finalne_linie)}")