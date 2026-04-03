---
description: Planuje, wykonuje, testuje i dokumentuje kolejny krok z harmonogramu migracji.
---

1. Otwórz i przeanalizuj plik @memory.md, aby zobaczyć, co już zostało zrobione (sekcja [DONE]) oraz nad czym ostatnio pracowaliśmy (sekcja [ACTIVE]), sprawdź ostatnią wersję v2.x.y.
2. Otwórz dokument @Bricscad_AgentAI_V2/docs/3_Migration_Plan.md i zlokalizuj pierwszy logiczny krok, który nie został jeszcze zrealizowany.
3. Poinformuj mnie, jaki to krok i zaproponuj plan jego wykonania w kodzie. Zapytaj mnie, czy zgadzam się na przystąpienie do kodowania.
4. (Poczekaj na moją akceptację).
5. **Zanim zaczniesz pisać kod, sprawdź swoją listę Umiejętności (Skills). Jeśli istnieje umiejętność pasująca do tego zadania (np. `port-bricscad-tool`), przeczytaj jej instrukcje.**
6. Zaimplementuj kod wymagany dla tego kroku. Pamiętaj o architekturze docelowej zawartej w @Bricscad_AgentAI_V2/docs/1_PRD.md oraz @System_Blueprint.md.
7. **[KLUCZOWY KROK - TESTY]**: Po napisaniu kodu, automatycznie użyj skilla `test-bricscad-tool`. 
   - Wygeneruj odpowiednie testy jednostkowe.
8. **Weryfikacja**: Pokaż mi wyniki testów lub kod testujący.

9. **Dokumentowanie**: Użyj `user-doc-manager`, aby zaktualizować instrukcję dla użytkownika.
10. **Raport**: Przedstaw mi podsumowanie:
   - Nowa wersja: v2.x.y
   - Wykonany krok: [KROK-X.Y]
   - Co przetestowano.
   - Co dodano do instrukcji obsługi.
11. Zaktualizuj plik @memory.md dodając wykonany krok do sekcji [DONE] i ustawiając nowy [ACTIVE].Zaktualizuj @memory.md używając tagów zdefiniowanych w regułach (np. v2.1.0 FEAT: [KROK-1.1])
12. Git: Wywołaj workflow /commit-changes, aby trwale zapisać wykonany krok w historii repozytorium.
