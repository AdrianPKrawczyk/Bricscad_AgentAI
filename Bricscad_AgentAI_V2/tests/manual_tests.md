# Instrukcja Manualnych Testów Wieloagentowych Bielik AI V2

Ten dokument zawiera scenariusze testowe, które pozwalają zweryfikować poprawne działanie Supervisora oraz 3 nowych, wyspecjalizowanych profilów ekspertów w BricsCAD.

## KROK 1: Wygenerowanie obiektów testowych w BricsCAD
1. Otwórz BricsCAD.
2. Załaduj skrypt LISP testowy (skrót poleceń w CAD):
   ```text
   (load "D:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/generate_test_objects.lsp")
   ```
3. Uruchom polecenie generowania obiektów:
   ```text
   GEN_BIELIK_TESTS
   ```
   *Na ekranie zostaną wygenerowane prostokąty (polilinie), okręgi, teksty oraz biurka (bloki z atrybutami ID i STAN).*

---

## SCENARIUSZ 1: Test dla `CadGeometryProfile` (Edycja i Rysowanie)
* **Cel:** Sprawdzenie selekcji i modyfikacji właściwości obiektów.
* **Treść zapytania do czatu AI:**
  ```text
  zaznacz wszystkie czerwone polilinie i zmień ich warstwę na Bielik_Wymiary
  ```
* **Oczekiwany rezultat:**
  1. Supervisor przekazuje zadanie do `CadGeometryProfile` (worker otrzymuje tylko 12 narzędzi).
  2. Prostokąty w rysunku zostają przeniesione na warstwę `Bielik_Wymiary` (zmieniają kolor na fioletowy/magenta).

---

## SCENARIUSZ 2: Test dla `CadBlocksProfile` (Bloki i Atrybuty)
* **Cel:** Sprawdzenie odczytu, precyzyjnego filtrowania i aktualizacji atrybutów bloków (dzięki nowym parametrom `FilterTag` / `FilterValue`).
* **Treść zapytania do czatu AI:**
  ```text
  zmień wartość atrybutu STAN na Wolne dla biurka o ID A2
  ```
* **Oczekiwany rezultat:**
  1. Supervisor przekazuje zadanie do `CadBlocksProfile` (worker otrzymuje tylko 11 narzędzi).
  2. Agent odczytuje atrybuty, identyfikuje blok z ID "A2" i aktualizuje wyłącznie jego atrybut STAN na wartość "Wolne".
  3. Napis przy środkowym biurku na ekranie BricsCAD zmienia się z "Zajete" na "Wolne".

---

## SCENARIUSZ 3: Test dla `CadMetadataProfile` (Analityka, XData i Pomiary)
* **Cel:** Sprawdzenie wyszukiwania obiektów po strukturze aplikacji i odczytu XData.
* **Treść zapytania do czatu AI:**
  ```text
  znajdź obiekt posiadający metadane XData aplikacji BIELIK_APP, odczytaj przypisany tam identyfikator tekstowy oraz wartość liczbową
  ```
* **Oczekiwany rezultat:**
  1. Supervisor przekazuje zadanie do `CadMetadataProfile`.
  2. Agent wyszukuje i odczytuje XData z polilinii o uchwycie (np. `14E`).
  3. Zwraca wartości: String = `Bielik-V2-ID-999`, Double = `123.45`.
