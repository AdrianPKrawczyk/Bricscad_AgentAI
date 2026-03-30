import json
import tkinter as tk
from tkinter import filedialog, messagebox
import os

def validate_and_fix_jsonl(file_path):
    if not file_path:
        return

    valid_lines = []
    errors = []
    
    with open(file_path, 'r', encoding='utf-8') as f:
        for line_num, line in enumerate(f, 1):
            original_line = line
            line = line.strip()
            
            # 1. Ignorujemy śmieci generowane przez LLM (puste linie, znaczniki markdown, nawiasy tablic)
            if not line or line.startswith("```") or line == "[" or line == "]":
                continue 
                
            # 2. Usuwamy przecinki na końcu linii (błąd tworzenia listy zamiast JSONL)
            if line.endswith(","):
                line = line[:-1]
                
            try:
                # 3. Próbujemy zdekodować JSON
                obj = json.loads(line)
                
                # 4. Sprawdzamy wymogi strukturalne dla formatu ChatML (wymóg Unsloth)
                if "messages" not in obj:
                    errors.append(f"Linia {line_num}: Brak głównego klucza 'messages'.")
                    continue
                    
                if not isinstance(obj["messages"], list):
                    errors.append(f"Linia {line_num}: 'messages' nie jest listą (tablicą).")
                    continue
                    
                has_structural_error = False
                for idx, msg in enumerate(obj["messages"]):
                    if "role" not in msg or "content" not in msg:
                        errors.append(f"Linia {line_num}: Wiadomość {idx} nie posiada klucza 'role' lub 'content'.")
                        has_structural_error = True
                        break
                        
                    if msg["role"] not in ["user", "assistant", "system"]:
                        errors.append(f"Linia {line_num}: Nieznana rola '{msg['role']}'. Dozwolone to user/assistant/system.")
                        has_structural_error = True
                        break
                
                # Jeśli wszystko jest idealnie, formatujemy i zapisujemy czystą linię
                if not has_structural_error:
                    # Używamy json.dumps aby mieć pewność, że zapisujemy w 100% znormalizowany ciąg
                    clean_json = json.dumps(obj, ensure_ascii=False)
                    valid_lines.append(clean_json)
                    
            except json.JSONDecodeError as e:
                errors.append(f"Linia {line_num}: Błąd składni JSON - {e}")
                
    # ==========================================
    # ZAPIS NAPRAWIONEGO PLIKU
    # ==========================================
    dir_name, file_name = os.path.split(file_path)
    name, ext = os.path.splitext(file_name)
    new_file_path = os.path.join(dir_name, f"{name}_ZWALIDOWANY{ext}")
    
    with open(new_file_path, 'w', encoding='utf-8') as f:
        for line in valid_lines:
            f.write(line + "\n")
            
    # ==========================================
    # WYŚWIETLANIE RAPORTU
    # ==========================================
    report = f"Sprawdzono plik:\n{file_name}\n\n"
    report += f"✅ Poprawne / Naprawione linie: {len(valid_lines)}\n"
    report += f"❌ Odrzucone błędy: {len(errors)}\n\n"
    
    if errors:
        report += "--- ZNALEZIONE BŁĘDY (Pierwsze 5) ---\n"
        report += "\n".join(errors[:5]) + "\n\n"
        
    report += f"Czysty, gotowy do treningu plik zapisano jako:\n{new_file_path}"
    
    # Wyświetlamy okienko z podsumowaniem
    if errors:
        messagebox.showwarning("Wynik Walidacji (Znaleziono błędy)", report)
    else:
        messagebox.showinfo("Wynik Walidacji (Plik Idealny!)", report)

def run_app():
    # Inicjalizacja biblioteki graficznej Tkinter (wbudowanej w Pythona)
    root = tk.Tk()
    root.withdraw() # Ukrywamy puste główne okno
    
    messagebox.showinfo("Walidator JSONL", "Za chwilę otworzy się okno.\nWybierz w nim plik .jsonl, który chcesz przygotować do Google Colab.")
    
    # Okno wyboru pliku
    file_path = filedialog.askopenfilename(
        title="Wybierz plik JSONL do walidacji",
        filetypes=[("Pliki JSONL", "*.jsonl"), ("Wszystkie pliki", "*.*")]
    )
    
    if file_path:
        validate_and_fix_jsonl(file_path)

if __name__ == "__main__":
    run_app()