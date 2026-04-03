---
description: Formatuje i wykonuje commit bieżących zmian zgodnie ze standardami projektu.
---

1. **Analiza zmian**: Wykonaj `git status` i `git diff --stat`, aby zobaczyć, co zostało zmienione.
2. **Pamięć**: Przeczytaj ostatni wpis w @memory.md, aby pobrać numer kroku (np. [KROK-4.1]) i wersję (v2.x.y).
3. **Propozycja**: Wygeneruj treść wiadomości commita zgodnie ze skillem `git-expert`. 
   - Przykład: `feat(v2): port CreateObject tool [KROK-4.1]`
4. **Zatwierdzenie**: Przedstaw mi tę propozycję i zapytaj: "Czy chcesz wykonać commit z tą wiadomością?".
5. **Akcja**: Po mojej zgodzie wykonaj:
   - `git add .`
   - `git commit -m "[wygenerowana wiadomość]"`
6. **Status**: Potwierdź sukces i podaj krótki identyfikator commita (hash).