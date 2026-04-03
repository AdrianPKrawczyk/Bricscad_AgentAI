---
description: Tworzy zupełnie nowe narzędzie CAD od podstaw (Greenfield Development).
---

1. **Definicja Intencji**: Zapytaj mnie, co dokładnie ma robić nowe narzędzie i jakie problemy użytkownika rozwiązuje.
2. **Research API**: Przeszukaj @Bricscad_AgentAI_V2/resources/BricsCAD_API_Quick.txt (i V22 jeśli trzeba), aby zidentyfikować klasy BricsCAD potrzebne do realizacji zadania.
3. **Projektowanie Schematu**: Zaproponuj strukturę JSON Schema dla tego narzędzia. Skup się na tym, by parametry były intuicyjne dla LLM.
4. **Implementacja Logiki**: 
   - Stwórz klasę w `src/Tools/`.
   - Użyj `IToolV2`.
   - Zaimplementuj logikę CAD korzystając z wzorca `CadWrapper` (Magiczny Wrapper).
5. **Walidacja i Dokumentacja**:
   - Użyj skilla `test-bricscad-tool`, aby stworzyć testy.
   - Użyj skilla `user-doc-manager`, aby opisać nową funkcję w USER_GUIDE.md.
6. **Aktualizacja Pamięci**: Zapisz nową funkcjonalność w @memory.md jako `FEAT: [NEW-TOOL]`.