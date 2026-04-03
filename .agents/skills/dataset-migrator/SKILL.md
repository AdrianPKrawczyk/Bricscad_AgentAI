    ---
    name: dataset-migrator
    description: Masowa konwersja datasetów JSONL z formatu V1 do V2 za pomocą skryptu Python.
    ---
    # Instrukcja Migracji Datasetu
    Kiedy użytkownik prosi o migrację pliku .jsonl, nie rób tego ręcznie.
    1. Skorzystaj ze skryptu znajdującego się w `@scripts/migrate_v1_to_v2.py`.
    2. Uruchom go w terminalu podając ścieżkę do pliku V1 i nową ścieżkę V2:
    `python .agents/skills/dataset-migrator/scripts/migrate_v1_to_v2.py <input> <output>`
    3. Po zakończeniu, sprawdź wyrywkowo 2-3 linie, czy format `tool_calls` zgadza się z @System_Blueprint.md.

    ## Walidacja Spójności Logicznej (KRYTYCZNE)
    Zanim uruchomisz skrypt migracyjny, musisz upewnić się, że nazewnictwo w skrypcie odpowiada aktualnej logice V2:
    1. Otwórz plik @System_Blueprint.md i sprawdź listę zarejestrowanych narzędzi (Tools).
    2. Otwórz skrypt `@scripts/migrate_v1_to_v2.py` i sprawdź słownik `TOOL_MAPPING`.
    3. Jeśli w Blueprint nazwałeś narzędzie inaczej niż w słowniku skryptu – **musisz najpierw zaktualizować skrypt**, aby dataset odpowiadał nowemu kodowi C#.
    4. **ZAKAZ HALUCYNACJI NAZW:** Dataset musi uczyć model tylko tych funkcji, które faktycznie zaimplementowałeś w folderze `src/Tools/`.