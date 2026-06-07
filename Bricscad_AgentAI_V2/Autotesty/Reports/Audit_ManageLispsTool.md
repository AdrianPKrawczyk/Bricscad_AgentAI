# Raport Audytu Statycznego: `manage_lisps`

**Data audytu:** 2024-05-23
**Status:** ⚠️ WYKRYTO LUKI BEZPIECZEŃSTWA I BŁĘDY LOGICZNE

## 1. Podsumowanie (Executive Summary)
Przeprowadzono analizę statyczną kodu źródłowego narzędzia `manage_lisps` oraz klasy wspierającej `LispManager`. Narzędzie posiada krytyczne luki w zakresie bezpieczeństwa (Path Traversal) oraz błędy logiczne, które mogą prowadzić do niekontrolowanego nadpisywania plików systemowych lub usunięcia danych.

## 2. Zidentyfikowane Problemy

### A. Krytyczna Luka: Path Traversal (Brak walidacji `LispId`)
*   **Lokalizacja:** `ManageLispsTool.cs` oraz `LispManager.cs`
*   **Opis:** Parametr `LispId` jest używany bezpośrednio do budowania ścieżek plików (`Path.Combine(categoryPath, $"{metadata.LispId}.json")`). Narzędzie nie sprawdza, czy `LispId` zawiera sekwencje typu `../`.
*   **Ryzyko:** Atakujący (lub błędnie skonfigurowany agent) może nadpisać dowolny plik w systemie lub wyczyścić katalogi poza docelowym folderem LISP, jeśli posiada uprawnienia zapisu.

### B. Luka: Brak mechanizmu usuwania (`delete_lisp`)
*   **Lokalizacja:** `ManageLispsTool.cs` (Metoda `Execute`)
*   **Opis:** Choć klasa `LispManager` posiada metodę `DeleteLisp`, narzędzie `manage_lisps` nie wystawia tej akcji w swoim schemacie (`GetToolSchema`) ani w logice `Execute`.
*   **Ryzyko:** Brak możliwości zarządzania cyklem życia skryptów (możliwość jedynie dodawania i odczytu), co prowadzi do "śmieci" w systemie.

### C. Błąd Logiczny: Niebezpieczne usuwanie (`DeleteLisp`)
*   **Lokalizacja:** `LispManager.cs` (Metoda `DeleteLisp`)
*   **Opis:** Metoda używa `Directory.GetFiles(basePath, $"{lispId}.json", SearchOption.AllDirectories)`. Jeśli `lispId` zostanie podany jako pusty ciąg lub bardzo krótki (np. `.`), metoda może zacząć usuwać pliki pasujące do wzorca w całym drzewie katalogów.
*   **Ryzyko:** Ryzyko przypadkowego usunięcia dużej ilości danych przy błędnym parametrze.

### D. Niespójność: Brak walidacji `Category` przed użyciem w ścieżce
*   **Lokalizacja:** `LispManager.cs` (Metoda `SaveLisp`)
*   **Opis:** Choć istnieje próba czyszczenia znaków nieprawidłowych z `safeCategory`, sam parametr `metadata.LispId` (który jest częścią ścieżki) nie podlega takiej sanityzacji w procesie tworzenia folderów/plików.

## 3. Rekomendacje

1.  **Implementacja Sanityzacji:** Wprowadzić rygorystyczną walidację `LispId`. Dozwolone tylko znaki alfanumeryczne i podkreślnik. Zakaz używania `/`, `\`, `.`.
2.  **Rozszerzenie API Narzędzia:** Dodać akcję `delete_lisp` do schematu JSON narzędzia `manage_lisps`.
3.  **Bezpieczne operacje na plikach:** Używać `Path.GetFileName()` na każdym elemencie składowym ścieżki, aby uniemożliwić ucieczkę z katalogu (Directory Traversal).
4.  **Wzmocnienie `DeleteLisp`:** Zmienić logikę wyszukiwania plików tak, aby operowała tylko w obrębie konkretnego folderu kategorii, zamiast przeszukiwać `AllDirectories`.

---
**Audyt zakończony.**
**Agent:** Rewident V2
