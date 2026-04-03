---
trigger: glob
globs: Bricscad_AgentAI_V2/**/*.cs
---

1. **ARCHITEKTURA:** Obowiązuje wyłącznie "Tool Calling". ABSOLUTNY ZAKAZ używania Regex do parsowania komend LLM.
2. **KONTRAKT:** Twoim głównym przewodnikiem jest @System_Blueprint.md oraz @docs/1_PRD.md.
3. **STRUKTURA:** Nowe pliki muszą trafiać do odpowiednich podfolderów zgodnie z @Bricscad_AgentAI_V2/docs/4_Directory_Structure.md.
4. **WERSJONOWANIE I TAGOWANIE **
   - Każdy wpis w @memory.md musi zawierać wersję `v2.x.y` oraz jeden z tagów:
     - `FEAT:` (nowa funkcja/narzędzie)
     - `FIX:` (poprawka błędu)
     - `DOC:` (zmiana w dokumentacji)
     - `TEST:` (dodanie testów)
   - Identyfikator kroku musi być zgodny z planem migracji, np. `[KROK-4.2]`.
5. **NARZĘDZIA:** Każde narzędzie musi implementować JSON Schema i przyjmować zdeserializowane obiekty.
6. **DOKUMENTACJA:** Po zakończeniu kodowania użyj skilla `user-doc-manager`, aby zaktualizować podręcznik użytkownika.