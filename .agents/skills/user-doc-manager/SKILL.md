---
name: user-doc-manager
description: Użyj tej umiejętności, aby zaktualizować dokumentację dla użytkownika końcowego (USER_GUIDE.md) po dodaniu lub zmodyfikowaniu narzędzia w V2.
---
# Instrukcja Tworzenia Dokumentacji Użytkownika

Twoim zadaniem jest dbanie o to, aby użytkownik wiedział, jak rozmawiać z nowym Agentem V2. Po każdej zmianie w `src/Tools/` wykonaj następujące kroki:

## 1. Aktualizacja pliku @Bricscad_AgentAI_V2/docs/USER_GUIDE.md
- Jeśli dodałeś nowe narzędzie (np. `DrawCircle`), dodaj sekcję w podręczniku.
- **Format opisu:**
  ### [NAZWA NARZĘDZIA]
  - **Co robi:** (Prosty opis po polsku)
  - **Jak o to zapytać:** (Przykładowe prompty, np. "Narysuj okrąg w punkcie 0,0 o promieniu 50")
  - **Wymagane dane:** (Co użytkownik musi podać, a co Agent sam wyliczy)

## 2. Język dokumentacji
- Dokumentacja musi być pisana prostym, nietechnicznym językiem (unikaj wspominania o JSON, klasach czy transakcjach).
- Skup się na korzyściach dla projektanta CAD.

## 3. Synchronizacja z Tool Schema
- Upewnij się, że opisy w USER_GUIDE.md są zgodne z tym, co zdefiniowałeś w `GetToolSchema()` danego narzędzia.

