---
description: Uruchamia kompilację projektu i naprawia ewentualne błędy krok po kroku (Loop-to-Fix).
---
1. Przeczytaj wytyczne kompilacji z umiejętności `project-compiler`.
2. Uruchom w terminalu `dotnet build Bricscad_AgentAI_V2/Bricscad_AgentAI_V2.csproj`.
3. Jeśli kompilacja powiodła się (Build succeeded, 0 Errors, 0 Warnings - lub wyłącznie zatwierdzone wyjątki/ostrzeżenia), przejdź dalej.
4. Jeśli wystąpiły błędy kompilacji (Errors):
   - Odczytaj dokładną linię błędu oraz identyfikator "CSXXXX".
   - Otwórz właściwy plik wykorzystując narzędzia nawigacji (np. `view_file`).
   - Napraw błąd w pliku.
   - Wróć do punktu drugiego i po raz kolejny spróbuj skompilować projekt, aż do zera błędów.
5. Po sukcesie zamortyzuj logi i zgłoś ukończenie kompilacji.