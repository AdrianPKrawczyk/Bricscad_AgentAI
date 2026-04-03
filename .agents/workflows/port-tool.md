---
description: Migruje pojedyncze narzędzie ITool z wersji Regex (V1) do Function Calling (V2) i generuje testy.
---

1. Zapytaj mnie o nazwę pliku z narzędziem z folderu `Bricscad_AgentAI/` (V1), które chcesz teraz przenieść. (Poczekaj na moją odpowiedź).
2. Przeczytaj wskazany plik źródłowy. Zrozum, jakie parametry narzędzie próbowało wyciągnąć za pomocą Regex. Zrozum logikę modyfikującą bazę DWG (Transaction, GetObject).
3. Przepisz to narzędzie do folderu `Bricscad_AgentAI_V2/src/Tools/` zgodnie z założeniami nowej architektury z @System_Blueprint.md. 
   - Usuń całkowicie parsowanie Regex.
   - Zastąp wejście na silnie typowany obiekt `Dictionary<string, object>` lub `JsonElement`.
   - Wygeneruj i zaimplementuj obiekt JSON Schema dla tego narzędzia.
4. Sprawdź ewentualne użycie BricsCAD API w pliku @Bricscad_AgentAI_V2/resources/BricsCAD_API_Quick.txt, aby upewnić się, że nie halucynujesz klas i metod, jeżeli w tym pliku nie ma tych informacji przeszukaj plik Bricscad_AgentAI_V2/resources/BricsCAD_API_V22.txt.
5. UŻYJ UMIEJĘTNOŚCI (SKILL): `test-bricscad-tool`**, aby wygenerować klasę testową dla nowo napisanego narzędzia. Skup się na przetestowaniu definicji Schematu i poprawności ładowania parametrów wejściowych (zdeserializowanego JSON-a).
6. Poproś mnie o weryfikację nowo napisanego kodu C#. Zaktualizuj @memory.md po mojej akceptacji.