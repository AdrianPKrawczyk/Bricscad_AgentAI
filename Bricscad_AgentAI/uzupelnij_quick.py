import re
import os

# --- USTAWIENIA NAZW PLIKÓW ---
quick_file = "BricsCAD_API_Quick.txt"
full_file = "BricsCAD_API_V22.txt"  # Jeśli Twój nowy plik nazywa się inaczej, zmień tę nazwę
output_file = "Brakujace_Klasy.txt"

# Nasza "Biała Lista" z kodu C# (zapisana małymi literami do łatwego porównywania)
white_list = [
    "entity", "line", "polyline", "polyline2d", "polyline3d", "arc", "circle", 
    "ellipse", "spline", "dbpoint", "xline", "ray", "hatch", "region", "wipeout", 
    "mtext", "dbtext", "dimension", "aligneddimension", "rotateddimension", 
    "radialdimension", "diametricdimension", "arcdimension", "lineangulardimension2", 
    "point3angulardimension", "leader", "mleader", "solid3d", "surface", "solid", 
    "trace", "polyfacemesh", "polygonmesh", "face", "blockreference"
]

def generuj_brakujace():
    quick_classes = set()
    
    # 1. Wczytanie klas, które JUŻ MASZ w pliku Quick
    if os.path.exists(quick_file):
        with open(quick_file, "r", encoding="utf-8") as f:
            content = f.read()
            # Szukamy tylko pierwszych słów przed kreską |
            matches = re.findall(r'\b([a-zA-Z0-9_]+)\|', content)
            quick_classes = {m.strip().lower() for m in matches}
    else:
        print(f"Ostrzeżenie: Nie znaleziono pliku '{quick_file}'. Skrypt uzna, że baza Quick jest pusta.")

    # 2. Wczytanie wielkiej bazy Full
    if not os.path.exists(full_file):
        print(f"BŁĄD: Nie znaleziono pliku '{full_file}'. Upewnij się, że nazwa jest poprawna.")
        return

    with open(full_file, "r", encoding="utf-8") as f:
        full_content = f.read()

    # Pancerny Regex (ten sam co w naszym C#)
    pattern = re.compile(r'\b([a-zA-Z0-9_]+)\|(.*?)(?=\b[a-zA-Z0-9_]+\||$)', re.IGNORECASE | re.DOTALL)
    matches_full = pattern.findall(full_content)

    brakujace_do_zapisania = []
    znalezione_nazwy = []

    # 3. Logika filtrowania
    for match in matches_full:
        klasa_nazwa = match[0].strip().lower()
        opis_surowy = match[1].strip()

        # Filtrujemy śmieci
        if " " in klasa_nazwa or len(klasa_nazwa) > 35:
            continue

        # SPRAWDZENIE: Czy jest na białej liście? ORAZ czy NIE MA jej w Quick?
        if klasa_nazwa in white_list and klasa_nazwa not in quick_classes:
            
            # Formatujemy nazwę tak, by pierwsza litera była wielka
            oryginalna_nazwa = klasa_nazwa.capitalize()
            # Wyjątki dla specyficznych nazw
            if oryginalna_nazwa == "Dbtext": oryginalna_nazwa = "DBText"
            if oryginalna_nazwa == "Mtext": oryginalna_nazwa = "MText"
            if oryginalna_nazwa == "Solid3d": oryginalna_nazwa = "Solid3d"
            if oryginalna_nazwa == "Dbpoint": oryginalna_nazwa = "DBPoint"
            
            # Usuwamy entery z opisu z pliku Full, żeby zachować strukturę "jedna klasa = jedna linijka"
            opis_czysty = opis_surowy.replace("\n", " ").replace("\r", " ").strip()
            
            brakujace_do_zapisania.append(f"{oryginalna_nazwa}|{opis_czysty}")
            znalezione_nazwy.append(oryginalna_nazwa)

    # 4. Zapis do nowego pliku
    if brakujace_do_zapisania:
        with open(output_file, "w", encoding="utf-8") as f:
            for b in brakujace_do_zapisania:
                f.write(b + "\n")
                
        print(f"\nSUKCES! Znalazłem {len(brakujace_do_zapisania)} brakujących klas z Białej Listy.")
        print(f"Zostały one wyeksportowane do pliku: {output_file}")
        print("-" * 45)
        for nazwa in znalezione_nazwy:
            print(f"+ Złapano: {nazwa}")
        print("-" * 45)
        print("Możesz teraz otworzyć plik 'Brakujace_Klasy.txt', usunąć z niego śmieciowe właściwości,")
        print("dopisać typy w nawiasach (np. Radius(double)) i wkleić te linijki na dół pliku Quick!")
    else:
        print("\nGratulacje! Twoja baza Quick zawiera już wszystkie obiekty z Białej Listy.")

if __name__ == "__main__":
    generuj_brakujace()