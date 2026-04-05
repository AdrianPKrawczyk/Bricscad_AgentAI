---
description: Planuje, wykonuje, testuje i dokumentuje kolejny krok rozwoju aplikacji.
---

1. Otwórz i przeanalizuj plik @memory.md, aby zobaczyć, co już zostało zrobione (sekcja [DONE]) oraz nad czym ostatnio pracowaliśmy (sekcja [ACTIVE]), sprawdź ostatnią wersję v2.x.y.

2. **Zanim zaczniesz pisać kod, sprawdź swoją listę Umiejętności (Skills). Jeśli istnieje umiejętność pasująca do tego zadania (np. `port-bricscad-tool`), przeczytaj jej instrukcje.**
3. Zaimplementuj kod wymagany dla tego kroku. Pamiętaj o architekturze docelowej zawartej w @Bricscad_AgentAI_V2/docs/1_PRD.md oraz @System_Blueprint.md.
4. [BRAMKA JAKOŚCI]: Po napisaniu kodu wywołaj workflow /build. Nie przechodź do testów jednostkowych ani dokumentacji, dopóki projekt nie będzie się kompilował bez błędów.
5. **[KLUCZOWY KROK - TESTY]**: Po napisaniu kodu, automatycznie użyj skilla `test-bricscad-tool`. 
   - Wygeneruj odpowiednie testy jednostkowe.

6. **Weryfikacja**: Pokaż mi wyniki testów lub kod testujący.

7. **Dokumentowanie**: Użyj `user-doc-manager`, aby zaktualizować instrukcję dla użytkownika.
8. **Raport**: Przedstaw mi podsumowanie:
   - Nowa wersja: v2.x.y
   - Wykonany krok: [KROK-X.Y]
   - Co przetestowano.
   - Co dodano do instrukcji obsługi.
9. Zaktualizuj plik @memory.md dodając wykonany krok do sekcji [DONE] i ustawiając nowy [ACTIVE].Zaktualizuj @memory.md używając tagów zdefiniowanych w regułach (np. v2.1.0 FEAT: [KROK-1.1])
10. Git: Wywołaj workflow /commit-changes, aby trwale zapisać wykonany krok w historii repozytorium.