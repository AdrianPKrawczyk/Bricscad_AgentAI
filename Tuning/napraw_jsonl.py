import json
import re
import os

# Ścieżka bazowa względem pliku skryptu
base_dir = os.path.dirname(os.path.abspath(__file__))
input_path = os.path.join(base_dir, 'Agent_Training_Data_v1_ZWALIDOWANY_FIXED.jsonl') 
output_path = os.path.join(base_dir, 'Bielik_Data_DO_TRENINGU.jsonl')

if not os.path.exists(input_path):
    print(f"BŁĄD: Nie znaleziono pliku {input_path}!")
else:
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
                        # Usuwamy ( i ) - zostawiamy tę logikę zgodnie z oryginałem
                        tresc = tresc.replace('(', '').replace(')', '')
                        
                        # Sprawdzamy czy w jednej odpowiedzi jest akcja (lub select/lisp) ORAZ wiadomość [MSG:]
                        ma_akcje = any(tag in tresc for tag in ['[ACTION:', '[SELECT:', '[LISP:'])
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
            
    print(f"SUKCES! Stworzono nowy plik: {output_path}")
    print(f"Przetworzono {len(finalne_linie)} poprawnych obiektów.")
