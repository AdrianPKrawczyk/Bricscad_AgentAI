---
name: port-bricscad-tool
description: Użyj tej umiejętności, gdy użytkownik prosi o migrację, przepisanie lub przeniesienie narzędzia BricsCAD z wersji V1 (Regex) do nowej architektury V2 (Tool Calling).
---
# Instrukcja Portowania Narzędzi BricsCAD V2

Kiedy masz przenieść narzędzie CAD do nowego standardu "Tool Calling", musisz bezwzględnie zastosować poniższe wzorce. Jeśli tego nie zrobisz, wtyczka nie zadziała.

## 1. Architektura wejścia (Brak Regex)
- Stare narzędzia V1 (w folderze `Bricscad_AgentAI`) używały `Regex.Match()` lub innych metod parsowania stringów, by wyciągnąć parametry z tagów (np. `[ACTION: ...]`).
- W wersji V2 CAŁKOWICIE USUŃ ten kod. Zakładamy, że serwer LLM dostarcza perfekcyjnie sformatowane, zdeserializowane dane C# (jako `Dictionary<string, object>` lub właściwości silnie typowanej klasy C#).

## 2. Implementacja IToolV2 i GetToolSchema()
- Każde przeniesione narzędzie musi implementować metodę lub właściwość zwracającą jego strukturę JSON Schema.
- Schemat musi precyzyjnie opisywać typy (np. `integer`, `string`, `boolean`) dla LLM.

## 3. Weryfikacja API BricsCAD (ZAKAZ HALUCYNACJI I ZARZĄDZANIE KONTEKSTEM)
- Zanim zaimplementujesz wywołania metod i właściwości na obiektach BricsCAD, **masz obowiązek zweryfikować ich istnienie**.
- KROK 1: Przeczytaj plik `@Bricscad_AgentAI_V2/resources/BricsCAD_API_Quick.txt`. Jest on mały (ok. 25k znaków) i bezpieczny do załadowania do kontekstu.
- KROK 2 (Tylko jeśli brakuje danych): Przeszukaj plik `@Bricscad_AgentAI_V2/resources/BricsCAD_API_V22.txt`. 
- **OSTRZEŻENIE KRYTYCZNE DOT. V22:** Ten plik jest GIGANTYCZNY. **POD ŻADNYM POZOREM nie próbuj czytać całego pliku do pamięci.** Struktura tego pliku to `nazwa_klasy|właściwości...`. Zamiast go czytać, wykonaj w terminalu komendę (podmieniając 'nazwa' na szukaną klasę):
  `grep -i "^nazwa|" Bricscad_AgentAI_V2/resources/BricsCAD_API_V22.txt`
- Przeczytaj tylko wynik z terminala. Nie zgaduj nazw metod (np. nie używaj `.Text` jeśli API wymaga `.TextString`).

## 4. Magiczny Wrapper BricsCAD (KRYTYCZNE)
- Narzędzia operujące na grafice (OpenMode.ForWrite, Transaction) **nie mogą** uruchamiać transakcji bezpośrednio w asynchronicznej metodzie wywoływanej przez HttpClient.
- Cała logika CAD musi być zamknięta i zwrócona w sposób umożliwiający wywołanie jej na głównym wątku przez `doc.SendStringToExecute("_AGENT_RUN_TOOL\n", ...)` (tzw. Magiczny Wrapper).

## 5. Ochrona Matematyki
- Jeśli w narzędziu V1 znajdowały się wstrzykiwania do Kalkulatora RPN (np. tagi `RPN:`), przenieś tę obsługę do nowej wersji, podłączając ją pod odpowiedni silnik ewaluacji.

## 6. Przykłady
- Zajrzyj do pliku `@.agents/skills/port-bricscad-tool/examples/SampleToolV2.cs`, aby zobaczyć, jak powinien wyglądać docelowy, czysty kod.