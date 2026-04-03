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
### [STAN_SYSTEMU]
- System jest gotowy na przyjęcie rzeczywistych implementacji narzędzi (`IToolV2`). "Mózg" potrafi interpretować komendy (ReAct loop) i wymuszać formaty funkcjonalne, odrzucając starą logikę Regex (V1).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- [KROK-3.1] Portowanie "Oczu Agenta": utworzenie `SelectEntitiesTool.cs` na podstawie starego algorytmu tagu `[SELECT]` (Zgodnie z Etapem 3 Planu Migracji).
