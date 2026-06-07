---
name: mark-tool-fixed-bricscad
description: Oznacza konkretne narzędzie jako poprawione po wykonaniu audytu i dopisuje log od Agenta w historii. Używaj tej komendy po skutecznym naprawieniu kodu narzędzia, np. `/mark-tool-fixed-bricscad --tool InsertBlock --desc "Usunięto błąd NaN i dodano try-catch"`.
---

# Zastosowanie
Ten workflow służy do zautomatyzowanego rejestrowania poprawek w narzędziach BricsCAD po wykryciu przez Rewidenta (Audytora) błędów (lub po zgłoszeniu problemu przez użytkownika). Zamiast zmuszać użytkownika do ręcznego "klikania" w UI, Ty – jako Agent Programista – od razu po wdrożeniu zmian w kodzie, zaktualizujesz dziennik.

# Kroki do wykonania

## 1. Wyodrębnij argumenty wejściowe z wywołania użytkownika
Użytkownik wywoła polecenie np. `/mark-tool-fixed-bricscad --tool NazwaNarzędzia --desc "opis"`.
Musisz wyciągnąć:
- `ToolName` - dokładna nazwa narzędzia tak, jak wyświetla się w interfejsie BricsCAD (np. `InsertBlock`, zazwyczaj bez przyrostka "Tool"!).
- `Description` - techniczny opis poprawek, które właśnie wprowadziłeś w kodzie C#. Czasem użytkownik pominie ten opis - w takim wypadku sam sformułuj zwięzłe podsumowanie dokonanych przez Ciebie zmian!

## 2. Zaktualizuj plik historii narzędzia
1. Sprawdź, czy istnieje plik logów dla narzędzia: `D:\GitHub\Bricscad_AgentAI\Bricscad_AgentAI_V2\Autotesty\Logs\{ToolName}_History.md`
2. Utwórz ten plik, jeśli nie istnieje.
3. Dopisz do pliku (korzystając z narzędzia lub skryptu dopisującego tekst) następujący blok Markdown na samym końcu:

```markdown

## [AGENT FIX] - Data: {dzisiejsza data np. 2026-06-07}
**Agent:** Bielik / Antigravity AI
**Opis Poprawki:**
{Tutaj wstaw swój techniczny opis z parametru Description}

---
```

## 3. Zaktualizuj status w Rejestrze
1. Otwórz plik `D:\GitHub\Bricscad_AgentAI\Bricscad_AgentAI_V2\Autotesty\test_registry.json`. (Z użyciem narzędzi do edycji plików).
2. Odszukaj obiekt, dla którego `ToolName` odpowiada {ToolName}.
3. Zmień jego właściwość `"LastStatus"` na `"Poprawione (Fixed)"`.
4. (Opcjonalnie) Jeśli narzędzia w ogóle nie ma w liście, dopisz go z domyślnymi `TestsCount` jako `0` i `LastStatus` = `"Poprawione (Fixed)"`.
5. Zapisz modyfikację pliku.

## 4. Raport końcowy
Powiedz użytkownikowi:
"✅ Oznaczyłem narzędzie `{ToolName}` jako **Poprawione (Fixed)**. Wygenerowałem notatkę w pliku historii (`Logs/{ToolName}_History.md`). Pamiętaj o przeładowaniu UI w BricsCADzie, jeśli chcesz od razu zobaczyć zmiany w tabeli Autotestów!"
