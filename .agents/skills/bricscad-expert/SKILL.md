---
name: bricscad-expert
description: Kompleksowa wiedza o programowaniu .NET dla BricsCAD. Używaj przy każdej modyfikacji kodu manipulującego obiektami DWG.
---
# Standardy Programowania BricsCAD V2

## Zarządzanie Transakcjami
- Zawsze używaj `using (Transaction tr = db.TransactionManager.StartTransaction())`.
- Pamiętaj o `tr.Commit()` na końcu operacji zapisu.

## Obsługa Wątków
- Pamiętaj, że asynchroniczne wywołania z LLM muszą zostać zsynchronizowane z głównym wątkiem CAD poprzez `SendStringToExecute`.

## Optymalizacja
- Przy dużych zaznaczeniach (SelectionSets) używaj filtrów (SelectionFilter), aby nie przeciążać pamięci RAM.
- Korzystaj z @Bricscad_AgentAI_V2/resources/BricsCAD_API_Quick.txt do weryfikacji właściwości.