---
name: git-expert
description: Zarządzanie kontrolą wersji Git. Używaj do tworzenia commitów, opisywania zmian i sprawdzania statusu repozytorium.
---
# Instrukcja Obsługi Gita V2

Twoim celem jest utrzymanie czytelnej, profesjonalnej historii zmian.

## 1. Standard Commitów (Conventional Commits)
Każdy commit musi mieć format: `<typ>(<zakres>): <opis> [KROK-X.Y]`
- **feat**: Nowe narzędzie lub funkcjonalność (np. `feat(tools): add CreateObject [KROK-4.1]`)
- **fix**: Poprawka błędu w kodzie (np. `fix(api): fix null reference in LayerManager`)
- **docs**: Zmiany w dokumentacji (np. `docs(user): update USER_GUIDE for Circle tool`)
- **test**: Dodanie lub zmiana testów (np. `test(tools): add unit tests for ManageLayers`)
- **refactor**: Zmiana kodu, która nie dodaje funkcji ani nie naprawia błędu.

## 2. Zasada "Jeden Krok = Jeden Commit"
- Nie łącz migracji dwóch różnych narzędzi w jednym komicie.
- Każdy commit powinien odpowiadać jednemu krokowi z @memory.md.

## 3. Procedura przed commitem
1. Uruchom `git status`, aby upewnić się, że nie dodajesz niechcianych plików.
2. Sprawdź, czy plik @memory.md jest zaktualizowany i zgadza się z wprowadzanymi zmianami.
3. Wygeneruj podsumowanie zmian dla użytkownika przed wykonaniem komendy `git commit`.