# Pamięć Projektu
# Pamięć Projektu

## [2026-04-04 01:03]
### [ZREALIZOWANO]
- Zainicjowano strukturę folderów dla wersji V2 (`Bricscad_AgentAI_V2`).
- Skonfigurowano reguły systemowe (`.agents/rules/`) oraz skille (`.agents/skills/`) jako fundament nowej architektury.
- Zaktualizowano `.gitignore` w celu obsługi nowej struktury i artefaktów.
- [KROK-1.1] Utworzono fundament V2 (`Bricscad_AgentAI_V2.csproj`) w .NET 4.8. Zdefiniowano bazowe modele danych `ToolDefinition`, `FunctionSchema`, `ToolParameter` dla OpenAI Tool Calling oraz główny interfejs agenta `IToolV2`.
- [KROK-1.2] Zaimplementowano klasę `ToolOrchestrator`. Odpowiada ona za automatyczne wykrywanie narzędzi (Reflection) oraz orkiestrację wywołań w formacie JSON rzuconym przez LLM.
- [KROK-1.3] Wdrożono `LLMClient.cs` wyposażonego w rekurencyjną pętlę decyzyjną (ReAct). System automatycznie mapuje i przetwarza ustrukturyzowaną tablicę `tool_calls` w JSON, izolując stan konwersacji od działania Orkiestratora.
- [KROK-3.1] Zaimplementowano `AgentMemoryState` do zarządzania stanem, przeniesiono logikę zaznaczania BricsCAD do pliku `SelectEntitiesTool.cs` wraz z silnie typowanym ToolSchema. Stworzono test walidujący logikę "Conditions".
- [KROK-4.1] Portowano "Ręce Agenta": Utworzono `CreateObjectTool.cs` oraz silnik `RpnCalculator.cs`. Wdrożono mechanizm Auto-Selection oraz obsługę zmiennych `@` w `AgentMemoryState`. Zaktualizowano dokumentację System_Blueprint oraz USER_GUIDE.
### [STAN_SYSTEMU]
- System posiada w pełni funkcjonalne „Oczy” (Select) i „Ręce” (Create). Wspiera zaawansowane obliczenia RPN oraz interakcje `AskUser`. Wszystkie komponenty kompilują się poprawnie (Build Success).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- [KROK-5.1] Implementacja narzędzi do modyfikacji właściwości (`ModifyPropertiesTool.cs`) – zmiana warstwy, koloru, grubości linii dla obiektów w `ActiveSelection`.
