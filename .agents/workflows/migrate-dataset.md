---
description: Automatyczna konwersja datasetu z weryfikacją logiki V2.
---

1. Zapytaj o plik źródłowy V1.
2. **Weryfikacja Logiki**: Porównaj nazwy narzędzi występujące w pliku V1 z definicjami w @System_Blueprint.md.
3. Jeśli wykryjesz rozbieżności (np. narzędzie w V1 nazywa się inaczej niż planowane w V2), poinformuj mnie o tym i zaproponuj aktualizację słownika mapowania w skrypcie Python.
4. Po uzyskaniu zgody, uruchom skrypt: `python .agents/skills/dataset-migrator/scripts/migrate_v1_to_v2.py <input> <output>`
5. Wyświetl raport spójności: "Skonwertowano X linii. Wszystkie nazwy narzędzi są zgodne z Blueprint V2".